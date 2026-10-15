using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineSalesManagement
{
    /// <summary>
    /// Sổ cái kho: mỗi lần tồn kho thay đổi tạo MỘT dòng (chỉ thêm, không sửa/xóa).
    /// QuantityChange mang dấu: dương = nhập, âm = xuất. StockAfter là tồn của sản phẩm ngay sau giao dịch.
    /// Bất biến: Product.QuantityInStock = tổng QuantityChange của sản phẩm đó.
    /// </summary>
    [Table("InventoryTransactions")]
    public class InventoryTransaction : IValidatableObject
    {
        public InventoryTransaction()
        {
            TransactionDate = DateTime.Now;
        }

        [Key]
        public int Id { get; set; }

        [Index("IX_InventoryTransaction_Product_Date", 1)]
        [Display(Name = "Sản phẩm")]
        public int ProductId { get; set; }

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }

        [Index("IX_InventoryTransaction_Product_Date", 2)]
        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm}", ApplyFormatInEditMode = true)]
        [Display(Name = "Thời điểm")]
        public DateTime TransactionDate { get; set; }

        [Display(Name = "Loại giao dịch")]
        public InventoryTransactionType TransactionType { get; set; }

        /// <summary>Số lượng thay đổi có dấu (+ nhập, - xuất). Khác 0.</summary>
        [Display(Name = "Số lượng thay đổi")]
        public int QuantityChange { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Tồn sau giao dịch không được âm.")]
        [Display(Name = "Tồn sau giao dịch")]
        public int StockAfter { get; set; }

        /// <summary>Đơn giá nhập (giao dịch nhập) hoặc đơn giá bán (giao dịch xuất) tại thời điểm giao dịch.</summary>
        [Range(0, ModelLimits.MaxMoney, ErrorMessage = "Đơn giá phải lớn hơn hoặc bằng 0.")]
        [DisplayFormat(DataFormatString = "{0:N0}", ApplyFormatInEditMode = false)]
        [Display(Name = "Đơn giá")]
        public decimal UnitPrice { get; set; }

        /// <summary>Phiếu nhập liên quan (null nếu giao dịch không xuất phát từ phiếu nhập).</summary>
        [Display(Name = "Phiếu nhập")]
        public int? PurchaseReceiptId { get; set; }

        [ForeignKey("PurchaseReceiptId")]
        public virtual PurchaseReceipt PurchaseReceipt { get; set; }

        /// <summary>Đơn hàng liên quan (null nếu giao dịch không xuất phát từ đơn hàng).</summary>
        [Display(Name = "Đơn hàng")]
        public int? SalesOrderId { get; set; }

        [ForeignKey("SalesOrderId")]
        public virtual SalesOrder SalesOrder { get; set; }

        [StringLength(500, ErrorMessage = "Ghi chú tối đa {1} ký tự.")]
        [Display(Name = "Ghi chú")]
        public string Note { get; set; }

        /// <summary>Tên đăng nhập người thực hiện (giả định lấy từ User.Identity.Name, không có khóa ngoại).</summary>
        [StringLength(256)]
        [Display(Name = "Người thực hiện")]
        public string CreatedBy { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (QuantityChange == 0)
            {
                yield return new ValidationResult(
                    "Số lượng thay đổi phải khác 0.", new[] { "QuantityChange" });
            }

            bool hasReceipt = PurchaseReceiptId.HasValue || PurchaseReceipt != null;
            bool hasOrder = SalesOrderId.HasValue || SalesOrder != null;

            if (hasReceipt && hasOrder)
            {
                yield return new ValidationResult(
                    "Một giao dịch kho không được gắn đồng thời với phiếu nhập và đơn hàng.");
            }

            switch (TransactionType)
            {
                case InventoryTransactionType.PurchaseReceipt:
                    if (QuantityChange <= 0)
                        yield return new ValidationResult("Giao dịch nhập kho phải có số lượng dương.", new[] { "QuantityChange" });
                    if (!hasReceipt)
                        yield return new ValidationResult("Giao dịch nhập kho phải gắn với phiếu nhập.", new[] { "PurchaseReceiptId" });
                    break;

                case InventoryTransactionType.PurchaseReceiptCancellation:
                    if (QuantityChange >= 0)
                        yield return new ValidationResult("Giao dịch hủy phiếu nhập phải có số lượng âm.", new[] { "QuantityChange" });
                    if (!hasReceipt)
                        yield return new ValidationResult("Giao dịch hủy phiếu nhập phải gắn với phiếu nhập.", new[] { "PurchaseReceiptId" });
                    break;

                case InventoryTransactionType.SalesOrder:
                    if (QuantityChange >= 0)
                        yield return new ValidationResult("Giao dịch xuất bán phải có số lượng âm.", new[] { "QuantityChange" });
                    if (!hasOrder)
                        yield return new ValidationResult("Giao dịch xuất bán phải gắn với đơn hàng.", new[] { "SalesOrderId" });
                    break;

                case InventoryTransactionType.SalesOrderCancellation:
                    if (QuantityChange <= 0)
                        yield return new ValidationResult("Giao dịch hoàn kho do hủy đơn phải có số lượng dương.", new[] { "QuantityChange" });
                    if (!hasOrder)
                        yield return new ValidationResult("Giao dịch hoàn kho do hủy đơn phải gắn với đơn hàng.", new[] { "SalesOrderId" });
                    break;

                case InventoryTransactionType.Adjustment:
                    if (hasReceipt || hasOrder)
                        yield return new ValidationResult("Giao dịch điều chỉnh tồn kho không gắn với phiếu nhập hoặc đơn hàng.");
                    break;
            }
        }
    }
}
