using System.Data.Entity;
using OnlineSalesManagement.Models;


namespace OnlineSalesManagement
{
    /// <summary>
    /// Cấu hình Fluent API cho các thực thể nghiệp vụ. Gọi MỘT dòng trong ApplicationDbContext.OnModelCreating:
    ///     base.OnModelCreating(modelBuilder);
    ///     OnlineSalesModelConfiguration.Configure(modelBuilder);
    ///
    /// Nguyên tắc:
    ///  - Mọi decimal là decimal(18,2).
    ///  - KHÔNG xóa dây chuyền (cascade) giữa các thực thể nghiệp vụ để không mất lịch sử,
    ///    ngoại lệ duy nhất: xóa phiếu nhập/đơn hàng thì xóa luôn các dòng chi tiết của nó.
    ///  - Không gỡ OneToManyCascadeDeleteConvention toàn cục vì các bảng của ASP.NET Identity đang dựa vào nó.
    /// </summary>
    public static class OnlineSalesModelConfiguration
    {
        public static void Configure(DbModelBuilder modelBuilder)
        {
            // Tiền tệ: decimal(18,2)
            modelBuilder.Properties<decimal>().Configure(p => p.HasPrecision(18, 2));

            // Product -> Category
            modelBuilder.Entity<Product>()
                .HasRequired(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .WillCascadeOnDelete(false);

            // Employee, Customer -> ApplicationUser (tùy chọn, không có thuộc tính điều hướng ngược)
            modelBuilder.Entity<Employee>()
                .HasOptional(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Customer>()
                .HasOptional(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .WillCascadeOnDelete(false);

            // PurchaseReceipt -> Supplier, Employee
            modelBuilder.Entity<PurchaseReceipt>()
                .HasRequired(r => r.Supplier)
                .WithMany(s => s.PurchaseReceipts)
                .HasForeignKey(r => r.SupplierId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<PurchaseReceipt>()
                .HasRequired(r => r.Employee)
                .WithMany(e => e.PurchaseReceipts)
                .HasForeignKey(r => r.EmployeeId)
                .WillCascadeOnDelete(false);

            // PurchaseReceiptDetail -> PurchaseReceipt (cascade), Product (restrict)
            modelBuilder.Entity<PurchaseReceiptDetail>()
                .HasRequired(d => d.PurchaseReceipt)
                .WithMany(r => r.Details)
                .HasForeignKey(d => d.PurchaseReceiptId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<PurchaseReceiptDetail>()
                .HasRequired(d => d.Product)
                .WithMany(p => p.PurchaseReceiptDetails)
                .HasForeignKey(d => d.ProductId)
                .WillCascadeOnDelete(false);

            // SalesOrder -> Customer (bắt buộc), Employee (tùy chọn)
            modelBuilder.Entity<SalesOrder>()
                .HasRequired(o => o.Customer)
                .WithMany(c => c.SalesOrders)
                .HasForeignKey(o => o.CustomerId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<SalesOrder>()
                .HasOptional(o => o.Employee)
                .WithMany(e => e.SalesOrders)
                .HasForeignKey(o => o.EmployeeId)
                .WillCascadeOnDelete(false);

            // SalesOrderDetail -> SalesOrder (cascade), Product (restrict)
            modelBuilder.Entity<SalesOrderDetail>()
                .HasRequired(d => d.SalesOrder)
                .WithMany(o => o.Details)
                .HasForeignKey(d => d.SalesOrderId)
                .WillCascadeOnDelete(true);

            modelBuilder.Entity<SalesOrderDetail>()
                .HasRequired(d => d.Product)
                .WithMany(p => p.SalesOrderDetails)
                .HasForeignKey(d => d.ProductId)
                .WillCascadeOnDelete(false);

            // InventoryTransaction -> Product (bắt buộc), PurchaseReceipt, SalesOrder (tùy chọn)
            modelBuilder.Entity<InventoryTransaction>()
                .HasRequired(t => t.Product)
                .WithMany(p => p.InventoryTransactions)
                .HasForeignKey(t => t.ProductId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<InventoryTransaction>()
                .HasOptional(t => t.PurchaseReceipt)
                .WithMany(r => r.InventoryTransactions)
                .HasForeignKey(t => t.PurchaseReceiptId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<InventoryTransaction>()
                .HasOptional(t => t.SalesOrder)
                .WithMany(o => o.InventoryTransactions)
                .HasForeignKey(t => t.SalesOrderId)
                .WillCascadeOnDelete(false);


            modelBuilder.Entity<InventoryShiftSnapshot>()
                .Property(x => x.TotalStockValue)
                .HasPrecision(18, 2);

            modelBuilder.Entity<InventoryShiftSnapshotDetail>()
                .Property(x => x.UnitCost)
                .HasPrecision(18, 2);

            modelBuilder.Entity<InventoryShiftSnapshotDetail>()
                .Property(x => x.StockValue)
                .HasPrecision(18, 2);

        }
    }
}
