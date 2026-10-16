using System.Collections.Generic;

namespace OnlineSalesManagement.ViewModels
{
    public class DashboardViewModel
    {
        public bool CanViewSales { get; set; }
        public bool CanViewInventory { get; set; }
        public int OrderCount { get; set; }
        public int PendingCount { get; set; }
        public decimal Revenue { get; set; }
        public int ProductCount { get; set; }
        public long StockQuantity { get; set; }
        public int LowStockCount { get; set; }
        public List<DashboardProductRow> LowStock { get; set; } = new List<DashboardProductRow>();
        public List<DashboardOrderRow> RecentOrders { get; set; } = new List<DashboardOrderRow>();
    }
    public class DashboardProductRow
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Sku { get; set; }
        public int Quantity { get; set; }
        public int ReorderLevel { get; set; }
    }
    public class DashboardOrderRow
    {
        public int Id { get; set; }
        public string Number { get; set; }
        public OrderStatus Status { get; set; }
        public decimal Total { get; set; }
    }
}
