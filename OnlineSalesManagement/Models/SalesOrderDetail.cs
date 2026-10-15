using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineSalesManagement
{
    /// <summary>
    /// Chi tiết đơn hàng. UnitPrice là giá bán TẠI THỜI ĐIỂM đặt hàng (không đổi khi Product.SalePrice đổi).
    /// Giả định: mỗi sản phẩm chỉ xuất hiện MỘT dòng trong một đơn (ràng buộc duy nhất SalesOrderId + ProductId).
    /// </summary>
    [Table("SalesOrderDetails")]
    public class SalesOrderDetail
    {
        [Key]
        public int Id { get; set; }

        [Index("IX_SalesOrderDetail_Order_Product", 1, IsUnique = true)]
        [Display(Name = "Đơn hàng")]
        public int SalesOrderId { get; set; }

        [ForeignKey("SalesOrderId")]
        public virtual SalesOrder SalesOrder { get; set; }

        [Index("IX_SalesOrderDetail_Order_Product", 2, IsUnique = true)]
        [Display(Name = "Sản phẩm")]
        public int ProductId { get; set; }

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Số lượng bán phải lớn hơn 0.")]
        [Display(Name = "Số lượng")]
        public int Quantity { get; set; }

        [Range(0, ModelLimits.MaxMoney, ErrorMessage = "Đơn giá bán phải lớn hơn hoặc bằng 0.")]
        [DisplayFormat(DataFormatString = "{0:N0}", ApplyFormatInEditMode = false)]
        [Display(Name = "Đơn giá bán")]
        public decimal UnitPrice { get; set; }

        /// <summary>Thành tiền = Quantity * UnitPrice (không lưu DB). Trong LINQ to Entities hãy viết d.Quantity * d.UnitPrice.</summary>
        [NotMapped]
        [DisplayFormat(DataFormatString = "{0:N0}", ApplyFormatInEditMode = false)]
        [Display(Name = "Thành tiền")]
        public decimal LineTotal
        {
            get { return Quantity * UnitPrice; }
        }
    }
}
