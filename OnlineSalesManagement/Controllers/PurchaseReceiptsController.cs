
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using OnlineSalesManagement.Models;
using OnlineSalesManagement.Services;
using OnlineSalesManagement.ViewModels;

namespace OnlineSalesManagement.Controllers
{
    [Authorize(Roles = "Admin,Kho")]
    public class PurchaseReceiptsController : Controller
    {
        private readonly ApplicationDbContext db =
            new ApplicationDbContext();

        private readonly PurchaseReceiptService service =
            new PurchaseReceiptService();

        // GET: /PurchaseReceipts
        public ActionResult Index(string keyword = null, ReceiptStatus? status = null,
            int? supplierId = null, int? employeeId = null, bool? draftOnly = null, bool? confirmedOnly = null, string receiptNumber = null,
            DateTime? fromDate = null, DateTime? toDate = null,
            decimal? minTotal = null, decimal? maxTotal = null)
        {
            // Keep date parameters within SQL datetime's supported range before executing EF queries.
            var firstDate = new DateTime(1753, 1, 1);
            var lastDate = DateTime.MaxValue.Date.AddDays(-1);
            if ((fromDate.HasValue && (fromDate.Value.Date < firstDate || fromDate.Value.Date > lastDate)) ||
                (toDate.HasValue && (toDate.Value.Date < firstDate || toDate.Value.Date > lastDate)))
                ModelState.AddModelError("", "Ngày lọc phải nằm trong khoảng 01/01/1753 đến 30/12/9999.");
            if (fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date)
                ModelState.AddModelError("", "Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
            if (minTotal < 0 || maxTotal < 0 || (minTotal.HasValue && maxTotal.HasValue && minTotal > maxTotal))
                ModelState.AddModelError("", "Khoảng tổng tiền không hợp lệ. Vui lòng nhập số không âm, tối thiểu không lớn hơn tối đa.");
            if (minTotal > 9999999999999999.99m || maxTotal > 9999999999999999.99m)
                ModelState.AddModelError("", "Tổng tiền lọc vượt quá giới hạn hỗ trợ. Vui lòng nhập số nhỏ hơn 10.000.000.000.000.000.");
            if (status.HasValue && !Enum.IsDefined(typeof(ReceiptStatus), status.Value))
                ModelState.AddModelError("status", "Trạng thái không hợp lệ.");
            var query = db.PurchaseReceipts.AsNoTracking().Include(x => x.Supplier).Include(x => x.Employee).AsQueryable();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();
                query = query.Where(x => x.ReceiptNumber.Contains(keyword) || x.Supplier.Name.Contains(keyword) || x.Note.Contains(keyword) || x.Employee.FullName.Contains(keyword));
            }
            if (status.HasValue) query = query.Where(x => x.Status == status.Value);
            if (fromDate.HasValue) { var start = fromDate.Value.Date; query = query.Where(x => x.ReceiptDate >= start); }
            if (toDate.HasValue && toDate.Value.Date < DateTime.MaxValue.Date)
            { var end = toDate.Value.Date.AddDays(1); query = query.Where(x => x.ReceiptDate < end); }
            if (minTotal.HasValue) query = query.Where(x => x.TotalAmount >= minTotal.Value);
            if (maxTotal.HasValue) query = query.Where(x => x.TotalAmount <= maxTotal.Value);
            if (supplierId.HasValue) query = query.Where(x => x.SupplierId == supplierId.Value);
            if (employeeId.HasValue) query = query.Where(x => x.EmployeeId == employeeId.Value);
            if (draftOnly == true) query = query.Where(x => x.Status == ReceiptStatus.Draft);
            if (confirmedOnly == true) query = query.Where(x => x.Status == ReceiptStatus.Confirmed);
            if (draftOnly == true && confirmedOnly == true)
                ModelState.AddModelError("", "Chỉ chọn một tùy chọn: Draft hoặc Confirmed.");
            if (!string.IsNullOrWhiteSpace(receiptNumber)) { receiptNumber = receiptNumber.Trim(); query = query.Where(x => x.ReceiptNumber.Contains(receiptNumber)); }
            ViewBag.SupplierOptions = new SelectList(db.Suppliers.AsNoTracking().OrderBy(x => x.Name).ToList(), "Id", "Name", supplierId);
            ViewBag.EmployeeOptions = new SelectList(db.Employees.AsNoTracking().OrderBy(x => x.FullName).ToList(), "Id", "FullName", employeeId);
            // Invalid filters must never silently display an unfiltered list.
            if (!ModelState.IsValid) return View(new List<PurchaseReceipt>());
            return View(query.OrderByDescending(x => x.Id).ToList());
        }

        // GET: /PurchaseReceipts/Details/5
        public ActionResult Details(int? id)
        {
            if (!id.HasValue)
                return new HttpStatusCodeResult(
                    HttpStatusCode.BadRequest);

            var receipt = db.PurchaseReceipts
                .Include(r => r.Supplier)
                .Include(r => r.Employee)
                .Include(r => r.Details.Select(d => d.Product))
                .FirstOrDefault(r => r.Id == id.Value);

            if (receipt == null)
                return HttpNotFound();

            return View(receipt);
        }

