using System;
using System.Configuration;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Data.Entity.Infrastructure;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using OnlineSalesManagement.Models;

public static class ResetAdminPasswordTests
{
    static readonly List<string> secrets = new List<string>();
    public static int Main()
    {
        try { return Run(); }
        catch (Exception ex)
        {
            var detail = ResetAdminPassword.Diagnostic(ex);
            Console.Error.WriteLine(detail);
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ResetAdminPasswordTests.error.log"), detail);
            return 1;
        }
    }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS: " + message); }
    static void Reject(Action action, string message) { try { action(); } catch (InvalidOperationException) { Check(true, message); return; } throw new Exception(message); }
    static int Run()
    {
        Database.SetInitializer<ApplicationDbContext>(null);
        var cs = new SqlConnectionStringBuilder(ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString);
        secrets.Add(cs.ConnectionString);
        secrets.Add(cs.Password);
        if (!Regex.IsMatch(cs.InitialCatalog, @"\AAdminReset_Test_[0-9a-f]{32}\z") || !cs.IntegratedSecurity ||
            !string.IsNullOrEmpty(cs.AttachDBFilename)) throw new InvalidOperationException("Unsafe test configuration rejected before connecting.");
        Console.WriteLine("Isolated database retained: " + cs.InitialCatalog);
        using (var db = new ApplicationDbContext())
        {
            if (db.Database.Exists()) throw new Exception("Test database must be new.");
            ((IObjectContextAdapter)db).ObjectContext.CreateDatabase(); // Schema fixture only; no DbMigrator.
            var server = db.Database.SqlQuery<string>("SELECT CONVERT(nvarchar(128), SERVERPROPERTY('ServerName'))").SingleAsync().Result;
            using (var manager = ResetAdminPassword.Manager(db))
            using (var roles = new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(db)))
            {
                var oldPassword = "Old!" + Guid.NewGuid().ToString("N") + "aA1";
                var password = "New!" + Guid.NewGuid().ToString("N") + "aA1";
                secrets.Add(oldPassword); secrets.Add(password);
                var user = new ApplicationUser { UserName = "reset-test@example.invalid", Email = "reset-test@example.invalid" };
                Check(manager.Create(user, oldPassword).Succeeded, "create fixture account");
                Check(roles.Create(new IdentityRole("Admin")).Succeeded, "create fixture role");
                Reject(() => { using (var probe = new ApplicationDbContext()) ResetAdminPassword.Validate(probe, user.Id, user.Email, server, cs.InitialCatalog); }, "non Admin rejected");
                Check(manager.AddToRole(user.Id, "Admin").Succeeded, "fixture Admin assigned");
                var customer = new OnlineSalesManagement.Customer {
                    Code = "RESET-TEST", FullName = "Reset test customer", PhoneNumber = "0901234567", UserId = user.Id
                };
                var order = new OnlineSalesManagement.SalesOrder {
                    OrderNumber = "RESET-TEST-ORDER", Customer = customer, SubTotal = 100m, TotalAmount = 100m
                };
                db.SalesOrders.Add(order);
                db.SaveChanges();
                var stamp = manager.GetSecurityStamp(user.Id);
                var hash = user.PasswordHash;
                // Validate uses a fresh connection/context for each invocation.
                Action<string,string,string,string> validate = (id,email,expectedServer,catalog) => {
                    using (var probe = new ApplicationDbContext()) ResetAdminPassword.Validate(probe,id,email,expectedServer,catalog);
                };
                Reject(() => validate("wrong", user.Email, server, cs.InitialCatalog), "wrong ID rejected");
                Reject(() => validate(user.Id, "wrong", server, cs.InitialCatalog), "wrong email rejected");
                Reject(() => validate(user.Id, user.Email, "wrong", cs.InitialCatalog), "wrong server rejected");
                Reject(() => validate(user.Id, user.Email, server, "wrong"), "wrong database rejected");
                validate(user.Id, user.Email, server, cs.InitialCatalog);
                Check(user.PasswordHash == hash && manager.GetSecurityStamp(user.Id) == stamp, "preview preserves hash and stamp");
                Reject(() => ResetAdminPassword.MatchPasswords("one", "two"), "password mismatch rejected");
                var provider = manager.UserTokenProvider;
                manager.UserTokenProvider = null;
                Reject(() => ResetAdminPassword.Reset(manager, user.Id, password), "missing token provider rejected");
                manager.UserTokenProvider = provider;
                using (var tx = db.Database.BeginTransaction())
                {
                    Reject(() => ResetAdminPassword.Reset(manager, user.Id, "x"), "weak password rejected");
                    tx.Rollback();
                }
                using (var tx = db.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    ResetAdminPassword.Stage = "TARGET_RECHECK";
                    var args = new[] { user.Id, user.Email, server, cs.InitialCatalog, "apply" };
                    try
                    {
                        db.Users.Single(u => u.Id == args[0]);
                        throw new Exception("Expected original ArrayIndex failure.");
                    }
                    catch (NotSupportedException ex)
                    {
                        Check(ResetAdminPassword.Diagnostic(ex).StartsWith("LINQ_ARRAY_INDEX:"), "original Apply ArrayIndex failure reproduced safely");
                    }
                    ResetAdminPassword.Recheck(db, args);
                    Check(true, "Apply transaction recheck");
                    ResetAdminPassword.Reset(manager, user.Id, password);
                    tx.Commit();
                }
                Check(manager.CheckPassword(user, password) && !manager.CheckPassword(user, oldPassword), "new password works and old password fails");
                Check(manager.GetSecurityStamp(user.Id) != stamp, "old session stamp revoked");
                Check(manager.IsInRole(user.Id, "Admin") && db.Users.CountAsync().Result == 1, "account and Admin role preserved");
                using (var persisted = new ApplicationDbContext())
                using (var persistedManager = ResetAdminPassword.Manager(persisted))
                {
                    var saved = persistedManager.FindById(user.Id);
                    Check(persistedManager.CheckPassword(saved, password) && !persistedManager.CheckPassword(saved, oldPassword), "committed credentials verified from fresh context");
                    Check(persisted.SalesOrders.Count() == 1 && persisted.Customers.Count() == 1 &&
                        persisted.SalesOrders.Single().TotalAmount == 100m &&
                        persisted.SalesOrders.Single().OrderNumber == "RESET-TEST-ORDER" &&
                        persisted.Customers.Single().UserId == user.Id, "sales fixture and customer account link preserved");
                }
            }
        }
        return 0;
    }
}
