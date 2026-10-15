using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Web.Mvc;
using OnlineSalesManagement.Models;

namespace OnlineSalesManagement.Controllers
{
    [Authorize(Roles = "Admin,Kho")]
    public class CategoriesController : Controller
    {
        private readonly ApplicationDbContext db = new ApplicationDbContext();
        public ActionResult Index() { return View(db.Categories.OrderBy(c => c.Name).ToList()); }
        public ActionResult Create() { return View("Edit", new Category()); }
        public ActionResult Edit(int id)
        {
            var category = db.Categories.Find(id);
            return category == null ? (ActionResult)HttpNotFound() : View(category);
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "Name,Description,IsActive")] Category model)
        {
            ValidateName(model);
            if (ModelState.IsValid)
            {
                db.Categories.Add(model);
                if (Save()) return RedirectToAction("Index");
            }
            return View("Edit", model);
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "Id,Name,Description,IsActive")] Category model)
        {
            var current = db.Categories.Find(model.Id);
            if (current == null) return HttpNotFound();
            ValidateName(model);
            if (ModelState.IsValid)
            {
                current.Name = model.Name;
                current.Description = model.Description;
                current.IsActive = model.IsActive;
                if (Save()) return RedirectToAction("Index");
            }
            return View(model);
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var current = db.Categories.Find(id);
            if (current == null) return HttpNotFound();
            if (db.Products.Any(p => p.CategoryId == id))
                TempData["Error"] = "Danh mục có sản phẩm; hãy ngừng sử dụng thay vì xóa.";
            else
            {
                db.Categories.Remove(current);
                if (!Save()) TempData["Error"] = "Không thể xóa danh mục có liên kết hoặc vừa thay đổi.";
            }
            return RedirectToAction("Index");
        }
        private void ValidateName(Category model)
        {
            model.Name = (model.Name ?? "").Trim();
            if (db.Categories.Any(c => c.Name == model.Name && c.Id != model.Id))
                ModelState.AddModelError("Name", "Tên danh mục đã tồn tại.");
        }
        private bool Save()
        {
            try { db.SaveChanges(); return true; }
            catch (DbUpdateException) { ModelState.AddModelError("", "Dữ liệu đã thay đổi hoặc tên bị trùng. Vui lòng kiểm tra lại."); return false; }
        }
        protected override void Dispose(bool disposing) { if (disposing) db.Dispose(); base.Dispose(disposing); }
    }
}
