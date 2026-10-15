using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using OnlineSalesManagement;
using OnlineSalesManagement.Models;

namespace OnlineSalesManagement.Controllers
{
    [Authorize(Roles = "Admin,Kho")]
    public class ProductsController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        // GET: Products
        public ActionResult Index(string q, int? categoryId, string stock, string status)
        {
            IQueryable<Product> products = db.Products.Include(p => p.Category);

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                products = products.Where(p => p.Sku.Contains(q) || p.Name.Contains(q));
            }

            if (categoryId.HasValue)
            {
                products = products.Where(p => p.CategoryId == categoryId.Value);
            }

            // Lưu ý: KHÔNG dùng p.IsLowStock trong truy vấn (thuộc tính [NotMapped], EF không dịch được sang SQL).
            // Điều kiện được viết lại bằng cột thật, cùng quy tắc với IsLowStock (tồn <= mức tối thiểu).
            if (stock == "out")
            {
                products = products.Where(p => p.QuantityInStock == 0);
            }
            else if (stock == "low")
            {
                products = products.Where(p => p.QuantityInStock > 0 && p.QuantityInStock <= p.ReorderLevel);
            }
            else if (stock == "ok")
            {
                products = products.Where(p => p.QuantityInStock > p.ReorderLevel);
            }

            if (status == "active")
            {
                products = products.Where(p => p.IsActive);
            }
            else if (status == "inactive")
            {
                products = products.Where(p => !p.IsActive);
            }

            // Dùng khóa ViewBag.Categories (KHÔNG dùng ViewBag.CategoryId để khỏi trùng với Create/Edit).
            ViewBag.Categories = new SelectList(db.Categories.OrderBy(c => c.Name), "Id", "Name", categoryId);
            ViewBag.Q = q;
            ViewBag.SelectedCategoryId = categoryId;
            ViewBag.Stock = stock;
            ViewBag.Status = status;

            return View(products.OrderBy(p => p.Id).ToList());
        }


        // GET: Products/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Product product = db.Products.Find(id);
            if (product == null)
            {
                return HttpNotFound();
            }
            return View(product);
        }

        // GET: Products/Create
        public ActionResult Create()
        {
            ViewBag.CategoryId = new SelectList(db.Categories, "Id", "Name");
            return View();
        }

        // POST: Products/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "Sku,Name,Description,CategoryId,Unit,CostPrice,SalePrice,ReorderLevel,ImageUrl,IsActive")] Product product)
        {
            product.QuantityInStock = 0;
            product.CreatedDate = DateTime.Now;
            ValidateProduct(product);
            if (ModelState.IsValid)
            {
                db.Products.Add(product);
                try
                {
                    db.SaveChanges();
                    return RedirectToAction("Index");
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError("", "Không thể lưu sản phẩm. Kiểm tra SKU trùng hoặc danh mục đã thay đổi.");
                }
            }

            ViewBag.CategoryId = new SelectList(db.Categories, "Id", "Name", product.CategoryId);
            return View(product);
        }

        // GET: Products/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Product product = db.Products.Find(id);
            if (product == null)
            {
                return HttpNotFound();
            }
            ViewBag.CategoryId = new SelectList(db.Categories, "Id", "Name", product.CategoryId);
            return View(product);
        }

        // POST: Products/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "Id,Sku,Name,Description,CategoryId,Unit,CostPrice,SalePrice,ReorderLevel,ImageUrl,IsActive,RowVersion")] Product product)
        {
            var current = db.Products.Find(product.Id);
            if (current == null) return HttpNotFound();
            ValidateProduct(product);
            if (product.RowVersion == null || product.RowVersion.Length != 8)
                ModelState.AddModelError("", "Thiếu phiên bản dữ liệu. Vui lòng tải lại trang.");
            if (ModelState.IsValid)
            {
                current.Sku = product.Sku;
                current.Name = product.Name;
                current.Description = product.Description;
                current.CategoryId = product.CategoryId;
                current.Unit = product.Unit;
                current.CostPrice = product.CostPrice;
                current.SalePrice = product.SalePrice;
                current.ReorderLevel = product.ReorderLevel;
                current.ImageUrl = product.ImageUrl;
                current.IsActive = product.IsActive;
                db.Entry(current).Property(p => p.RowVersion).OriginalValue = product.RowVersion;
                try
                {
                    db.SaveChanges();
                    return RedirectToAction("Index");
                }
                catch (DbUpdateConcurrencyException)
                {
                    ModelState.AddModelError("", "Sản phẩm đã được thay đổi hoặc xóa. Tải lại trang trước khi lưu lại.");
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError("", "Không thể lưu sản phẩm. Kiểm tra SKU trùng hoặc danh mục đã thay đổi.");
                }
            }
            product.QuantityInStock = current.QuantityInStock;
            product.CreatedDate = current.CreatedDate;
            ViewBag.CategoryId = new SelectList(db.Categories, "Id", "Name", product.CategoryId);
            return View(product);
        }

        // GET: Products/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Product product = db.Products.Find(id);
            if (product == null)
            {
                return HttpNotFound();
            }
            return View(product);
        }

        // POST: Products/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Product product = db.Products.Find(id);
            if (product == null) return HttpNotFound();
            if (product.QuantityInStock != 0 || db.InventoryTransactions.Any(t => t.ProductId == id)
                || db.PurchaseReceiptDetails.Any(d => d.ProductId == id)
                || db.SalesOrderDetails.Any(d => d.ProductId == id))
            {
                ModelState.AddModelError("", "Sản phẩm có tồn kho hoặc chứng từ liên quan. Hãy ngừng kinh doanh thay vì xóa.");
                return View("Delete", product);
            }
            db.Products.Remove(product);
            try { db.SaveChanges(); }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "Dữ liệu đã thay đổi hoặc có liên kết mới. Không thể xóa sản phẩm.");
                return View("Delete", product);
            }
            return RedirectToAction("Index");
        }

        private void ValidateProduct(Product product)
        {
            product.Sku = (product.Sku ?? "").Trim();
            if (db.Products.Any(p => p.Sku == product.Sku && p.Id != product.Id))
                ModelState.AddModelError("Sku", "SKU đã tồn tại.");
            if (!db.Categories.Any(c => c.Id == product.CategoryId))
                ModelState.AddModelError("CategoryId", "Danh mục không tồn tại.");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
