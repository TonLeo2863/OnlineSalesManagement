using System;
using System.Data.Entity;
using System.Linq;
using System.Security.Principal;
using OnlineSalesManagement.Models;
using OnlineSalesManagement.ViewModels;

namespace OnlineSalesManagement.Services
{
    public static class DashboardService
    {
        public static bool IsStaff(IPrincipal user)
        {
            return user != null && user.Identity != null && user.Identity.IsAuthenticated
                && (user.IsInRole("Admin") || user.IsInRole("Kho") || user.IsInRole("BanHang"));
        }
        public static DashboardViewModel Load(ApplicationDbContext db, IPrincipal user)
        {
            if (!IsStaff(user)) throw new UnauthorizedAccessException();
            var model = new DashboardViewModel {
                CanViewSales = user.IsInRole("Admin") || user.IsInRole("BanHang"),
                CanViewInventory = user.IsInRole("Admin") || user.IsInRole("Kho")
            };
            if (model.CanViewSales)
            {
                var orders = db.SalesOrders.AsNoTracking();
                model.OrderCount = orders.Count();
                model.PendingCount = orders.Count(o => o.Status == OrderStatus.Pending);
                model.Revenue = orders.Where(o => o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Completed)
                    .Sum(o => (decimal?)o.TotalAmount) ?? 0m;
                model.RecentOrders = orders.OrderByDescending(o => o.OrderDate).ThenByDescending(o => o.Id).Take(6)
                    .Select(o => new DashboardOrderRow { Id = o.Id, Number = o.OrderNumber, Status = o.Status, Total = o.TotalAmount }).ToList();
            }
            if (model.CanViewInventory)
            {
                var products = db.Products.AsNoTracking();
                model.ProductCount = products.Count();
                model.StockQuantity = products.Sum(p => (long?)p.QuantityInStock) ?? 0L;
                var low = products.Where(p => p.IsActive && p.QuantityInStock <= p.ReorderLevel);
                model.LowStockCount = low.Count();
                model.LowStock = low.OrderBy(p => p.QuantityInStock).ThenBy(p => p.Id).Take(6)
                    .Select(p => new DashboardProductRow { Id = p.Id, Name = p.Name, Sku = p.Sku, Quantity = p.QuantityInStock, ReorderLevel = p.ReorderLevel }).ToList();
            }
            return model;
        }
    }
}
