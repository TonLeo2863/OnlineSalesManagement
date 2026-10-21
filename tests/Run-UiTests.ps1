[CmdletBinding()]
param([int]$Port = 64265, [switch]$IncludeBrowser)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$work = Join-Path $root ('artifacts\ui-verification\run-' + [Guid]::NewGuid().ToString('N'))
$site = Join-Path $work 'site'
[void][IO.Directory]::CreateDirectory($site)
$env:DACN_UI_TEST_PASSWORD = 'UiTest!9-' + [Guid]::NewGuid().ToString('N')
$iis = $null
$chrome = $null
$count = 0
function Check([bool]$ok, [string]$message) {
    if (!$ok) { throw ('FAIL: ' + $message) }
    $script:count++
    Write-Output ('PASS: ' + $message)
}
function RequestPage([string]$path, [Net.CookieContainer]$cookies, [string]$data = $null) {
    $request = [Net.HttpWebRequest]::Create("http://localhost:$Port$path")
    $request.CookieContainer = $cookies
    $request.AllowAutoRedirect = $false
    $request.Timeout = 120000
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
    Check ($null -ne $guest -and $guest.Status -eq 200) 'anonymous home responds'
    Check ($guest.ContentType -match 'charset=utf-8' -and $guest.Body.Contains('Quản lý bán hàng') -and !$guest.Body.Contains('Quáº£n')) 'HTTP UTF-8 bytes and Vietnamese text'
    Check (!$guest.Body.Contains('Doanh thu ghi nhận') -and $guest.Body.Contains('Khám phá cửa hàng')) 'anonymous shopping home excludes staff data'
    foreach ($role in @('Admin','Kho','BanHang','KhachHang')) {
        $cookies = New-Object Net.CookieContainer
        $login = RequestPage '/Account/Login' $cookies
        $post = FormData @{ __RequestVerificationToken = (Token $login.Body); Email = ($role + '@ui.example.test'); Password = $env:DACN_UI_TEST_PASSWORD; RememberMe = 'false' }
        $signedIn = RequestPage '/Account/Login' $cookies $post
        Check ($signedIn.Status -eq 302) ($role + ' HTTP login')
        $dashboardPage = RequestPage '/' $cookies
        Check ($dashboardPage.Status -eq 200) ($role + ' home renders')
        [IO.File]::WriteAllText((Join-Path $work ($role + '.html')), $dashboardPage.RawBody, (New-Object Text.UTF8Encoding($false)))
        Check ($dashboardPage.Body.Contains('Quản lý bán hàng') -and !$dashboardPage.Body.Contains('Quáº£n')) ($role + ' Vietnamese render')
        Check ($dashboardPage.Body.Contains('Doanh thu ghi nhận') -eq ($role -in @('Admin','BanHang'))) ($role + ' sales dashboard visibility')
        Check ($dashboardPage.Body.Contains('Cần bổ sung hàng') -eq ($role -in @('Admin','Kho'))) ($role + ' inventory dashboard visibility')
        foreach ($controller in @('AdminUsers','Products','Categories','Customers','Employees','Suppliers','InventoryReports','PurchaseReceipts','SalesOrders','Reports')) {
            $allowed = $role -eq 'Admin' -or ($role -eq 'Kho' -and $controller -in @('Products','Categories','Suppliers','InventoryReports','PurchaseReceipts')) -or ($role -eq 'BanHang' -and $controller -in @('Customers','SalesOrders'))
            Check ($dashboardPage.Body.Contains('href="/' + $controller + '"') -eq $allowed) ($role + ' sidebar ' + $controller)
            $page = RequestPage ('/' + $controller) $cookies
            Check (($allowed -and $page.Status -eq 200) -or (!$allowed -and $page.Status -in @(302,401,403))) ($role + ' direct GET ' + $controller)
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
    # All writes below remain on the guarded Dacn05_Test_ fixture.
    $sql = New-Object Data.SqlClient.SqlConnection($builder.ConnectionString)
    try {
        $sql.Open()
        $command = $sql.CreateCommand(); $command.CommandText = "SELECT Id FROM Products WHERE Sku='IT-03'"
        $productId = [int]$command.ExecuteScalar()
        $command.CommandText = "SELECT QuantityInStock FROM Products WHERE Id=$productId"
        $stockBefore = [int]$command.ExecuteScalar()
        $checkout = RequestPage ("/Store/Checkout/$productId") $registeredCookies
        Check ($checkout.Status -eq 200) 'customer checkout renders'
        $orderTokenMatch = [regex]::Match($checkout.Body, '<input[^>]*name="Token"[^>]*value="([^"]+)"')
        Check ($orderTokenMatch.Success) 'checkout provides replay token'
        $checkoutForm = FormData @{ __RequestVerificationToken=(Token $checkout.Body); Token=$orderTokenMatch.Groups[1].Value; ProductId=$productId; Quantity=1; FullName='HTTP Customer'; PhoneNumber='0901234567'; ShippingAddress='Isolated test address' }
        $placed = RequestPage '/Store/Checkout' $registeredCookies $checkoutForm
        Check ($placed.Status -eq 302 -and $placed.Location -match '/Store/Details/\d+') 'COD checkout redirects to own order'
        $orderId = [int]([regex]::Match($placed.Location, '/Store/Details/(\d+)').Groups[1].Value)
        $command.CommandText = "SELECT COUNT(*) FROM SalesOrders WHERE Id=$orderId AND Status=0 AND PaymentMethod=2"
        Check ([int]$command.ExecuteScalar() -eq 1) 'COD order persists Pending'
        $command.CommandText = "SELECT QuantityInStock FROM Products WHERE Id=$productId"
        Check ([int]$command.ExecuteScalar() -eq $stockBefore) 'HTTP checkout does not subtract stock'
        $pendingPage = RequestPage $placed.Location $registeredCookies
        [IO.File]::WriteAllText((Join-Path $work 'customer-pending.html'), $pendingPage.RawBody, (New-Object Text.UTF8Encoding($false)))
        Check ($pendingPage.Status -eq 200 -and $pendingPage.Body.Contains('Third demo product') -and ($pendingPage.Body.Contains('Chờ xác nhận') -or $pendingPage.Body.Contains('Pending'))) 'customer sees own Pending order'
        $replay = RequestPage '/Store/Checkout' $registeredCookies $checkoutForm
        Check ($replay.Status -eq 400) 'checkout replay rejected'
        $otherCookies = New-Object Net.CookieContainer
        $login = RequestPage '/Account/Login' $otherCookies
        $otherLogin = RequestPage '/Account/Login' $otherCookies (FormData @{ __RequestVerificationToken=(Token $login.Body); Email='KhachHang@ui.example.test'; Password=$env:DACN_UI_TEST_PASSWORD })
        Check ($otherLogin.Status -eq 302) 'second customer login for ownership test'
        Check ((RequestPage $placed.Location $otherCookies).Status -eq 404) 'other customer cannot read order'
        $adminCookies = New-Object Net.CookieContainer
        $login = RequestPage '/Account/Login' $adminCookies
        $adminLogin = RequestPage '/Account/Login' $adminCookies (FormData @{ __RequestVerificationToken=(Token $login.Body); Email='Admin@ui.example.test'; Password=$env:DACN_UI_TEST_PASSWORD })
        Check ($adminLogin.Status -eq 302) 'Admin login for order confirmation'
        $adminOrder = RequestPage ("/SalesOrders/Details/$orderId") $adminCookies
        $confirmData = FormData @{ __RequestVerificationToken=(Token $adminOrder.Body); id=$orderId }
        Check ((RequestPage '/SalesOrders/Confirm' $adminCookies $confirmData).Status -eq 302) 'Admin confirms online order'
        Check ((RequestPage '/SalesOrders/Confirm' $adminCookies $confirmData).Status -eq 302) 'duplicate confirmation handled without HTTP crash'
        $command.CommandText = "SELECT QuantityInStock FROM Products WHERE Id=$productId"
        Check ([int]$command.ExecuteScalar() -eq ($stockBefore-1)) 'HTTP confirmation subtracts stock exactly once'
        $command.CommandText = "SELECT COUNT(*) FROM InventoryTransactions WHERE SalesOrderId=$orderId AND QuantityChange=-1"
        Check ([int]$command.ExecuteScalar() -eq 1) 'HTTP confirmation writes one ledger entry'
        $confirmedPage = RequestPage $placed.Location $registeredCookies
        [IO.File]::WriteAllText((Join-Path $work 'customer-confirmed.html'), $confirmedPage.RawBody, (New-Object Text.UTF8Encoding($false)))
        Check ($confirmedPage.Status -eq 200 -and ($confirmedPage.Body.Contains('Đã xác nhận') -or $confirmedPage.Body.Contains('Confirmed'))) 'customer page reflects changed order status'
        Check ((RequestPage ("/SalesOrders/Invoice/$orderId") $adminCookies).Status -eq 200) 'confirmed invoice renders over HTTP'
        foreach ($path in @('/Reports','/Reports/Purchases','/Reports/Sales','/InventoryReports')) {
            Check ((RequestPage $path $adminCookies).Status -eq 200) ('post-confirmation report renders ' + $path)
        }
        $completePage = RequestPage ("/SalesOrders/Details/$orderId") $adminCookies
        Check ((RequestPage '/SalesOrders/Complete' $adminCookies (FormData @{ __RequestVerificationToken=(Token $completePage.Body); id=$orderId })).Status -eq 302) 'Admin completes confirmed order'
        $command.CommandText = "SELECT QuantityInStock FROM Products WHERE Id=$productId"
        Check ([int]$command.ExecuteScalar() -eq ($stockBefore-1)) 'HTTP Complete does not subtract stock again'
    } finally { $sql.Dispose() }
    if ($IncludeBrowser) {
        $chromePath = Join-Path $env:ProgramFiles 'Google\Chrome\Application\chrome.exe'
        if (!(Test-Path -LiteralPath $chromePath)) { throw 'Chrome unavailable; browser/print checks NOT TESTED' }
        $listener = New-Object Net.Sockets.TcpListener([Net.IPAddress]::Loopback, 0)
        $listener.Start(); $debugPort = $listener.LocalEndpoint.Port; $listener.Stop()
        $browserOutput = Join-Path $work 'browser'
        [void][IO.Directory]::CreateDirectory($browserOutput)
        $env:DACN_BROWSER_BASE = "http://localhost:$Port"
        $env:DACN_BROWSER_OUTPUT = $browserOutput
        $env:DACN_BROWSER_DATABASE = $builder.InitialCatalog
        $env:DACN_BROWSER_PORT = [string]$debugPort
        $env:DACN_BROWSER_ORDER = [string]$orderId
        $profile = Join-Path $work 'chrome-profile'
        $chrome = Start-Process -FilePath $chromePath -ArgumentList '--headless=new','--disable-gpu','--no-first-run',('--remote-debugging-port=' + $debugPort),('--user-data-dir="' + $profile + '"'),'about:blank' -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $work 'chrome.log') -RedirectStandardError (Join-Path $work 'chrome-error.log')
        $ready = $false
        for ($attempt=0; $attempt -lt 30; $attempt++) {
            try { $null = Invoke-WebRequest -UseBasicParsing -Uri "http://127.0.0.1:$debugPort/json/version"; $ready=$true; break } catch { Start-Sleep -Milliseconds 200 }
        }
        if (!$ready) { throw 'Chrome DevTools did not start' }
        & node (Join-Path $PSScriptRoot 'Browser-Handover.mjs')
        if ($LASTEXITCODE -ne 0) { throw 'Browser handover checks failed; evidence retained' }
    }
    Write-Output ('TOTAL HTTP PASS: ' + $count)
    Write-Output ('Evidence: ' + $work)
    Write-Output ('Retained isolated DB: ' + $builder.InitialCatalog)
} finally {
    $env:DACN_UI_TEST_PASSWORD = $null
    foreach ($name in @('DACN_BROWSER_BASE','DACN_BROWSER_OUTPUT','DACN_BROWSER_DATABASE','DACN_BROWSER_PORT','DACN_BROWSER_ORDER')) { [Environment]::SetEnvironmentVariable($name,$null,'Process') }
    if ($chrome -and !$chrome.HasExited) { Stop-Process -Id $chrome.Id }
    if ($iis -and !$iis.HasExited) { Stop-Process -Id $iis.Id }
}
