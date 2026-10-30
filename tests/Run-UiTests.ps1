[CmdletBinding()]
param([int]$Port = 64265)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$work = Join-Path $root ('artifacts\ui-verification\run-' + [Guid]::NewGuid().ToString('N'))
$site = Join-Path $work 'site'
[void][IO.Directory]::CreateDirectory($site)
$env:DACN_UI_TEST_PASSWORD = 'UiTest!9-' + [Guid]::NewGuid().ToString('N')
$iis = $null
$count = 0
function Check([bool]$ok, [string]$message) {
    if (!$ok) { throw ('FAIL: ' + $message) }
    $script:count++
    Write-Output ('PASS: ' + $message)
}
function RequestPage([string]$path, [Net.CookieContainer]$cookies, [string]$data = $null, [switch]$Ajax) {
    $uri = if ($path.StartsWith('http://localhost:' + $Port + '/')) { $path } else { "http://localhost:$Port$path" }
    $request = [Net.HttpWebRequest]::Create($uri)
    $request.CookieContainer = $cookies
    $request.AllowAutoRedirect = $false
    $request.Timeout = 120000
    if ($Ajax) { $request.Headers['X-Requested-With']='XMLHttpRequest'; $request.Accept='application/json' }
    if ($PSBoundParameters.ContainsKey('data')) {
        $request.Method = 'POST'
        $request.ContentType = 'application/x-www-form-urlencoded; charset=utf-8'
        $bytes = [Text.Encoding]::UTF8.GetBytes($data)
        $request.ContentLength = $bytes.Length
        $stream = $request.GetRequestStream(); $stream.Write($bytes,0,$bytes.Length); $stream.Dispose()
    }
    try { $response = $request.GetResponse() }
    catch [Net.WebException] { if (!$_.Exception.Response) { throw }; $response = $_.Exception.Response }
    try {
        $memory = New-Object IO.MemoryStream
        $response.GetResponseStream().CopyTo($memory)
        $body = [Text.Encoding]::UTF8.GetString($memory.ToArray())
        [pscustomobject]@{ Status = [int]$response.StatusCode; Body = [Net.WebUtility]::HtmlDecode($body); RawBody = $body; ContentType = $response.ContentType; Location = $response.Headers['Location'] }
    } finally { $response.Dispose() }
}
function Token([string]$body) {
    $match = [regex]::Match($body, '<input[^>]*name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    if (!$match.Success) { throw 'Missing anti-forgery field' }
    [Net.WebUtility]::HtmlDecode($match.Groups[1].Value)
}
function FormData([hashtable]$fields) {
    ($fields.GetEnumerator() | ForEach-Object { [Net.WebUtility]::UrlEncode($_.Key) + '=' + [Net.WebUtility]::UrlEncode([string]$_.Value) }) -join '&'
}
function Field([string]$body, [string]$name) {
    $m = [regex]::Match($body, '<input[^>]*name="' + [regex]::Escape($name) + '"[^>]*value="([^"]*)"')
    if (!$m.Success) { throw ('Missing form field: ' + $name) }
    $m.Groups[1].Value
}
function SqlScalar([string]$query) {
    $connection = New-Object Data.SqlClient.SqlConnection($script:builder.ConnectionString)
    try { $connection.Open(); $cmd = $connection.CreateCommand(); $cmd.CommandText = $query; $cmd.ExecuteScalar() }
    finally { $connection.Dispose() }
}
function UploadWorkbook([Net.CookieContainer]$cookies,[string]$kind,[byte[]]$bytes) {
    $page=RequestPage ('/CatalogImport/Upload?kind='+$kind) $cookies
    $token=Token $page.Body;$boundary='ImportBoundary'+[Guid]::NewGuid().ToString('N')
    $request=[Net.HttpWebRequest]::Create("http://localhost:$Port/CatalogImport/Upload")
    $request.Method='POST';$request.CookieContainer=$cookies;$request.AllowAutoRedirect=$false;$request.ContentType='multipart/form-data; boundary='+$boundary
    $body=New-Object IO.MemoryStream
    $prefix="--$boundary"+"`r`n"+'Content-Disposition: form-data; name="__RequestVerificationToken"'+"`r`n`r`n"+$token+"`r`n--$boundary`r`n"+'Content-Disposition: form-data; name="kind"'+"`r`n`r`n"+$kind+"`r`n--$boundary`r`n"+'Content-Disposition: form-data; name="workbook"; filename="fixture.xlsx"'+"`r`nContent-Type: application/octet-stream`r`n`r`n"
    $first=[Text.Encoding]::UTF8.GetBytes($prefix);$body.Write($first,0,$first.Length);$body.Write($bytes,0,$bytes.Length)
    $last=[Text.Encoding]::UTF8.GetBytes("`r`n--$boundary--`r`n");$body.Write($last,0,$last.Length)
    $request.ContentLength=$body.Length;$stream=$request.GetRequestStream();$body.Position=0;$body.CopyTo($stream);$stream.Dispose();$body.Dispose()
    try{$response=$request.GetResponse()}catch [Net.WebException]{$response=$_.Exception.Response;if(!$response){throw}}
    try{$reader=New-Object IO.StreamReader($response.GetResponseStream());[pscustomobject]@{Status=[int]$response.StatusCode;Location=$response.Headers['Location'];Body=$reader.ReadToEnd()}}finally{$response.Dispose()}
}
function CartPost([string]$action, [Net.CookieContainer]$cookies, [int]$productId, [int]$quantity) {
    $catalog = RequestPage '/Store' $cookies
    RequestPage ('/Store/' + $action) $cookies (FormData @{ __RequestVerificationToken=(Token $catalog.Body); version=(Field $catalog.Body 'version'); productId=$productId; quantity=$quantity })
}
try {
    # This harness invokes only the reviewed isolated-DB runner. No application DB writes.
    & (Join-Path $PSScriptRoot 'Run-IntegrationTests.ps1')
    $sourceSite = Join-Path $root 'OnlineSalesManagement'
    Get-ChildItem -LiteralPath $sourceSite | Where-Object { $_.Name -notin @('obj','.vs') } | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $site -Recurse
    }
    [xml]$web = Get-Content -Encoding UTF8 (Join-Path $site 'Web.config')
    [xml]$fixture = Get-Content -Encoding UTF8 (Join-Path $sourceSite 'bin\Dacn05IntegrationTests.exe.config')
    $builder = New-Object Data.SqlClient.SqlConnectionStringBuilder($fixture.configuration.connectionStrings.add.connectionString)
    if (!$builder.InitialCatalog.StartsWith('Dacn05_Test_') -or !$builder.IntegratedSecurity) { throw 'Unsafe fixture connection' }
    $web.configuration.connectionStrings.add.connectionString = $builder.ConnectionString
    $web.Save((Join-Path $site 'Web.config'))
    $iisPath = Join-Path ${env:ProgramFiles} 'IIS Express\iisexpress.exe'
    $iis = Start-Process -FilePath $iisPath -ArgumentList ('/path:' + $site),('/port:' + $Port) -WindowStyle Hidden -RedirectStandardOutput (Join-Path $work 'iis.log') -RedirectStandardError (Join-Path $work 'iis-error.log') -PassThru
    $guestCookies = New-Object Net.CookieContainer
    $guest = $null
    for ($attempt=0; $attempt -lt 20; $attempt++) {
        try { $guest = RequestPage '/' $guestCookies; break } catch { Start-Sleep -Seconds 1 }
    }
    if ($guest -and $guest.Status -ne 200) { [IO.File]::WriteAllText((Join-Path $work 'home-error.html'), $guest.RawBody) }
    Check ($null -ne $guest -and $guest.Status -eq 200) 'anonymous home responds'
    Check ($guest.ContentType -match 'charset=utf-8' -and $guest.Body.Contains('Quản lý bán hàng') -and !$guest.Body.Contains('Quáº£n')) 'HTTP UTF-8 bytes and Vietnamese text'
    Check (!$guest.Body.Contains('Doanh thu ghi nhận') -and $guest.Body.Contains('Khám phá cửa hàng')) 'anonymous shopping home excludes staff data'
    Check ($guest.Body.Contains('class="customer-body"') -and !$guest.Body.Contains('id="sidebar"')) 'guest uses customer layout without admin sidebar'
    $guestStore = RequestPage '/Store' $guestCookies
    Check ($guestStore.Status -eq 200 -and $guestStore.Body.Contains('product-grid') -and !$guestStore.Body.Contains('id="sidebar"')) 'guest browses customer product grid without admin navigation'
    $forgot=RequestPage '/Account/ForgotPassword' $guestCookies
    $knownRecovery=RequestPage '/Account/ForgotPassword' $guestCookies (FormData @{__RequestVerificationToken=(Token $forgot.Body);Email='privacyconfirmed@example.test'})
    $forgot=RequestPage '/Account/ForgotPassword' $guestCookies
    $unknownRecovery=RequestPage '/Account/ForgotPassword' $guestCookies (FormData @{__RequestVerificationToken=(Token $forgot.Body);Email='unknownprivacy@example.test'})
    Check ($knownRecovery.Status -eq 200 -and $unknownRecovery.Status -eq 200 -and $knownRecovery.Body.Contains('không công bố tài khoản') -and $unknownRecovery.Body.Contains('không công bố tài khoản')) 'recovery response does not reveal known or confirmed account status'
    foreach($email in @('privacyconfirmed@example.test','unknownprivacy@example.test')) {
        $reset=RequestPage '/Account/ResetPassword?code=invalid-reset-token' $guestCookies
        $rejected=RequestPage '/Account/ResetPassword' $guestCookies (FormData @{__RequestVerificationToken=(Token $reset.Body);Email=$email;Code='invalid-reset-token';Password='TestOnly!Password9';ConfirmPassword='TestOnly!Password9'})
        Check ($rejected.Status -eq 200 -and $rejected.Body.Contains('Không thể xử lý thông tin hoặc mã khôi phục')) 'invalid recovery token has generic response for either account'
    }
    $anonymousProtected = RequestPage '/AdminUsers' $guestCookies
    Check ($anonymousProtected.Status -eq 302 -and $anonymousProtected.Location -match '/Account/Login\?ReturnUrl=') 'anonymous protected URL challenges Login'
    $anonymousLogin = RequestPage $anonymousProtected.Location $guestCookies
    Check ($anonymousLogin.Status -eq 200 -and !$anonymousLogin.Location) 'anonymous redirect chain terminates at Login'
    foreach ($role in @('Admin','Kho','BanHang','KhachHang')) {
        $cookies = New-Object Net.CookieContainer
        $login = RequestPage '/Account/Login' $cookies
        $post = FormData @{ __RequestVerificationToken = (Token $login.Body); Email = ($role + '@ui.example.test'); Password = $env:DACN_UI_TEST_PASSWORD; RememberMe = 'false' }
        $signedIn = RequestPage '/Account/Login' $cookies $post
        Check ($signedIn.Status -eq 302) ($role + ' HTTP login')
        $repeatLogin = RequestPage '/Account/Login?returnUrl=%2FAdminUsers' $cookies
        Check ($repeatLogin.Status -eq 302 -and $repeatLogin.Location -eq '/') ($role + ' authenticated Login returns home without loop')
        $dashboardPage = RequestPage '/' $cookies
        Check ($dashboardPage.Status -eq 200) ($role + ' home renders')
        Check ($dashboardPage.Body.Contains('id="sidebar"') -eq ($role -ne 'KhachHang')) ($role + ' correct layout and sidebar')
        if ($role -ne 'KhachHang') {
            $staffStore = RequestPage '/Store' $cookies
            Check ($staffStore.Status -eq 200 -and $staffStore.Body.Contains('id="sidebar"') -eq ($role -ne 'Admin') -and $staffStore.Body.Contains('product-grid') -and ([regex]::Matches($staffStore.Body,'src="/Scripts/customer.js"')).Count -eq 1) ($role + ' store uses intended layout and loads customer script once')
        }
        [IO.File]::WriteAllText((Join-Path $work ($role + '.html')), $dashboardPage.RawBody, (New-Object Text.UTF8Encoding($false)))
        Check ($dashboardPage.Body.Contains('Quản lý bán hàng') -and !$dashboardPage.Body.Contains('Quáº£n')) ($role + ' Vietnamese render')
        Check ($dashboardPage.Body.Contains('Doanh thu ghi nhận') -eq ($role -in @('Admin','BanHang'))) ($role + ' sales dashboard visibility')
        Check ($dashboardPage.Body.Contains('Cần bổ sung hàng') -eq ($role -in @('Admin','Kho'))) ($role + ' inventory dashboard visibility')
        foreach ($controller in @('AdminUsers','Products','Categories','Customers','Employees','Suppliers','InventoryReports','PurchaseReceipts','SalesOrders','Reports')) {
            $allowed = $role -eq 'Admin' -or ($role -eq 'Kho' -and $controller -in @('Products','Categories','Suppliers','InventoryReports','PurchaseReceipts')) -or ($role -eq 'BanHang' -and $controller -in @('Customers','SalesOrders'))
            Check ($dashboardPage.Body.Contains('href="/' + $controller + '"') -eq $allowed) ($role + ' sidebar ' + $controller)
            $page = RequestPage ('/' + $controller) $cookies
            if (!$allowed) {
                Write-Output ('TRACE ' + $role + ' GET /' + $controller + ' -> ' + $page.Status + ' Location=' + $page.Location)
                if ($page.Status -eq 302) {
                    $next = RequestPage $page.Location $cookies
                    Write-Output ('TRACE follow -> ' + $next.Status + ' Location=' + $next.Location)
                    Check ($next.Status -eq 200) 'baseline denied redirect terminates at login'
                }
            }
            Check (($allowed -and $page.Status -eq 200) -or (!$allowed -and $page.Status -eq 403 -and !$page.Location)) ($role + ' direct GET ' + $controller)
        }
        if ($role -eq 'Admin') {
            $preview=RequestPage '/Store' $cookies
            Check ($preview.Body.Contains('Chế độ xem trước — Quản trị viên') -and $preview.Body.Contains('Quay lại quản trị') -and !$preview.Body.Contains('href="/Store/Cart"') -and !$preview.Body.Contains('href="/Store/Orders"')) 'Admin gets customer preview with no shopping navigation'
            $previewId=SqlScalar 'SELECT TOP(1) p.Id FROM Products p JOIN Categories c ON c.Id=p.CategoryId WHERE p.IsActive=1 AND c.IsActive=1 ORDER BY p.Id'
            $previewJson=(RequestPage ('/Store/ProductDetails/'+$previewId) $cookies).Body | ConvertFrom-Json
            Check ($previewJson.success -and $previewJson.previewOnly -and !$previewJson.signedIn -and !$previewJson.version) 'Admin can read actual product details without cart or purchase capability'
            $previewAjax=RequestPage '/Store/Cart' $cookies -Ajax
            Check ($previewAjax.Status -eq 403 -and !($previewAjax.Body | ConvertFrom-Json).success) 'Admin AJAX cart GET is safely denied with JSON'
            $profileCount=SqlScalar 'SELECT COUNT(*) FROM Customers';$salesCount=SqlScalar 'SELECT COUNT(*) FROM SalesOrders'
            foreach($action in @('AddToCart','UpdateCart','ClearCart','Checkout','Reorder','CancelPending')) {
                $blocked=RequestPage ('/Store/'+$action) $cookies (FormData @{__RequestVerificationToken=(Token $preview.Body);productId=1;quantity=1;version='forged';Token='forged';id=1})
                Check ($blocked.Status -eq 403) ('Admin manual POST denied '+$action)
            }
            foreach($action in @('Cart','Checkout','Orders','Details/1')) {Check ((RequestPage ('/Store/'+$action) $cookies).Status -eq 403) ('Admin customer GET denied '+$action)}
            $blockedAjax=RequestPage '/Store/AddToCart' $cookies (FormData @{__RequestVerificationToken=(Token $preview.Body);productId=1;quantity=1;version='forged'}) -Ajax
            Check ($blockedAjax.Status -eq 403 -and !($blockedAjax.Body | ConvertFrom-Json).success) 'Admin AJAX purchase gets JSON 403'
            Check ((SqlScalar 'SELECT COUNT(*) FROM Customers') -eq $profileCount -and (SqlScalar 'SELECT COUNT(*) FROM SalesOrders') -eq $salesCount) 'Admin browsing and forged posts create no profile or order'
            $accounts = RequestPage '/AdminUsers' $cookies
            Check (([regex]::Matches($accounts.Body, 'data-user-card')).Count -ge 6) 'six account cards render including unusual roles'
            Check ($accounts.Body.Contains('role-grid') -and $accounts.Body.Contains('data-roles=') -and $accounts.Body.Contains('id="q"')) 'account grid confirmation metadata and search render'
            [IO.File]::WriteAllText((Join-Path $work 'AdminUsers.html'), $accounts.RawBody, (New-Object Text.UTF8Encoding($false)))
            $catalog = RequestPage '/Store' $cookies
            Check ($catalog.Status -eq 200 -and $catalog.Body.Contains('data-mega-menu') -and $catalog.Body.Contains('aria-controls="category-panel-') -and $catalog.Body.Contains('/Scripts/store-menu.js')) 'server renders mega menu and local script'
            [IO.File]::WriteAllText((Join-Path $work 'Store.html'), $catalog.RawBody, (New-Object Text.UTF8Encoding($false)))
            $categoryId = [int](SqlScalar "SELECT CategoryId FROM Products WHERE Sku='OSDEMO-08-01'")
            $filtered = RequestPage ('/Store?categoryId=' + $categoryId) $cookies
            $ids = [regex]::Matches($filtered.Body, '<article[^>]*data-product-id="(\d+)"')
            Check ($filtered.Status -eq 200 -and $ids.Count -ge 4) 'category URL renders demo SSD products'
            foreach ($match in $ids) { Check ((SqlScalar ('SELECT CategoryId FROM Products WHERE Id=' + [int]$match.Groups[1].Value)) -eq $categoryId) 'HTTP category filter contains no foreign category' }
            $searched = RequestPage ('/Store?categoryId=' + $categoryId + '&q=OSDEMO-08-01') $cookies
            Check (([regex]::Matches($searched.Body, '<article[^>]*data-product-id=')).Count -eq 1) 'HTTP SKU search respects selected category'
            Check ($searched.Body.Contains('name="categoryId"')) 'search form preserves selected category'
        }
        if ($role -eq 'Admin') {
            $targetId = SqlScalar "SELECT Id FROM AspNetUsers WHERE Email='KhachHang@ui.example.test'"
            $oldCookies=New-Object Net.CookieContainer
            $targetLogin=RequestPage '/Account/Login' $oldCookies
            $signed=RequestPage '/Account/Login' $oldCookies (FormData @{__RequestVerificationToken=(Token $targetLogin.Body);Email='KhachHang@ui.example.test';Password=$env:DACN_UI_TEST_PASSWORD})
            Check ($signed.Status -eq 302) 'governance target starts authenticated'
            $page=RequestPage '/AdminUsers' $cookies
            $noConfirm=RequestPage '/AdminUsers/SetSuspension' $cookies (FormData @{__RequestVerificationToken=(Token $page.Body);id=$targetId;suspended='true';reason='HTTP safety test';confirmed='false'})
            Check ((SqlScalar "SELECT CAST(IsAdministrativelySuspended AS int) FROM AspNetUsers WHERE Id='$targetId'") -eq 0) 'unconfirmed suspension never persists'
            $suspended=RequestPage '/AdminUsers/SetSuspension' $cookies (FormData @{__RequestVerificationToken=(Token $page.Body);id=$targetId;suspended='true';reason='HTTP suspension test';confirmed='true'})
            $noTokenState=RequestPage '/AdminUsers/SetSuspension' $cookies 'id=invalid&suspended=true&confirmed=true&reason=SafetyTest'
            Check ($noTokenState.Status -ge 400) 'suspension POST requires antiforgery token'
            Check ($suspended.Status -eq 302 -and (SqlScalar "SELECT CAST(IsAdministrativelySuspended AS int) FROM AspNetUsers WHERE Id='$targetId'") -eq 1) 'confirmed suspension persists'
            $oldAccess=RequestPage '/Store/Orders' $oldCookies
            Check ($oldAccess.Status -eq 302 -and $oldAccess.Location -match '/Account/Login') 'old cookie loses access immediately after suspension'
            $targetLogin=RequestPage '/Account/Login' $oldCookies
            $blocked=RequestPage '/Account/Login' $oldCookies (FormData @{__RequestVerificationToken=(Token $targetLogin.Body);Email='KhachHang@ui.example.test';Password=$env:DACN_UI_TEST_PASSWORD})
            Check ($blocked.Status -eq 200 -and !$blocked.Body.Contains('Đình chỉ')) 'suspended login refused without public status disclosure'
            $page=RequestPage '/AdminUsers' $cookies
            $restored=RequestPage '/AdminUsers/SetSuspension' $cookies (FormData @{__RequestVerificationToken=(Token $page.Body);id=$targetId;suspended='false';reason='HTTP restore test';confirmed='true'})
            Check ($restored.Status -eq 302 -and (SqlScalar "SELECT CAST(IsAdministrativelySuspended AS int) FROM AspNetUsers WHERE Id='$targetId'") -eq 0) 'restore clears administrative flag'
            Check ((SqlScalar "SELECT COUNT(*) FROM AccountAdministrationAudits WHERE TargetUserId='$targetId'") -eq 2) 'HTTP suspend and restore audit events recorded'
            $targetLogin=RequestPage '/Account/Login' $oldCookies
            $signed=RequestPage '/Account/Login' $oldCookies (FormData @{__RequestVerificationToken=(Token $targetLogin.Body);Email='KhachHang@ui.example.test';Password=$env:DACN_UI_TEST_PASSWORD})
            Check ($signed.Status -eq 302) 'restored account can log in again'
            $phoneForm=RequestPage '/Manage/AddPhoneNumber' $oldCookies
            $phoneAttempt=RequestPage '/Manage/AddPhoneNumber' $oldCookies (FormData @{__RequestVerificationToken=(Token $phoneForm.Body);Number='0909999999'})
            Check ($phoneAttempt.Status -eq 200 -and $phoneAttempt.Body.Contains('Chưa cấu hình SMS') -and (SqlScalar "SELECT CAST(PhoneNumberConfirmed AS int) FROM AspNetUsers WHERE Id='$targetId'") -eq 0) 'unconfigured SMS fails closed and cannot mark ownership verified'
            $verifyForm=RequestPage '/Manage/AddPhoneNumber' $oldCookies
            $forgedVerify=RequestPage '/Manage/VerifyPhoneNumber' $oldCookies (FormData @{__RequestVerificationToken=(Token $verifyForm.Body);PhoneNumber='+84909999999';Code='123456'})
            Check ($forgedVerify.Status -eq 200 -and (SqlScalar "SELECT CAST(PhoneNumberConfirmed AS int) FROM AspNetUsers WHERE Id='$targetId'") -eq 0) 'forged phone confirmation without issued challenge is rejected'
            $adminId=SqlScalar "SELECT Id FROM AspNetUsers WHERE Email='admin@ui.example.test'"
            $page=RequestPage '/AdminUsers' $cookies
            $self=RequestPage '/AdminUsers/SetSuspension' $cookies (FormData @{__RequestVerificationToken=(Token $page.Body);id=$adminId;suspended='true';reason='Self suspension test';confirmed='true'})
            Check ((SqlScalar "SELECT CAST(IsAdministrativelySuspended AS int) FROM AspNetUsers WHERE Id='$adminId'") -eq 0) 'HTTP self suspension cannot remove active Admin'

            $filter=RequestPage '/AdminUsers?role=KhachHang&q=governance-page&page=2' $cookies
            Check ($filter.Status -eq 200 -and ([regex]::Matches($filter.Body,'data-user-card')).Count -eq 3) 'account server-side filters and pagination render'
            Check ((RequestPage '/AdminUsers/AccountAudit' $cookies).Status -eq 200) 'Admin audit page renders'
        }
        if($role -eq 'Admin') {
            $bytes=[IO.File]::ReadAllBytes((Join-Path $sourceSite 'bin\CatalogImportFixture.xlsx'))
            $upload=UploadWorkbook $cookies 'products' $bytes
            Check ($upload.Status -eq 302 -and $upload.Location -match 'Preview') 'Excel upload redirects to server Preview'
            $preview=RequestPage $upload.Location $cookies
            $previewToken=[regex]::Match($upload.Location,'token=([a-f0-9]+)').Groups[1].Value
            Check ($preview.Status -eq 200 -and $preview.Body.Contains('HTTP-EXCEL-001')) 'Excel Preview displays parsed scalar rows'
            Check ((SqlScalar "SELECT COUNT(*) FROM Products WHERE Sku='HTTP-EXCEL-001'") -eq 0) 'Excel Preview never writes records'
            $cancel=RequestPage '/CatalogImport/Cancel' $cookies (FormData @{__RequestVerificationToken=(Token $preview.Body);token=$previewToken})
            Check ($cancel.Status -eq 302 -and (SqlScalar "SELECT COUNT(*) FROM Products WHERE Sku='HTTP-EXCEL-001'") -eq 0) 'Excel cancel discards preview without writes'
            $upload=UploadWorkbook $cookies 'products' $bytes;$preview=RequestPage $upload.Location $cookies;$previewToken=[regex]::Match($upload.Location,'token=([a-f0-9]+)').Groups[1].Value
            $data=FormData @{__RequestVerificationToken=(Token $preview.Body);token=$previewToken}
            $confirmed=RequestPage '/CatalogImport/Confirm' $cookies $data
            Check ($confirmed.Status -eq 302 -and (SqlScalar "SELECT COUNT(*) FROM Products WHERE Sku='HTTP-EXCEL-001' AND QuantityInStock=0") -eq 1) 'Excel explicit confirmation creates product with zero stock'
            $replay=RequestPage '/CatalogImport/Confirm' $cookies $data
            Check ($replay.Status -eq 404 -and (SqlScalar "SELECT COUNT(*) FROM Products WHERE Sku='HTTP-EXCEL-001'") -eq 1) 'Excel replay cannot import twice'
            $bytes=[IO.File]::ReadAllBytes((Join-Path $sourceSite 'bin\CategoryImportFixture.xlsx'))
            $upload=UploadWorkbook $cookies 'categories' $bytes;$preview=RequestPage $upload.Location $cookies;$previewToken=[regex]::Match($upload.Location,'token=([a-f0-9]+)').Groups[1].Value
            $confirmed=RequestPage '/CatalogImport/Confirm' $cookies (FormData @{__RequestVerificationToken=(Token $preview.Body);token=$previewToken})
            Check ($confirmed.Status -eq 302 -and (SqlScalar "SELECT COUNT(*) FROM Categories WHERE Name='HTTP Excel category'") -eq 1) 'Excel category confirmation creates new category'
            $denied=RequestPage '/CatalogImport/Upload' $oldCookies
            Check ($denied.Status -eq 403) 'Excel upload is Admin only'
        }
        if($role -eq 'Admin') {
            $employees=RequestPage '/Employees?code=SEARCH-E&active=false' $cookies
            Check ($employees.Status -eq 200 -and $employees.Body.Contains('<td>SEARCH-E</td>')) 'employee GET code and active filters render'
            $customers=RequestPage '/Customers?code=SEARCH-C&phone=0909999998&active=false' $cookies
            Check ($customers.Status -eq 200 -and $customers.Body.Contains('<td>SEARCH-C</td>')) 'customer advanced GET filters render'
            $invalid=RequestPage '/Employees?fromDate=2026-10-30&toDate=2026-10-01' $cookies
            Check ($invalid.Status -eq 200 -and $invalid.Body.Contains('validation-summary-errors')) 'employee invalid dates do not crash'
            $invalid=RequestPage '/Customers?phone=not-a-phone' $cookies
            Check ($invalid.Status -eq 200 -and $invalid.Body.Contains('validation-summary-errors')) 'customer invalid contact search does not crash'
            $employeeId=SqlScalar "SELECT Id FROM Employees WHERE Code='SEARCH-E'"
            $edit=RequestPage ('/Employees/Edit/'+$employeeId) $cookies
            Check ((Field $edit.Body 'HireDate') -match '^\d{4}-\d{2}-\d{2}$') 'employee date input preserves ISO value on edit'
            Check ($edit.Status -eq 200 -and $edit.Body.Contains('name="positionChoice"') -and $edit.Body.Contains('Chuc vu cu dac biet')) 'employee dropdown retains legacy Other value'
            $fields=FormData @{__RequestVerificationToken=(Token $edit.Body);Id=$employeeId;Code='SEARCH-E';FullName='Search Employee edited';Email='search.employee@example.test';PhoneNumber='0909999998';Position='Chuc vu cu dac biet';positionChoice='__other__';HireDate='2026-10-29';IsActive='false'}
            $saved=RequestPage ('/Employees/Edit/'+$employeeId) $cookies $fields
            Check ($saved.Status -eq 302 -and (SqlScalar "SELECT Position FROM Employees WHERE Id=$employeeId") -eq 'Chuc vu cu dac biet') 'editing employee does not lose legacy position'
        }
        if($role -eq 'Admin') {
            $historyCustomer=SqlScalar "SELECT Id FROM Customers WHERE Code='HISTORY-C'"
            $historyEmployee=SqlScalar "SELECT Id FROM Employees WHERE Code='HISTORY-E'"
            $historySupplier=SqlScalar "SELECT Id FROM Suppliers WHERE Code='HISTORY-S'"
            $page=RequestPage ('/Customers/Details/'+$historyCustomer+'?ordersPage=2') $cookies
            Check ($page.Status -eq 200 -and $page.Body.Contains('Lịch sử mua hàng') -and $page.Body.Contains('Trang 2 / 2') -and $page.Body.Contains('/SalesOrders/Details/')) 'customer history page and real order links render'
            $page=RequestPage ('/Employees/Details/'+$historyEmployee+'?ordersPage=2&receiptsPage=2') $cookies
            Check ($page.Status -eq 200 -and $page.Body.Contains('Đơn hàng đã xử lý') -and $page.Body.Contains('Phiếu nhập do nhân viên lập') -and !$page.Body.Contains('HISTORY-ONLINE')) 'employee separates receipts and assigned sales without claiming all online orders'
            $page=RequestPage ('/Suppliers/Details/'+$historySupplier+'?receiptsPage=2') $cookies
            Check ($page.Status -eq 200 -and $page.Body.Contains('Lịch sử cung cấp hàng') -and $page.Body.Contains('/PurchaseReceipts/Details/')) 'supplier history links to purchase receipts'
            $emptyCustomer=SqlScalar "SELECT Id FROM Customers WHERE Code='SEARCH-C'"
            $page=RequestPage ('/Customers/Details/'+$emptyCustomer) $cookies
            Check ($page.Status -eq 200 -and $page.Body.Contains('Chưa có đơn hàng gắn với hồ sơ này')) 'customer empty history is friendly'
            Check ((RequestPage ('/Customers/Details/'+$historyCustomer) $oldCookies).Status -eq 403) 'customer account cannot read staff customer history pages'
        }
        $noToken = RequestPage '/AdminUsers/SetRole' $cookies 'id=invalid&role=Admin'
        Check (($role -eq 'Admin' -and $noToken.Status -eq 500 -and $noToken.Body -match 'HttpAntiForgeryException|anti-forgery') -or ($role -ne 'Admin' -and $noToken.Status -in @(302,401,403))) ($role + ' unauthorized/tokenless role POST rejected')
        $withToken = RequestPage '/AdminUsers/SetRole' $cookies (FormData @{ __RequestVerificationToken = (Token $dashboardPage.Body); id='invalid'; role='INVALID' })
        Check (($role -eq 'Admin' -and $withToken.Status -eq 400) -or ($role -ne 'Admin' -and $withToken.Status -in @(302,401,403))) ($role + ' valid-token role POST authorization')
        $logout = RequestPage '/Account/LogOff' $cookies (FormData @{ __RequestVerificationToken = (Token $dashboardPage.Body) })
        Check ($logout.Status -eq 302) ($role + ' logout')
        $afterLogout = RequestPage '/AdminUsers' $cookies
        Check ($afterLogout.Status -in @(302,401,403)) ($role + ' protected GET after logout')
    }
    $registeredCookies = New-Object Net.CookieContainer
    $register = RequestPage '/Account/Register' $registeredCookies
    $email = 'registered-' + [Guid]::NewGuid().ToString('N') + '@ui.example.test'
    $registration = RequestPage '/Account/Register' $registeredCookies (FormData @{ __RequestVerificationToken = (Token $register.Body); Email=$email; Password=$env:DACN_UI_TEST_PASSWORD; ConfirmPassword=$env:DACN_UI_TEST_PASSWORD })
    Check ($registration.Status -eq 302) 'HTTP registration succeeds'
    $registeredHome = RequestPage '/' $registeredCookies
    Check ($registeredHome.Status -eq 200 -and !$registeredHome.Body.Contains('Doanh thu ghi nhận')) 'new account gets customer home'
    $sql = New-Object Data.SqlClient.SqlConnection($builder.ConnectionString)
    try {
        $sql.Open(); $command = $sql.CreateCommand()
        $command.CommandText = 'SELECT r.Name FROM AspNetUsers u JOIN AspNetUserRoles ur ON ur.UserId=u.Id JOIN AspNetRoles r ON r.Id=ur.RoleId WHERE u.Email=@email'
        [void]$command.Parameters.AddWithValue('@email',$email)
        $reader = $command.ExecuteReader(); $roles = @(); while ($reader.Read()) { $roles += $reader.GetString(0) }
        Check ($roles.Count -eq 1 -and $roles[0] -eq 'KhachHang') 'HTTP registration persisted only KhachHang role'
    } finally { $sql.Dispose() }
    # Keep independent Identity cookies while deliberately sharing ASP.NET session:
    # this also covers switching accounts within a browser session.
    $secondCookies = New-Object Net.CookieContainer
    $null = RequestPage '/Store' $registeredCookies
    $login = RequestPage '/Account/Login' $secondCookies
    $signedIn = RequestPage '/Account/Login' $secondCookies (FormData @{ __RequestVerificationToken=(Token $login.Body); Email='KhachHang@ui.example.test'; Password=$env:DACN_UI_TEST_PASSWORD; RememberMe='false' })
    Check ($signedIn.Status -eq 302) 'second customer login'
    foreach ($cookie in $registeredCookies.GetCookies([uri]"http://localhost:$Port/")) {
        if ($cookie.Name -eq 'ASP.NET_SessionId') { $secondCookies.Add([uri]"http://localhost:$Port/", (New-Object Net.Cookie($cookie.Name,$cookie.Value,'/'))) }
    }
    $ids = @(); $prices = @{}; $stocks = @{}
    $connection = New-Object Data.SqlClient.SqlConnection($builder.ConnectionString)
    try {
        $connection.Open(); $cmd = $connection.CreateCommand()
        $cmd.CommandText = 'SELECT TOP 3 p.Id,p.SalePrice,p.QuantityInStock FROM Products p JOIN Categories c ON p.CategoryId=c.Id WHERE p.IsActive=1 AND c.IsActive=1 AND p.QuantityInStock>=5 ORDER BY p.Id'
        $reader = $cmd.ExecuteReader()
        while ($reader.Read()) { $cartProductId=$reader.GetInt32(0); $ids += $cartProductId; $prices[$cartProductId]=$reader.GetDecimal(1); $stocks[$cartProductId]=$reader.GetInt32(2) }
    } finally { $connection.Dispose() }
    Check ($ids.Count -eq 3) 'three available fixture products'
    foreach ($cartProductId in $ids) { $added = CartPost 'AddToCart' $registeredCookies $cartProductId 1; Check ($added.Status -eq 302) ('add product ' + $cartProductId) }
    $cart = RequestPage '/Store/Cart' $registeredCookies
    Check ($cart.Body.Contains('cart-thumbnail') -and $cart.Body.Contains('Tăng số lượng') -and $cart.Body.Contains('Giảm số lượng') -and $cart.Body.Contains('Tiến hành đặt hàng')) 'cart renders images stepper POST controls and checkout summary'
    foreach ($cartProductId in $ids) { Check ($cart.Body -match ('id="qty-' + $cartProductId + '"[^>]*value="1"')) ('cart line ' + $cartProductId) }
    $otherCart = RequestPage '/Store/Cart' $secondCookies
    Check ($otherCart.Status -eq 200 -and $otherCart.Body -notmatch 'id="qty-') 'second customer cannot see cart even with same session'
    $catalog = RequestPage '/Store' $registeredCookies
    $addData = FormData @{ __RequestVerificationToken=(Token $catalog.Body); version=(Field $catalog.Body 'version'); productId=$ids[0]; quantity=1 }
    $null = RequestPage '/Store/AddToCart' $registeredCookies $addData
    $null = RequestPage '/Store/AddToCart' $registeredCookies $addData
    $cart = RequestPage '/Store/Cart' $registeredCookies
    Check ($cart.Body -match ('id="qty-' + $ids[0] + '"[^>]*value="2"')) 'replayed add changes quantity only once'
    $null = CartPost 'UpdateCart' $registeredCookies $ids[0] 3
    $null = CartPost 'UpdateCart' $registeredCookies $ids[0] 2
    $null = CartPost 'UpdateCart' $registeredCookies $ids[2] 0
    $cart = RequestPage '/Store/Cart' $registeredCookies
    Check ($cart.Body -notmatch ('id="qty-' + $ids[2] + '"')) 'remove individual product'
    $expected = 2*$prices[$ids[0]] + $prices[$ids[1]]
    Check ($cart.Body.Contains($expected.ToString('N0',[Globalization.CultureInfo]::GetCultureInfo('vi-VN')))) 'cart total recalculated after quantity and removal'
    $null = CartPost 'AddToCart' $registeredCookies $ids[2] 1
    $negative = CartPost 'UpdateCart' $registeredCookies $ids[0] -1
    Check ($negative.Status -eq 400) 'negative quantity rejected'
    $null = CartPost 'AddToCart' $registeredCookies 2147483647 1
    $null = CartPost 'UpdateCart' $registeredCookies $ids[0] ([Math]::Min(1000,$stocks[$ids[0]]+1))
    $cart = RequestPage '/Store/Cart' $registeredCookies
    Check ($cart.Body -match ('id="qty-' + $ids[0] + '"[^>]*value="2"') -and $cart.Body -notmatch 'qty-2147483647') 'invalid product and excess stock preserve cart'
    $noCsrf = RequestPage '/Store/ClearCart' $registeredCookies 'version=invalid'
    Check ($noCsrf.Status -eq 500 -and $noCsrf.Body -match 'HttpAntiForgeryException|anti-forgery') 'cart requires anti-forgery'
    $checkout = RequestPage '/Store/Checkout' $registeredCookies
    Check ($checkout.Status -eq 200) 'multi-line checkout renders'
    $checkoutToken = Field $checkout.Body 'Token'
    $checkoutData = FormData @{ __RequestVerificationToken=(Token $checkout.Body); Token=$checkoutToken; FullName='HTTP Customer'; PhoneNumber='0901234567'; ShippingAddress='HTTP test address'; Note='HTTP note'; SalePrice=1; TotalAmount=1; Status='Completed'; CustomerId=1; ProductId=2147483647; Quantity=1000 }
    $foreign = RequestPage '/Store/Checkout' $secondCookies (FormData @{ __RequestVerificationToken=(Token $otherCart.Body); Token=$checkoutToken; FullName='Other'; PhoneNumber='0901234567'; ShippingAddress='Other' })
    Check ($foreign.Status -eq 400) 'checkout token belongs to customer'
    $placed = RequestPage '/Store/Checkout' $registeredCookies $checkoutData
    Check ($placed.Status -eq 302 -and $placed.Location -match '/Store/Details/(\d+)') 'one multi-product order created'
    $orderId = [int]([regex]::Match($placed.Location,'/Store/Details/(\d+)').Groups[1].Value)
    $replayed = RequestPage '/Store/Checkout' $registeredCookies $checkoutData
    Check ($replayed.Status -eq 302 -and $replayed.Location -eq $placed.Location) 'checkout replay redirects to same order'
    Check ((SqlScalar "SELECT COUNT(*) FROM SalesOrders WHERE OrderNumber='$checkoutToken'") -eq 1) 'checkout persisted once'
    Check ((SqlScalar "SELECT COUNT(*) FROM SalesOrderDetails WHERE SalesOrderId=$orderId") -eq 3) 'one order contains three details'
    $expected += $prices[$ids[2]]
    Check ((SqlScalar "SELECT TotalAmount FROM SalesOrders WHERE Id=$orderId AND Status=0 AND PaymentStatus=0") -eq $expected) 'pending unpaid total ignores posted prices and status'
    Check ((SqlScalar "SELECT COUNT(*) FROM InventoryTransactions WHERE SalesOrderId=$orderId") -eq 0) 'checkout never deducts inventory'
    foreach ($cartProductId in $ids) { Check ((SqlScalar "SELECT QuantityInStock FROM Products WHERE Id=$cartProductId") -eq $stocks[$cartProductId]) ('pending stock unchanged ' + $cartProductId) }
    $details = RequestPage $placed.Location $registeredCookies
    Check ($details.Status -eq 200 -and $details.Body.Contains('HTTP note')) 'owner sees order and note'
    Check ($details.Body.Contains('Chờ xác nhận') -and $details.Body.Contains('Chưa thanh toán') -and !$details.Body.Contains('>Pending<') -and !$details.Body.Contains('>Unpaid<')) 'customer order and payment statuses render Vietnamese labels'
    $otherDetails = RequestPage $placed.Location $secondCookies
    Check ($otherDetails.Status -eq 404) 'other customer cannot read order'
    $ownOrders = RequestPage '/Store/Orders' $registeredCookies
    $otherOrders = RequestPage '/Store/Orders' $secondCookies
    Check ($ownOrders.Body.Contains($checkoutToken) -and !$otherOrders.Body.Contains($checkoutToken)) 'order lists isolated by Identity'
    Check ($ownOrders.Body.Contains('Xem chi tiết') -and $ownOrders.Body.Contains('Chờ xác nhận')) 'order list includes Vietnamese status and detail button'
    $cart = RequestPage '/Store/Cart' $registeredCookies
    Check ($cart.Body -notmatch 'id="qty-') 'successful checkout empties cart'
    $null = CartPost 'AddToCart' $registeredCookies $ids[0] 1
    $cart = RequestPage '/Store/Cart' $registeredCookies
    $cleared = RequestPage '/Store/ClearCart' $registeredCookies (FormData @{ __RequestVerificationToken=(Token $cart.Body); version=(Field $cart.Body 'version') })
    $cart = RequestPage '/Store/Cart' $registeredCookies
    Check ($cleared.Status -eq 302 -and $cart.Body -notmatch 'id="qty-') 'clear whole cart'
    # Reorder uses the already owned multi-line order; all DB mutations below are isolated test fixture state only.
    $firstId=$ids[0];$secondId=$ids[1];$thirdId=$ids[2]
    $oldPrice=[decimal](SqlScalar "SELECT SalePrice FROM Products WHERE Id=$firstId")
    $oldActive=[int](SqlScalar "SELECT CAST(IsActive AS int) FROM Products WHERE Id=$thirdId")
    $newPrice=$oldPrice+123;$newPriceSql=$newPrice.ToString([Globalization.CultureInfo]::InvariantCulture)
    $orderCountBefore=SqlScalar 'SELECT COUNT(*) FROM SalesOrders'
    try {
        $null=CartPost 'AddToCart' $registeredCookies $firstId 1
        $null=SqlScalar "UPDATE Products SET SalePrice=$newPriceSql WHERE Id=$firstId; UPDATE Products SET IsActive=0 WHERE Id=$thirdId"
        $ownDetails=RequestPage ('/Store/Details/'+$orderId) $registeredCookies
        Check ($ownDetails.Status -eq 200 -and $ownDetails.Body.Contains('Mua lại')) 'reorder button renders below shopping link'
        $reorderData=FormData @{__RequestVerificationToken=(Token $ownDetails.Body);id=$orderId;version=(Field $ownDetails.Body 'version')}
        $reorder=RequestPage '/Store/Reorder' $registeredCookies $reorderData
        $reorderCart=RequestPage '/Store/Cart' $registeredCookies
        $orderedFirst=[int](SqlScalar "SELECT Quantity FROM SalesOrderDetails WHERE SalesOrderId=$orderId AND ProductId=$firstId")
        $orderedSecond=[int](SqlScalar "SELECT Quantity FROM SalesOrderDetails WHERE SalesOrderId=$orderId AND ProductId=$secondId")
        $firstExpected=[Math]::Min(1000,[Math]::Min(1+$orderedFirst,[int](SqlScalar "SELECT QuantityInStock FROM Products WHERE Id=$firstId")))
        $firstActual=[int]([regex]::Match($reorderCart.Body,('id="qty-'+$firstId+'"[^>]*value="(\d+)"')).Groups[1].Value)
        $secondActual=[int]([regex]::Match($reorderCart.Body,('id="qty-'+$secondId+'"[^>]*value="(\d+)"')).Groups[1].Value)
        Check ($reorder.Status -eq 302 -and $firstActual -eq $firstExpected -and $secondActual -eq $orderedSecond) 'reorder merges owned multi-line order into existing cart'
        Check (!$reorderCart.Body.Contains('data-cart-product="'+$thirdId+'"') -and $reorderCart.Body.Contains('ngừng bán')) 'reorder skips inactive line with clear warning'
        $priceLabel=$newPrice.ToString('N0',[Globalization.CultureInfo]::GetCultureInfo('vi-VN'))+' ₫'
        Check ($reorderCart.Body.Contains($priceLabel)) 'reorder cart uses current price rather than old order price'
        Check ((SqlScalar 'SELECT COUNT(*) FROM SalesOrders') -eq $orderCountBefore) 'reorder never creates a Pending order'
        $replay=RequestPage '/Store/Reorder' $registeredCookies $reorderData
        $sameCart=RequestPage '/Store/Cart' $registeredCookies
        $afterReplay=[int]([regex]::Match($sameCart.Body,('id="qty-'+$firstId+'"[^>]*value="(\d+)"')).Groups[1].Value)
        Check ($replay.Status -eq 302 -and $afterReplay -eq $firstExpected) 'reorder repeat with stale version cannot add duplicates'
        $otherCatalog=RequestPage '/Store' $secondCookies
        $foreign=RequestPage '/Store/Reorder' $secondCookies (FormData @{__RequestVerificationToken=(Token $otherCatalog.Body);id=$orderId;version=(Field $otherCatalog.Body 'version')})
        Check ($foreign.Status -eq 404) 'reorder POST rejects another customer order'
        $noCsrf=RequestPage '/Store/Reorder' $registeredCookies ('id='+$orderId+'&version=invalid')
        Check ($noCsrf.Status -ge 400) 'reorder POST requires valid CSRF token'
    } finally {
        $oldPriceSql=$oldPrice.ToString([Globalization.CultureInfo]::InvariantCulture)
        $null=SqlScalar "UPDATE Products SET SalePrice=$oldPriceSql WHERE Id=$firstId; UPDATE Products SET IsActive=$oldActive WHERE Id=$thirdId"
        $cleanup=RequestPage '/Store/Cart' $registeredCookies
        $null=RequestPage '/Store/ClearCart' $registeredCookies (FormData @{__RequestVerificationToken=(Token $cleanup.Body);version=(Field $cleanup.Body 'version')})
    }

    $cancelPage=RequestPage ('/Store/Details/'+$orderId) $registeredCookies
    Check ($cancelPage.Body.Contains('data-cancel-order=') -and $cancelPage.Body.Contains('Giữ đơn hàng') -and $cancelPage.Body.Contains('Xác nhận hủy')) 'pending details renders cancel confirmation modal'
    $foreignCancel=RequestPage '/Store/CancelPending' $secondCookies (FormData @{__RequestVerificationToken=(Token $otherCatalog.Body);id=$orderId})
    Check ($foreignCancel.Status -eq 404 -and (SqlScalar "SELECT Status FROM SalesOrders WHERE Id=$orderId") -eq 0) 'another customer cannot cancel order'
    $missingCancel=RequestPage '/Store/CancelPending' $registeredCookies ('id='+$orderId)
    Check ($missingCancel.Status -ge 400 -and (SqlScalar "SELECT Status FROM SalesOrders WHERE Id=$orderId") -eq 0) 'cancel missing CSRF cannot write'
    $stockBeforeCancel=SqlScalar 'SELECT SUM(CAST(QuantityInStock AS bigint)) FROM Products';$ledgerBeforeCancel=SqlScalar 'SELECT COUNT(*) FROM InventoryTransactions'
    $cancelData=FormData @{__RequestVerificationToken=(Token $cancelPage.Body);id=$orderId}
    $cancelled=RequestPage '/Store/CancelPending' $registeredCookies $cancelData
    $afterCancel=RequestPage ('/Store/Details/'+$orderId) $registeredCookies
    Check ($cancelled.Status -eq 302 -and $afterCancel.Body.Contains('Đã hủy đơn hàng.') -and $afterCancel.Body.Contains('Đã hủy') -and !$afterCancel.Body.Contains('data-cancel-order=')) 'owner cancels pending and sees success/history without another cancel button'
    Check ((SqlScalar "SELECT COUNT(*) FROM SalesOrderDetails WHERE SalesOrderId=$orderId") -eq 3 -and (SqlScalar "SELECT Status FROM SalesOrders WHERE Id=$orderId") -eq 3 -and (SqlScalar 'SELECT SUM(CAST(QuantityInStock AS bigint)) FROM Products') -eq $stockBeforeCancel -and (SqlScalar 'SELECT COUNT(*) FROM InventoryTransactions') -eq $ledgerBeforeCancel) 'customer cancellation preserves lines stock and ledger'
    $repeatedCancel=RequestPage '/Store/CancelPending' $registeredCookies $cancelData
    $repeatMessage=RequestPage ('/Store/Details/'+$orderId) $registeredCookies
    Check ($repeatedCancel.Status -eq 302 -and $repeatMessage.Body.Contains('không thể hủy lại')) 'customer cancel replay rejected with friendly message'
    $catalog = RequestPage '/Store' $registeredCookies
    Check ($catalog.Status -eq 200 -and $catalog.Body.Contains('product-grid') -and $catalog.Body.Contains('modal-dialog-centered') -and $catalog.Body.Contains('aria-labelledby="productModalTitle"')) 'product grid and accessible centered modal render'
    $cardsOnly = ($catalog.Body -split '<div class="modal fade"')[0]
    Check ($cardsOnly -notmatch 'type="number"' -and $cardsOnly -match 'type="hidden" name="quantity" value="1"') 'product cards add one without quantity input'
    $css = RequestPage '/Content/customer.css' $guestCookies
    $js = RequestPage '/Scripts/customer.js' $guestCookies
    $placeholder = RequestPage '/Content/product-placeholder.svg' $guestCookies
    Check ($css.Status -eq 200 -and $js.Status -eq 200 -and $placeholder.Status -eq 200) 'local customer CSS JS and placeholder served'
    $product = RequestPage ('/Store/ProductDetails/' + $ids[0]) $guestCookies
    $productJson = $product.RawBody | ConvertFrom-Json
    Check ($product.Status -eq 200 -and $productJson.success -and $productJson.product.id -eq $ids[0] -and $productJson.product.sku -and $productJson.product.category -and !$productJson.signedIn) 'anonymous modal data comes from active server product'
    $missing = RequestPage '/Store/ProductDetails/2147483647' $guestCookies
    Check ($missing.Status -eq 404 -and !($missing.RawBody | ConvertFrom-Json).success) 'missing modal product is JSON 404'
    $ajaxData = FormData @{ __RequestVerificationToken=(Token $catalog.Body); version=(Field $catalog.Body 'version'); productId=$ids[0]; quantity=1; price=1; stock=9999 }
    $ajaxAdded = RequestPage '/Store/AddToCart' $registeredCookies $ajaxData -Ajax
    $ajaxJson = $ajaxAdded.RawBody | ConvertFrom-Json
    Check ($ajaxAdded.Status -eq 200 -and !$ajaxAdded.Location -and $ajaxJson.success -and $ajaxJson.count -eq 1 -and $ajaxJson.productName) 'AJAX add returns success count version and server name without redirect'
    $ajaxReplayed = RequestPage '/Store/AddToCart' $registeredCookies $ajaxData -Ajax
    $replayJson = $ajaxReplayed.RawBody | ConvertFrom-Json
    Check ($ajaxReplayed.Status -eq 409 -and !$replayJson.success -and $replayJson.count -eq 1 -and $replayJson.version -eq $ajaxJson.version) 'AJAX replay returns conflict and does not double add'
    $nextData = FormData @{ __RequestVerificationToken=(Token $catalog.Body); version=$ajaxJson.version; productId=$ids[1]; quantity=2 }
    $nextAdd = RequestPage '/Store/AddToCart' $registeredCookies $nextData -Ajax
    $nextJson = $nextAdd.RawBody | ConvertFrom-Json
    Check ($nextAdd.Status -eq 200 -and $nextJson.success -and $nextJson.count -eq 3) 'AJAX adds different product and returns total units'
    $cart = RequestPage '/Store/Cart' $registeredCookies
    Check ($cart.Body.Contains('aria-label="3 sản phẩm trong giỏ"')) 'header cart badge persists after reload'
    $excess = RequestPage '/Store/AddToCart' $registeredCookies (FormData @{ __RequestVerificationToken=(Token $catalog.Body); version=$nextJson.version; productId=$ids[0]; quantity=$stocks[$ids[0]] }) -Ajax
    $excessJson = $excess.RawBody | ConvertFrom-Json
    Check ($excess.Status -eq 409 -and !$excessJson.success -and $excessJson.count -eq 3) 'AJAX checks server stock and retains cart on failure'
    $inactiveId = [int]$ids[0]
    try {
        $null = SqlScalar "UPDATE Products SET IsActive=0 WHERE Id=$inactiveId"
        $inactive = RequestPage ('/Store/ProductDetails/' + $inactiveId) $registeredCookies
        Check ($inactive.Status -eq 404 -and !($inactive.RawBody | ConvertFrom-Json).success) 'modal rechecks discontinued product'
        $inactiveAdd = RequestPage '/Store/AddToCart' $registeredCookies (FormData @{ __RequestVerificationToken=(Token $catalog.Body); version=$nextJson.version; productId=$inactiveId; quantity=1 }) -Ajax
        Check ($inactiveAdd.Status -eq 409 -and !($inactiveAdd.RawBody | ConvertFrom-Json).success) 'AJAX rejects discontinued product from stale card'
    } finally { $null = SqlScalar "UPDATE Products SET IsActive=1 WHERE Id=$inactiveId" }
    $ajaxCheckout = RequestPage '/Store/Checkout' $registeredCookies
    $ajaxToken = Field $ajaxCheckout.Body 'Token'
    $ajaxPlaced = RequestPage '/Store/Checkout' $registeredCookies (FormData @{ __RequestVerificationToken=(Token $ajaxCheckout.Body); Token=$ajaxToken; FullName='AJAX Customer'; PhoneNumber='0901234567'; ShippingAddress='AJAX address' })
    Check ($ajaxPlaced.Status -eq 302 -and (SqlScalar "SELECT COUNT(*) FROM SalesOrders WHERE OrderNumber='$ajaxToken' AND Status=0 AND PaymentStatus=0") -eq 1) 'AJAX-built cart checks out as Pending Unpaid'
    $confirmedId=SqlScalar "SELECT Id FROM SalesOrders WHERE OrderNumber='$ajaxToken'"
    $stalePending=RequestPage ('/Store/Details/'+$confirmedId) $registeredCookies
    $adminAgain=New-Object Net.CookieContainer;$adminLogin=RequestPage '/Account/Login' $adminAgain
    $null=RequestPage '/Account/Login' $adminAgain (FormData @{__RequestVerificationToken=(Token $adminLogin.Body);Email='Admin@ui.example.test';Password=$env:DACN_UI_TEST_PASSWORD})
    $adminOrder=RequestPage ('/SalesOrders/Details/'+$confirmedId) $adminAgain
    $confirmedByAdmin=RequestPage '/SalesOrders/Confirm' $adminAgain (FormData @{__RequestVerificationToken=(Token $adminOrder.Body);id=$confirmedId})
    Check ($confirmedByAdmin.Status -eq 302 -and (SqlScalar "SELECT Status FROM SalesOrders WHERE Id=$confirmedId") -eq 1) 'admin existing confirmation still succeeds'
    $staleCancel=RequestPage '/Store/CancelPending' $registeredCookies (FormData @{__RequestVerificationToken=(Token $stalePending.Body);id=$confirmedId})
    $processedMessage=RequestPage ('/Store/Details/'+$confirmedId) $registeredCookies
    Check ($staleCancel.Status -eq 302 -and $processedMessage.Body.Contains('Không thể hủy do đơn hàng đã được xử lý.') -and (SqlScalar "SELECT Status FROM SalesOrders WHERE Id=$confirmedId") -eq 1) 'stale pending form cannot cancel admin confirmed order'
    $anonymousAjax = RequestPage '/Store/AddToCart' $guestCookies 'productId=1&quantity=1&version=invalid' -Ajax
    Check ($anonymousAjax.Status -eq 401 -and !$anonymousAjax.Location -and !($anonymousAjax.RawBody | ConvertFrom-Json).success) 'anonymous AJAX returns JSON 401 without Login redirect'
    $latest = RequestPage '/Store' $registeredCookies
    $null = RequestPage '/Account/LogOff' $registeredCookies (FormData @{ __RequestVerificationToken=(Token $latest.Body) })
    $expired = RequestPage '/Store/AddToCart' $registeredCookies $ajaxData -Ajax
    Check ($expired.Status -eq 401 -and !$expired.Location -and !($expired.RawBody | ConvertFrom-Json).success) 'expired AJAX session returns clear JSON error without redirect loop'
    $switchLogin=RequestPage '/Account/Login' $registeredCookies
    $null=RequestPage '/Account/Login' $registeredCookies (FormData @{__RequestVerificationToken=(Token $switchLogin.Body);Email='Admin@ui.example.test';Password=$env:DACN_UI_TEST_PASSWORD})
    $sameBrowser=RequestPage '/Store' $registeredCookies
    $oldTab=RequestPage '/Store/Checkout' $registeredCookies
    Check ($sameBrowser.Body.Contains('Chế độ xem trước') -and $oldTab.Status -eq 403) 'same cookie container switched to Admin affects all tabs and blocks stale customer checkout'
    Write-Output ('TOTAL HTTP PASS: ' + $count)
    Write-Output ('Evidence: ' + $work)
    Write-Output ('Retained isolated DB: ' + $builder.InitialCatalog)
} finally {
    $env:DACN_UI_TEST_PASSWORD = $null
    if ($iis -and !$iis.HasExited) { Stop-Process -Id $iis.Id }
}
