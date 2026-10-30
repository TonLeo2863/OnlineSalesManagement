param([Parameter(Mandatory=$true)][string]$TestDatabase,[int]$Port=64345)
$ErrorActionPreference='Stop'
if($TestDatabase -notmatch '^Dacn05_Restore_[0-9_]+$'){throw 'Unsafe database'}
$root=Split-Path $PSScriptRoot -Parent
$work=Join-Path $root ('artifacts\restore-verification\'+$TestDatabase)
if(!(Test-Path (Join-Path $work 'after.json'))){throw 'Missing verified restore/migration evidence'}
$site=Join-Path $work 'site';[void][IO.Directory]::CreateDirectory($site)
$bin=Join-Path $root 'OnlineSalesManagement\bin'
$exe=Join-Path $bin 'RestoredDatabaseTests.exe'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$refs=@('OnlineSalesManagement.dll','EntityFramework.dll','Microsoft.AspNet.Identity.Core.dll','Microsoft.AspNet.Identity.EntityFramework.dll') | ForEach-Object {'/reference:'+(Join-Path $bin $_)}
& $compiler /nologo /target:exe "/out:$exe" /reference:System.Configuration.dll /reference:System.Data.dll /reference:System.ComponentModel.DataAnnotations.dll @refs (Join-Path $PSScriptRoot 'RestoredDatabaseTests.cs')
if($LASTEXITCODE -ne 0){throw 'Compile failed'}
[xml]$source=Get-Content -Encoding UTF8 (Join-Path $root 'OnlineSalesManagement\Web.config')
$connection=[IO.File]::ReadAllText((Join-Path $work 'test-connection.txt')).Trim()
$builder=New-Object Data.SqlClient.SqlConnectionStringBuilder($connection)
if($builder.InitialCatalog -ne $TestDatabase -or !$builder.IntegratedSecurity){throw 'Unsafe test connection'}
$config=New-Object Xml.XmlDocument;$configuration=$config.CreateElement('configuration');[void]$config.AppendChild($configuration)
foreach($name in @('configSections','connectionStrings','entityFramework','runtime')){$node=$source.configuration.SelectSingleNode($name);if($node){[void]$configuration.AppendChild($config.ImportNode($node,$true))}}
$configuration.connectionStrings.add.connectionString=$connection;$config.Save($exe+'.config')
$env:DACN_UI_TEST_PASSWORD='RestoreTest!9-'+[Guid]::NewGuid().ToString('N')
$env:DACN_RESTORE_RUN=[Guid]::NewGuid().ToString('N').Substring(0,8)
$iis=$null;$count=0
function Check([bool]$ok,[string]$msg){if(!$ok){throw ('FAIL: '+$msg)};$script:count++;'PASS: '+$msg}
function Request([string]$path,[Net.CookieContainer]$cookies,[string]$data=$null){
 $request=[Net.HttpWebRequest]::Create("http://localhost:$Port$path");$request.CookieContainer=$cookies;$request.AllowAutoRedirect=$false;$request.Timeout=120000
 if($PSBoundParameters.ContainsKey('data')){$request.Method='POST';$request.ContentType='application/x-www-form-urlencoded';$bytes=[Text.Encoding]::UTF8.GetBytes($data);$request.ContentLength=$bytes.Length;$stream=$request.GetRequestStream();$stream.Write($bytes,0,$bytes.Length);$stream.Dispose()}
 try{$response=$request.GetResponse()}catch [Net.WebException]{if(!$_.Exception.Response){throw};$response=$_.Exception.Response}
 try{$reader=New-Object IO.StreamReader($response.GetResponseStream());[pscustomobject]@{Status=[int]$response.StatusCode;Body=$reader.ReadToEnd();Location=$response.Headers['Location']}}finally{$response.Dispose()}
}
function Token([string]$body){$m=[regex]::Match($body,'name="__RequestVerificationToken"[^>]*value="([^"]+)"');if(!$m.Success){throw 'Missing token'};[Net.WebUtility]::HtmlDecode($m.Groups[1].Value)}
function Form([hashtable]$fields){($fields.GetEnumerator() | ForEach-Object{[Net.WebUtility]::UrlEncode($_.Key)+'='+[Net.WebUtility]::UrlEncode([string]$_.Value)}) -join '&'}
function FixtureScalar([string]$sql,[string]$email){
 $db=New-Object Data.SqlClient.SqlConnection($connection)
 try{$db.Open();$cmd=$db.CreateCommand();$cmd.CommandText=$sql;[void]$cmd.Parameters.AddWithValue('@email',$email);$cmd.ExecuteScalar()}finally{$db.Dispose()}
}
try{
 & $exe
 if($LASTEXITCODE -ne 0){throw 'Restored service tests failed'}
 Get-ChildItem (Join-Path $root 'OnlineSalesManagement') | Where-Object {$_.Name -notin @('obj','.vs')} | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $site -Recurse -Force}
 $source.configuration.connectionStrings.add.connectionString=$connection;$source.Save((Join-Path $site 'Web.config'))
 $iis=Start-Process -FilePath (Join-Path ${env:ProgramFiles} 'IIS Express\iisexpress.exe') -ArgumentList ('/path:'+$site),('/port:'+$Port) -WindowStyle Hidden -RedirectStandardOutput (Join-Path $work 'iis.log') -RedirectStandardError (Join-Path $work 'iis-error.log') -PassThru
 $guest=New-Object Net.CookieContainer
 for($i=0;$i -lt 20;$i++){try{$page=Request '/' $guest;break}catch{Start-Sleep -Seconds 1}}
 Check ($page.Status -eq 200) 'restored website home works'
 Check ((Request '/AdminUsers' $guest).Status -eq 302) 'anonymous AdminUsers challenges login'
 foreach($role in @('Admin','Kho','BanHang','KhachHang')){
  $cookies=New-Object Net.CookieContainer;$login=Request '/Account/Login' $cookies
  $signed=Request '/Account/Login' $cookies (Form @{__RequestVerificationToken=(Token $login.Body);Email=('restore'+$env:DACN_RESTORE_RUN+$role.ToLowerInvariant()+'@fixture.example.test');Password=$env:DACN_UI_TEST_PASSWORD;RememberMe='false'})
  Check ($signed.Status -eq 302) ($role+' login on restored database')
  foreach($controller in @('AdminUsers','Products','Customers','Employees','Suppliers','PurchaseReceipts','SalesOrders','Reports','InventoryReports')){
   $allowed=$role -eq 'Admin' -or ($role -eq 'Kho' -and $controller -in @('Products','Suppliers','PurchaseReceipts','InventoryReports')) -or ($role -eq 'BanHang' -and $controller -in @('Customers','SalesOrders'))
   $result=Request ('/'+$controller) $cookies
   Check ($result.Status -eq $(if($allowed){200}else{403})) ($role+' authorization '+$controller)
  }
  if($role -eq 'Admin'){
   foreach($url in @('/Reports','/Reports/Purchases','/Reports/Sales','/Store','/AdminUsers?role=Admin&status=active')){Check ((Request $url $cookies).Status -eq 200) ('Admin restored page '+$url)}
   $targetEmail='restore'+$env:DACN_RESTORE_RUN+'khachhang@fixture.example.test'
   $targetId=FixtureScalar 'SELECT Id FROM dbo.AspNetUsers WHERE Email=@email' $targetEmail
   $oldStamp=FixtureScalar 'SELECT SecurityStamp FROM dbo.AspNetUsers WHERE Email=@email' $targetEmail
   $targetCookies=New-Object Net.CookieContainer;$targetLogin=Request '/Account/Login' $targetCookies
   $targetSigned=Request '/Account/Login' $targetCookies (Form @{__RequestVerificationToken=(Token $targetLogin.Body);Email=$targetEmail;Password=$env:DACN_UI_TEST_PASSWORD})
   Check ($targetSigned.Status -eq 302) 'suspension target logs in before administration'
   $accounts=Request '/AdminUsers' $cookies
   $cancelled=Request '/AdminUsers/SetSuspension' $cookies (Form @{__RequestVerificationToken=(Token $accounts.Body);id=$targetId;suspended='true';reason='Deployment fixture suspension';confirmed='false'})
   Check (!(FixtureScalar 'SELECT IsAdministrativelySuspended FROM dbo.AspNetUsers WHERE Email=@email' $targetEmail)) 'unconfirmed suspension writes no state'
   $accounts=Request '/AdminUsers' $cookies
   $suspended=Request '/AdminUsers/SetSuspension' $cookies (Form @{__RequestVerificationToken=(Token $accounts.Body);id=$targetId;suspended='true';reason='Deployment fixture suspension';confirmed='true'})
   Check ($suspended.Status -eq 302 -and (FixtureScalar 'SELECT IsAdministrativelySuspended FROM dbo.AspNetUsers WHERE Email=@email' $targetEmail)) 'confirmed suspension persists on restored test DB only'
   Check ((FixtureScalar 'SELECT SecurityStamp FROM dbo.AspNetUsers WHERE Email=@email' $targetEmail) -ne $oldStamp) 'suspension changes security stamp'
   Check ((Request '/Manage' $targetCookies).Status -eq 302) 'suspended account old cookie loses protected access immediately'
   $deniedCookies=New-Object Net.CookieContainer;$deniedLogin=Request '/Account/Login' $deniedCookies
   $denied=Request '/Account/Login' $deniedCookies (Form @{__RequestVerificationToken=(Token $deniedLogin.Body);Email=$targetEmail;Password=$env:DACN_UI_TEST_PASSWORD})
   Check ($denied.Status -eq 200 -and (Request '/Manage' $deniedCookies).Status -eq 302) 'suspended account cannot log in again'
   $accounts=Request '/AdminUsers' $cookies
   $restored=Request '/AdminUsers/SetSuspension' $cookies (Form @{__RequestVerificationToken=(Token $accounts.Body);id=$targetId;suspended='false';reason='Deployment fixture restoration';confirmed='true'})
   Check ($restored.Status -eq 302 -and !(FixtureScalar 'SELECT IsAdministrativelySuspended FROM dbo.AspNetUsers WHERE Email=@email' $targetEmail)) 'restoration re-enables account for later login test'
   Check ((FixtureScalar 'SELECT COUNT(*) FROM dbo.AccountAdministrationAudits WHERE TargetUserId=(SELECT Id FROM dbo.AspNetUsers WHERE Email=@email)' $targetEmail) -eq 2) 'suspend/restore creates exactly two audit events'
  }
  $dashboardPage=Request '/' $cookies;$out=Request '/Account/LogOff' $cookies (Form @{__RequestVerificationToken=(Token $dashboardPage.Body)})
  Check ($out.Status -eq 302 -and (Request '/AdminUsers' $cookies).Status -eq 302) ($role+' logout revokes access')
 }
 'TOTAL RESTORED HTTP PASS: '+$count
 'Test site retained: '+$site
}finally{if($iis -and !$iis.HasExited){Stop-Process -Id $iis.Id};$env:DACN_UI_TEST_PASSWORD=$null;$env:DACN_RESTORE_RUN=$null}
