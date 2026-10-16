using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Security.Claims;
using System.Security.Principal;
using System.Web;
using System.Web.Routing;
using System.Web.Mvc;
using OnlineSalesManagement;
using OnlineSalesManagement.Controllers;

// Tests without database writes. Compile/run with Run-SmokeTests.ps1.
public static class SmokeTests
{
    private static int count;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
        count++;
    }
    private sealed class TestSession : HttpSessionStateBase
    {
        private readonly Dictionary<string, object> values = new Dictionary<string, object>();
        public override object this[string name] { get { object value; return values.TryGetValue(name, out value) ? value : null; } set { values[name] = value; } }
    }
    private sealed class TestContext : HttpContextBase
    {
        private IPrincipal principal;
        private readonly TestSession session = new TestSession();
        public TestContext(string userId, string owner, int productId)
        {
            var claims = new List<Claim>();
            if (userId != null) claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
            principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
            session["OnlineCheckoutToken"] = "checkout-test";
            session["OnlineCheckoutOwner"] = owner;
            session["OnlineCheckoutProductId"] = productId;
        }
        public override IPrincipal User { get { return principal; } set { principal = value; } }
        public override HttpSessionStateBase Session { get { return session; } }
    }
    private static StoreController StoreFor(string userId, string owner, int productId)
    {
        var store = new StoreController();
        store.ControllerContext = new ControllerContext(new TestContext(userId, owner, productId), new RouteData(), store);
        return store;
    }
    public static int Main()
    {
        try
        {
            var p = new Product { QuantityInStock = 10 };
            p.IncreaseStock(5);
            Check(p.QuantityInStock == 15, "stock increase");
            p.DecreaseStock(4);
            Check(p.QuantityInStock == 11, "stock decrease");
            try { p.DecreaseStock(12); throw new Exception("oversell accepted"); }
            catch (InvalidOperationException) { Check(p.QuantityInStock == 11, "oversell preserves stock"); }
            try { p.IncreaseStock(0); throw new Exception("zero accepted"); }
            catch (ArgumentOutOfRangeException) { Check(p.QuantityInStock == 11, "zero rejected"); }
            p.QuantityInStock = int.MaxValue;
            try { p.IncreaseStock(1); throw new Exception("overflow accepted"); }
            catch (OverflowException) { Check(p.QuantityInStock == int.MaxValue, "overflow preserves stock"); }

            var controllers = new[] { typeof(ProductsController), typeof(CustomersController),
                typeof(EmployeesController), typeof(SuppliersController), typeof(PurchaseReceiptsController),
                typeof(SalesOrdersController), typeof(InventoryReportsController), typeof(ReportsController),
                typeof(CategoriesController), typeof(AdminUsersController) };
            foreach (var type in controllers)
            {
                var auth = type.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single();
                Check(auth.Roles.Split(',').Contains("Admin") && !auth.Roles.Split(',').Contains("KhachHang"), type.Name + " role boundary");
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(m => m.IsDefined(typeof(HttpPostAttribute), true)))
                    Check(method.IsDefined(typeof(ValidateAntiForgeryTokenAttribute), true), type.Name + "." + method.Name + " anti-forgery");
            }
            foreach (var name in new[] { "Create", "Edit" })
            {
                var post = typeof(ProductsController).GetMethods().Single(m => m.Name == name && m.IsDefined(typeof(HttpPostAttribute), true));
                var bind = post.GetParameters()[0].GetCustomAttributes(typeof(BindAttribute), true).Cast<BindAttribute>().Single();
                Check(!bind.Include.Split(',').Contains("QuantityInStock") && !bind.Include.Split(',').Contains("CreatedDate"), name + " rejects stock/date binding");
            }
            Check(typeof(StoreController).IsDefined(typeof(AuthorizeAttribute), true), "store requires authentication by default");
            Check(typeof(StoreController).GetMethods().Single(m => m.Name == "Index").IsDefined(typeof(AllowAnonymousAttribute), true), "catalog public");
            var checkout = typeof(StoreController).GetMethods().Single(m => m.Name == "Checkout" && m.IsDefined(typeof(HttpPostAttribute), true));
            Check(checkout.IsDefined(typeof(ValidateAntiForgeryTokenAttribute), true), "checkout anti-forgery");
            var checkoutBind = checkout.GetParameters()[0].GetCustomAttributes(typeof(BindAttribute), true).Cast<BindAttribute>().Single().Include;
            Check(!checkoutBind.Contains("Price") && !checkoutBind.Contains("CustomerId") && !checkoutBind.Contains("Status"), "checkout rejects price owner and status binding");
            using (var report = new ReportsController())
            {
                report.Index(new DateTime(2026, 2, 2), new DateTime(2026, 2, 1));
                Check(!report.ModelState.IsValid, "reversed report dates rejected before query");
            }
            using (var report = new ReportsController())
            {
                report.ModelState.AddModelError("fromDate", "invalid date");
                report.Purchases(null, null);
                Check(!report.ModelState.IsValid, "malformed date does not fall back to query");
            }
            using (var store = StoreFor(null, "customer-a", 1))
            {
                Check(store.Orders() is HttpUnauthorizedResult, "orders reject missing Identity user id before query");
                Check(store.Details(1) is HttpUnauthorizedResult, "order details reject missing Identity user id before query");
                Check(store.Checkout(1) is HttpUnauthorizedResult, "checkout GET rejects missing Identity user id before query");
                Check(store.Checkout(new OnlineCheckoutModel()) is HttpUnauthorizedResult, "checkout POST rejects missing Identity user id before session access");
            }
            using (var store = StoreFor("customer-b", "customer-a", 1))
            {
                var result = store.Checkout(new OnlineCheckoutModel { Token = "checkout-test", ProductId = 1 }) as HttpStatusCodeResult;
                Check(result != null && result.StatusCode == 400, "checkout token bound to customer");
            }
            using (var store = StoreFor("customer-a", "customer-a", 1))
            {
                var result = store.Checkout(new OnlineCheckoutModel { Token = "checkout-test", ProductId = 2 }) as HttpStatusCodeResult;
                Check(result != null && result.StatusCode == 400, "checkout token bound to selected product");
            }
            Console.WriteLine("TOTAL PASS: " + count);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
