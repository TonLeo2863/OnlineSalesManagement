
using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using OnlineSalesManagement.Models;
using OnlineSalesManagement.ViewModels;

namespace OnlineSalesManagement.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext db =
            new ApplicationDbContext();

        private bool PrepareDates(
            DateTime? fromDate,
            DateTime? toDate,
            out DateTime from,
            out DateTime toExclusive)
        {
            from = (fromDate ??
                new DateTime(DateTime.Today.Year,
                    DateTime.Today.Month, 1)).Date;

            DateTime to = (toDate ?? DateTime.Today).Date;

            if (from > to)
            {
                ModelState.AddModelError(
                    "", "Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
                toExclusive = from;
                return false;
            }

            if (to >= DateTime.MaxValue.Date)
            {
                ModelState.AddModelError(
                    "", "Ngày kết thúc không hợp lệ.");
                toExclusive = from;
                return false;
            }

            toExclusive = to.AddDays(1);
            return true;
        }

        // GET: /Reports
        // Doanh thu theo ngày đặt đơn, chưa phải tiền thực thu.
        public ActionResult Index(
            DateTime? fromDate, DateTime? toDate)
        {
            DateTime from, end;
            var model = new BusinessReportViewModel();

            if (!PrepareDates(fromDate, toDate, out from, out end))
                return View(model);

            model.FromDate = from;
            model.ToDate = end.AddDays(-1);

            var orders = db.SalesOrders
                .AsNoTracking()
                .Include(o => o.Customer)
                .Where(o =>
                    o.OrderDate >= from &&
                    o.OrderDate < end &&
                    (o.Status == OrderStatus.Confirmed ||
                     o.Status == OrderStatus.Completed))
                .OrderByDescending(o => o.OrderDate)
                .ToList();

            model.DocumentCount = orders.Count;
            model.TotalAmount = orders.Sum(o => o.TotalAmount);
            model.CounterRevenue = orders
                .Where(o => o.Channel == SalesChannel.Counter)
                .Sum(o => o.TotalAmount);
            model.OnlineRevenue = orders
                .Where(o => o.Channel == SalesChannel.Online)
                .Sum(o => o.TotalAmount);

            var ids = orders.Select(o => o.Id).ToList();

            var details = db.SalesOrderDetails
                .AsNoTracking()
                .Where(d => ids.Contains(d.SalesOrderId))
                .ToList();

            model.TotalQuantity = details.Sum(d => (long)d.Quantity);

            model.Rows = orders.Select(o => new BusinessReportRow
            {
                DocumentId = o.Id,
                DocumentNumber = o.OrderNumber,
                Date = o.OrderDate,
                PartnerName = o.Customer != null
                    ? o.Customer.FullName : "",
                Channel = o.Channel == SalesChannel.Online
                    ? "Trực tuyến" : "Tại quầy",
                Amount = o.TotalAmount
            }).ToList();

            return View(model);
        }

        // GET: /Reports/Purchases
        public ActionResult Purchases(
            DateTime? fromDate, DateTime? toDate)
        {
            DateTime from, end;
            var model = new BusinessReportViewModel();

            if (!PrepareDates(fromDate, toDate, out from, out end))
                return View(model);

            model.FromDate = from;
            model.ToDate = end.AddDays(-1);

            var details = db.PurchaseReceiptDetails
                .AsNoTracking()
                .Include(d => d.Product)
                .Include(d => d.PurchaseReceipt.Supplier)
                .Where(d =>
                    d.PurchaseReceipt.Status == ReceiptStatus.Confirmed &&
                    d.PurchaseReceipt.ReceiptDate >= from &&
                    d.PurchaseReceipt.ReceiptDate < end)
                .ToList();

            model.Rows = details.Select(d => new BusinessReportRow
            {
                DocumentId = d.PurchaseReceiptId,
                DocumentNumber = d.PurchaseReceipt.ReceiptNumber,
                Date = d.PurchaseReceipt.ReceiptDate,
                PartnerName = d.PurchaseReceipt.Supplier.Name,
                ProductSku = d.Product.Sku,
                ProductName = d.Product.Name,
                Quantity = d.Quantity,
                UnitPrice = d.UnitCost,
                Amount = d.Quantity * d.UnitCost
            }).OrderByDescending(x => x.Date).ToList();

            model.DocumentCount =
                model.Rows.Select(x => x.DocumentId).Distinct().Count();

            model.TotalQuantity =
                model.Rows.Sum(x => (long)x.Quantity);

            model.TotalAmount =
                model.Rows.Sum(x => x.Amount);

            return View(model);
        }

        // GET: /Reports/Sales
        public ActionResult Sales(
            DateTime? fromDate, DateTime? toDate)
        {
            DateTime from, end;
            var model = new BusinessReportViewModel();

            if (!PrepareDates(fromDate, toDate, out from, out end))
                return View(model);

            model.FromDate = from;
            model.ToDate = end.AddDays(-1);

            var details = db.SalesOrderDetails
                .AsNoTracking()
                .Include(d => d.Product)
                .Include(d => d.SalesOrder.Customer)
                .Where(d =>
                    (d.SalesOrder.Status == OrderStatus.Confirmed ||
                     d.SalesOrder.Status == OrderStatus.Completed) &&
                    d.SalesOrder.OrderDate >= from &&
                    d.SalesOrder.OrderDate < end)
                .ToList();

            model.Rows = details.Select(d => new BusinessReportRow
            {
                DocumentId = d.SalesOrderId,
                DocumentNumber = d.SalesOrder.OrderNumber,
                Date = d.SalesOrder.OrderDate,
                PartnerName = d.SalesOrder.Customer.FullName,
                ProductSku = d.Product.Sku,
                ProductName = d.Product.Name,
                Quantity = d.Quantity,
                UnitPrice = d.UnitPrice,
                Amount = d.Quantity * d.UnitPrice
            }).OrderByDescending(x => x.Date).ToList();

            model.DocumentCount =
                model.Rows.Select(x => x.DocumentId).Distinct().Count();

            model.TotalQuantity =
                model.Rows.Sum(x => (long)x.Quantity);

            // Giá trị hàng xuất trước giảm giá đơn hàng.
            model.TotalAmount =
                model.Rows.Sum(x => x.Amount);

            return View(model);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();

            base.Dispose(disposing);
        }
    }
}
