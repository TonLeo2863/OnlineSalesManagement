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
    [Authorize(Roles = "Admin,BanHang")]
    public class CustomersController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        // GET: Customers
        public ActionResult Index(string code = null, string fullName = null, string email = null, string phone = null, bool? active = null,
            DateTime? fromDate = null, DateTime? toDate = null, int page = 1)
        {
            PeopleDirectory.CheckDates(ModelState, fromDate, toDate);
            var contact = PeopleDirectory.ContactSearch(phone, ModelState);
            if ((code ?? "").Length > 20 || (fullName ?? "").Length > 150 || (email ?? "").Length > 256) ModelState.AddModelError("", "Điều kiện tìm kiếm quá dài.");

            if (!ModelState.IsValid) { ViewBag.Total = 0; ViewBag.Page = 1; ViewBag.PageCount = 1; return View(new System.Collections.Generic.List<Customer>()); }
            var query = db.Customers.AsNoTracking().Include(x => x.User).AsQueryable();
            if (!string.IsNullOrWhiteSpace(code)) { code = code.Trim(); query = query.Where(x => x.Code.Contains(code)); }
            if (!string.IsNullOrWhiteSpace(fullName)) { fullName = fullName.Trim(); query = query.Where(x => x.FullName.Contains(fullName)); }
            if (!string.IsNullOrWhiteSpace(email)) { email = email.Trim(); query = query.Where(x => x.Email.Contains(email)); }
            if (contact != null) query = query.Where(x => x.PhoneNumber != null && x.PhoneNumber.Replace("+84", "0").Replace(" ", "").Replace("-", "").Replace(".", "").Replace("(", "").Replace(")", "").Contains(contact));
            if (active.HasValue) query = query.Where(x => x.IsActive == active.Value);
            if (fromDate.HasValue) { var start = fromDate.Value.Date; query = query.Where(x => x.CreatedDate >= start); }
            if (toDate.HasValue) { var end = toDate.Value.Date.AddDays(1); query = query.Where(x => x.CreatedDate < end); }

            var total = query.Count(); var pages = Math.Max(1, (total + 19) / 20); page = Math.Min(Math.Max(1, page), pages);
            ViewBag.Total = total; ViewBag.Page = page; ViewBag.PageCount = pages;
            return View(query.OrderBy(x => x.Code).ThenBy(x => x.Id).Skip((page - 1) * 20).Take(20).ToList());
        }

        // GET: Customers/Details/5
        public ActionResult Details(int? id, int ordersPage = 1)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Customer customer = db.Customers.Find(id);
            if (customer == null)
            {
                return HttpNotFound();
            }
            ViewBag.OrderHistory = TransactionHistory.Sales(db.SalesOrders.Where(o => o.CustomerId == customer.Id), customer.Id, ordersPage, "Lịch sử mua hàng", false);
            return View(customer);
        }

        // GET: Customers/Create
        public ActionResult Create()
        {
            ViewBag.UserId = new SelectList(db.Users, "Id", "Email");
            return View();
        }

        // POST: Customers/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "Code,FullName,Email,PhoneNumber,Address,IsActive")] Customer customer)
        {
            ValidateRecord(customer);
            if (ModelState.IsValid)
            {
                db.Customers.Add(customer);
                if (SaveRecord()) return RedirectToAction("Index");
            }

            ViewBag.UserId = new SelectList(db.Users, "Id", "Email", customer.UserId);
            return View(customer);
        }

        // GET: Customers/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Customer customer = db.Customers.Find(id);
            if (customer == null)
            {
                return HttpNotFound();
            }
            ViewBag.UserId = new SelectList(db.Users, "Id", "Email", customer.UserId);
            return View(customer);
        }

        // POST: Customers/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "Id,Code,FullName,Email,PhoneNumber,Address,IsActive")] Customer customer)
        {
            ValidateRecord(customer);
            if (ModelState.IsValid)
            {
                var current = db.Customers.Find(customer.Id);
                if (current == null) return HttpNotFound();
                customer.CreatedDate = current.CreatedDate;
                customer.UserId = current.UserId;
                db.Entry(current).CurrentValues.SetValues(customer);
                if (SaveRecord()) return RedirectToAction("Index");
            }
            ViewBag.UserId = new SelectList(db.Users, "Id", "Email", customer.UserId);
            return View(customer);
        }

        // GET: Customers/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Customer customer = db.Customers.Find(id);
            if (customer == null)
            {
                return HttpNotFound();
            }
            return View(customer);
        }

        // POST: Customers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Customer customer = db.Customers.Find(id);
            if (customer == null) return HttpNotFound();
            if (db.SalesOrders.Any(o => o.CustomerId == id))
            {
                ModelState.AddModelError("", "Dữ liệu có chứng từ liên quan. Hãy ngừng hoạt động thay vì xóa.");
                return View("Delete", customer);
            }
            db.Customers.Remove(customer);
            if (!SaveRecord()) return View("Delete", customer);
            return RedirectToAction("Index");
        }

        private void ValidateRecord(Customer model)
        {
            model.Code = (model.Code ?? "").Trim();
            if (db.Customers.Any(x => x.Code == model.Code && x.Id != model.Id))
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
