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
            Check ($staffStore.Status -eq 200 -and $staffStore.Body.Contains('id="sidebar"') -and $staffStore.Body.Contains('product-grid') -and ([regex]::Matches($staffStore.Body,'src="/Scripts/customer.js"')).Count -eq 1) ($role + ' store retains sidebar and loads customer script once')
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
            $accounts = RequestPage '/AdminUsers' $cookies
            Check (([regex]::Matches($accounts.Body, 'data-user-card')).Count -ge 6) 'six account cards render including unusual roles'
            Check ($accounts.Body.Contains('role-grid') -and $accounts.Body.Contains('data-roles=') -and $accounts.Body.Contains('userSearch')) 'account grid confirmation metadata and search render'
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
    $anonymousAjax = RequestPage '/Store/AddToCart' $guestCookies 'productId=1&quantity=1&version=invalid' -Ajax
    Check ($anonymousAjax.Status -eq 401 -and !$anonymousAjax.Location -and !($anonymousAjax.RawBody | ConvertFrom-Json).success) 'anonymous AJAX returns JSON 401 without Login redirect'
    $latest = RequestPage '/Store' $registeredCookies
    $null = RequestPage '/Account/LogOff' $registeredCookies (FormData @{ __RequestVerificationToken=(Token $latest.Body) })
    $expired = RequestPage '/Store/AddToCart' $registeredCookies $ajaxData -Ajax
    Check ($expired.Status -eq 401 -and !$expired.Location -and !($expired.RawBody | ConvertFrom-Json).success) 'expired AJAX session returns clear JSON error without redirect loop'
    Write-Output ('TOTAL HTTP PASS: ' + $count)
    Write-Output ('Evidence: ' + $work)
    Write-Output ('Retained isolated DB: ' + $builder.InitialCatalog)
} finally {
    $env:DACN_UI_TEST_PASSWORD = $null
    if ($iis -and !$iis.HasExited) { Stop-Process -Id $iis.Id }
}
