using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using OnlineSalesManagement;
using OnlineSalesManagement.Models;

namespace OnlineSalesManagement.Services
{
    // Don gia ban lay tu Products.SalePrice o server; khong tin gia client gui len.
    public sealed class SalesOrderLineInput
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public sealed class SalesOrderPendingInput
    {
        public string OrderNumber { get; set; }
        public int CustomerId { get; set; }
        public int? EmployeeId { get; set; }
        public SalesChannel Channel { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public string ShippingAddress { get; set; }
        public string Note { get; set; }
        public decimal DiscountAmount { get; set; }
        public IList<SalesOrderLineInput> Details { get; set; }
    }

    /// <summary>
    /// Pending -> Confirmed: tru ton mot lan; Confirmed -> Completed: khong dong ton.
    /// Pending -> Cancelled: khong dong ton; Confirmed -> Cancelled: hoan ton mot lan.
    /// Completed khong huy truc tiep: can quy trinh tra hang rieng.
    /// </summary>
    public sealed class SalesOrderService
    {
        private const decimal MaxSqlMoney18_2 = 9999999999999999.99m;

        public int CreatePending(SalesOrderPendingInput input)
        {
            ValidateInput(input);
            using (var db = new ApplicationDbContext())
            using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                ValidateReferences(db, input);
                var order = new SalesOrder
                {
                    OrderNumber = input.OrderNumber.Trim(),
                    CustomerId = input.CustomerId,
                    EmployeeId = input.EmployeeId,
                    Channel = input.Channel,
                    PaymentMethod = input.PaymentMethod,
                    PaymentStatus = PaymentStatus.Unpaid,
                    ShippingAddress = input.ShippingAddress,
                    Note = input.Note,
                    DiscountAmount = input.DiscountAmount,
                    Status = OrderStatus.Pending
                };
                foreach (var line in input.Details)
                {
                    var product = LoadSaleProduct(db, line.ProductId);
                    order.Details.Add(new SalesOrderDetail
                    {
                        ProductId = line.ProductId,
                        Quantity = line.Quantity,
                        UnitPrice = product.SalePrice
                    });
                }
                order.RecalculateTotals();
                EnsureSqlAmount(order.SubTotal);
                EnsureSqlAmount(order.TotalAmount);
                db.SalesOrders.Add(order);
                db.SaveChanges();
                transaction.Commit();
                return order.Id;
            }
        }

        public void UpdatePending(int orderId, SalesOrderPendingInput input)
        {
            ValidateInput(input);
            try
            {
                using (var db = new ApplicationDbContext())
                using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
                {
                    var order = LoadLockedOrder(db, orderId);
                    if (order.Status != OrderStatus.Pending)
                        throw new InvalidOperationException("Chi duoc sua don hang Pending.");
                    if (db.InventoryTransactions.Any(t => t.SalesOrderId == orderId))
                        throw new InvalidOperationException("Don Pending da co giao dich kho bat thuong.");
                    ValidateReferences(db, input);
                    db.Entry(order).Collection(o => o.Details).Load();
                    var submitted = input.Details.ToDictionary(d => d.ProductId);
                    foreach (var existing in order.Details.ToList())
                    {
                        SalesOrderLineInput newLine;
                        if (!submitted.TryGetValue(existing.ProductId, out newLine))
                        {
                            order.Details.Remove(existing);
                            db.SalesOrderDetails.Remove(existing);
                        }
                        else
                        {
                            existing.Quantity = newLine.Quantity;
                            // Don gia cua dong da co la gia tai thoi diem tao don: giu nguyen.
                        }
                    }
                    var existingProductIds = new HashSet<int>(order.Details.Select(d => d.ProductId));
                    foreach (var line in input.Details.Where(d => !existingProductIds.Contains(d.ProductId)))
                    {
                        var product = LoadSaleProduct(db, line.ProductId);
                        order.Details.Add(new SalesOrderDetail
                        {
                            SalesOrderId = orderId,
                            ProductId = line.ProductId,
                            Quantity = line.Quantity,
                            UnitPrice = product.SalePrice
                        });
                    }
                    order.OrderNumber = input.OrderNumber.Trim();
                    order.CustomerId = input.CustomerId;
                    order.EmployeeId = input.EmployeeId;
                    order.Channel = input.Channel;
                    order.PaymentMethod = input.PaymentMethod;
                    order.ShippingAddress = input.ShippingAddress;
                    order.Note = input.Note;
                    order.DiscountAmount = input.DiscountAmount;
                    order.RecalculateTotals();
                    EnsureSqlAmount(order.SubTotal);
                    EnsureSqlAmount(order.TotalAmount);
                    db.SaveChanges();
                    transaction.Commit();
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw ConcurrencyException(ex);
            }
        }

        public void ConfirmOrder(int orderId, string createdBy)
        {
            ValidateActor(createdBy);
            try
            {
                using (var db = new ApplicationDbContext())
                using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
                {
                    var order = LoadLockedOrder(db, orderId);
                    if (order.Status != OrderStatus.Pending)
                        throw new InvalidOperationException("Chi duoc xac nhan don Pending. Don da xu ly khong duoc xuat trung.");
                    if (db.InventoryTransactions.Any(t => t.SalesOrderId == orderId))
                        throw new InvalidOperationException("Don da co giao dich kho; dung xu ly de tranh xuat trung.");
                    db.Entry(order).Collection(o => o.Details).Load();
                    var details = order.Details.OrderBy(d => d.ProductId).ToList();
                    ValidatePersistedDetails(details);
                    order.RecalculateTotals();
                    EnsureSqlAmount(order.SubTotal);
                    EnsureSqlAmount(order.TotalAmount);
                    var transactionDate = DateTime.Now;
                    foreach (var detail in details)
                    {
                        var product = LoadLockedProduct(db, detail.ProductId);
                        if (!product.IsActive)
                            throw new InvalidOperationException("San pham da ngung kinh doanh: " + product.Name);
                        product.DecreaseStock(detail.Quantity);
                        db.InventoryTransactions.Add(new InventoryTransaction
                        {
                            ProductId = product.Id,
                            SalesOrderId = order.Id,
                            TransactionDate = transactionDate,
                            TransactionType = InventoryTransactionType.SalesOrder,
                            QuantityChange = -detail.Quantity,
                            StockAfter = product.QuantityInStock,
                            UnitPrice = detail.UnitPrice,
                            CreatedBy = createdBy
                        });
                    }
                    order.Status = OrderStatus.Confirmed;
                    db.SaveChanges();
                    transaction.Commit();
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw ConcurrencyException(ex);
            }
        }

        public void CompleteOrder(int orderId)
        {
            try
            {
                using (var db = new ApplicationDbContext())
                using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
                {
                    var order = LoadLockedOrder(db, orderId);
                    if (order.Status != OrderStatus.Confirmed)
                        throw new InvalidOperationException("Chi duoc hoan tat don Confirmed.");
                    if (!db.InventoryTransactions.Any(t => t.SalesOrderId == orderId &&
                          t.TransactionType == InventoryTransactionType.SalesOrder))
                        throw new InvalidOperationException("Khong co lich su xuat kho cua don; dung xu ly.");
                    order.Status = OrderStatus.Completed;
                    db.SaveChanges();
                    transaction.Commit();
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw ConcurrencyException(ex);
            }
        }

        public void CancelOrder(int orderId, string createdBy)
        {
            ValidateActor(createdBy);
            try
            {
                using (var db = new ApplicationDbContext())
                using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
                {
                    var order = LoadLockedOrder(db, orderId);
                    if (order.Status == OrderStatus.Cancelled)
                        throw new InvalidOperationException("Don hang da huy, khong duoc huy lan nua.");
                    if (order.Status == OrderStatus.Completed)
                        throw new InvalidOperationException("Don Completed can quy trinh tra hang, khong huy truc tiep.");
                    if (order.Status == OrderStatus.Pending)
                    {
                        if (db.InventoryTransactions.Any(t => t.SalesOrderId == orderId))
                            throw new InvalidOperationException("Don Pending da co giao dich kho bat thuong.");
                    }
                    else if (order.Status == OrderStatus.Confirmed)
                    {
                        db.Entry(order).Collection(o => o.Details).Load();
                        var details = order.Details.OrderBy(d => d.ProductId).ToList();
                        ValidatePersistedDetails(details);
                        var ledger = db.InventoryTransactions
                            .Where(t => t.SalesOrderId == orderId)
                            .Select(t => new { t.ProductId, t.QuantityChange, t.TransactionType })
                            .ToList();
                        if (ledger.Count != details.Count ||
                            details.Any(d => ledger.Count(t =>
                                t.ProductId == d.ProductId &&
                                t.TransactionType == InventoryTransactionType.SalesOrder &&
                                t.QuantityChange == -d.Quantity) != 1))
                            throw new InvalidOperationException("Lich su xuat kho khong khop chi tiet don; dung huy de kiem tra.");

                        var transactionDate = DateTime.Now;
                        foreach (var detail in details)
                        {
                            var product = LoadLockedProduct(db, detail.ProductId);
                            product.IncreaseStock(detail.Quantity);
                            db.InventoryTransactions.Add(new InventoryTransaction
                            {
                                ProductId = product.Id,
                                SalesOrderId = order.Id,
                                TransactionDate = transactionDate,
                                TransactionType = InventoryTransactionType.SalesOrderCancellation,
                                QuantityChange = detail.Quantity,
                                StockAfter = product.QuantityInStock,
                                UnitPrice = detail.UnitPrice,
                                CreatedBy = createdBy
                            });
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException("Trang thai don hang khong hop le.");
                    }
                    order.Status = OrderStatus.Cancelled;
                    if (order.PaymentStatus == PaymentStatus.Paid)
                        order.PaymentStatus = PaymentStatus.Refunded; // Chi la trang thai mo phong; khong chuyen tien that.
                    db.SaveChanges();
                    transaction.Commit();
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw ConcurrencyException(ex);
            }
        }

        private static SalesOrder LoadLockedOrder(ApplicationDbContext db, int orderId)
        {
            var order = db.SalesOrders.SqlQuery(
                "SELECT * FROM [SalesOrders] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = @p0", orderId)
                .SingleOrDefault();
            if (order == null) throw new InvalidOperationException("Khong tim thay don hang Id=" + orderId);
            return order;
        }

        private static Product LoadLockedProduct(ApplicationDbContext db, int productId)
        {
            var product = db.Products.SqlQuery(
                "SELECT * FROM [Products] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = @p0", productId)
                .SingleOrDefault();
            if (product == null) throw new InvalidOperationException("Khong tim thay san pham Id=" + productId);
            return product;
        }

        private static Product LoadSaleProduct(ApplicationDbContext db, int productId)
        {
            var product = db.Products.SingleOrDefault(p => p.Id == productId);
            if (product == null || !product.IsActive)
                throw new InvalidOperationException("San pham khong ton tai hoac ngung kinh doanh: " + productId);
            if (product.SalePrice < 0m || product.SalePrice > MaxSqlMoney18_2)
                throw new InvalidOperationException("Gia ban san pham khong hop le: " + productId);
            return product;
        }

        private static void ValidateReferences(ApplicationDbContext db, SalesOrderPendingInput input)
        {
            if (!db.Customers.Any(c => c.Id == input.CustomerId))
                throw new InvalidOperationException("Khach hang khong ton tai.");
            if (input.EmployeeId.HasValue && !db.Employees.Any(e => e.Id == input.EmployeeId.Value))
                throw new InvalidOperationException("Nhan vien khong ton tai.");
        }

        private static void ValidateInput(SalesOrderPendingInput input)
        {
            if (input == null) throw new ArgumentNullException("input");
            if (string.IsNullOrWhiteSpace(input.OrderNumber) || input.OrderNumber.Trim().Length > 30)
                throw new ArgumentException("So don hang bat buoc va toi da 30 ky tu.");
            if (input.CustomerId <= 0 || (input.EmployeeId.HasValue && input.EmployeeId.Value <= 0))
                throw new ArgumentException("Ma khach hang/nhan vien khong hop le.");
            if (!Enum.IsDefined(typeof(SalesChannel), input.Channel) ||
                !Enum.IsDefined(typeof(PaymentMethod), input.PaymentMethod))
                throw new ArgumentException("Kenh ban hoac phuong thuc thanh toan khong hop le.");
            if (input.Channel == SalesChannel.Online && string.IsNullOrWhiteSpace(input.ShippingAddress))
                throw new ArgumentException("Don Online phai co dia chi giao hang.");
            if (input.ShippingAddress != null && input.ShippingAddress.Length > 300)
                throw new ArgumentException("Dia chi giao hang toi da 300 ky tu.");
            if (input.Note != null && input.Note.Length > 500)
                throw new ArgumentException("Ghi chu toi da 500 ky tu.");
            if (input.DiscountAmount < 0m || input.DiscountAmount > MaxSqlMoney18_2 ||
                decimal.Round(input.DiscountAmount, 2) != input.DiscountAmount)
                throw new ArgumentException("Tien giam gia khong hop le.");
            if (input.Details == null || input.Details.Count == 0 ||
                input.Details.Any(d => d == null || d.ProductId <= 0 || d.Quantity <= 0))
                throw new ArgumentException("Don hang can chi tiet voi san pham va so luong duong.");
            if (input.Details.Select(d => d.ProductId).Distinct().Count() != input.Details.Count)
                throw new ArgumentException("Moi san pham chi duoc xuat hien mot lan trong don.");
        }

        private static void ValidatePersistedDetails(IList<SalesOrderDetail> details)
        {
            if (details.Count == 0 || details.Any(d => d.ProductId <= 0 || d.Quantity <= 0 ||
                    d.UnitPrice < 0m || d.UnitPrice > MaxSqlMoney18_2) ||
                details.Select(d => d.ProductId).Distinct().Count() != details.Count)
                throw new InvalidOperationException("Chi tiet don hang luu trong DB khong hop le.");
        }

        private static void ValidateActor(string createdBy)
        {
            if (createdBy != null && createdBy.Length > 256)
                throw new ArgumentException("Ten tai khoan thuc hien toi da 256 ky tu.");
        }

        private static void EnsureSqlAmount(decimal amount)
        {
            if (amount < 0m || amount > MaxSqlMoney18_2)
                throw new ArgumentOutOfRangeException("amount", "Tong tien vuot gioi han decimal(18,2).");
        }

        private static InvalidOperationException ConcurrencyException(DbUpdateConcurrencyException inner)
        {
            return new InvalidOperationException(
                "Ton kho vua bi nguoi khac thay doi (RowVersion). Giao dich da rollback; vui long tai lai va thu lai.", inner);
        }
    }
}
