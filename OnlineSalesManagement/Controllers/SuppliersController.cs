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
    public class SuppliersController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        // GET: Suppliers
        public ActionResult Index()
        {
            return View(db.Suppliers.ToList());
        }

        // GET: Suppliers/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Supplier supplier = db.Suppliers.Find(id);
            if (supplier == null)
            {
                return HttpNotFound();
            }
            return View(supplier);
        }

        // GET: Suppliers/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Suppliers/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "Code,Name,ContactPerson,Email,PhoneNumber,Address,TaxCode,IsActive")] Supplier supplier)
        {
            ValidateRecord(supplier);
            if (ModelState.IsValid)
            {
                db.Suppliers.Add(supplier);
                if (SaveRecord()) return RedirectToAction("Index");
            }

            return View(supplier);
        }

        // GET: Suppliers/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Supplier supplier = db.Suppliers.Find(id);
            if (supplier == null)
            {
                return HttpNotFound();
            }
            return View(supplier);
        }

        // POST: Suppliers/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "Id,Code,Name,ContactPerson,Email,PhoneNumber,Address,TaxCode,IsActive")] Supplier supplier)
        {
            ValidateRecord(supplier);
            if (ModelState.IsValid)
            {
                var current = db.Suppliers.Find(supplier.Id);
                if (current == null) return HttpNotFound();
                db.Entry(current).CurrentValues.SetValues(supplier);
                if (SaveRecord()) return RedirectToAction("Index");
            }
            return View(supplier);
        }

        // GET: Suppliers/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Supplier supplier = db.Suppliers.Find(id);
            if (supplier == null)
            {
                return HttpNotFound();
            }
            return View(supplier);
        }

        // POST: Suppliers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Supplier supplier = db.Suppliers.Find(id);
            if (supplier == null) return HttpNotFound();
            if (db.PurchaseReceipts.Any(r => r.SupplierId == id))
            {
                ModelState.AddModelError("", "Dữ liệu có chứng từ liên quan. Hãy ngừng hoạt động thay vì xóa.");
                return View("Delete", supplier);
            }
            db.Suppliers.Remove(supplier);
            if (!SaveRecord()) return View("Delete", supplier);
            return RedirectToAction("Index");
        }

        private void ValidateRecord(Supplier model)
        {
            model.Code = (model.Code ?? "").Trim();
            if (db.Suppliers.Any(x => x.Code == model.Code && x.Id != model.Id))
                ModelState.AddModelError("Code", "Mã đã tồn tại.");
        }
        private bool SaveRecord()
        {
            try { db.SaveChanges(); return true; }
            catch (DbUpdateException) { ModelState.AddModelError("", "Không thể lưu: mã trùng, liên kết hoặc dữ liệu vừa thay đổi. Vui lòng tải lại và kiểm tra."); return false; }
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
