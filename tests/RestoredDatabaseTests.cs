using System;
using System.Linq;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Data.SqlClient;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using OnlineSalesManagement;
using OnlineSalesManagement.Models;
using OnlineSalesManagement.Services;
public static class RestoredDatabaseTests
{
 static int n;static void Check(bool ok,string msg){if(!ok)throw new Exception("FAIL: "+msg);Console.WriteLine("PASS: "+msg);n++;}
 static void Reject(Action action,string msg){try{action();}catch(InvalidOperationException){Check(true,msg);return;}throw new Exception("FAIL: "+msg);}
 static int Stock(int id){using(var db=new ApplicationDbContext())return db.Products.Find(id).QuantityInStock;}
 public static int Main(){try{
  var cs=new SqlConnectionStringBuilder(ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString);
  if(!cs.InitialCatalog.StartsWith("Dacn05_Restore_",StringComparison.Ordinal)||!cs.IntegratedSecurity)throw new Exception("Unsafe target");
  int p,c,e,s;var run=Environment.GetEnvironmentVariable("DACN_RESTORE_RUN");
  using(var db=new ApplicationDbContext()){
   Check(db.Database.CompatibleWithModel(false),"EF model matches restored migrated database without disabling initializer");
   Check(db.Users.All(u=>!u.IsAdministrativelySuspended),"all restored accounts default to not suspended");
   var users=new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(db));var roles=new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(db));
   foreach(var role in new[]{"Admin","Kho","BanHang","KhachHang"}){
    if(!roles.RoleExists(role))Check(roles.Create(new IdentityRole(role)).Succeeded,"create test role "+role);
    var email="restore"+run+role.ToLowerInvariant()+"@fixture.example.test";
    var user=new ApplicationUser{Email=email,UserName=email};
    Check(users.Create(user,Environment.GetEnvironmentVariable("DACN_UI_TEST_PASSWORD")).Succeeded,"new test account "+role);
    Check(users.AddToRole(user.Id,role).Succeeded,"assign test role "+role);
   }
   var category=new Category{Name="Restore rehearsal fixture "+run};var product=new Product{Sku="RESTORE-"+run,Name="Migration test product",Category=category,SalePrice=100m};
   var customer=new Customer{Code="RC-"+run,FullName="Restore test customer",PhoneNumber="0901234567"};var employee=new Employee{Code="RE-"+run,FullName="Restore test employee"};var supplier=new Supplier{Code="RS-"+run,Name="Restore test supplier"};
   db.Products.Add(product);db.Customers.Add(customer);db.Employees.Add(employee);db.Suppliers.Add(supplier);db.SaveChanges();p=product.Id;c=customer.Id;e=employee.Id;s=supplier.Id;
   product.Name="Updated test product";customer.FullName="Updated test customer";employee.Position="Nhan vien test";supplier.Name="Updated test supplier";db.SaveChanges();
   Check(db.Products.AsNoTracking().Single(x=>x.Id==p).Name=="Updated test product","CRUD create/read/update fixtures only");
   var disposable=new Category{Name="Disposable restore test category"};db.Categories.Add(disposable);db.SaveChanges();db.Categories.Remove(disposable);db.SaveChanges();Check(!db.Categories.Any(x=>x.Name=="Disposable restore test category"),"delete unreferenced fixture only");
  }
  var receipts=new PurchaseReceiptService();var sales=new SalesOrderService();
  var receipt=receipts.CreateDraft(new PurchaseReceiptDraftInput{ReceiptNumber="RI-"+run,SupplierId=s,EmployeeId=e,Details=new List<PurchaseReceiptLineInput>{new PurchaseReceiptLineInput{ProductId=p,Quantity=10,UnitCost=60m}}});
  Check(Stock(p)==0,"draft leaves stock unchanged");receipts.ConfirmReceipt(receipt,"restore-rehearsal");Check(Stock(p)==10,"receipt confirms through existing service");Reject(()=>receipts.ConfirmReceipt(receipt,"restore-rehearsal"),"receipt replay rejected");
  Func<string,int,int> order=(code,qty)=>sales.CreatePending(new SalesOrderPendingInput{OrderNumber=code+run,CustomerId=c,EmployeeId=e,Channel=SalesChannel.Counter,Details=new List<SalesOrderLineInput>{new SalesOrderLineInput{ProductId=p,Quantity=qty}}});
  var id=order("RESTORE-OUT",3);Check(Stock(p)==10,"pending leaves stock unchanged");sales.ConfirmOrder(id,"restore-rehearsal");Check(Stock(p)==7,"sales confirmation deducts stock");Reject(()=>sales.ConfirmOrder(id,"restore-rehearsal"),"sale replay rejected");sales.CompleteOrder(id);Check(Stock(p)==7,"completion does not deduct twice");
  var cancelled=order("RESTORE-CANCEL",2);sales.ConfirmOrder(cancelled,"restore-rehearsal");sales.CancelOrder(cancelled,"restore-rehearsal");Check(Stock(p)==7,"confirmed cancellation restores stock");
  var over=order("RESTORE-OVER",100);Reject(()=>sales.ConfirmOrder(over,"restore-rehearsal"),"insufficient stock rejected");Check(Stock(p)==7,"insufficient stock rollback");
  using(var db=new ApplicationDbContext()){
   Check(db.SalesOrders.Find(id).Status==OrderStatus.Completed,"completed state persisted");
   var sums=db.InventoryTransactions.GroupBy(t=>t.ProductId).Select(g=>new{Id=g.Key,Quantity=g.Sum(t=>(long)t.QuantityChange)}).ToDictionary(x=>x.Id,x=>x.Quantity);
   var mismatches=db.Products.AsNoTracking().ToList().Count(x=>x.QuantityInStock!=(sums.ContainsKey(x.Id)?sums[x.Id]:0));
   Check(mismatches==0,"all restored and new inventory reconciles with ledger");
  }
  Console.WriteLine("TOTAL RESTORED DB PASS: "+n);return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
