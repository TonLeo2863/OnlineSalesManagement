using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Migrations;
using System.Data.SqlClient;
using System.Linq;
using OnlineSalesManagement;
using OnlineSalesManagement.Controllers;
using OnlineSalesManagement.Models;
using OnlineSalesManagement.Services;
using OnlineSalesManagement.ViewModels;
using System.Web.Mvc;
using System.Security.Principal;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;

public static class IntegrationTests
{
    private static int count;
    private static void Check(bool ok, string text) { if (!ok) throw new Exception("FAIL: " + text); Console.WriteLine("PASS: " + text); count++; }
    private static void Reject(Action action, string text)
    {
        try { action(); } catch (InvalidOperationException) { Check(true, text); return; }
        throw new Exception("FAIL: " + text);
    }
    private static int Stock(int id) { using (var db = new ApplicationDbContext()) return db.Products.Find(id).QuantityInStock; }
    public static int Main()
    {
        try
        {
            var cs = new SqlConnectionStringBuilder(ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString);
            if (!cs.InitialCatalog.StartsWith("Dacn05_Test_", StringComparison.Ordinal) || !cs.IntegratedSecurity)
                throw new Exception("Refusing to run outside an isolated Dacn05_Test_ database with Windows authentication.");
            Console.WriteLine("Isolated database: " + cs.InitialCatalog + " (retained; never dropped)");
            var configType = typeof(Product).Assembly.GetType("OnlineSalesManagement.Migrations.Configuration", true);
            var config = (DbMigrationsConfiguration)Activator.CreateInstance(configType, true);
            new DbMigrator(config).Update();
            int productId, product2Id, customerId, employeeId, supplierId;
            using (var db = new ApplicationDbContext())
            {
                Check(db.Database.CompatibleWithModel(false), "migration model compatibility");
                var category = new Category { Name = "Integration category" };
                var p = new Product { Sku = "IT-01", Name = "Integration product", Category = category, SalePrice = 100m };
                var p2 = new Product { Sku = "IT-02", Name = "Integration product 2", Category = category, SalePrice = 200m };
                var customer = new Customer { Code = "IT-C", FullName = "Test customer", PhoneNumber = "0901234567" };
                var employee = new Employee { Code = "IT-E", FullName = "Test employee" };
                var supplier = new Supplier { Code = "IT-S", Name = "Test supplier" };
                db.Products.Add(p); db.Products.Add(p2); db.Customers.Add(customer); db.Employees.Add(employee); db.Suppliers.Add(supplier); db.SaveChanges();
                productId = p.Id; product2Id = p2.Id; customerId = customer.Id; employeeId = employee.Id; supplierId = supplier.Id;
            }
            var receipts = new PurchaseReceiptService();
            var receipt = receipts.CreateDraft(new PurchaseReceiptDraftInput {
                ReceiptNumber = "IT-IN", SupplierId = supplierId, EmployeeId = employeeId,
                Details = new List<PurchaseReceiptLineInput> {
                    new PurchaseReceiptLineInput { ProductId = productId, Quantity = 10, UnitCost = 60m },
                    new PurchaseReceiptLineInput { ProductId = product2Id, Quantity = 5, UnitCost = 120m }
                }
            });
            Check(Stock(productId) == 0 && Stock(product2Id) == 0, "draft does not change stock");
            using (var db = new ApplicationDbContext()) { db.PurchaseReceipts.Find(receipt).ReceiptDate = DateTime.Today.AddDays(-10); db.SaveChanges(); }
            receipts.ConfirmReceipt(receipt, "integration-test");
            Check(Stock(productId) == 10 && Stock(product2Id) == 5, "multi-line receipt confirmed once");
            Reject(() => receipts.ConfirmReceipt(receipt, "integration-test"), "duplicate receipt confirmation rejected");
            var sales = new SalesOrderService();
            var order = sales.CreatePending(new SalesOrderPendingInput {
                OrderNumber = "IT-OUT", CustomerId = customerId, Channel = SalesChannel.Online,
                ShippingAddress = "Test address", PaymentMethod = PaymentMethod.CashOnDelivery,
                Details = new List<SalesOrderLineInput> { new SalesOrderLineInput { ProductId = productId, Quantity = 3 } }
            });
            Check(Stock(productId) == 10, "pending does not change stock");
            using (var db = new ApplicationDbContext()) {
                Check(db.SalesOrderDetails.Single(d => d.SalesOrderId == order).UnitPrice == 100m, "server price snapshot");
                db.SalesOrders.Find(order).OrderDate = DateTime.Today.AddDays(-10); db.Products.Find(productId).SalePrice = 999m; db.SaveChanges();
            }
            sales.ConfirmOrder(order, "integration-test");
            Check(Stock(productId) == 7, "confirmation decrements stock");
            Reject(() => sales.ConfirmOrder(order, "integration-test"), "duplicate sale confirmation rejected");
            using (var report = new ReportsController()) {
                var result = (ViewResult)report.Purchases(DateTime.Today, DateTime.Today);
                var model = (BusinessReportViewModel)result.Model;
                Check(model.Rows.Any(r => r.DocumentId == receipt && r.Quantity == 10 && r.UnitPrice == 60m), "receipt report uses transaction day and snapshot");
                var saleResult = (BusinessReportViewModel)((ViewResult)report.Sales(DateTime.Today, DateTime.Today)).Model;
                Check(saleResult.Rows.Any(r => r.DocumentId == order && r.Quantity == 3 && r.UnitPrice == 100m), "sales report uses transaction day and snapshot");
            }
            var oversized = sales.CreatePending(new SalesOrderPendingInput {
                OrderNumber = "IT-OVER", CustomerId = customerId, Channel = SalesChannel.Counter,
                Details = new List<SalesOrderLineInput> { new SalesOrderLineInput { ProductId = productId, Quantity = 100 } }
            });
            Reject(() => sales.ConfirmOrder(oversized, "integration-test"), "oversell rejected");
            Check(Stock(productId) == 7, "oversell rolls stock back");
            Reject(() => receipts.CancelReceipt(receipt, "integration-test"), "receipt cancellation rejected when stock insufficient");
            Check(Stock(productId) == 7 && Stock(product2Id) == 5, "failed cancellation is atomic");
            sales.CancelOrder(order, "integration-test");
            Check(Stock(productId) == 10, "confirmed cancellation restores stock");
            Reject(() => sales.CancelOrder(order, "integration-test"), "duplicate sale cancellation rejected");
            receipts.CancelReceipt(receipt, "integration-test");
            Check(Stock(productId) == 0 && Stock(product2Id) == 0, "receipt cancellation restores both products");
            using (var db = new ApplicationDbContext()) {
                foreach (var p in db.Products.ToList()) {
                    var sum = db.InventoryTransactions.Where(t => t.ProductId == p.Id).Sum(t => (long?)t.QuantityChange) ?? 0;
                    Check(sum == p.QuantityInStock, "ledger reconciliation product " + p.Id);
                }
            }
            using (var a = new ApplicationDbContext()) using (var b = new ApplicationDbContext()) {
                var pa = a.Products.Find(productId); var pb = b.Products.Find(productId);
                pa.Name = "First writer"; a.SaveChanges(); pb.Name = "Stale writer";
                try { b.SaveChanges(); throw new Exception("stale write accepted"); }
                catch (DbUpdateConcurrencyException) { Check(true, "rowversion conflict detected"); }
            }
            VerifyThreeProductOrder(productId, product2Id, customerId, employeeId, supplierId);
            VerifyRolesAndDashboard();
            Console.WriteLine("TOTAL INTEGRATION PASS: " + count);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void VerifyThreeProductOrder(int first, int second, int customer, int employee, int supplier)
    {
        int third;
        using (var db = new ApplicationDbContext()) {
            var product = new Product { Sku = "IT-03", Name = "Third demo product", CategoryId = db.Products.Find(first).CategoryId, SalePrice = 300m };
            db.Products.Add(product); db.SaveChanges(); third = product.Id;
        }
        var ids = new[] { first, second, third };
        var receipts = new PurchaseReceiptService();
        int receipt = receipts.CreateDraft(new PurchaseReceiptDraftInput {
            ReceiptNumber = "IT-THREE-IN", SupplierId = supplier, EmployeeId = employee,
            Details = ids.Select(id => new PurchaseReceiptLineInput { ProductId = id, Quantity = 10, UnitCost = 50m }).ToList()
        });
        receipts.ConfirmReceipt(receipt, "integration-test");
        Check(ids.All(id => Stock(id) == 10), "three-product receipt stock");
        var sales = new SalesOrderService();
        int order = sales.CreatePending(new SalesOrderPendingInput {
            OrderNumber = "IT-THREE-OUT", CustomerId = customer, Channel = SalesChannel.Online,
            ShippingAddress = "Isolated test address", PaymentMethod = PaymentMethod.CashOnDelivery,
            Details = ids.Select(id => new SalesOrderLineInput { ProductId = id, Quantity = 2 }).ToList()
        });
        using (var db = new ApplicationDbContext()) {
            Check(db.SalesOrders.Find(order).Status == OrderStatus.Pending && db.SalesOrderDetails.Count(d => d.SalesOrderId == order) == 3,
                "one Pending order contains three different products at service layer");
        }
        Check(ids.All(id => Stock(id) == 10), "three-product Pending preserves stock");
        sales.ConfirmOrder(order, "integration-test");
        Reject(() => sales.ConfirmOrder(order, "integration-test"), "three-product duplicate confirmation rejected");
        Check(ids.All(id => Stock(id) == 8), "three-product confirmation subtracts once");
        using (var reports = new ReportsController()) {
            var model = (BusinessReportViewModel)((ViewResult)reports.Sales(DateTime.Today, DateTime.Today)).Model;
            Check(model.Rows.Count(r => r.DocumentId == order) == 3 && model.Rows.Where(r => r.DocumentId == order).Sum(r => r.Quantity) == 6,
                "three-product sale appears in export report");
        }
        sales.CompleteOrder(order);
        Check(ids.All(id => Stock(id) == 8), "Complete preserves stock for all three products");
        Reject(() => sales.CancelOrder(order, "integration-test"), "Completed order cannot be cancelled");
        using (var controller = new SalesOrdersController()) {
            Check(controller.Invoice(order) is ViewResult, "Completed invoice returns view model (not A4 rendering)");
        }
    }
    private static void VerifyRolesAndDashboard()
    {
        using (var db = new ApplicationDbContext())
        {
            var users = new ApplicationUserManager(new UserStore<ApplicationUser>(db));
            var roleManager = new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(db));
            var password = Environment.GetEnvironmentVariable("DACN_UI_TEST_PASSWORD") ?? "TestOnly!" + Guid.NewGuid().ToString("N");
            foreach (var role in new[] { "Admin", "Kho", "BanHang", "KhachHang" })
            {
                var user = new ApplicationUser { UserName = role + "@ui.example.test", Email = role + "@ui.example.test" };
                var created = IdentityProvisioning.RegisterCustomerAsync(db, users, user, password).GetAwaiter().GetResult();
                Check(created.Succeeded && users.IsInRole(user.Id, "KhachHang") && !users.IsInRole(user.Id, "Admin"), "registration assigns only KhachHang: " + role);
                if (role == "Admin")
                {
                    Reject(() => IdentityProvisioning.InitializeFirstAdmin(db, user.Id, user.Email, false), "bootstrap requires confirmation");
                    Reject(() => IdentityProvisioning.InitializeFirstAdmin(db, user.Id, "wrong@example.test", true), "bootstrap verifies target email");
                    Check(!roleManager.RoleExists("Admin"), "rejected bootstrap did not create Admin role");
                    IdentityProvisioning.InitializeFirstAdmin(db, user.Id, user.Email, true);
                    Check(users.IsInRole(user.Id, "Admin"), "confirmed first Admin created");
                    Reject(() => IdentityProvisioning.InitializeFirstAdmin(db, user.Id, user.Email, true), "bootstrap refuses when Admin already exists");
                }
                else if (role != "KhachHang")
                {
                    Check(roleManager.Create(new IdentityRole(role)).Succeeded, "create staff role " + role);
                    Check(users.RemoveFromRole(user.Id, "KhachHang").Succeeded && users.AddToRole(user.Id, role).Succeeded, "assign staff fixture " + role);
                }
                var principal = new GenericPrincipal(new GenericIdentity(user.Email, "test"), new[] { role });
                foreach (var controller in new[] { "AdminUsers", "Products", "Categories", "Customers", "Employees", "Suppliers", "InventoryReports", "PurchaseReceipts", "SalesOrders", "Reports" })
                {
                    var expected = role == "Admin" || (role == "Kho" && new[] { "Products", "Categories", "Suppliers", "InventoryReports", "PurchaseReceipts" }.Contains(controller))
                        || (role == "BanHang" && new[] { "Customers", "SalesOrders" }.Contains(controller));
                    Check(NavigationAccess.CanAccess(principal, controller) == expected, role + " menu " + controller);
                }
                if (role == "KhachHang")
                {
                    try { DashboardService.Load(db, principal); throw new Exception("customer read dashboard"); }
                    catch (UnauthorizedAccessException) { Check(true, "customer denied staff dashboard data"); }
                }
                else
                {
                    var model = DashboardService.Load(db, principal);
                    Check(model.CanViewSales == (role != "Kho") && model.CanViewInventory == (role != "BanHang"), role + " dashboard sections");
                    if (model.CanViewSales)
                        Check(model.OrderCount == db.SalesOrders.Count() && model.Revenue == (db.SalesOrders.Where(o => o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Completed).Sum(o => (decimal?)o.TotalAmount) ?? 0m), role + " dashboard matches database sales");
                    else Check(model.OrderCount == 0 && model.RecentOrders.Count == 0 && model.Revenue == 0m, "Kho has no sales data");
                    if (model.CanViewInventory)
                        Check(model.ProductCount == db.Products.Count() && model.StockQuantity == (db.Products.Sum(p => (long?)p.QuantityInStock) ?? 0L), role + " dashboard matches database inventory");
                    else Check(model.ProductCount == 0 && model.LowStock.Count == 0, "BanHang has no inventory data");
                }
            }
            var guest = new GenericPrincipal(new GenericIdentity(""), new string[0]);
            Check(!DashboardService.IsStaff(guest) && NavigationAccess.CanAccess(guest, "Home") && NavigationAccess.CanAccess(guest, "Store") && !NavigationAccess.CanAccess(guest, "AdminUsers"), "anonymous access boundaries");
        }
    }
}
