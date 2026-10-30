using System;
using System.Linq;
using System.Data.Entity;
using OnlineSalesManagement.ViewModels;
namespace OnlineSalesManagement.Services
{
    public static class TransactionHistory
    {
        public static TransactionHistoryViewModel Sales(IQueryable<SalesOrder> query, int ownerId, int page, string title, bool showCustomer)
        {
            query = query.AsNoTracking(); var count = query.Count(); var pages = Math.Max(1, (count + 9) / 10); page = Math.Min(Math.Max(page, 1), pages);
            return new TransactionHistoryViewModel { OwnerId = ownerId, Title = title, Kind = "sales", PartnerTitle = showCustomer ? "Khách hàng" : "Nhân viên xử lý", PageParameter = "ordersPage", Page = page, PageCount = pages, TotalCount = count,
                ConfirmedAmount = query.Where(o => o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Completed).Sum(o => (decimal?)o.TotalAmount) ?? 0,
                Rows = query.OrderByDescending(o => o.OrderDate).ThenByDescending(o => o.Id).Skip((page - 1) * 10).Take(10)
                    .Select(o => new TransactionHistoryRow { Id = o.Id, Number = o.OrderNumber, Date = o.OrderDate, Status = (int)o.Status, Channel = (int)o.Channel,
                        Partner = showCustomer ? o.Customer.FullName : o.Employee.FullName, Amount = o.TotalAmount }).ToList() };
        }
        public static TransactionHistoryViewModel Receipts(IQueryable<PurchaseReceipt> query, int ownerId, int page, string title, bool showSupplier)
        {
            query = query.AsNoTracking(); var count = query.Count(); var pages = Math.Max(1, (count + 9) / 10); page = Math.Min(Math.Max(page, 1), pages);
            return new TransactionHistoryViewModel { OwnerId = ownerId, Title = title, Kind = "receipts", PartnerTitle = showSupplier ? "Nhà cung cấp" : "Nhân viên lập", PageParameter = "receiptsPage", Page = page, PageCount = pages, TotalCount = count,
                ConfirmedAmount = query.Where(o => o.Status == ReceiptStatus.Confirmed).Sum(o => (decimal?)o.TotalAmount) ?? 0,
                Rows = query.OrderByDescending(o => o.ReceiptDate).ThenByDescending(o => o.Id).Skip((page - 1) * 10).Take(10)
                    .Select(o => new TransactionHistoryRow { Id = o.Id, Number = o.ReceiptNumber, Date = o.ReceiptDate, Status = (int)o.Status,
                        Partner = showSupplier ? o.Supplier.Name : o.Employee.FullName, Amount = o.TotalAmount }).ToList() };
        }
        public static string Status(string kind, int value)
        {
            var values = kind == "sales" ? new[] { "Chờ xác nhận", "Đã xác nhận", "Hoàn tất", "Đã hủy" } : new[] { "Nháp", "Đã nhập kho", "Đã hủy" };
            return value >= 0 && value < values.Length ? values[value] : "Không xác định";
        }
    }
}
