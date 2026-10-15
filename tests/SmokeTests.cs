using System;
using System.Linq;
using System.Reflection;
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
                typeof(SalesOrdersController), typeof(InventoryReportsController), typeof(ReportsController) };
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
            Console.WriteLine("TOTAL PASS: " + count);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
