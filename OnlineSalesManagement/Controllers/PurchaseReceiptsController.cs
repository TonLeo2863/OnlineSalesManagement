
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
    [Authorize]
    public class PurchaseReceiptsController : Controller
    {
        private readonly ApplicationDbContext db =
            new ApplicationDbContext();

        private readonly PurchaseReceiptService service =
            new PurchaseReceiptService();

        // GET: /PurchaseReceipts
        public ActionResult Index()
        {
            var receipts = db.PurchaseReceipts
                .Include(r => r.Supplier)
                .Include(r => r.Employee)
                .OrderByDescending(r => r.Id)
                .ToList();

            return View(receipts);
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
