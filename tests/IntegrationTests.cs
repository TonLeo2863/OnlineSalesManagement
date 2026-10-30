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
            VerifyAccountGovernance();
            VerifyCatalogImport();
            VerifyPeopleFilters();
            VerifyTransactionHistory();
            VerifyReorderCart();
            VerifyIdentityPrivacy();
            VerifyCustomerCancellation();
            using (var db = new ApplicationDbContext()) {
                var balances = db.InventoryTransactions.GroupBy(t => t.ProductId).Select(g => new { Id=g.Key, Quantity=g.Sum(t=>(long)t.QuantityChange) }).ToDictionary(x=>x.Id,x=>x.Quantity);
                Check(db.Products.AsNoTracking().ToList().All(p => p.QuantityInStock == (balances.ContainsKey(p.Id) ? balances[p.Id] : 0L)), "final entire test database inventory agrees with ledger after all workflows");
            }
            Console.WriteLine("TOTAL INTEGRATION PASS: " + count);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void VerifyCustomerCancellation()
    {
        int productId,customerId,supplierId,employeeId;string owner;
        using(var db=new ApplicationDbContext()) {
            owner=db.Users.Single(u=>u.Email=="KhachHang@ui.example.test").Id;
            var p=new Product{Sku="CANCEL-P",Name="Cancellation fixture",SalePrice=100m,Category=new Category{Name="Cancellation fixtures"}};
            var c=new Customer{Code="CANCEL-C",FullName="Cancellation owner",PhoneNumber="0901234567",UserId=owner};
            db.Products.Add(p);db.Customers.Add(c);db.SaveChanges();productId=p.Id;customerId=c.Id;supplierId=db.Suppliers.First().Id;employeeId=db.Employees.First().Id;
        }
        var receipt=new PurchaseReceiptService();var service=new SalesOrderService();
        var receiptId=receipt.CreateDraft(new PurchaseReceiptDraftInput{ReceiptNumber="CANCEL-IN",SupplierId=supplierId,EmployeeId=employeeId,Details=new List<PurchaseReceiptLineInput>{new PurchaseReceiptLineInput{ProductId=productId,Quantity=100,UnitCost=60}}});receipt.ConfirmReceipt(receiptId,"test");
        Func<string,int> make=number=>service.CreatePending(new SalesOrderPendingInput{OrderNumber=number,CustomerId=customerId,Channel=SalesChannel.Online,ShippingAddress="Test address",Details=new List<SalesOrderLineInput>{new SalesOrderLineInput{ProductId=productId,Quantity=1}}});
        var order=make("CC-OWN");
        try{service.CancelPendingByCustomer(order,"another-user");throw new Exception("foreign cancel accepted");}catch(UnauthorizedAccessException){Check(true,"foreign owner cancellation rejected inside service transaction");}
        var stock=Stock(productId);service.CancelPendingByCustomer(order,owner);
        using(var db=new ApplicationDbContext())Check(db.SalesOrders.Find(order).Status==OrderStatus.Cancelled&&db.SalesOrderDetails.Count(d=>d.SalesOrderId==order)==1&&!db.InventoryTransactions.Any(t=>t.SalesOrderId==order)&&Stock(productId)==stock,"pending cancel preserves order/details and leaves stock/ledger unchanged");
        Reject(()=>service.CancelPendingByCustomer(order,owner),"cancelled cannot be customer-cancelled again");
        var confirmed=make("CC-CONF");service.ConfirmOrder(confirmed,"test");Reject(()=>service.CancelPendingByCustomer(confirmed,owner),"customer cannot cancel confirmed");service.CompleteOrder(confirmed);Reject(()=>service.CancelPendingByCustomer(confirmed,owner),"customer cannot cancel completed");
        var adminCancel=make("CC-ADMIN");service.ConfirmOrder(adminCancel,"test");var before=Stock(productId);service.CancelOrder(adminCancel,"test");Check(Stock(productId)==before+1,"admin confirmed cancellation retains stock restoration behavior");
        for(int i=0;i<12;i++) {
            var id=make("CC-RACE-"+i);var start=new System.Threading.ManualResetEventSlim(false);int won=0;
            var tasks=new[]{System.Threading.Tasks.Task.Run(()=>{start.Wait();try{service.ConfirmOrder(id,"test");System.Threading.Interlocked.Increment(ref won);}catch(InvalidOperationException){}}),System.Threading.Tasks.Task.Run(()=>{start.Wait();try{service.CancelPendingByCustomer(id,owner);System.Threading.Interlocked.Increment(ref won);}catch(InvalidOperationException){}})};
            var startStock=Stock(productId);start.Set();System.Threading.Tasks.Task.WaitAll(tasks);start.Dispose();
            using(var db=new ApplicationDbContext()) {
                var current=db.SalesOrders.Find(id);var countLedger=db.InventoryTransactions.Count(t=>t.SalesOrderId==id);
                Check(won==1&&((current.Status==OrderStatus.Cancelled&&countLedger==0&&Stock(productId)==startStock)||(current.Status==OrderStatus.Confirmed&&countLedger==1&&Stock(productId)==startStock-1)),"concurrent confirm/customer cancel exactly one transition with correct stock/ledger "+i);
            }
        }
    }
    private sealed class PendingPhoneSession : System.Web.HttpSessionStateBase
    {
        private readonly Dictionary<string,object> values=new Dictionary<string,object>();
        public override object this[string name] { get { object value;return values.TryGetValue(name,out value)?value:null; } set { values[name]=value; } }
        public override void Remove(string name){values.Remove(name);}
    }
    private static void VerifyIdentityPrivacy()
    {
        var session=new PendingPhoneSession();session["PhoneOtpOwnerId"]="previous-user";session["PhoneOtpNumber"]="+84901231234";session["PhoneOtpSentUtc"]=DateTime.UtcNow;session["PhoneOtpAttempts"]=0;
        var principal=new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]{new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier,"different-user")},"test"));
        using(var c=new ManageController()) {
            c.ControllerContext=new ControllerContext(new DemoHttpContext(principal,session),new System.Web.Routing.RouteData(),c);
            Check(c.VerifyPhoneNumber((string)null) is RedirectToRouteResult,"OTP GET cannot disclose previous account pending phone in shared session");
            var result=c.VerifyPhoneNumber(new VerifyPhoneNumberViewModel{PhoneNumber="+84901231234",Code="123456"}).GetAwaiter().GetResult();
            Check(result is ViewResult && !c.ModelState.IsValid,"OTP POST cannot use a previous account challenge");
        }
        var tasks=Enumerable.Range(0,2).Select(i=>System.Threading.Tasks.Task.Run(async ()=>{
            using(var db=new ApplicationDbContext())using(var users=new ApplicationUserManager(new UserStore<ApplicationUser>(db))) {
                return (await IdentityProvisioning.RegisterCustomerAsync(db,users,new ApplicationUser{Email="race@example.test",UserName="race@example.test"},"TestOnly!Password9")).Succeeded;
            }
        })).ToArray();
        System.Threading.Tasks.Task.WaitAll(tasks);
        using(var db=new ApplicationDbContext())Check(tasks.Count(t=>t.Result)==1 && db.Users.Count(u=>u.Email=="race@example.test")==1,"concurrent normalized registration yields exactly one account");
        using(var db=new ApplicationDbContext())using(var users=new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(db))) {
            var user=new ApplicationUser{Email="privacyconfirmed@example.test",UserName="privacyconfirmed@example.test",EmailConfirmed=true};users.Create(user);
            var phoneUser=new ApplicationUser{Email="phoneownership@example.test",UserName="phoneownership@example.test"};Check(users.Create(phoneUser).Succeeded,"phone ownership fixture created");
            var phone="+84901231234";var token=users.GenerateChangePhoneNumberToken(phoneUser.Id,phone);
            Check(!users.ChangePhoneNumber(phoneUser.Id,phone,"not-a-valid-code").Succeeded,"incorrect ownership token is rejected");
            Check(users.ChangePhoneNumber(phoneUser.Id,phone,token).Succeeded && users.IsPhoneNumberConfirmed(phoneUser.Id),"valid ownership token confirms account phone in isolated test only");
            Check(AccountGovernance.VerifiedPhoneUsed(db,phone,"another-user"),"verified phone cannot be claimed by another account");
        }
    }
    private static void VerifyReorderCart()
    {
        var cart=new ShoppingCart("reorder-owner");cart.SetQuantity(9001,2);cart.CheckoutToken="stale-checkout";
        var category=new Category{Id=9901,Name="Reorder unit category"};
        var available=new Product{Id=9001,Name="Available",Category=category,SalePrice=100,QuantityInStock=5};
        var missing=new Product{Id=9002,Name="Out of stock",Category=category,SalePrice=200,QuantityInStock=0};
        var inactive=new Product{Id=9003,Name="Inactive",Category=category,IsActive=false,SalePrice=300,QuantityInStock=5};
        var hiddenCategory=new Product{Id=9004,Name="Inactive category",Category=new Category{IsActive=false},SalePrice=400,QuantityInStock=5};
        var order=new SalesOrder{Customer=new Customer{UserId=cart.OwnerId},DiscountAmount=50};
        order.Details.Add(new SalesOrderDetail{ProductId=9001,Product=available,Quantity=4,UnitPrice=1});
        order.Details.Add(new SalesOrderDetail{ProductId=9002,Product=missing,Quantity=1,UnitPrice=1});
        order.Details.Add(new SalesOrderDetail{ProductId=9003,Product=inactive,Quantity=1,UnitPrice=1});
        order.Details.Add(new SalesOrderDetail{ProductId=9004,Product=hiddenCategory,Quantity=1,UnitPrice=1});
        var version=cart.Version;var result=ReorderCart.Add(cart,order,version);
        Check(cart.Items[9001]==5 && result.AddedUnits==3,"reorder merges existing cart and caps by current stock");
        Check(cart.Items.Count==1 && result.Notices.Count==4,"reorder explains unavailable products and clamped quantity");
        Check(cart.CheckoutToken==null && cart.Version!=version,"reorder changes cart version once and invalidates old checkout");
        var replay=ReorderCart.Add(cart,order,version);Check(replay.AddedUnits==0 && cart.Items[9001]==5,"reorder replay does not add twice");
        order.Customer.UserId="other-owner";bool denied=false;try{ReorderCart.Add(cart,order,cart.Version);}catch(UnauthorizedAccessException){denied=true;}Check(denied,"reorder service rejects a foreign owner");
        var before=cart.Version;bool invalid=false;try{cart.ApplyReorder(before,new Dictionary<int,int>{{9001,1},{9002,1001}});}catch(ArgumentException){invalid=true;}
        Check(invalid && cart.Items[9001]==5 && cart.Version==before,"invalid reorder plan cannot partially mutate cart");
    }
    private static void VerifyTransactionHistory()
    {
        int customerId,employeeId,supplierId,productId;
        using(var db=new ApplicationDbContext()) {
            var customer=new Customer{Code="HISTORY-C",FullName="History customer",PhoneNumber="0901234567"};
            var employee=new Employee{Code="HISTORY-E",FullName="History employee"};var supplier=new Supplier{Code="HISTORY-S",Name="History supplier"};
            var product=new Product{Sku="HISTORY-P",Name="History product",CategoryId=db.Categories.First(c=>c.IsActive).Id,SalePrice=100,CostPrice=50};
            db.Customers.Add(customer);db.Employees.Add(employee);db.Suppliers.Add(supplier);db.Products.Add(product);db.SaveChanges();
            customerId=customer.Id;employeeId=employee.Id;supplierId=supplier.Id;productId=product.Id;
        }
        var receipts=new PurchaseReceiptService();var sales=new SalesOrderService();
        var receipt=receipts.CreateDraft(new PurchaseReceiptDraftInput{ReceiptNumber="HISTORY-IN",SupplierId=supplierId,EmployeeId=employeeId,Details=new List<PurchaseReceiptLineInput>{new PurchaseReceiptLineInput{ProductId=productId,Quantity=20,UnitCost=50}}});
        receipts.ConfirmReceipt(receipt,"history-test");
        for(int i=0;i<11;i++)receipts.CreateDraft(new PurchaseReceiptDraftInput{ReceiptNumber="HISTORY-DRAFT-"+i,SupplierId=supplierId,EmployeeId=employeeId,Details=new List<PurchaseReceiptLineInput>{new PurchaseReceiptLineInput{ProductId=productId,Quantity=1,UnitCost=50}}});
        for(int i=0;i<12;i++) {
            var id=sales.CreatePending(new SalesOrderPendingInput{OrderNumber="HISTORY-OUT-"+i,CustomerId=customerId,EmployeeId=employeeId,Channel=SalesChannel.Counter,PaymentMethod=PaymentMethod.Cash,Details=new List<SalesOrderLineInput>{new SalesOrderLineInput{ProductId=productId,Quantity=i==0?2:1}}});
            if(i==0){sales.ConfirmOrder(id,"history-test");sales.CompleteOrder(id);}if(i==1)sales.CancelOrder(id,"history-test");
        }
        sales.CreatePending(new SalesOrderPendingInput{OrderNumber="HISTORY-ONLINE",CustomerId=customerId,Channel=SalesChannel.Online,PaymentMethod=PaymentMethod.CashOnDelivery,ShippingAddress="History test address",Details=new List<SalesOrderLineInput>{new SalesOrderLineInput{ProductId=productId,Quantity=1}}});
        using(var c=new CustomersController()) {
            c.Details(customerId,ordersPage:2);var history=(TransactionHistoryViewModel)c.ViewBag.OrderHistory;
            Check(history.TotalCount==13 && history.Rows.Count==3 && history.ConfirmedAmount==200,"customer history uses own FK, page 2 and completed-only value");
            using(var db=new ApplicationDbContext())Check(history.Rows.All(r=>db.SalesOrders.Any(o=>o.Id==r.Id && o.CustomerId==customerId)),"customer history links resolve to owned profile orders");
        }
        using(var c=new EmployeesController()) {
            c.Details(employeeId,ordersPage:2,receiptsPage:2);var orders=(TransactionHistoryViewModel)c.ViewBag.OrderHistory;var incoming=(TransactionHistoryViewModel)c.ViewBag.ReceiptHistory;
            Check(orders.TotalCount==12 && orders.Rows.Count==2 && orders.ConfirmedAmount==200,"employee history excludes unassigned online and canceled totals");
            Check(incoming.TotalCount==12 && incoming.Rows.Count==2 && incoming.ConfirmedAmount==1000,"employee receipts kept in separate paged section");
        }
        using(var c=new SuppliersController()) {
            c.Details(supplierId,receiptsPage:2);var history=(TransactionHistoryViewModel)c.ViewBag.ReceiptHistory;
            Check(history.Kind=="receipts" && history.TotalCount==12 && history.Rows.Count==2 && history.ConfirmedAmount==1000,"supplier history is purchase receipts, with draft excluded from value");
        }
        using(var db=new ApplicationDbContext())using(var c=new CustomersController()) {
            c.Details(db.Customers.Single(x=>x.Code=="SEARCH-C").Id);var history=(TransactionHistoryViewModel)c.ViewBag.OrderHistory;
            Check(history.TotalCount==0 && !history.Rows.Any() && history.ConfirmedAmount==0,"empty customer history does not fabricate records");
        }
    }
    private static void VerifyPeopleFilters()
    {
        using(var db=new ApplicationDbContext()) {
            db.Employees.Add(new Employee {Code="SEARCH-E",FullName="Search Employee",Email="search.employee@example.test",PhoneNumber="0909999998",Position="Chuc vu cu dac biet",HireDate=DateTime.Today.AddDays(-1),IsActive=false});
            db.Customers.Add(new Customer {Code="SEARCH-C",FullName="Search Customer",Email="search.customer@example.test",PhoneNumber="0909999998",CreatedDate=DateTime.Today.AddDays(-1).AddHours(23),IsActive=false});
            for(int i=0;i<21;i++)db.Employees.Add(new Employee {Code="PAGE-E"+i.ToString("D2"),FullName="Page employee "+i});
            db.SaveChanges();
        }
        using(var c=new EmployeesController()) {
            var rows=(IEnumerable<Employee>)((ViewResult)c.Index(code:"SEARCH-E",fullName:"Employee",email:"search.employee",phone:"+84909999998",position:"Chuc vu cu dac biet",active:false,fromDate:DateTime.Today.AddDays(-1),toDate:DateTime.Today.AddDays(-1))).Model;
            Check(rows.Count()==1 && rows.Single().Code=="SEARCH-E","employee combined advanced filters include legacy position and VN phone");
        }
        using(var c=new EmployeesController()) {
            var rows=(IEnumerable<Employee>)((ViewResult)c.Index(code:"PAGE-E",page:2)).Model;
            Check(rows.Count()==1 && (int)c.ViewBag.Total==21,"employee server pagination");
        }
        using(var c=new CustomersController()) {
            var rows=(IEnumerable<Customer>)((ViewResult)c.Index(code:"SEARCH-C",fullName:"Customer",email:"search.customer",phone:"0909999998",active:false,fromDate:DateTime.Today.AddDays(-1),toDate:DateTime.Today.AddDays(-1))).Model;
            Check(rows.Count()==1,"customer filters include final-day time boundary");
        }
        using(var c=new AdminUsersController()) {
            Check(!((IEnumerable<UserRoleRow>)((ViewResult)c.Index(verifiedPhone:"+84909999998")).Model).Any(),"customer contact number is not a verified account identifier");
        }
        using(var c=new EmployeesController()) { Check(!((IEnumerable<Employee>)((ViewResult)c.Index(fromDate:DateTime.Today,toDate:DateTime.Today.AddDays(-1))).Model).Any() && !c.ModelState.IsValid,"employee reversed date range returns validation"); }
        using(var c=new CustomersController()) { Check(!((IEnumerable<Customer>)((ViewResult)c.Index(phone:"invalid-phone")).Model).Any() && !c.ModelState.IsValid,"customer invalid phone filter returns validation"); }
        Check(PeopleDirectory.ResolvePosition("__other__","Chuc vu cu dac biet")=="Chuc vu cu dac biet","legacy position preserved via Other");
        Check(PeopleDirectory.ResolvePosition(PeopleDirectory.Positions[0],"ignored")==PeopleDirectory.Positions[0],"position dropdown uses known value independently of Identity roles");
    }
    private sealed class FailSecondCatalogInsert : System.Data.Entity.Infrastructure.Interception.DbCommandInterceptor
    {
        public int Inserts;
        public override void ReaderExecuting(System.Data.Common.DbCommand command, System.Data.Entity.Infrastructure.Interception.DbCommandInterceptionContext<System.Data.Common.DbDataReader> context)
        {
            if(command.CommandText.IndexOf("INSERT [dbo].[Products]",StringComparison.OrdinalIgnoreCase)>=0 && ++Inserts==2) throw new InvalidOperationException("Synthetic test-only failure before second insert");
            base.ReaderExecuting(command,context);
        }
    }
    private static void VerifyCatalogImport()
    {
        using(var db=new ApplicationDbContext()) {
            var category=db.Categories.First(c=>c.IsActive).Name;
            var template=CatalogWorkbook.Template("products",category);
            var preview=CatalogWorkbook.Read(new System.IO.MemoryStream(template),"products");
            CatalogImportService.Validate(db,preview);
            Check(preview.ValidCount==1 && !preview.Issues.Any(),"xlsx product template parses and validates");
            Check(CatalogImportService.Confirm(db,preview)==1,"xlsx confirmation creates one product");
            var product=db.Products.Single(p=>p.Sku=="EXCEL-DEMO-001");
            Check(product.QuantityInStock==0 && !db.InventoryTransactions.Any(t=>t.ProductId==product.Id),"Excel never imports inventory or fake ledger");
            Check(CatalogImportService.Confirm(db,preview)==0 && preview.DuplicateCount==1,"Excel repeated confirmation cannot duplicate SKU");
            var categories=CatalogWorkbook.Read(new System.IO.MemoryStream(CatalogWorkbook.Template("categories")),"categories");
            Check(CatalogImportService.Confirm(db,categories)==1 && CatalogImportService.Confirm(db,categories)==0,"Excel category creation and duplicate protection");
            var rows=new[]{CatalogWorkbook.Headers("products"),new[]{"IMPORT-DUP","First",category,"","Cai","10","20","1","","true"},new[]{"IMPORT-DUP","Second",category,"","Cai","10","20","1","","true"}};
            var duplicates=CatalogWorkbook.Read(new System.IO.MemoryStream(CatalogWorkbook.WriteRows("products",rows)),"products");
            CatalogImportService.Validate(db,duplicates);Check(duplicates.DuplicateCount==2 && duplicates.ValidCount==0,"Excel marks both duplicate rows by SKU column");
            rows[1][0]="IMPORT-INVALID";rows[1][1]="";rows[1][2]="Missing category";rows[1][5]="-1";
            var invalid=CatalogWorkbook.Read(new System.IO.MemoryStream(CatalogWorkbook.WriteRows("products",rows)),"products");var before=db.Products.Count();
            Check(CatalogImportService.Confirm(db,invalid)==0 && db.Products.Count()==before && invalid.Issues.Any(i=>i.Row==2 && i.Column=="Danh mục"),"Excel all-or-nothing invalid row with row/column issues");
            var extra=new[]{CatalogWorkbook.Headers("products").Concat(new[]{"QuantityInStock"}).ToArray(),new[]{"X","X",category,"","Cai","1","2","1","","true","100"}};
            bool rejected=false;try{CatalogWorkbook.Read(new System.IO.MemoryStream(CatalogWorkbook.WriteRows("products",extra)),"products");}catch(ArgumentException){rejected=true;}
            Check(rejected,"Excel QuantityInStock column is refused");
            var many=new List<string[]>{CatalogWorkbook.Headers("categories")};for(int i=0;i<501;i++)many.Add(new[]{"Row"+i,"","true"});
            rejected=false;try{CatalogWorkbook.Read(new System.IO.MemoryStream(CatalogWorkbook.WriteRows("categories",many)),"categories");}catch(ArgumentException){rejected=true;}
            Check(rejected,"Excel row limit enforced");
            var bytes=new System.IO.MemoryStream();bytes.Write(template,0,template.Length);bytes.Position=0;
            using(var archive=new System.IO.Compression.ZipArchive(bytes,System.IO.Compression.ZipArchiveMode.Update,true)) {
                var entry=archive.GetEntry("xl/worksheets/sheet1.xml");System.Xml.Linq.XDocument xml;using(var stream=entry.Open())xml=System.Xml.Linq.XDocument.Load(stream);
                System.Xml.Linq.XNamespace ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                xml.Descendants(ns+"c").First().Add(new System.Xml.Linq.XElement(ns+"f","1+1"));entry.Delete();using(var stream=archive.CreateEntry("xl/worksheets/sheet1.xml").Open())xml.Save(stream);
            }
            bytes.Position=0;rejected=false;try{CatalogWorkbook.Read(bytes,"products");}catch(ArgumentException){rejected=true;}
            Check(rejected,"Excel formula is rejected without evaluation");
            var rollbackRows=new[]{CatalogWorkbook.Headers("products"),new[]{"IMPORT-ROLL-A","First",category,"","Cai","10","20","1","","true"},new[]{"IMPORT-ROLL-B","Second",category,"","Cai","10","20","1","","true"}};
            var rollback=CatalogWorkbook.Read(new System.IO.MemoryStream(CatalogWorkbook.WriteRows("products",rollbackRows)),"products");
            var interceptor=new FailSecondCatalogInsert();System.Data.Entity.Infrastructure.Interception.DbInterception.Add(interceptor);bool failed=false;
            try { using(var isolated=new ApplicationDbContext())CatalogImportService.Confirm(isolated,rollback); } catch(Exception) { failed=true; }
            finally {System.Data.Entity.Infrastructure.Interception.DbInterception.Remove(interceptor);}
            using(var fresh=new ApplicationDbContext())Check(failed && interceptor.Inserts==2 && !fresh.Products.Any(p=>p.Sku=="IMPORT-ROLL-A" || p.Sku=="IMPORT-ROLL-B"),"Excel actual partial-save failure rolls back all inserted rows");
            var httpRows=new[]{CatalogWorkbook.Headers("products"),new[]{"HTTP-EXCEL-001","HTTP Excel demo",category,"Demo test only","Cai","10","20","1","","true"}};
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"CatalogImportFixture.xlsx"),CatalogWorkbook.WriteRows("products",httpRows));
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"CategoryImportFixture.xlsx"),CatalogWorkbook.WriteRows("categories",new[]{CatalogWorkbook.Headers("categories"),new[]{"HTTP Excel category","Demo test only","true"}}));
        }
    }
    private static void VerifyAccountGovernance()
    {
        string targetId, adminId, stamp;
        using(var db=new ApplicationDbContext()) {
            var users=new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(db));
            users.UserValidator=new UserValidator<ApplicationUser>(users){AllowOnlyAlphanumericUserNames=false,RequireUniqueEmail=true};
            var target=new ApplicationUser{UserName="governance-target@example.test",Email="governance-target@example.test", PhoneNumber="0901234567",PhoneNumberConfirmed=true,LockoutEnabled=true,LockoutEndDateUtc=DateTime.UtcNow.AddMinutes(15)};
            Check(users.Create(target).Succeeded,"governance target fixture created");
            targetId=target.Id;stamp=target.SecurityStamp;adminId=users.FindByEmail("admin@ui.example.test").Id;
            Check(AccountGovernance.NormalizeEmail(" User@Example.Test ")=="user@example.test","email normalization trims and folds case");
            Check(AccountGovernance.NormalizePhone("0901 234 567")=="+84901234567","VN phone normalization");
            Check(AccountGovernance.VerifiedPhoneUsed(db,"+84901234567",null) && !AccountGovernance.VerifiedPhoneUsed(db,"+84901234567",targetId),"verified account phone collision and owner exception");
            AccountGovernance.SetSuspension(db,adminId,targetId,true,"Integration suspension test");
        }
        using(var db=new ApplicationDbContext()) {
            var target=db.Users.Find(targetId);
            Check(target.IsAdministrativelySuspended && target.SecurityStamp!=stamp,"suspension independent flag and session stamp changed");
            Check(target.LockoutEndDateUtc>DateTime.UtcNow,"suspension does not overwrite temporary lockout");
            Check(db.AccountAdministrationAudits.Count(a=>a.TargetUserId==targetId)==1,"suspension creates one audit event");
            AccountGovernance.SetSuspension(db,adminId,targetId,true,"Repeat suspension request");
            Check(db.AccountAdministrationAudits.Count(a=>a.TargetUserId==targetId)==1,"repeated suspension is idempotent");
            Reject(()=>AccountGovernance.SetSuspension(db,adminId,adminId,true,"Self suspension invalid"),"self suspension blocked");
            AccountGovernance.SetSuspension(db,adminId,targetId,false,"Integration restore test");
        }
        using(var db=new ApplicationDbContext()) {
            Check(!db.Users.Find(targetId).IsAdministrativelySuspended && db.AccountAdministrationAudits.Count(a=>a.TargetUserId==targetId)==2,"restore commits with second audit event");
            var users=new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(db));
            users.UserValidator=new UserValidator<ApplicationUser>(users){AllowOnlyAlphanumericUserNames=false,RequireUniqueEmail=true};
            for(int i=0;i<15;i++) {
                var user=new ApplicationUser{UserName="governance-page"+i+"@example.test",Email="governance-page"+i+"@example.test"};
                Check(users.Create(user).Succeeded,"page fixture "+i);users.AddToRole(user.Id,"KhachHang");
            }
        }
        using(var c=new AdminUsersController()) {
            var rows=(IEnumerable<UserRoleRow>)((ViewResult)c.Index(q:"governance-page",role:"KhachHang",page:2)).Model;
            Check(rows.Count()==3 && (int)c.ViewBag.TotalUsers==15,"server role filter and second page");
        }
        using(var c=new AdminUsersController()) {
            var rows=(IEnumerable<UserRoleRow>)((ViewResult)c.Index(verifiedPhone:"+84 901 234 567",status:"locked")).Model;
            Check(rows.Count()==1 && rows.Single().Id==targetId,"advanced verified phone and temporary lockout filter");
        }
        using(var c=new AdminUsersController()) {
            var rows=(IEnumerable<UserRoleRow>)((ViewResult)c.Index(status:"invalid")).Model;
            Check(!rows.Any() && !c.ModelState.IsValid,"invalid account status returns validation and no data");
        }
        using(var db=new ApplicationDbContext()) {
            var manager=new ApplicationUserManager(new UserStore<ApplicationUser>(db));
            var duplicate=new ApplicationUser{Email=" ADMIN@UI.EXAMPLE.TEST ",UserName=" ADMIN@UI.EXAMPLE.TEST "};
            var result=IdentityProvisioning.RegisterCustomerAsync(db,manager,duplicate,"TestOnly!Password9").GetAwaiter().GetResult();
            Check(!result.Succeeded,"normalized duplicate registration is rejected");
        }
    }
    private sealed class DemoHttpContext : System.Web.HttpContextBase
    {
        private readonly System.Security.Principal.IPrincipal principal;
        private readonly System.Web.HttpSessionStateBase session;
        public DemoHttpContext(System.Security.Principal.IPrincipal value, System.Web.HttpSessionStateBase valueSession = null) { principal = value; session = valueSession; }
        public override System.Web.HttpSessionStateBase Session { get { return session; } }
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
