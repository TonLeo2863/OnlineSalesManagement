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
    // Dung input rieng thay vi nhan PurchaseReceipt tu form (chong overposting Status/TotalAmount).
    public sealed class PurchaseReceiptLineInput
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitCost { get; set; }
    }

    public sealed class PurchaseReceiptDraftInput
    {
        public string ReceiptNumber { get; set; }
        public int SupplierId { get; set; }
        public int EmployeeId { get; set; }
        public string Note { get; set; }
        public IList<PurchaseReceiptLineInput> Details { get; set; }
    }

    /// <summary>
    /// EF6 + SQL Server; moi method tu quan ly DbContext va transaction.
    /// Status Draft -> Confirmed: tang ton + ghi so kho 1 lan.
    /// Confirmed -> Cancelled: hoan tac nhap neu van du ton.
    /// Timestamp RowVersion do EF6 tu kiem tra khi SaveChanges tren Product duoc load/tracked.
    /// </summary>
    public sealed class PurchaseReceiptService
    {
        private const decimal MaxSqlMoney18_2 = 9999999999999999.99m;

        public int CreateDraft(PurchaseReceiptDraftInput input)
        {
            ValidateInput(input);
            using (var db = new ApplicationDbContext())
            using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                ValidateReferences(db, input);
                var receipt = new PurchaseReceipt
                {
                    ReceiptNumber = input.ReceiptNumber.Trim(),
                    SupplierId = input.SupplierId,
                    EmployeeId = input.EmployeeId,
                    Note = input.Note,
                    Status = ReceiptStatus.Draft
                };
                foreach (var line in input.Details)
                {
                    EnsureProductExists(db, line.ProductId);
                    receipt.Details.Add(new PurchaseReceiptDetail
                    {
                        ProductId = line.ProductId,
                        Quantity = line.Quantity,
                        UnitCost = line.UnitCost
                    });
                }
                receipt.RecalculateTotal();
                EnsureSqlAmount(receipt.TotalAmount);
                db.PurchaseReceipts.Add(receipt);
                db.SaveChanges();
                transaction.Commit();
                return receipt.Id;
            }
        }

        public void UpdateDraft(int receiptId, PurchaseReceiptDraftInput input)
        {
            ValidateInput(input);
            try
            {
                using (var db = new ApplicationDbContext())
                using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
                {
                    var receipt = LoadLockedReceipt(db, receiptId);
                    if (receipt.Status != ReceiptStatus.Draft)
                        throw new InvalidOperationException("Chi duoc sua phieu nhap o trang thai Draft.");
                    if (db.InventoryTransactions.Any(t => t.PurchaseReceiptId == receiptId))
                        throw new InvalidOperationException("Phieu da co giao dich kho; can kiem tra du lieu truoc khi sua.");

                    ValidateReferences(db, input);
                    db.Entry(receipt).Collection(r => r.Details).Load();
                    var submitted = input.Details.ToDictionary(d => d.ProductId);
                    foreach (var existing in receipt.Details.ToList())
                    {
                        PurchaseReceiptLineInput newLine;
                        if (!submitted.TryGetValue(existing.ProductId, out newLine))
                        {
                            receipt.Details.Remove(existing);
                            db.PurchaseReceiptDetails.Remove(existing);
                        }
                        else
                        {
                            existing.Quantity = newLine.Quantity;
                            existing.UnitCost = newLine.UnitCost;
                        }
                    }
                    var existingProductIds = new HashSet<int>(receipt.Details.Select(d => d.ProductId));
                    foreach (var line in input.Details.Where(d => !existingProductIds.Contains(d.ProductId)))
                    {
                        EnsureProductExists(db, line.ProductId);
                        receipt.Details.Add(new PurchaseReceiptDetail
                        {
                            PurchaseReceiptId = receiptId,
                            ProductId = line.ProductId,
                            Quantity = line.Quantity,
                            UnitCost = line.UnitCost
                        });
                    }
                    receipt.ReceiptNumber = input.ReceiptNumber.Trim();
                    receipt.SupplierId = input.SupplierId;
                    receipt.EmployeeId = input.EmployeeId;
                    receipt.Note = input.Note;
                    receipt.RecalculateTotal();
                    EnsureSqlAmount(receipt.TotalAmount);
                    db.SaveChanges();
                    transaction.Commit();
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw ConcurrencyException(ex);
            }
        }

        public void ConfirmReceipt(int receiptId, string createdBy)
        {
            ValidateActor(createdBy);
            try
            {
                using (var db = new ApplicationDbContext())
                using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
                {
                    var receipt = LoadLockedReceipt(db, receiptId);
                    if (receipt.Status != ReceiptStatus.Draft)
                        throw new InvalidOperationException("Chi duoc xac nhan phieu Draft; phieu da xu ly khong duoc nhap trung.");
                    if (db.InventoryTransactions.Any(t => t.PurchaseReceiptId == receiptId))
                        throw new InvalidOperationException("Phieu da co giao dich kho; dung xu ly de tranh nhap trung.");

                    db.Entry(receipt).Collection(r => r.Details).Load();
                    var details = receipt.Details.OrderBy(d => d.ProductId).ToList();
                    ValidatePersistedDetails(details);
                    receipt.RecalculateTotal();
                    EnsureSqlAmount(receipt.TotalAmount);
                    var transactionDate = DateTime.Now;
                    foreach (var detail in details)
                    {
                        // UPDLOCK + HOLDLOCK khoa theo Id tang dan, tranh hai phieu ghi de len nhau.
                        // Entity van duoc EF track: RowVersion la concurrency token trong UPDATE.
                        var product = LoadLockedProduct(db, detail.ProductId);
                        product.IncreaseStock(detail.Quantity);
                        product.CostPrice = detail.UnitCost; // gia nhap gan nhat
                        db.InventoryTransactions.Add(new InventoryTransaction
                        {
                            ProductId = product.Id,
                            PurchaseReceiptId = receipt.Id,
                            TransactionDate = transactionDate,
                            TransactionType = InventoryTransactionType.PurchaseReceipt,
                            QuantityChange = detail.Quantity,
                            StockAfter = product.QuantityInStock,
                            UnitPrice = detail.UnitCost,
                            CreatedBy = createdBy
                        });
                    }
                    receipt.Status = ReceiptStatus.Confirmed;
                    db.SaveChanges(); // luu Status + Products + InventoryTransactions cung transaction
                    transaction.Commit();
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw ConcurrencyException(ex);
            }
        }

        public void CancelReceipt(int receiptId, string createdBy)
        {
            ValidateActor(createdBy);
            try
            {
                using (var db = new ApplicationDbContext())
                using (var transaction = db.Database.BeginTransaction(IsolationLevel.Serializable))
                {
                    var receipt = LoadLockedReceipt(db, receiptId);
                    if (receipt.Status == ReceiptStatus.Cancelled)
                        throw new InvalidOperationException("Phieu nhap da huy, khong duoc huy lan nua.");
                    if (receipt.Status == ReceiptStatus.Draft)
                    {
                        if (db.InventoryTransactions.Any(t => t.PurchaseReceiptId == receiptId))
                            throw new InvalidOperationException("Phieu Draft da co giao dich kho bat thuong.");
                        receipt.Status = ReceiptStatus.Cancelled;
                        db.SaveChanges();
                        transaction.Commit();
                        return;
                    }
                    if (receipt.Status != ReceiptStatus.Confirmed)
                        throw new InvalidOperationException("Trang thai phieu nhap khong hop le.");

                    db.Entry(receipt).Collection(r => r.Details).Load();
                    var details = receipt.Details.OrderBy(d => d.ProductId).ToList();
                    ValidatePersistedDetails(details);
                    // Khong cho phep huy neu giao dich nhap goc khong day du/da hoan tac.
                    var originalEntries = db.InventoryTransactions
                        .Where(t => t.PurchaseReceiptId == receiptId)
                        .Select(t => new { t.ProductId, t.QuantityChange, t.TransactionType })
                        .ToList();
                    if (originalEntries.Count != details.Count ||
                        details.Any(d => originalEntries.Count(t =>
                            t.ProductId == d.ProductId &&
                            t.TransactionType == InventoryTransactionType.PurchaseReceipt &&
                            t.QuantityChange == d.Quantity) != 1))
                        throw new InvalidOperationException("Lich su nhap kho khong khop chi tiet phieu; dung huy de kiem tra.");

                    var transactionDate = DateTime.Now;
                    foreach (var detail in details)
                    {
                        var product = LoadLockedProduct(db, detail.ProductId);
                        // Neu hang da xuat ban, khong cho phep ton am.
                        product.DecreaseStock(detail.Quantity);
                        db.InventoryTransactions.Add(new InventoryTransaction
                        {
                            ProductId = product.Id,
                            PurchaseReceiptId = receipt.Id,
                            TransactionDate = transactionDate,
                            TransactionType = InventoryTransactionType.PurchaseReceiptCancellation,
                            QuantityChange = -detail.Quantity,
                            StockAfter = product.QuantityInStock,
                            UnitPrice = detail.UnitCost,
                            CreatedBy = createdBy
                        });
                    }
                    receipt.Status = ReceiptStatus.Cancelled;
                    db.SaveChanges();
                    transaction.Commit();
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw ConcurrencyException(ex);
            }
        }

        private static PurchaseReceipt LoadLockedReceipt(ApplicationDbContext db, int receiptId)
        {
            // Phu hop SQL Server + ten bang da co trong [Table("PurchaseReceipts")].
            var receipt = db.PurchaseReceipts.SqlQuery(
                "SELECT * FROM [PurchaseReceipts] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = @p0", receiptId)
                .SingleOrDefault();
            if (receipt == null) throw new InvalidOperationException("Khong tim thay phieu nhap Id=" + receiptId);
            return receipt;
        }

        private static Product LoadLockedProduct(ApplicationDbContext db, int productId)
        {
            var product = db.Products.SqlQuery(
                "SELECT * FROM [Products] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = @p0", productId)
                .SingleOrDefault();
            if (product == null) throw new InvalidOperationException("Khong tim thay san pham Id=" + productId);
            return product;
        }

        private static void EnsureProductExists(ApplicationDbContext db, int productId)
        {
            if (!db.Products.Any(p => p.Id == productId))
                throw new InvalidOperationException("Khong tim thay san pham Id=" + productId);
        }

        private static void ValidateReferences(ApplicationDbContext db, PurchaseReceiptDraftInput input)
        {
            if (!db.Suppliers.Any(s => s.Id == input.SupplierId))
                throw new InvalidOperationException("Nha cung cap khong ton tai.");
            if (!db.Employees.Any(e => e.Id == input.EmployeeId))
                throw new InvalidOperationException("Nhan vien khong ton tai.");
        }

        private static void ValidateInput(PurchaseReceiptDraftInput input)
        {
            if (input == null) throw new ArgumentNullException("input");
            if (string.IsNullOrWhiteSpace(input.ReceiptNumber) || input.ReceiptNumber.Trim().Length > 30)
                throw new ArgumentException("So phieu nhap bat buoc va toi da 30 ky tu.");
            if (input.SupplierId <= 0 || input.EmployeeId <= 0)
                throw new ArgumentException("Can chon nha cung cap va nhan vien.");
            if (input.Note != null && input.Note.Length > 500)
                throw new ArgumentException("Ghi chu toi da 500 ky tu.");
            if (input.Details == null || input.Details.Count == 0)
                throw new ArgumentException("Phieu nhap can it nhat mot dong chi tiet.");
            if (input.Details.Any(d => d == null || d.ProductId <= 0 || d.Quantity <= 0 ||
                    d.UnitCost < 0 || d.UnitCost > MaxSqlMoney18_2 ||
                    decimal.Round(d.UnitCost, 2) != d.UnitCost))
                throw new ArgumentException("Chi tiet nhap kho co san pham, so luong hoac gia nhap khong hop le.");
            if (input.Details.Select(d => d.ProductId).Distinct().Count() != input.Details.Count)
                throw new ArgumentException("Mot san pham chi duoc xuat hien mot lan trong phieu.");
            EnsureSqlAmount(input.Details.Sum(d => d.Quantity * d.UnitCost));
        }

        private static void ValidatePersistedDetails(IList<PurchaseReceiptDetail> details)
        {
            if (details.Count == 0 || details.Any(d => d.ProductId <= 0 || d.Quantity <= 0 ||
                    d.UnitCost < 0 || d.UnitCost > MaxSqlMoney18_2 ||
                    decimal.Round(d.UnitCost, 2) != d.UnitCost) ||
                details.Select(d => d.ProductId).Distinct().Count() != details.Count)
                throw new InvalidOperationException("Chi tiet phieu nhap luu trong DB khong hop le.");
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
