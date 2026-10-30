using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using OnlineSalesManagement.Services;
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
        public ActionResult Index(string code = null, string fullName = null, string email = null, string phone = null, bool? active = null,
            DateTime? fromDate = null, DateTime? toDate = null, int page = 1, string position = null)
        {
            PeopleDirectory.CheckDates(ModelState, fromDate, toDate);
            var contact = PeopleDirectory.ContactSearch(phone, ModelState);
            if ((code ?? "").Length > 20 || (fullName ?? "").Length > 150 || (email ?? "").Length > 256) ModelState.AddModelError("", "Điều kiện tìm kiếm quá dài.");
            ViewBag.PositionOptions = new SelectList(PeopleDirectory.Positions.Concat(db.Employees.AsNoTracking().Where(x => x.Position != null && x.Position != "").Select(x => x.Position).Distinct().ToList()).Distinct().OrderBy(x => x).ToList(), position);
            if (!ModelState.IsValid) { ViewBag.Total = 0; ViewBag.Page = 1; ViewBag.PageCount = 1; return View(new System.Collections.Generic.List<Employee>()); }
            var query = db.Employees.AsNoTracking().Include(x => x.User).AsQueryable();
            if (!string.IsNullOrWhiteSpace(code)) { code = code.Trim(); query = query.Where(x => x.Code.Contains(code)); }
            if (!string.IsNullOrWhiteSpace(fullName)) { fullName = fullName.Trim(); query = query.Where(x => x.FullName.Contains(fullName)); }
            if (!string.IsNullOrWhiteSpace(email)) { email = email.Trim(); query = query.Where(x => x.Email.Contains(email)); }
            if (contact != null) query = query.Where(x => x.PhoneNumber != null && x.PhoneNumber.Replace("+84", "0").Replace(" ", "").Replace("-", "").Replace(".", "").Replace("(", "").Replace(")", "").Contains(contact));
            if (active.HasValue) query = query.Where(x => x.IsActive == active.Value);
            if (fromDate.HasValue) { var start = fromDate.Value.Date; query = query.Where(x => x.HireDate >= start); }
            if (toDate.HasValue) { var end = toDate.Value.Date.AddDays(1); query = query.Where(x => x.HireDate < end); }
            if (!string.IsNullOrWhiteSpace(position)) query = query.Where(x => x.Position == position);
            var total = query.Count(); var pages = Math.Max(1, (total + 19) / 20); page = Math.Min(Math.Max(1, page), pages);
            ViewBag.Total = total; ViewBag.Page = page; ViewBag.PageCount = pages;
            return View(query.OrderBy(x => x.Code).ThenBy(x => x.Id).Skip((page - 1) * 20).Take(20).ToList());
        }

        // GET: Employees/Details/5
        public ActionResult Details(int? id, int ordersPage = 1, int receiptsPage = 1)
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
            ViewBag.OrderHistory = TransactionHistory.Sales(db.SalesOrders.Where(o => o.EmployeeId == employee.Id), employee.Id, ordersPage, "Đơn hàng đã xử lý", true);
            ViewBag.ReceiptHistory = TransactionHistory.Receipts(db.PurchaseReceipts.Where(o => o.EmployeeId == employee.Id), employee.Id, receiptsPage, "Phiếu nhập do nhân viên lập", true);
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
        public ActionResult Create([Bind(Include = "Code,FullName,Email,PhoneNumber,Address,Position,HireDate,IsActive,UserId")] Employee employee, string positionChoice = null)
        {
            try { employee.Position = PeopleDirectory.ResolvePosition(positionChoice, employee.Position); if (!string.IsNullOrEmpty(positionChoice)) ModelState.Remove("Position"); }
            catch (ArgumentException ex) { ModelState.AddModelError("Position", ex.Message); }
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
        public ActionResult Edit([Bind(Include = "Id,Code,FullName,Email,PhoneNumber,Address,Position,HireDate,IsActive,UserId")] Employee employee, string positionChoice = null)
        {
            try { employee.Position = PeopleDirectory.ResolvePosition(positionChoice, employee.Position); if (!string.IsNullOrEmpty(positionChoice)) ModelState.Remove("Position"); }
            catch (ArgumentException ex) { ModelState.AddModelError("Position", ex.Message); }
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
