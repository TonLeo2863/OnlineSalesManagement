using System;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using OnlineSalesManagement.Models;
namespace OnlineSalesManagement.Services
{
    public static class AccountGovernance
    {
        public static string NormalizeEmail(string email) { return (email ?? "").Trim().Normalize(NormalizationForm.FormKC).ToLowerInvariant(); }
        // VN local and E.164 numbers only. Order/customer contact numbers are intentionally not consulted.
        public static string NormalizePhone(string phone)
        {
            phone = Regex.Replace((phone ?? "").Trim(), @"[\s().-]", "");
            if (phone.StartsWith("00")) phone = "+" + phone.Substring(2);
            if (Regex.IsMatch(phone, @"^0[35789]\d{8}$")) phone = "+84" + phone.Substring(1);
            if (!Regex.IsMatch(phone, @"^\+[1-9]\d{7,14}$")) throw new ArgumentException("Số điện thoại phải đúng định dạng Việt Nam hoặc quốc tế (+...).");
            return phone;
        }
        public static bool VerifiedPhoneUsed(ApplicationDbContext db, string canonicalPhone, string exceptId)
        {
            // Include legacy formatting without changing any existing profile or number.
            return db.Users.Where(u => u.Id != exceptId && u.PhoneNumberConfirmed && u.PhoneNumber != null)
                .Select(u => u.PhoneNumber).ToList().Any(p => {
                    try { return NormalizePhone(p) == canonicalPhone; } catch (ArgumentException) { return false; }
                });
        }
        public static void SetSuspension(ApplicationDbContext db, string actorId, string targetId, bool suspended, string reason)
        {
            reason = (reason ?? "").Trim();
            if (reason.Length < 5 || reason.Length > 500) throw new ArgumentException("Lý do cần từ 5 đến 500 ký tự; không nhập mật khẩu, OTP hoặc thông tin nhạy cảm.");
            using (var tx = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                var adminRole = db.Roles.Single(r => r.Name == "Admin").Id;
                var actor = db.Users.SingleOrDefault(u => u.Id == actorId);
                if (actor == null || actor.IsAdministrativelySuspended || !actor.Roles.Any(r => r.RoleId == adminRole)) throw new UnauthorizedAccessException();
                var target = db.Users.SingleOrDefault(u => u.Id == targetId);
                if (target == null) throw new ArgumentException("Không tìm thấy tài khoản.");
                if (suspended && (actorId == targetId || (target.Roles.Any(r => r.RoleId == adminRole) &&
                    db.Users.Count(u => !u.IsAdministrativelySuspended && u.Roles.Any(r => r.RoleId == adminRole)) <= 1)))
                    throw new InvalidOperationException("Không được tự đình chỉ hoặc đình chỉ Admin hoạt động cuối cùng.");
                if (target.IsAdministrativelySuspended == suspended) return;
                target.IsAdministrativelySuspended = suspended;
                target.SecurityStamp = Guid.NewGuid().ToString("N");
                db.AccountAdministrationAudits.Add(new AccountAdministrationAudit { ActorUserId = actorId, TargetUserId = targetId,
                    Action = suspended ? "Suspend" : "Restore", Reason = reason, OccurredUtc = DateTime.UtcNow });
                db.SaveChanges(); tx.Commit();
            }
        }
    }
}
