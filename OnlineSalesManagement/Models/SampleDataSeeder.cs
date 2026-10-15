using OnlineSalesManagement.Models;
using System;
using System.Linq;

namespace OnlineSalesManagement
{
    /// <summary>
    /// Dữ liệu mẫu để thử CRUD và báo cáo. CHỈ dùng khi phát triển/trình diễn.
    ///
    /// Tính chất:
    ///  - Idempotent: nếu đã có Category hoặc Product thì không làm gì.
    ///  - Tồn kho nhất quán: Product.QuantityInStock được tính từ phiếu nhập/đơn hàng
    ///    và mỗi lần thay đổi đều có dòng InventoryTransaction tương ứng.
    ///  - Không tạo tài khoản Identity; Employee.UserId và Customer.UserId để null.
    ///
    /// Gọi từ Migrations/Configuration.cs:
    ///     protected override void Seed(ApplicationDbContext context)
    ///     {
    ///         SampleDataSeeder.Seed(context);
    ///     }
    /// </summary>
    public static class SampleDataSeeder
    {
        private const string SeedUser = "seed";

        public static void Seed(ApplicationDbContext context)
        {
            if (context == null) throw new ArgumentNullException("context");
            if (context.Categories.Any() || context.Products.Any()) return;

            // ---------- Danh mục ----------
            var phones = new Category { Name = "Điện thoại", Description = "Điện thoại di động" };
            var laptops = new Category { Name = "Laptop", Description = "Máy tính xách tay" };
            var accessories = new Category { Name = "Phụ kiện", Description = "Phụ kiện điện thoại, máy tính" };
            var network = new Category { Name = "Thiết bị mạng", Description = "Router, switch, access point" };
            context.Categories.AddRange(new[] { phones, laptops, accessories, network });

            // ---------- Sản phẩm (tồn kho bắt đầu bằng 0, sẽ tăng qua phiếu nhập) ----------
            var p1 = NewProduct("DT-001", "Điện thoại Samsung Galaxy A55", phones, 8500000m, 9990000m, 5);
            var p2 = NewProduct("DT-002", "Điện thoại iPhone 15 128GB", phones, 17500000m, 19990000m, 3);
            var p3 = NewProduct("LT-001", "Laptop Dell Inspiron 15", laptops, 13000000m, 15490000m, 3);
            var p4 = NewProduct("LT-002", "Laptop ASUS Vivobook 14", laptops, 11200000m, 12990000m, 6); // tồn = mức tối thiểu để thử cảnh báo
            var p5 = NewProduct("PK-001", "Tai nghe Bluetooth", accessories, 350000m, 590000m, 10);
            var p6 = NewProduct("PK-002", "Sạc nhanh 20W", accessories, 120000m, 220000m, 20);
            var p7 = NewProduct("PK-003", "Chuột không dây", accessories, 150000m, 270000m, 10);
            var p8 = NewProduct("NW-001", "Router Wi-Fi 6", network, 850000m, 1190000m, 5);
            context.Products.AddRange(new[] { p1, p2, p3, p4, p5, p6, p7, p8 });

            // ---------- Nhà cung cấp ----------
            var s1 = new Supplier
            {
                Code = "NCC001", Name = "Công ty TNHH Phân phối Viễn Thông Việt", ContactPerson = "Nguyễn Hoài Nam",
                Email = "lienhe@vienthongviet.example.com", PhoneNumber = "0281234567",
                Address = "12 Nguyễn Huệ, Quận 1, TP.HCM", TaxCode = "0312345678"
            };
            var s2 = new Supplier
            {
                Code = "NCC002", Name = "Công ty CP Công nghệ Máy tính Sao Mai", ContactPerson = "Trần Quang Huy",
                Email = "kinhdoanh@saomai.example.com", PhoneNumber = "0287654321",
                Address = "45 Lê Văn Việt, TP. Thủ Đức, TP.HCM", TaxCode = "0398765432"
            };
            var s3 = new Supplier
            {
                Code = "NCC003", Name = "Công ty TNHH Phụ kiện Số An Phát", ContactPerson = "Lê Thu Hà",
                Email = "anphat@example.com", PhoneNumber = "0909000003",
                Address = "88 Cách Mạng Tháng Tám, Quận 3, TP.HCM", TaxCode = "0311122233"
            };
            context.Suppliers.AddRange(new[] { s1, s2, s3 });

            // ---------- Nhân viên ----------
            var e1 = new Employee
            {
                Code = "NV001", FullName = "Nguyễn Văn An", Email = "an.nguyen@example.com", PhoneNumber = "0900000001",
                Address = "Quận Bình Thạnh, TP.HCM", Position = "Quản lý kho", HireDate = new DateTime(2024, 3, 1)
            };
            var e2 = new Employee
            {
                Code = "NV002", FullName = "Trần Thị Bình", Email = "binh.tran@example.com", PhoneNumber = "0900000002",
                Address = "Quận Gò Vấp, TP.HCM", Position = "Nhân viên bán hàng", HireDate = new DateTime(2024, 6, 15)
            };
            var e3 = new Employee
            {
                Code = "NV003", FullName = "Lê Minh Châu", Email = "chau.le@example.com", PhoneNumber = "0900000003",
                Address = "Quận 7, TP.HCM", Position = "Kế toán", HireDate = new DateTime(2025, 1, 10)
            };
            context.Employees.AddRange(new[] { e1, e2, e3 });

            // ---------- Khách hàng (KH001 là khách lẻ dùng cho bán tại quầy) ----------
            var c1 = new Customer { Code = "KH001", FullName = "Khách lẻ", PhoneNumber = "0900000000", Address = "Mua tại quầy" };
            var c2 = new Customer
            {
                Code = "KH002", FullName = "Phạm Quốc Dũng", Email = "dung.pham@example.com", PhoneNumber = "0911000002",
                Address = "25 Lý Thường Kiệt, Quận 10, TP.HCM"
            };
            var c3 = new Customer
            {
                Code = "KH003", FullName = "Hoàng Thị Lan", Email = "lan.hoang@example.com", PhoneNumber = "0911000003",
                Address = "7 Trần Hưng Đạo, TP. Biên Hòa, Đồng Nai"
            };
            var c4 = new Customer
            {
                Code = "KH004", FullName = "Công ty TNHH Minh Phát", Email = "mua-hang@minhphat.example.com", PhoneNumber = "0911000004",
                Address = "103 Điện Biên Phủ, Quận 3, TP.HCM"
            };
            context.Customers.AddRange(new[] { c1, c2, c3, c4 });

            // ---------- Phiếu nhập kho ----------
            // PN2026-0001: 170.000.000 + 175.000.000 = 345.000.000
            var r1 = NewReceipt("PN2026-0001", DaysAgo(30, 9), s1, e1, ReceiptStatus.Confirmed, "Nhập lô điện thoại đầu kỳ");
            AddReceiptLine(context, r1, p1, 20, 8500000m);
            AddReceiptLine(context, r1, p2, 10, 17500000m);
            FinishReceipt(context, r1);

            // PN2026-0002: 104.000.000 + 67.200.000 = 171.200.000
            var r2 = NewReceipt("PN2026-0002", DaysAgo(20, 9), s2, e1, ReceiptStatus.Confirmed, "Nhập laptop");
            AddReceiptLine(context, r2, p3, 8, 13000000m);
            AddReceiptLine(context, r2, p4, 6, 11200000m);
            FinishReceipt(context, r2);

            // PN2026-0003: 17.500.000 + 12.000.000 + 6.000.000 + 12.750.000 = 48.250.000
            var r3 = NewReceipt("PN2026-0003", DaysAgo(10, 9), s3, e1, ReceiptStatus.Confirmed, "Nhập phụ kiện và router");
            AddReceiptLine(context, r3, p5, 50, 350000m);
            AddReceiptLine(context, r3, p6, 100, 120000m);
            AddReceiptLine(context, r3, p7, 40, 150000m);
            AddReceiptLine(context, r3, p8, 15, 850000m);
            FinishReceipt(context, r3);

            // PN2026-0004: phiếu NHÁP, không ảnh hưởng tồn kho. 86.000.000
            var r4 = NewReceipt("PN2026-0004", DaysAgo(0, 8), s1, e1, ReceiptStatus.Draft, "Phiếu nháp - chưa nhập kho");
            AddReceiptLine(context, r4, p1, 10, 8600000m);
            FinishReceipt(context, r4);

            // ---------- Đơn hàng bán ----------
            // HD2026-0001 (tại quầy, hoàn tất): 1.180.000 + 660.000 = 1.840.000
            var o1 = NewOrder("HD2026-0001", DaysAgo(8, 10), c1, e2, SalesChannel.Counter, OrderStatus.Confirmed,
                PaymentMethod.Cash, PaymentStatus.Paid, null, 0m);
            AddOrderLine(context, o1, p5, 2, 590000m);
            AddOrderLine(context, o1, p6, 3, 220000m);
            FinishOrder(context, o1);
            o1.Status = OrderStatus.Completed;

            // HD2026-0002 (online, hoàn tất, giảm 570.000): 19.980.000 + 590.000 = 20.570.000 -> 20.000.000
            var o2 = NewOrder("HD2026-0002", DaysAgo(5, 14), c2, e2, SalesChannel.Online, OrderStatus.Confirmed,
                PaymentMethod.BankTransfer, PaymentStatus.Paid, c2.Address, 570000m);
            AddOrderLine(context, o2, p1, 2, 9990000m);
            AddOrderLine(context, o2, p5, 1, 590000m);
            FinishOrder(context, o2);
            o2.Status = OrderStatus.Completed;

            // HD2026-0003 (online, đã xác nhận, COD, chưa thu tiền, chưa có nhân viên xử lý): 15.490.000 + 540.000 = 16.030.000
            var o3 = NewOrder("HD2026-0003", DaysAgo(3, 16), c3, null, SalesChannel.Online, OrderStatus.Confirmed,
                PaymentMethod.CashOnDelivery, PaymentStatus.Unpaid, c3.Address, 0m);
            AddOrderLine(context, o3, p3, 1, 15490000m);
            AddOrderLine(context, o3, p7, 2, 270000m);
            FinishOrder(context, o3);

            // HD2026-0004 (online, CHỜ XÁC NHẬN, chưa trừ tồn): 39.980.000
            var o4 = NewOrder("HD2026-0004", DaysAgo(1, 11), c4, null, SalesChannel.Online, OrderStatus.Pending,
                PaymentMethod.BankTransfer, PaymentStatus.Unpaid, c4.Address, 0m);
            AddOrderLine(context, o4, p2, 2, 19990000m);
            FinishOrder(context, o4);

            // HD2026-0005 (tại quầy, đã xác nhận rồi HỦY -> hoàn kho): 1.100.000, không tính doanh thu
            var o5 = NewOrder("HD2026-0005", DaysAgo(2, 9), c1, e2, SalesChannel.Counter, OrderStatus.Confirmed,
                PaymentMethod.Cash, PaymentStatus.Unpaid, null, 0m);
            AddOrderLine(context, o5, p6, 5, 220000m);
            FinishOrder(context, o5);
            CancelConfirmedOrder(context, o5, DaysAgo(2, 10));

            // SaveChanges của EF6 chạy toàn bộ trong một transaction: hoặc lưu hết hoặc không lưu gì.
            context.SaveChanges();
        }

