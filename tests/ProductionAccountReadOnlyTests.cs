using System;
using System.Linq;
using System.Configuration;
using System.Data.Entity;
using System.Data.SqlClient;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using OnlineSalesManagement.Models;
public static class ProductionAccountReadOnlyTests
{
 static int count;
 static void Check(bool ok,string text){if(!ok)throw new Exception("FAIL: "+text);Console.WriteLine("PASS: "+text);count++;}
 public static int Main(){try{
  var cs=new SqlConnectionStringBuilder(ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString);
  if(cs.DataSource!="DESKTOP-AUTD3A3\\SQLEXPRESS"||cs.InitialCatalog!="OnlineSalesDb"||!cs.IntegratedSecurity)throw new Exception("Unexpected production read-only test target");
  using(var db=new ApplicationDbContext()){
   Check(db.Database.CompatibleWithModel(false),"EF6 model matches migrated production schema without disabling checks");
   var user=db.Users.AsNoTracking().OrderBy(u=>u.Id).FirstOrDefault();
   Check(user!=null,"existing Identity user readable");
   var users=new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(db));
   Check(users.FindByIdAsync(user.Id).GetAwaiter().GetResult()!=null,"FindByIdAsync used by cookie validation executes successfully");
   Check(!db.Users.Any(u=>u.IsAdministrativelySuspended),"all original accounts remain not suspended");
   Check(db.Users.Any(u=>u.Roles.Any(ur=>db.Roles.Any(r=>r.Id==ur.RoleId&&r.Name=="Admin"))),"existing Admin role membership preserved");
   Check(!db.AccountAdministrationAudits.Any(),"new audit table reads successfully and is empty");
   Check(db.Products.AsNoTracking().Count()==9&&db.Customers.AsNoTracking().Count()==6,"existing products/customers readable with expected preserved counts");
   Check(db.SalesOrders.AsNoTracking().Count()==13&&db.PurchaseReceipts.AsNoTracking().Count()==8,"existing sales and receipt records readable");
   Check(db.InventoryTransactions.AsNoTracking().Count()==27,"existing inventory transactions readable");
   // Deliberately no Add/Update/Delete/SaveChanges, password check, sign-in or SecurityStamp update.
  }
  Console.WriteLine("TOTAL PRODUCTION READ-ONLY EF PASS: "+count);return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
