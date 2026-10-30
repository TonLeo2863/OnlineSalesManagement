using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using OnlineSalesManagement.Models;
using OnlineSalesManagement.Services;
using OnlineSalesManagement.ViewModels;
namespace OnlineSalesManagement.Controllers
{
    [Authorize(Roles = "Admin")]
    [OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
    public class CatalogImportController : Controller
    {
        private readonly ApplicationDbContext db = new ApplicationDbContext();
        private const string Key = "CatalogImportPending";
        public ActionResult Upload(string kind = "products") { if (kind != "products" && kind != "categories") return new HttpStatusCodeResult(400); ViewBag.Kind = kind; return View(); }
        public ActionResult Template(string kind = "products")
        {
            if (kind != "products" && kind != "categories") return new HttpStatusCodeResult(400);
            var category = db.Categories.Where(c => c.IsActive).Select(c => c.Name).FirstOrDefault() ?? "Tạo danh mục trước";
            return File(CatalogWorkbook.Template(kind, category), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", kind + "-template.xlsx");
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Upload(string kind, HttpPostedFileBase workbook)
        {
            if (kind != "products" && kind != "categories") return new HttpStatusCodeResult(400); ViewBag.Kind = kind;
            try
            {
                if (string.IsNullOrWhiteSpace(User.Identity.GetUserId())) return new HttpUnauthorizedResult();
                Session.Remove(Key); // A new upload attempt invalidates any older confirmation intent.
                if (workbook == null || workbook.ContentLength <= 0 || workbook.ContentLength > CatalogWorkbook.MaxBytes || !string.Equals(Path.GetExtension(workbook.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Chỉ nhận .xlsx, tối đa 2 MB và 500 dòng.");
                var preview = CatalogWorkbook.Read(workbook.InputStream, kind);
                CatalogImportService.Validate(db, preview);
                preview.OwnerId = User.Identity.GetUserId(); preview.Token = Guid.NewGuid().ToString("N"); preview.ExpiresUtc = DateTime.UtcNow.AddMinutes(20);
                Session[Key] = preview;
                return RedirectToAction("Preview", new { token = preview.Token });
            }
            catch (ArgumentException ex) { ModelState.AddModelError("", ex.Message); }
            catch (Exception) { ModelState.AddModelError("", "Không đọc được workbook an toàn. Hãy dùng file mẫu, dán giá trị thuần và thử lại."); }
            return View();
        }
        private CatalogImportSnapshot Pending(string token)
        {
            var value = Session[Key] as CatalogImportSnapshot;
            return value != null && value.OwnerId == User.Identity.GetUserId() && value.Token == token && value.ExpiresUtc > DateTime.UtcNow ? value : null;
        }
        public ActionResult Preview(string token) { var model = Pending(token); return model == null ? (ActionResult)HttpNotFound("Preview đã hết hạn hoặc không thuộc phiên hiện tại.") : View(model); }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Confirm(string token)
        {
            var model = Pending(token); if (model == null) return HttpNotFound();
            try {
                var count = CatalogImportService.Confirm(db, model);
                if (count == 0) { ModelState.AddModelError("", "Dữ liệu còn lỗi hoặc đã thay đổi. Chưa lưu bản ghi nào."); return View("Preview", model); }
                Session.Remove(Key); TempData["CatalogImportResult"] = new CatalogImportResult { Kind = model.Kind, Added = count };
                return RedirectToAction("Result");
            } catch (Exception) { ModelState.AddModelError("", "Giao dịch đã rollback: dữ liệu trùng, liên kết thay đổi hoặc lỗi lưu. Chưa nhập một phần dữ liệu."); return View("Preview", model); }
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Cancel(string token) { var model = Pending(token); if (model == null) return HttpNotFound(); Session.Remove(Key); return RedirectToAction("Upload", new { kind = model.Kind }); }
        public ActionResult Result() { var result = TempData["CatalogImportResult"] as CatalogImportResult; return result == null ? (ActionResult)RedirectToAction("Upload") : View(result); }
        protected override void Dispose(bool disposing) { if (disposing) db.Dispose(); base.Dispose(disposing); }
    }
}
