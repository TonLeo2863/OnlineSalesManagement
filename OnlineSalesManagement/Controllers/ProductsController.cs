using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using OnlineSalesManagement;
using OnlineSalesManagement.Models;

namespace OnlineSalesManagement.Controllers
{
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
        public ActionResult Create([Bind(Include = "Id,Sku,Name,Description,CategoryId,Unit,CostPrice,SalePrice,QuantityInStock,ReorderLevel,ImageUrl,IsActive,CreatedDate,RowVersion")] Product product)
        {
            if (ModelState.IsValid)
            {
                db.Products.Add(product);
                db.SaveChanges();
                return RedirectToAction("Index");
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
        public ActionResult Edit([Bind(Include = "Id,Sku,Name,Description,CategoryId,Unit,CostPrice,SalePrice,QuantityInStock,ReorderLevel,ImageUrl,IsActive,CreatedDate,RowVersion")] Product product)
        {
            if (ModelState.IsValid)
            {
                db.Entry(product).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
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
            db.Products.Remove(product);
            db.SaveChanges();
            return RedirectToAction("Index");
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
