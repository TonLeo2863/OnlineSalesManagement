using System;
using System.Linq;
using OnlineSalesManagement.Models;
using OnlineSalesManagement.Services;

public static class FirstAdminTool
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length < 2 || args.Length > 3) throw new ArgumentException("Expected user ID, email, optional --apply.");
            System.Data.Entity.Database.SetInitializer<ApplicationDbContext>(null);
            var userId = args[0];
            using (var db = new ApplicationDbContext())
            {
                Console.WriteLine("Database: " + db.Database.Connection.DataSource + " / " + db.Database.Connection.Database);
                var user = db.Users.SingleOrDefault(u => u.Id == userId);
                if (user == null || !string.Equals(user.Email, args[1], StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("ID/email mismatch.");
                Console.WriteLine("Target: " + user.Id + " / " + user.Email);
                if (args.Length == 2) { Console.WriteLine("PREVIEW ONLY. No data changed."); return 0; }
                if (args[2] != "--apply") throw new ArgumentException("Unknown option.");
                var phrase = "GRANT ADMIN " + user.Id + " " + user.Email;
                Console.WriteLine("Type exactly to confirm: " + phrase);
                if (!string.Equals(Console.ReadLine(), phrase, StringComparison.Ordinal)) throw new InvalidOperationException("Confirmation declined.");
                IdentityProvisioning.InitializeFirstAdmin(db, user.Id, user.Email, true);
                Console.WriteLine("First Admin initialized. Sign out and sign in again.");
            }
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }
}