        // GET: /PurchaseReceipts/Create
        public ActionResult Create()
        {
            var model = new PurchaseReceiptCreateViewModel
            {
                ReceiptNumber = "PN" +
                    DateTime.Now.ToString("yyyyMMddHHmmssfff"),

                Details = new List<PurchaseReceiptLineViewModel>
                {
                    new PurchaseReceiptLineViewModel()
                }
            };

            LoadDropdowns(model);
            return View(model);
        }

        // POST: /PurchaseReceipts/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(
            PurchaseReceiptCreateViewModel model)
        {
            if (model.Details == null)
                model.Details =
                    new List<PurchaseReceiptLineViewModel>();

            var validLines =
                new List<PurchaseReceiptLineInput>();

            var usedProductIds = new HashSet<int>();

            for (int i = 0; i < model.Details.Count; i++)
            {
                var line = model.Details[i];

                if (line == null)
                    continue;

                // Dòng hoàn toàn trống: bỏ qua.
                bool empty =
                    !line.ProductId.HasValue &&
                    !line.Quantity.HasValue &&
                    !line.UnitCost.HasValue;

                if (empty)
                    continue;

                if (!line.ProductId.HasValue ||
                    line.ProductId.Value <= 0)
                {
                    ModelState.AddModelError(
                        "", "Dòng " + (i + 1) +
                        ": Vui lòng chọn sản phẩm.");
                    continue;
                }

                if (!line.Quantity.HasValue ||
                    line.Quantity.Value <= 0)
                {
                    ModelState.AddModelError(
                        "", "Dòng " + (i + 1) +
                        ": Số lượng phải lớn hơn 0.");
                    continue;
                }

                if (!line.UnitCost.HasValue ||
                    line.UnitCost.Value < 0 ||
                    decimal.Round(line.UnitCost.Value, 2)
                        != line.UnitCost.Value)
                {
                    ModelState.AddModelError(
                        "", "Dòng " + (i + 1) +
                        ": Giá nhập phải hợp lệ và tối đa 2 chữ số thập phân.");
                    continue;
                }

                if (!usedProductIds.Add(line.ProductId.Value))
                {
                    ModelState.AddModelError(
                        "", "Không được chọn trùng sản phẩm.");
                    continue;
                }

                validLines.Add(new PurchaseReceiptLineInput
                {
                    ProductId = line.ProductId.Value,
                    Quantity = line.Quantity.Value,
                    UnitCost = line.UnitCost.Value
                });
            }

            if (validLines.Count == 0)
                ModelState.AddModelError(
                    "", "Phiếu nhập cần ít nhất một sản phẩm hợp lệ.");

            if (model.Details.Count > 8)
                ModelState.AddModelError(
                    "", "Mỗi phiếu chỉ nhập tối đa 8 dòng.");

            if (!ModelState.IsValid)
            {
                LoadDropdowns(model);
                return View(model);
            }

            try
            {
                var input = new PurchaseReceiptDraftInput
                {
                    ReceiptNumber = model.ReceiptNumber,
                    SupplierId = model.SupplierId.Value,
                    EmployeeId = model.EmployeeId.Value,
                    Note = model.Note,
                    Details = validLines
                };

                int id = service.CreateDraft(input);

                TempData["Success"] =
                    "Đã tạo phiếu nhập nháp thành công.";

                return RedirectToAction(
                    "Details", new { id = id });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                LoadDropdowns(model);
                return View(model);
            }
        }

        // POST: /PurchaseReceipts/Confirm/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Confirm(int id)
        {
            try
            {
                service.ConfirmReceipt(
                    id, User.Identity.Name);

                TempData["Success"] =
                    "Xác nhận nhập kho thành công.";

                return RedirectToAction(
                    "Details", new { id = id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;

                return RedirectToAction(
                    "Details", new { id = id });
            }
        }

        // POST: /PurchaseReceipts/Cancel/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Cancel(int id)
        {
            try
            {
                service.CancelReceipt(
                    id, User.Identity.Name);

                TempData["Success"] =
                    "Đã hủy phiếu nhập.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(
                "Details", new { id = id });
        }

        private void LoadDropdowns(
            PurchaseReceiptCreateViewModel model)
        {
            model.Suppliers = db.Suppliers
                .OrderBy(s => s.Name)
                .ToList()
                .Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = s.Name
                }).ToList();

            model.Employees = db.Employees
                .OrderBy(e => e.FullName)
                .ToList()
                .Select(e => new SelectListItem
                {
                    Value = e.Id.ToString(),
                    Text = e.FullName
                }).ToList();

            model.Products = db.Products
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToList()
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = p.Name + " (" + p.Sku + ")"
                }).ToList();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();

            base.Dispose(disposing);
        }
    }
}
