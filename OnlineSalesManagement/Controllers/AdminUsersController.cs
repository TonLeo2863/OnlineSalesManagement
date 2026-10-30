using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using OnlineSalesManagement.Services;
using System.Linq;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using OnlineSalesManagement.Models;

namespace OnlineSalesManagement.Controllers
{
    public class UserRoleRow
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public string Roles { get; set; }
        public string UserName { get; set; }
        public bool IsSuspended { get; set; }
        public bool IsTemporarilyLocked { get; set; }
        public bool PhoneVerified { get; set; }
    }
    [Authorize(Roles = "Admin")]
    [OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
    public class AdminUsersController : Controller
    {
        public static readonly string[] AllowedRoles = { "Admin", "Kho", "BanHang", "KhachHang" };
        private readonly ApplicationDbContext db = new ApplicationDbContext();
        // Display ordering only; authorization continues to use Identity roles.
        public static int DisplayRoleOrder(string roles)
        {
            var current = (roles ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(r => r.Trim()).ToArray();
            for (int i = 0; i < AllowedRoles.Length; i++) if (current.Contains(AllowedRoles[i], StringComparer.OrdinalIgnoreCase)) return i;
            return current.Length == 0 ? 5 : 4;
        }
        public ActionResult Index(string q = null, string role = null, string status = null, string email = null, string verifiedPhone = null, int page = 1)
        {
            var names = db.Roles.ToDictionary(r => r.Id, r => r.Name);
            var query = db.Users.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(q)) { q = q.Trim(); query = query.Where(u => u.Email.Contains(q) || u.UserName.Contains(q)); }
            if (!string.IsNullOrWhiteSpace(email)) { email = AccountGovernance.NormalizeEmail(email); query = query.Where(u => u.Email.ToLower().Contains(email)); }
            if (!string.IsNullOrWhiteSpace(role))
            {
                if (role == "None") query = query.Where(u => !u.Roles.Any());
                else if (AllowedRoles.Contains(role)) { var id = names.FirstOrDefault(r => r.Value == role).Key; query = query.Where(u => u.Roles.Any(r => r.RoleId == id)); }
                else ModelState.AddModelError("", "Vai trò lọc không hợp lệ.");
            }
            var now = DateTime.UtcNow;
            if (status == "active") query = query.Where(u => !u.IsAdministrativelySuspended && (!u.LockoutEnabled || u.LockoutEndDateUtc == null || u.LockoutEndDateUtc <= now));
            else if (status == "suspended") query = query.Where(u => u.IsAdministrativelySuspended);
            else if (status == "locked") query = query.Where(u => !u.IsAdministrativelySuspended && u.LockoutEnabled && u.LockoutEndDateUtc > now);
            else if (!string.IsNullOrEmpty(status)) ModelState.AddModelError("", "Trạng thái lọc không hợp lệ.");
            if (!string.IsNullOrWhiteSpace(verifiedPhone))
            {
                try {
                    var phone = AccountGovernance.NormalizePhone(verifiedPhone);
                    var ids = db.Users.AsNoTracking().Where(u => u.PhoneNumberConfirmed && u.PhoneNumber != null).Select(u => new {u.Id, u.PhoneNumber}).ToList()
                        .Where(u => { try { return AccountGovernance.NormalizePhone(u.PhoneNumber) == phone; } catch (ArgumentException) { return false; } }).Select(u => u.Id).ToList();
                    query = query.Where(u => ids.Contains(u.Id));
                } catch (ArgumentException ex) { ModelState.AddModelError("verifiedPhone", ex.Message); }
            }
            if (!ModelState.IsValid) query = query.Where(u => false);
            const int size = 12;
            var total = query.Count();
            var pages = Math.Max(1, (total + size - 1) / size);
            page = Math.Min(Math.Max(1, page), pages);
            var admin = names.FirstOrDefault(r => r.Value == "Admin").Key;
            var kho = names.FirstOrDefault(r => r.Value == "Kho").Key;
            var sales = names.FirstOrDefault(r => r.Value == "BanHang").Key;
            var customer = names.FirstOrDefault(r => r.Value == "KhachHang").Key;
            var rows = query.Include(u => u.Roles).OrderBy(u => u.Roles.Any(r => r.RoleId == admin) ? 0 : u.Roles.Any(r => r.RoleId == kho) ? 1 : u.Roles.Any(r => r.RoleId == sales) ? 2 : u.Roles.Any(r => r.RoleId == customer) ? 3 : u.Roles.Any() ? 4 : 5)
                .ThenBy(u => u.Email ?? u.UserName).ThenBy(u => u.Id).Skip((page - 1) * size).Take(size).ToList().Select(u => new UserRoleRow {
                    Id = u.Id, Email = string.IsNullOrWhiteSpace(u.Email) ? u.UserName : u.Email, UserName = u.UserName,
                    Roles = string.Join(", ", u.Roles.Select(r => names.ContainsKey(r.RoleId) ? names[r.RoleId] : "Vai trò không xác định")),
                    IsSuspended = u.IsAdministrativelySuspended, IsTemporarilyLocked = u.LockoutEnabled && u.LockoutEndDateUtc > now, PhoneVerified = u.PhoneNumberConfirmed
                }).ToList();
            ViewBag.TotalUsers = total; ViewBag.Page = page; ViewBag.PageCount = pages;
            return View(rows);
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult SetSuspension(string id, bool suspended, string reason, bool confirmed = false)
        {
            if (!ModelState.IsValid || !confirmed) { TempData["Error"] = "Cần xác nhận thao tác trước khi lưu."; return RedirectToAction("Index"); }
            try {
                AccountGovernance.SetSuspension(db, User.Identity.GetUserId(), id, suspended, reason);
                TempData["Success"] = suspended ? "Đã đình chỉ tài khoản và thu hồi phiên cũ." : "Đã khôi phục tài khoản. Người dùng cần đăng nhập lại.";
            }
            catch (UnauthorizedAccessException) { return new HttpStatusCodeResult(403); }
            catch (ArgumentException ex) { TempData["Error"] = ex.Message; }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            catch (Exception) { TempData["Error"] = "Không thể lưu do dữ liệu vừa thay đổi. Hãy tải lại và thử lại."; }
            return RedirectToAction("Index");
        }
        public ActionResult AccountAudit(int page = 1)
        {
            page = Math.Max(1, Math.Min(page, 100000));
            return View(db.AccountAdministrationAudits.AsNoTracking().OrderByDescending(a => a.Id).Skip((page - 1) * 50).Take(50).ToList());
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult SetRole(string id, string role)
        {
            if (!AllowedRoles.Contains(role)) return new HttpStatusCodeResult(400);
            try
            {
                using (var tx = db.Database.BeginTransaction(IsolationLevel.Serializable))
                using (var users = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(db)))
                using (var roles = new RoleManager<IdentityRole>(new RoleStore<IdentityRole>(db)))
                {
                    var user = users.FindById(id);
                    if (user == null) return HttpNotFound();
                    var existing = users.GetRoles(id).ToArray();
                    if (existing.Contains("Admin") && role != "Admin")
                    {
                        var admin = db.Roles.Single(r => r.Name == "Admin");
                        if (id == User.Identity.GetUserId() || (!user.IsAdministrativelySuspended && db.Users.Count(u => !u.IsAdministrativelySuspended && u.Roles.Any(r => r.RoleId == admin.Id)) <= 1))
                            throw new InvalidOperationException("Không được gỡ quyền Admin của chính mình hoặc Admin cuối cùng.");
                    }
                    if (!roles.RoleExists(role)) Ensure(roles.Create(new IdentityRole(role)));
                    if (existing.Length > 0) Ensure(users.RemoveFromRoles(id, existing));
                    Ensure(users.AddToRole(id, role));
                    Ensure(users.UpdateSecurityStamp(id));
                    tx.Commit();
                    TempData["Success"] = "Đã cập nhật vai trò. Phiên cũ của tài khoản sẽ được kiểm tra lại.";
                }
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Role update failed: {0}", ex.GetType().Name);
                TempData["Error"] = "Không thể cập nhật vai trò do xung đột dữ liệu. Vui lòng tải lại và thử lại.";
            }
            return RedirectToAction("Index");
        }
        private static void Ensure(IdentityResult result)
        {
            if (!result.Succeeded) throw new InvalidOperationException("Không thể cập nhật quyền tài khoản.");
        }
        public ActionResult Activity()
        {
            return View(db.InventoryTransactions.OrderByDescending(t => t.Id).Take(200).ToList());
        }
        protected override void Dispose(bool disposing) { if (disposing) db.Dispose(); base.Dispose(disposing); }
    }
}
