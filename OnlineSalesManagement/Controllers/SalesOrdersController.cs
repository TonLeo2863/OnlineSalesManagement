
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
    [Authorize(Roles = "Admin,BanHang")]
    public class SalesOrdersController : Controller
    {
        private readonly ApplicationDbContext db =
            new ApplicationDbContext();

        private readonly SalesOrderService service =
            new SalesOrderService();

        // GET: /SalesOrders
        public ActionResult Index(string keyword = null, OrderStatus? status = null,
            int? customerId = null, SalesChannel? channel = null,
            DateTime? fromDate = null, DateTime? toDate = null,
            decimal? minTotal = null, decimal? maxTotal = null, string orderNumber = null)
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
            if (status.HasValue && !Enum.IsDefined(typeof(OrderStatus), status.Value))
                ModelState.AddModelError("status", "Trạng thái không hợp lệ.");
            var query = db.SalesOrders.AsNoTracking().Include(x => x.Customer).Include(x => x.Employee).AsQueryable();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim();
                query = query.Where(x => x.OrderNumber.Contains(keyword) || x.Customer.FullName.Contains(keyword) || x.Note.Contains(keyword));
            }
            if (status.HasValue) query = query.Where(x => x.Status == status.Value);
            if (fromDate.HasValue) { var start = fromDate.Value.Date; query = query.Where(x => x.OrderDate >= start); }
            if (toDate.HasValue && toDate.Value.Date < DateTime.MaxValue.Date)
            { var end = toDate.Value.Date.AddDays(1); query = query.Where(x => x.OrderDate < end); }
            if (minTotal.HasValue) query = query.Where(x => x.TotalAmount >= minTotal.Value);
            if (maxTotal.HasValue) query = query.Where(x => x.TotalAmount <= maxTotal.Value);
            if (!string.IsNullOrWhiteSpace(orderNumber))
            {
                orderNumber = orderNumber.Trim();
                query = query.Where(x => x.OrderNumber.Contains(orderNumber));
            }
            if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId.Value);
            if (channel.HasValue && !Enum.IsDefined(typeof(SalesChannel), channel.Value))
                ModelState.AddModelError("channel", "Kênh bán không hợp lệ.");
            if (channel.HasValue) query = query.Where(x => x.Channel == channel.Value);
            ViewBag.CustomerOptions = new SelectList(db.Customers.AsNoTracking().OrderBy(x => x.FullName).ToList(), "Id", "FullName", customerId);
            // Invalid filters must never silently display an unfiltered list.
            if (!ModelState.IsValid) return View(new List<SalesOrder>());
            return View(query.OrderByDescending(x => x.Id).ToList());
        }

        // GET: /SalesOrders/Details/5
        public ActionResult Details(int? id)
        {
            if (!id.HasValue)
                return new HttpStatusCodeResult(
                    HttpStatusCode.BadRequest);

            var order = db.SalesOrders
                .Include(o => o.Customer)
                .Include(o => o.Employee)
                .Include(o => o.Details.Select(d => d.Product))
                .FirstOrDefault(o => o.Id == id.Value);

            if (order == null)
                return HttpNotFound();

            return View(order);
        }

        // GET: /SalesOrders/Create
        public ActionResult Create()
        {
            var model = new SalesOrderCreateViewModel
            {
                OrderNumber = "DH" +
                    DateTime.Now.ToString("yyyyMMddHHmmssfff"),

                Channel = SalesChannel.Counter,
                PaymentMethod = PaymentMethod.Cash,

                Details = new List<SalesOrderLineViewModel>
                {
                    new SalesOrderLineViewModel()
                }
            };

            LoadDropdowns(model);
            return View(model);
        }

        // POST: /SalesOrders/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(
            SalesOrderCreateViewModel model)
        {
            if (model.Details == null)
                model.Details =
                    new List<SalesOrderLineViewModel>();

            var lines = new List<SalesOrderLineInput>();
            var usedIds = new HashSet<int>();

            if (model.Details.Count > 8)
                ModelState.AddModelError(
                    "", "Tối đa 8 dòng sản phẩm.");

            for (int i = 0; i < model.Details.Count; i++)
            {
                var item = model.Details[i];

                if (item == null)
                    continue;

                if (!item.ProductId.HasValue &&
                    !item.Quantity.HasValue)
                    continue;

                if (!item.ProductId.HasValue ||
                    item.ProductId.Value <= 0 ||
                    !item.Quantity.HasValue ||
                    item.Quantity.Value <= 0)
                {
                    ModelState.AddModelError(
                        "", "Dòng " + (i + 1) +
                        ": sản phẩm hoặc số lượng không hợp lệ.");
                    continue;
                }

                if (!usedIds.Add(item.ProductId.Value))
                {
                    ModelState.AddModelError(
                        "", "Không được chọn trùng sản phẩm.");
                    continue;
                }

                lines.Add(new SalesOrderLineInput
                {
                    ProductId = item.ProductId.Value,
                    Quantity = item.Quantity.Value
                });
            }

            if (lines.Count == 0)
                ModelState.AddModelError(
                    "", "Đơn hàng cần ít nhất một sản phẩm.");

            if (model.Channel == SalesChannel.Online &&
                string.IsNullOrWhiteSpace(model.ShippingAddress))
                ModelState.AddModelError(
                    "ShippingAddress",
                    "Đơn trực tuyến cần địa chỉ giao hàng.");

            if (!ModelState.IsValid)
            {
                LoadDropdowns(model);
                return View(model);
            }

            try
            {
                var input = new SalesOrderPendingInput
                {
                    OrderNumber = model.OrderNumber,
                    CustomerId = model.CustomerId.Value,
                    EmployeeId = model.EmployeeId,
                    Channel = model.Channel,
                    PaymentMethod = model.PaymentMethod,
                    ShippingAddress = model.ShippingAddress,
                    Note = model.Note,
                    DiscountAmount = model.DiscountAmount,
                    Details = lines
                };

                int id = service.CreatePending(input);

                TempData["Success"] =
                    "Tạo đơn hàng chờ xác nhận thành công.";

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

        // POST: /SalesOrders/Confirm/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Confirm(int id)
        {
            try
            {
                service.ConfirmOrder(
                    id, User.Identity.Name);

                TempData["Success"] =
                    "Xuất kho thành công. Tồn kho đã được cập nhật.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(
                "Details", new { id = id });
        }

        // POST: /SalesOrders/Cancel/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Cancel(int id)
        {
            try
            {
                service.CancelOrder(
                    id, User.Identity.Name);

                TempData["Success"] = "Đã hủy đơn hàng.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(
                "Details", new { id = id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Complete(int id)
        {
            try
            {
                service.CompleteOrder(id);
                TempData["Success"] = "Đã hoàn tất đơn hàng. Trạng thái thanh toán được giữ nguyên.";
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction("Details", new { id = id });
        }

        private void LoadDropdowns(
            SalesOrderCreateViewModel model)
        {
            model.Customers = db.Customers
                .Where(c => c.IsActive)
                .OrderBy(c => c.FullName)
                .ToList()
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.FullName
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
                    Text = p.Name + " (" + p.Sku +
                           ") - Tồn: " + p.QuantityInStock +
                           " - Giá: " + p.SalePrice.ToString("N0")
                }).ToList();
        }


        // GET: /SalesOrders/Invoice/5
        [HttpGet]
        public ActionResult Invoice(int? id)
        {
            if (!id.HasValue)
                return new HttpStatusCodeResult(
                    HttpStatusCode.BadRequest);

            var order = db.SalesOrders
                .Include(o => o.Customer)
                .Include(o => o.Employee)
                .Include(o => o.Details.Select(d => d.Product))
                .FirstOrDefault(o => o.Id == id.Value);

            if (order == null)
                return HttpNotFound();

            // Chỉ in hóa đơn khi đơn đã xác nhận xuất kho
            // hoặc đã hoàn tất.
            if (order.Status != OrderStatus.Confirmed &&
                order.Status != OrderStatus.Completed)
            {
                TempData["Error"] =
                    "Chỉ được in hóa đơn của đơn hàng đã xác nhận hoặc hoàn tất.";

                return RedirectToAction(
                    "Details", new { id = order.Id });
            }

            return View(order);
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();

            base.Dispose(disposing);
        }
    }
}
