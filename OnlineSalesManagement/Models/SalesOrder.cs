using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace OnlineSalesManagement
{
    /// <summary>
    /// Đơn hàng bán (header). Giả định: số đơn (OrderNumber) đồng thời là số hóa đơn;
    /// hóa đơn là đơn có Status = Confirmed hoặc Completed (không có bảng Invoice riêng).
    /// </summary>
    [Table("SalesOrders")]
    public class SalesOrder : IValidatableObject
    {
        public SalesOrder()
        {
            OrderDate = DateTime.Now;
            Channel = SalesChannel.Counter;
            Status = OrderStatus.Pending;
            PaymentMethod = PaymentMethod.Cash;
            PaymentStatus = PaymentStatus.Unpaid;
            Details = new HashSet<SalesOrderDetail>();
            InventoryTransactions = new HashSet<InventoryTransaction>();
        }

        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số đơn hàng.")]
        [StringLength(30, ErrorMessage = "Số đơn hàng tối đa {1} ký tự.")]
        [Index("IX_SalesOrder_OrderNumber", IsUnique = true)]
        [Display(Name = "Số đơn hàng")]
        public string OrderNumber { get; set; }

        [Index("IX_SalesOrder_OrderDate")]
        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm}", ApplyFormatInEditMode = true)]
        [Display(Name = "Ngày đặt")]
        public DateTime OrderDate { get; set; }

        [Display(Name = "Khách hàng")]
        public int CustomerId { get; set; }

        [ForeignKey("CustomerId")]
        public virtual Customer Customer { get; set; }

        /// <summary>Nhân viên xử lý đơn. Null với đơn khách tự đặt online chưa có người xử lý.</summary>
        [Display(Name = "Nhân viên xử lý")]
        public int? EmployeeId { get; set; }

        [ForeignKey("EmployeeId")]
        public virtual Employee Employee { get; set; }

        [Display(Name = "Kênh bán hàng")]
        public SalesChannel Channel { get; set; }

        [Display(Name = "Trạng thái")]
        public OrderStatus Status { get; set; }

        [Display(Name = "Phương thức thanh toán")]
        public PaymentMethod PaymentMethod { get; set; }

        [Display(Name = "Trạng thái thanh toán")]
        public PaymentStatus PaymentStatus { get; set; }

        [StringLength(300, ErrorMessage = "Địa chỉ giao hàng tối đa {1} ký tự.")]
        [Display(Name = "Địa chỉ giao hàng")]
        public string ShippingAddress { get; set; }

        [StringLength(500, ErrorMessage = "Ghi chú tối đa {1} ký tự.")]
        [Display(Name = "Ghi chú")]
        public string Note { get; set; }

        /// <summary>Tạm tính = tổng thành tiền các dòng.</summary>
        [Range(0, ModelLimits.MaxMoney, ErrorMessage = "Tạm tính không hợp lệ.")]
        [DisplayFormat(DataFormatString = "{0:N0}", ApplyFormatInEditMode = false)]
        [Display(Name = "Tạm tính")]
        public decimal SubTotal { get; set; }

        [Range(0, ModelLimits.MaxMoney, ErrorMessage = "Giảm giá phải lớn hơn hoặc bằng 0.")]
        [DisplayFormat(DataFormatString = "{0:N0}", ApplyFormatInEditMode = false)]
        [Display(Name = "Giảm giá")]
        public decimal DiscountAmount { get; set; }

        /// <summary>Tổng thanh toán = SubTotal - DiscountAmount.</summary>
        [Range(0, ModelLimits.MaxMoney, ErrorMessage = "Tổng thanh toán không hợp lệ.")]
        [DisplayFormat(DataFormatString = "{0:N0}", ApplyFormatInEditMode = false)]
        [Display(Name = "Tổng thanh toán")]
        public decimal TotalAmount { get; set; }

        public virtual ICollection<SalesOrderDetail> Details { get; set; }
        public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; }

        /// <summary>
        /// Tính lại SubTotal và TotalAmount bằng decimal từ các dòng chi tiết.
        /// Ném InvalidOperationException nếu giảm giá lớn hơn tạm tính.
        /// </summary>
        public void RecalculateTotals()
        {
            SubTotal = Details == null ? 0m : Details.Sum(d => d.LineTotal);

            if (DiscountAmount > SubTotal)
                throw new InvalidOperationException("Số tiền giảm giá không được lớn hơn tạm tính.");

            TotalAmount = SubTotal - DiscountAmount;
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (DiscountAmount > SubTotal)
            {
                yield return new ValidationResult(
                    "Số tiền giảm giá không được lớn hơn tạm tính.",
                    new[] { "DiscountAmount" });
            }

            if (Channel == SalesChannel.Online && string.IsNullOrWhiteSpace(ShippingAddress))
            {
                yield return new ValidationResult(
                    "Đơn hàng trực tuyến cần có địa chỉ giao hàng.",
                    new[] { "ShippingAddress" });
            }
        }
    }
}
