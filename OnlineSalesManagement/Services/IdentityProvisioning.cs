using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using OnlineSalesManagement.Models;

namespace OnlineSalesManagement.Services
{
    public static class IdentityProvisioning
    {
        // The user and role membership commit together; no cookie is issued on failure.
        public static async Task<IdentityResult> RegisterCustomerAsync(ApplicationDbContext db,
            ApplicationUserManager users, ApplicationUser user, string password, UserLoginInfo login = null)
        {
            using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                var roles = new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(db));
                if (!roles.RoleExists("KhachHang"))
                {
                    var roleResult = roles.Create(new IdentityRole("KhachHang"));
                    if (!roleResult.Succeeded) return roleResult;
                }
                var result = password == null ? await users.CreateAsync(user) : await users.CreateAsync(user, password);
                if (!result.Succeeded) return result;
                result = await users.AddToRoleAsync(user.Id, "KhachHang");
                if (!result.Succeeded) return result;
                if (login != null)
                {
                    result = await users.AddLoginAsync(user.Id, login);
                    if (!result.Succeeded) return result;
                }
                transaction.Commit();
                return IdentityResult.Success;
            }
        }

        // Invoked only by the local operator tool, never by registration or an HTTP endpoint.
        public static void InitializeFirstAdmin(ApplicationDbContext db, string userId, string expectedEmail, bool confirmed)
        {
            if (!confirmed || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(expectedEmail))
                throw new InvalidOperationException("Cần xác nhận rõ ID và email của Admin đầu tiên.");
            using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                if (db.Users.Any(u => u.Roles.Any(ur => db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Admin"))))
                    throw new InvalidOperationException("Đã có Admin. Hãy dùng chức năng quản trị người dùng.");
                var user = db.Users.SingleOrDefault(u => u.Id == userId);
                if (user == null || !string.Equals(user.Email, expectedEmail, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("ID/email không khớp tài khoản được xác nhận.");
                var roles = new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(db));
                var users = new ApplicationUserManager(new UserStore<ApplicationUser>(db));
                if (!roles.RoleExists("Admin")) Ensure(roles.Create(new IdentityRole("Admin")));
                Ensure(users.AddToRole(userId, "Admin"));
                Ensure(users.UpdateSecurityStamp(userId));
                transaction.Commit();
            }
        }
        private static void Ensure(IdentityResult result)
        {
            if (!result.Succeeded) throw new InvalidOperationException("Không thể khởi tạo Admin; giao dịch đã hủy.");
        }
    }
}
