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
    [Authorize(Roles = "Admin")]
    public class EmployeesController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        // GET: Employees
        public ActionResult Index()
        {
            var employees = db.Employees.Include(e => e.User);
            return View(employees.ToList());
        }

        // GET: Employees/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Employee employee = db.Employees.Find(id);
            if (employee == null)
            {
                return HttpNotFound();
            }
            return View(employee);
        }

        // GET: Employees/Create
        public ActionResult Create()
        {
            ViewBag.UserId = new SelectList(db.Users, "Id", "Email");
            return View();
        }

        // POST: Employees/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "Code,FullName,Email,PhoneNumber,Address,Position,HireDate,IsActive,UserId")] Employee employee)
        {
            ValidateRecord(employee);
            if (ModelState.IsValid)
            {
                db.Employees.Add(employee);
                if (SaveRecord()) return RedirectToAction("Index");
            }

            ViewBag.UserId = new SelectList(db.Users, "Id", "Email", employee.UserId);
            return View(employee);
        }

        // GET: Employees/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Employee employee = db.Employees.Find(id);
            if (employee == null)
            {
                return HttpNotFound();
            }
            ViewBag.UserId = new SelectList(db.Users, "Id", "Email", employee.UserId);
            return View(employee);
        }

        // POST: Employees/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "Id,Code,FullName,Email,PhoneNumber,Address,Position,HireDate,IsActive,UserId")] Employee employee)
        {
            ValidateRecord(employee);
            if (ModelState.IsValid)
            {
                var current = db.Employees.Find(employee.Id);
                if (current == null) return HttpNotFound();
                db.Entry(current).CurrentValues.SetValues(employee);
                if (SaveRecord()) return RedirectToAction("Index");
            }
            ViewBag.UserId = new SelectList(db.Users, "Id", "Email", employee.UserId);
            return View(employee);
        }

        // GET: Employees/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Employee employee = db.Employees.Find(id);
            if (employee == null)
            {
                return HttpNotFound();
            }
            return View(employee);
        }

        // POST: Employees/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Employee employee = db.Employees.Find(id);
            if (employee == null) return HttpNotFound();
            if (db.PurchaseReceipts.Any(r => r.EmployeeId == id) || db.SalesOrders.Any(o => o.EmployeeId == id))
            {
                ModelState.AddModelError("", "Dữ liệu có chứng từ liên quan. Hãy ngừng hoạt động thay vì xóa.");
                return View("Delete", employee);
            }
            db.Employees.Remove(employee);
            if (!SaveRecord()) return View("Delete", employee);
            return RedirectToAction("Index");
        }

        private void ValidateRecord(Employee model)
        {
            model.Code = (model.Code ?? "").Trim();
            if (db.Employees.Any(x => x.Code == model.Code && x.Id != model.Id))
                ModelState.AddModelError("Code", "Mã đã tồn tại.");
            if (!string.IsNullOrEmpty(model.UserId) && !db.Users.Any(u => u.Id == model.UserId))
                ModelState.AddModelError("UserId", "Tài khoản không tồn tại.");
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
