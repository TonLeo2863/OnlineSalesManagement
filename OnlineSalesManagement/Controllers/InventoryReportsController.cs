
using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using OnlineSalesManagement.Models;

namespace OnlineSalesManagement.Controllers
{
    [Authorize(Roles = "Admin,Kho")]
    public class InventoryReportsController : Controller
    {
        private readonly ApplicationDbContext db =
            new ApplicationDbContext();

        // GET: /InventoryReports
        [HttpGet]
        public ActionResult Index(string search, bool lowStockOnly = false)
        {
            var query = db.Products
                .Include(p => p.Category)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string keyword = search.Trim();

                query = query.Where(p =>
                    p.Name.Contains(keyword) ||
                    p.Sku.Contains(keyword));
            }

            if (lowStockOnly)
            {
                query = query.Where(
                    p => p.QuantityInStock <= p.ReorderLevel);
            }

            var products = query
                .OrderBy(p => p.Name)
                .ToList();

            ViewBag.Search = search;
            ViewBag.LowStockOnly = lowStockOnly;

            ViewBag.TotalProducts = products.Count;

            ViewBag.TotalQuantity = products.Sum(
                p => (long)p.QuantityInStock);

            ViewBag.TotalStockValue = products.Sum(
                p => p.QuantityInStock * p.CostPrice);

            ViewBag.LowStockCount = products.Count(
                p => p.QuantityInStock <= p.ReorderLevel);

            var ledger = db.InventoryTransactions.AsNoTracking()
                .GroupBy(t => t.ProductId)
                .Select(g => new { ProductId = g.Key, Quantity = g.Sum(t => (long)t.QuantityChange) })
                .ToDictionary(g => g.ProductId, g => g.Quantity);
            ViewBag.ReconciliationMismatchCount = products.Count(p =>
                p.QuantityInStock != (ledger.ContainsKey(p.Id) ? ledger[p.Id] : 0L));

            ViewBag.RecentTransactions = db.InventoryTransactions
                .Include(t => t.Product)
                .AsNoTracking()
                .OrderByDescending(t => t.TransactionDate)
                .ThenByDescending(t => t.Id)
                .Take(20)
                .ToList();

            return View(products);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();

            base.Dispose(disposing);
        }
    }
}
