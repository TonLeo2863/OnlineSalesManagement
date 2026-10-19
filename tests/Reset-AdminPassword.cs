using System;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Security;
using System.Runtime.InteropServices;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security.DataProtection;
using OnlineSalesManagement;
using OnlineSalesManagement.Models;

public static class ResetAdminPassword
{
    public sealed class SafeFailure : InvalidOperationException
    {
        public SafeFailure(string code, string message) : base(code + ": " + message) { }
    }
    public static string Stage = "START";
    public static string Diagnostic(Exception exception)
    {
        if (exception is SafeFailure) return exception.Message;
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current is NotSupportedException && current.Message == "The LINQ expression node type 'ArrayIndex' is not supported in LINQ to Entities.")
                return "LINQ_ARRAY_INDEX: stage=" + Stage + "; EF6 cannot translate array indexing in a database predicate.";
            var sql = current as SqlException;
            if (sql != null) return "DATABASE_ERROR: stage=" + Stage + "; sql=" + sql.Number + "; state=" + sql.State + "; class=" + sql.Class;
            if (current is System.Security.Cryptography.CryptographicException)
                return "TOKEN_CRYPTO_ERROR: stage=" + Stage + "; hresult=" + current.HResult;
            if (current is System.Data.Entity.Validation.DbEntityValidationException)
                return "DATABASE_ENTITY_VALIDATION: stage=" + Stage + "; entity validation rejected persistence (values suppressed).";
            if (current is System.Data.Entity.Infrastructure.DbUpdateConcurrencyException)
                return "DATABASE_CONCURRENCY: stage=" + Stage + "; account changed concurrently.";
            if (current is System.Configuration.ConfigurationErrorsException)
                return "CONFIGURATION_ERROR: stage=" + Stage + "; hresult=" + current.HResult;
            if (current is System.IO.FileNotFoundException || current is System.IO.FileLoadException || current is TypeLoadException)
                return "DEPENDENCY_ERROR: stage=" + Stage + "; hresult=" + current.HResult;
        }
        return "OPERATION_ERROR: stage=" + Stage + "; hresult=" + exception.HResult;
    }
    public static void MatchPasswords(string password, string repeated)
    {
        if (password != repeated) throw new SafeFailure("PASSWORD_MISMATCH", "The two passwords do not match.");
    }
    public static void ValidatePassword(ApplicationUserManager manager, string password)
    {
        if (manager.PasswordValidator.ValidateAsync(password).GetAwaiter().GetResult().Succeeded) return;
        // Fixed messages only; never echo Identity/custom validator errors containing input.
        var policy = manager.PasswordValidator as PasswordValidator;
        string message = "Password rejected by the configured validator.";
        if (policy != null && policy.GetType() == typeof(PasswordValidator))
            message = "Password requires length >= " + policy.RequiredLength +
                "; uppercase=" + policy.RequireUppercase + "; lowercase=" + policy.RequireLowercase +
                "; digit=" + policy.RequireDigit + "; non-alphanumeric=" + policy.RequireNonLetterOrDigit + ".";
        throw new SafeFailure("PASSWORD_POLICY", message);
    }
    public static ApplicationUserManager Manager(ApplicationDbContext db)
    {
        return ApplicationUserManager.Create(new IdentityFactoryOptions<ApplicationUserManager> {
            DataProtectionProvider = new DpapiDataProtectionProvider("OnlineSalesManagement.LocalAdminReset")
        }, Context(db));
    }
    private static Microsoft.Owin.OwinContext Context(ApplicationDbContext db)
    {
        var context = new Microsoft.Owin.OwinContext();
        context.Set<ApplicationDbContext>(db);
        return context;
    }
    public static void Validate(ApplicationDbContext db, string id, string email, string server, string database)
    {
        db.Database.Connection.Open();
        using (var command = db.Database.Connection.CreateCommand())
        {
            command.CommandText = "SELECT CONVERT(nvarchar(128), SERVERPROPERTY('ServerName')), DB_NAME()";
            using (var reader = command.ExecuteReader())
            {
                reader.Read();
                if (!string.Equals(reader.GetString(0), server, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(reader.GetString(1), database, StringComparison.Ordinal))
                    throw new SafeFailure("TARGET_DATABASE_MISMATCH", "Actual SQL Server/database mismatch.");
            }
        }
        var user = db.Users.SingleOrDefault(u => u.Id == id);
        if (user == null || !string.Equals(user.Id, id, StringComparison.Ordinal) ||
            !string.Equals(user.Email, email, StringComparison.Ordinal))
            throw new SafeFailure("TARGET_ACCOUNT_MISMATCH", "Exact UserId/Email mismatch.");
        var admin = db.Roles.SingleOrDefault(r => r.Name == "Admin");
        if (admin == null || admin.Name != "Admin" || !user.Roles.Any(r => r.RoleId == admin.Id))
            throw new SafeFailure("TARGET_NOT_ADMIN", "Target is not an Admin.");
    }
    public static void Reset(ApplicationUserManager manager, string id, string password)
    {
        Stage = "PASSWORD_VALIDATION";
        ValidatePassword(manager, password);
        Stage = "SECURITY_STAMP_READ";
        var stamp = manager.GetSecurityStamp(id);
        Stage = "TOKEN_GENERATION";
        if (manager.UserTokenProvider == null) throw new SafeFailure("TOKEN_PROVIDER_MISSING", "No UserTokenProvider configured.");
        var token = manager.GeneratePasswordResetToken(id);
        Stage = "TOKEN_VALIDATION";
        if (!manager.VerifyUserToken(id, "ResetPassword", token))
            throw new SafeFailure("TOKEN_INVALID", "Generated reset token failed validation.");
        Stage = "RESET_PASSWORD";
        var result = manager.ResetPassword(id, token, password);
        // Never print validator/exception content: it could contain sensitive input.
        if (!result.Succeeded) throw new SafeFailure("RESET_REJECTED", "Identity rejected reset after password/token prechecks; account validation or token state may have changed.");
        Stage = "STAMP_VERIFICATION";
        if (manager.GetSecurityStamp(id) == stamp)
            throw new SafeFailure("STAMP_NOT_ROTATED", "Security stamp was not rotated; transaction must roll back.");
    }
    private static SecureString ReadSecret()
    {
        var value = new SecureString();
        while (true)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return value; }
            if (key.Key == ConsoleKey.Backspace) { if (value.Length > 0) value.RemoveAt(value.Length - 1); }
            else if (!char.IsControl(key.KeyChar)) value.AppendChar(key.KeyChar);
        }
    }
    private static string Unwrap(SecureString value)
    {
        var ptr = Marshal.SecureStringToBSTR(value);
        try { return Marshal.PtrToStringBSTR(ptr); }
        finally { Marshal.ZeroFreeBSTR(ptr); }
    }
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length != 5 || (args[4] != "preview" && args[4] != "apply"))
                throw new SafeFailure("ARGUMENT_ERROR", "Expected UserId Email Server Database preview|apply.");
            Database.SetInitializer<ApplicationDbContext>(null);
            using (var db = new ApplicationDbContext())
            {
                db.Database.Log = null;
                Stage = "TARGET_VALIDATION";
                Validate(db, args[0], args[1], args[2], args[3]);
                Console.WriteLine("Verified Admin: " + args[0] + " / " + args[1]);
                Console.WriteLine("Verified SQL Server/database: " + args[2] + " / " + args[3]);
                if (args[4] == "preview") { Console.WriteLine("PREVIEW: no writes."); return 0; }
                if (Console.IsInputRedirected) throw new SafeFailure("CONSOLE_REQUIRED", "Apply requires an interactive local console.");
                var phrase = "RESET " + args[0] + " " + args[1] + " ON " + args[2] + "/" + args[3];
                Console.WriteLine("Type exactly: " + phrase);
                if (Console.ReadLine() != phrase) throw new SafeFailure("CONFIRMATION_DECLINED", "Confirmation declined.");
                Console.Write("New password (hidden): ");
                using (var first = ReadSecret())
                {
                    Console.Write("Repeat password (hidden): ");
                    using (var second = ReadSecret())
                    {
                        string password = Unwrap(first), repeated = Unwrap(second);
                        try
                        {
                            MatchPasswords(password, repeated);
                            Stage = "TRANSACTION_BEGIN";
                            using (var tx = db.Database.BeginTransaction(IsolationLevel.Serializable))
                            {
                                Stage = "MANAGER_CREATE";
                                using (var manager = Manager(db))
                                {
                                Stage = "TARGET_RECHECK";
                                Recheck(db, args);
                                Reset(manager, args[0], password);
                                Stage = "TRANSACTION_COMMIT";
                                tx.Commit();
                                }
                            }
                        }
                        finally { password = null; repeated = null; }
                    }
                }
                Console.WriteLine("Password reset complete; old application sessions are invalid on their next request.");
            }
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(Diagnostic(ex)); return 1; }
    }
    public static void Recheck(ApplicationDbContext db, string[] args)
    {
        // EF6 cannot translate ArrayIndex (args[0]) inside an expression tree.
        var id = args[0];
        var target = db.Users.Single(u => u.Id == id);
        db.Entry(target).Reload();
        db.Entry(target).Collection(u => u.Roles).Load();
        ValidateOpen(db, args);
    }
    public static void ValidateOpen(ApplicationDbContext db, string[] args)
    {
        var id = args[0];
        var email = args[1];
        var user = db.Users.AsNoTracking().SingleOrDefault(u => u.Id == id);
        if (user == null || user.Id != id || user.Email != email ||
            !db.Roles.Any(r => r.Name == "Admin" && r.Users.Any(u => u.UserId == user.Id)))
            throw new SafeFailure("TARGET_CHANGED", "Account or Admin membership changed; reset cancelled.");
    }
}
