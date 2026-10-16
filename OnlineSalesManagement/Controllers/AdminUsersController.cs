using System;
using System.Collections.Generic;
using System.Data;
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
    }
    [Authorize(Roles = "Admin")]
    public class AdminUsersController : Controller
    {
        public static readonly string[] AllowedRoles = { "Admin", "Kho", "BanHang", "KhachHang" };
        private readonly ApplicationDbContext db = new ApplicationDbContext();
        public ActionResult Index()
        {
            var names = db.Roles.ToDictionary(r => r.Id, r => r.Name);
            var rows = db.Users.ToList().Select(u => new UserRoleRow {
                Id = u.Id, Email = u.Email,
                Roles = string.Join(", ", u.Roles.Select(r => names[r.RoleId]))
            }).ToList();
            return View(rows);
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
                        if (id == User.Identity.GetUserId() || db.Users.Count(u => u.Roles.Any(r => r.RoleId == admin.Id)) <= 1)
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