        // ================= Hàm hỗ trợ =================

        private static DateTime DaysAgo(int days, int hour)
        {
            return DateTime.Today.AddDays(-days).AddHours(hour);
        }

        private static Product NewProduct(string sku, string name, Category category, decimal cost, decimal sale, int reorderLevel)
        {
            return new Product
            {
                Sku = sku,
                Name = name,
                Category = category,
                Unit = "Cái",
                CostPrice = cost,
                SalePrice = sale,
                ReorderLevel = reorderLevel,
                QuantityInStock = 0
            };
        }

        private static PurchaseReceipt NewReceipt(string number, DateTime date, Supplier supplier, Employee employee,
            ReceiptStatus status, string note)
        {
            return new PurchaseReceipt
            {
                ReceiptNumber = number,
                ReceiptDate = date,
                Supplier = supplier,
                Employee = employee,
                Status = status,
                Note = note
            };
        }

        /// <summary>Thêm dòng chi tiết. Nếu phiếu đã Confirmed thì tăng tồn và ghi sổ kho ngay.</summary>
        private static void AddReceiptLine(ApplicationDbContext context, PurchaseReceipt receipt, Product product,
            int quantity, decimal unitCost)
        {
            var detail = new PurchaseReceiptDetail
            {
                PurchaseReceipt = receipt,
                Product = product,
                Quantity = quantity,
                UnitCost = unitCost
            };
            receipt.Details.Add(detail);

            if (receipt.Status == ReceiptStatus.Confirmed)
            {
                product.IncreaseStock(quantity);
                context.InventoryTransactions.Add(new InventoryTransaction
                {
                    Product = product,
                    TransactionDate = receipt.ReceiptDate,
                    TransactionType = InventoryTransactionType.PurchaseReceipt,
                    QuantityChange = quantity,
                    StockAfter = product.QuantityInStock,
                    UnitPrice = unitCost,
                    PurchaseReceipt = receipt,
                    Note = "Nhập kho theo phiếu " + receipt.ReceiptNumber,
                    CreatedBy = SeedUser
                });
            }
        }

