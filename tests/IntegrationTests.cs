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
            // Query filters execute against the isolated SQL database, including day boundaries.
            using (var c = new PurchaseReceiptsController()) {
                var result = (IEnumerable<PurchaseReceipt>)((ViewResult)c.Index(keyword: "IT-IN", status: ReceiptStatus.Confirmed,
                    supplierId: supplierId, employeeId: employeeId, receiptNumber: "IT-", minTotal: 1200m, maxTotal: 1200m,
                    fromDate: DateTime.Today.AddDays(-10), toDate: DateTime.Today.AddDays(-10), confirmedOnly: true)).Model;
                Check(result.Count() == 1 && result.Single().Id == receipt, "receipt combined filters and inclusive final day");
            }
            using (var c = new PurchaseReceiptsController()) {
                Check(!((IEnumerable<PurchaseReceipt>)((ViewResult)c.Index(keyword: "IT-IN", draftOnly: true)).Model).Any(), "draft filter excludes confirmed receipt");
            }
            using (var c = new PurchaseReceiptsController()) {
                Check(!((IEnumerable<PurchaseReceipt>)((ViewResult)c.Index(draftOnly: true, confirmedOnly: true)).Model).Any() && !c.ModelState.IsValid, "conflicting receipt filters rejected");
            }
            using (var c = new SalesOrdersController()) {
                var result = (IEnumerable<SalesOrder>)((ViewResult)c.Index(keyword: "IT-OUT", status: OrderStatus.Confirmed,
                    customerId: customerId, channel: SalesChannel.Online, minTotal: 300m, maxTotal: 300m,
                    fromDate: DateTime.Today.AddDays(-10), toDate: DateTime.Today.AddDays(-10))).Model;
                Check(result.Count() == 1 && result.Single().Id == order, "sales combined filters and inclusive final day");
            }
            using (var c = new SalesOrdersController()) {
                Check(!((IEnumerable<SalesOrder>)((ViewResult)c.Index(keyword: "IT-OUT", channel: SalesChannel.Counter)).Model).Any(), "channel filter excludes online order");
            }
            using (var c = new SalesOrdersController()) {
                Check(!((IEnumerable<SalesOrder>)((ViewResult)c.Index(minTotal: 100m, maxTotal: 10m)).Model).Any() && !c.ModelState.IsValid, "invalid amount interval rejected");
            }
            using (var c = new SalesOrdersController()) {
                Check(!((IEnumerable<SalesOrder>)((ViewResult)c.Index(fromDate: DateTime.Today, toDate: DateTime.Today.AddDays(-1))).Model).Any() && !c.ModelState.IsValid, "invalid date interval rejected");
            }

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
            VerifyRolesAndDashboard();
            VerifyOnlineCart();
            VerifyDemoCatalog();
            Console.WriteLine("TOTAL INTEGRATION PASS: " + count);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private sealed class DemoHttpContext : System.Web.HttpContextBase
    {
        private readonly System.Security.Principal.IPrincipal principal;
        public DemoHttpContext(System.Security.Principal.IPrincipal value) { principal = value; }
        public override System.Security.Principal.IPrincipal User { get { return principal; } set { } }
    }
    private static void VerifyDemoCatalog()
    {
        using (var db = new ApplicationDbContext()) {
            var original = db.Products.AsNoTracking().ToDictionary(p => p.Id, p => p.Name + "|" + p.SalePrice + "|" + p.QuantityInStock);
            int before = db.Products.Count();
            Check(DemoCatalog.Apply(db) == 64, "demo first seed adds 64 products");
            Check(DemoCatalog.Apply(db) == 0 && db.Products.Count() == before + 64, "demo second seed is idempotent");
            foreach(var p in db.Products.AsNoTracking().Where(p => !p.Sku.StartsWith("OSDEMO-")).ToList())
                Check(original[p.Id] == p.Name + "|" + p.SalePrice + "|" + p.QuantityInStock, "demo preserves old product " + p.Id);
            Check(db.Products.Where(p => p.Sku.StartsWith("OSDEMO-")).All(p => p.QuantityInStock == 0), "demo never directly seeds inventory");
            var demo = db.Products.Where(p => p.Sku.StartsWith("OSDEMO-")).OrderBy(p => p.Id).ToList();
            var service = new PurchaseReceiptService();
            for(int i=0;i<demo.Count;i+=8) {
                var receipt = service.CreateDraft(new PurchaseReceiptDraftInput { ReceiptNumber = "DEMO-IN-" + i,
                    SupplierId = db.Suppliers.First().Id, EmployeeId = db.Employees.First().Id,
                    Note = "Isolated DEMO stock via real service", Details = demo.Skip(i).Take(8).Select(p => new PurchaseReceiptLineInput { ProductId=p.Id,Quantity=5,UnitCost=p.CostPrice }).ToList() });
                service.ConfirmReceipt(receipt,"isolated-demo-test");
            }
            foreach(var p in db.Products.AsNoTracking().ToList()) {
                var ledger = db.InventoryTransactions.Where(t=>t.ProductId==p.Id).Sum(t=>(long?)t.QuantityChange) ?? 0L;
                Check(ledger == p.QuantityInStock, "demo ledger matches stock " + p.Sku);
            }
            var guest = new GenericPrincipal(new GenericIdentity(""), new string[0]);
            foreach(var category in demo.Select(p=>p.CategoryId).Distinct()) {
                using(var controller = new StoreController()) {
                    controller.ControllerContext = new ControllerContext(new DemoHttpContext(guest),new System.Web.Routing.RouteData(),controller);
                    var rows=(IEnumerable<Product>)((ViewResult)controller.Index(null, category)).Model;
                    Check(rows.Any() && rows.All(p=>p.CategoryId==category),"store category links isolate category " + category);
                }
            }
            using(var controller = new StoreController()) {
                controller.ControllerContext=new ControllerContext(new DemoHttpContext(guest),new System.Web.Routing.RouteData(),controller);
                var one=demo.First();var rows=(IEnumerable<Product>)((ViewResult)controller.Index(one.Sku,one.CategoryId)).Model;
                Check(rows.Count()==1 && rows.Single().Id==one.Id,"store search and category intersect correctly");
            }
            var users=new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(db));
            var roles=new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(db));
            roles.Create(new IdentityRole("UnexpectedDemo"));
            foreach(var email in new[]{"extra@ui.example.test","norole@ui.example.test"}) {
                var user=new ApplicationUser{UserName=email,Email=email};Check(users.Create(user).Succeeded,"extra card fixture " + email);
                if(email.StartsWith("extra"))users.AddToRole(user.Id,"UnexpectedDemo");
            }
            var admin=users.FindByEmail("Admin@ui.example.test");
            var identity=new System.Security.Claims.ClaimsIdentity(new[]{new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier,admin.Id)},"test");
            using(var controller=new AdminUsersController()) {
                controller.ControllerContext=new ControllerContext(new DemoHttpContext(new System.Security.Claims.ClaimsPrincipal(identity)),new System.Web.Routing.RouteData(),controller);
                var rows=((IEnumerable<UserRoleRow>)((ViewResult)controller.Index()).Model).ToList();
                Check(rows.Count>=6,"at least six account cards including unknown and empty roles");
                Check(rows.SequenceEqual(rows.OrderBy(r=>AdminUsersController.DisplayRoleOrder(r.Roles)).ThenBy(r=>r.Email,StringComparer.OrdinalIgnoreCase)),"role display order and email sorting");
                controller.SetRole(admin.Id,"Kho");Check(controller.TempData["Error"]!=null && users.IsInRole(admin.Id,"Admin"),"self last admin demotion rejected");
            }
            var actor=new System.Security.Claims.ClaimsIdentity(new[]{new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier,"other-admin-test")},"test");
            using(var controller=new AdminUsersController()) {
                controller.ControllerContext=new ControllerContext(new DemoHttpContext(new System.Security.Claims.ClaimsPrincipal(actor)),new System.Web.Routing.RouteData(),controller);
                controller.SetRole(admin.Id,"Kho");Check(controller.TempData["Error"]!=null,"last admin protected from other actor");
            }
            var target=users.FindByEmail("extra@ui.example.test");
            using(var controller=new AdminUsersController()) {
                controller.ControllerContext=new ControllerContext(new DemoHttpContext(new System.Security.Claims.ClaimsPrincipal(identity)),new System.Web.Routing.RouteData(),controller);
                controller.SetRole(target.Id,"Kho");Check(controller.TempData["Success"]!=null,"confirmed role action succeeds on test account");
            }
            using(var fresh=new ApplicationDbContext()) {
                var roleId=fresh.Roles.Single(r=>r.Name=="Kho").Id;
                Check(fresh.Users.Single(u=>u.Id==target.Id).Roles.Any(r=>r.RoleId==roleId),"confirmed role persisted");
            }
            Check(AdminUsersController.DisplayRoleOrder("UnexpectedDemo, Admin")==0 && AdminUsersController.DisplayRoleOrder("UnexpectedDemo")==4 && AdminUsersController.DisplayRoleOrder("")==5,"unusual and multiple roles have stable display order");
        }
    }
    private static void VerifyOnlineCart()
    {
        int customerId;
        List<Product> products;
        using (var db = new ApplicationDbContext())
        {
            customerId = db.Customers.First(c => c.IsActive).Id;
            products = db.Products.AsNoTracking().Where(p => p.IsActive && p.Category.IsActive && p.QuantityInStock >= 5).OrderBy(p => p.Id).Take(3).ToList();
        }
        Check(products.Count == 3, "three stocked online products");
        var input = new SalesOrderPendingInput { OrderNumber = "IT-CART", CustomerId = customerId,
            Channel = SalesChannel.Online, PaymentMethod = PaymentMethod.CashOnDelivery,
            ShippingAddress = "Online cart test", Details = products.Select(p => new SalesOrderLineInput { ProductId = p.Id, Quantity = 2 }).ToList() };
        var service = new SalesOrderService();
        var id = service.CreatePending(input);
        using (var db = new ApplicationDbContext())
        {
            var order = db.SalesOrders.Include(o => o.Details).Single(o => o.Id == id);
            Check(order.Details.Count == 3 && order.Status == OrderStatus.Pending && order.PaymentStatus == PaymentStatus.Unpaid, "online cart is one pending unpaid order with three lines");
            Check(order.TotalAmount == products.Sum(p => p.SalePrice * 2), "online total uses server prices");
            Check(!db.InventoryTransactions.Any(t => t.SalesOrderId == id), "pending online cart has no stock ledger entries");
        }
        foreach (var p in products) Check(Stock(p.Id) == p.QuantityInStock, "online pending preserves stock " + p.Id);
        input.OrderNumber = "IT-CART-OVER";
        input.Details[0].Quantity = products[0].QuantityInStock + 1;
        Reject(() => service.CreatePending(input), "online stock checked inside transaction");
        using (var db = new ApplicationDbContext()) Check(!db.SalesOrders.Any(o => o.OrderNumber == input.OrderNumber), "invalid online order rolls back all lines");
        input.OrderNumber = "IT-CART-INACTIVE";
        input.Details[0].Quantity = 1;
        using (var db = new ApplicationDbContext()) { db.Products.Find(products[0].Id).IsActive = false; db.SaveChanges(); }
        Reject(() => service.CreatePending(input), "inactive online product rejected");
        using (var db = new ApplicationDbContext()) { db.Products.Find(products[0].Id).IsActive = true; db.SaveChanges(); }
        int categoryId;
        using (var db = new ApplicationDbContext()) { categoryId = db.Products.Find(products[0].Id).CategoryId; db.Categories.Find(categoryId).IsActive = false; db.SaveChanges(); }
        Reject(() => service.CreatePending(input), "inactive online category rejected");
        using (var db = new ApplicationDbContext()) { db.Categories.Find(categoryId).IsActive = true; db.SaveChanges(); }
        service.ConfirmOrder(id, "online-cart-test");
        foreach (var p in products) Check(Stock(p.Id) == p.QuantityInStock - 2, "online multi-line confirm deducts " + p.Id);
        Reject(() => service.ConfirmOrder(id, "online-cart-test"), "multi-line confirmation replay rejected");
        service.CancelOrder(id, "online-cart-test");
        foreach (var p in products) Check(Stock(p.Id) == p.QuantityInStock, "online multi-line cancel restores " + p.Id);
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
