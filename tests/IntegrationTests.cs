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
            Console.WriteLine("TOTAL INTEGRATION PASS: " + count);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
