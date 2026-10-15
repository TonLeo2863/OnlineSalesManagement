using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineSalesManagement
{
    /// <summary>
    /// Chi tiết phiếu nhập. Giả định: mỗi sản phẩm chỉ xuất hiện MỘT dòng trong một phiếu
    /// (ràng buộc duy nhất PurchaseReceiptId + ProductId) - giao diện cần gộp số lượng nếu chọn trùng.
    /// </summary>
    [Table("PurchaseReceiptDetails")]
    public class PurchaseReceiptDetail
    {
        [Key]
        public int Id { get; set; }

        [Index("IX_PurchaseReceiptDetail_Receipt_Product", 1, IsUnique = true)]
        [Display(Name = "Phiếu nhập")]
        public int PurchaseReceiptId { get; set; }

        [ForeignKey("PurchaseReceiptId")]
        public virtual PurchaseReceipt PurchaseReceipt { get; set; }

        [Index("IX_PurchaseReceiptDetail_Receipt_Product", 2, IsUnique = true)]
        [Display(Name = "Sản phẩm")]
        public int ProductId { get; set; }

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Số lượng nhập phải lớn hơn 0.")]
        [Display(Name = "Số lượng")]
        public int Quantity { get; set; }

        [Range(0, ModelLimits.MaxMoney, ErrorMessage = "Đơn giá nhập phải lớn hơn hoặc bằng 0.")]
        [DisplayFormat(DataFormatString = "{0:N0}", ApplyFormatInEditMode = false)]
        [Display(Name = "Đơn giá nhập")]
        public decimal UnitCost { get; set; }

        /// <summary>Thành tiền = Quantity * UnitCost (không lưu DB). Trong LINQ to Entities hãy viết d.Quantity * d.UnitCost.</summary>
        [NotMapped]
        [DisplayFormat(DataFormatString = "{0:N0}", ApplyFormatInEditMode = false)]
        [Display(Name = "Thành tiền")]
        public decimal LineTotal
        {
            get { return Quantity * UnitCost; }
        }
    }
}