        private static void FinishReceipt(ApplicationDbContext context, PurchaseReceipt receipt)
        {
            receipt.RecalculateTotal();
            context.PurchaseReceipts.Add(receipt);
        }

        private static SalesOrder NewOrder(string number, DateTime date, Customer customer, Employee employee,
            SalesChannel channel, OrderStatus status, PaymentMethod method, PaymentStatus paymentStatus,
            string shippingAddress, decimal discount)
        {
            return new SalesOrder
            {
                OrderNumber = number,
                OrderDate = date,
                Customer = customer,
                Employee = employee,
                Channel = channel,
                Status = status,
                PaymentMethod = method,
                PaymentStatus = paymentStatus,
                ShippingAddress = shippingAddress,
                DiscountAmount = discount
            };
        }

        private static bool AffectsStock(OrderStatus status)
        {
            return status == OrderStatus.Confirmed || status == OrderStatus.Completed;
        }

        /// <summary>
        /// Thêm dòng chi tiết. Nếu đơn đã Confirmed/Completed thì kiểm tra tồn, trừ tồn và ghi sổ kho
        /// (DecreaseStock ném InvalidOperationException nếu không đủ hàng).
        /// </summary>
        private static void AddOrderLine(ApplicationDbContext context, SalesOrder order, Product product,
            int quantity, decimal unitPrice)
        {
            var detail = new SalesOrderDetail
            {
                SalesOrder = order,
                Product = product,
                Quantity = quantity,
                UnitPrice = unitPrice
            };
            order.Details.Add(detail);

            if (AffectsStock(order.Status))
            {
                product.DecreaseStock(quantity);
                context.InventoryTransactions.Add(new InventoryTransaction
                {
                    Product = product,
                    TransactionDate = order.OrderDate,
                    TransactionType = InventoryTransactionType.SalesOrder,
                    QuantityChange = -quantity,
                    StockAfter = product.QuantityInStock,
                    UnitPrice = unitPrice,
                    SalesOrder = order,
                    Note = "Xuất bán theo đơn " + order.OrderNumber,
                    CreatedBy = SeedUser
                });
            }
        }

        private static void FinishOrder(ApplicationDbContext context, SalesOrder order)
        {
            order.RecalculateTotals();
            context.SalesOrders.Add(order);
        }

        /// <summary>Hủy đơn đã trừ tồn: hoàn lại tồn từng dòng, ghi sổ kho và chuyển trạng thái Cancelled.</summary>
        private static void CancelConfirmedOrder(ApplicationDbContext context, SalesOrder order, DateTime when)
        {
            foreach (var detail in order.Details)
            {
                detail.Product.IncreaseStock(detail.Quantity);
                context.InventoryTransactions.Add(new InventoryTransaction
                {
                    Product = detail.Product,
                    TransactionDate = when,
                    TransactionType = InventoryTransactionType.SalesOrderCancellation,
                    QuantityChange = detail.Quantity,
                    StockAfter = detail.Product.QuantityInStock,
                    UnitPrice = detail.UnitPrice,
                    SalesOrder = order,
                    Note = "Hoàn kho do hủy đơn " + order.OrderNumber,
                    CreatedBy = SeedUser
                });
            }
            order.Status = OrderStatus.Cancelled;
        }
    }
}
