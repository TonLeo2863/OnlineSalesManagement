using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace OnlineSalesManagement
{
    /// <summary>Phiếu nhập kho (header). Chỉ phiếu ở trạng thái Confirmed mới làm tăng tồn.</summary>
    [Table("PurchaseReceipts")]
    public class PurchaseReceipt
    {
        public PurchaseReceipt()
        {
            ReceiptDate = DateTime.Now;
            Status = ReceiptStatus.Draft;
            Details = new HashSet<PurchaseReceiptDetail>();
            InventoryTransactions = new HashSet<InventoryTransaction>();
        }

        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số phiếu nhập.")]
        [StringLength(30, ErrorMessage = "Số phiếu nhập tối đa {1} ký tự.")]
        [Index("IX_PurchaseReceipt_ReceiptNumber", IsUnique = true)]
        [Display(Name = "Số phiếu nhập")]
        public string ReceiptNumber { get; set; }

        [Index("IX_PurchaseReceipt_ReceiptDate")]
        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy HH:mm}", ApplyFormatInEditMode = true)]
        [Display(Name = "Ngày nhập")]
        public DateTime ReceiptDate { get; set; }

        [Display(Name = "Nhà cung cấp")]
        public int SupplierId { get; set; }

        [ForeignKey("SupplierId")]
        public virtual Supplier Supplier { get; set; }

        [Display(Name = "Nhân viên lập phiếu")]
        public int EmployeeId { get; set; }

        [ForeignKey("EmployeeId")]
        public virtual Employee Employee { get; set; }

        [Display(Name = "Trạng thái")]
        public ReceiptStatus Status { get; set; }

        [StringLength(500, ErrorMessage = "Ghi chú tối đa {1} ký tự.")]
        [Display(Name = "Ghi chú")]
        public string Note { get; set; }

        /// <summary>Tổng tiền phiếu nhập = tổng (Quantity * UnitCost) của các dòng. Gọi RecalculateTotal() trước khi lưu.</summary>
        [Range(0, ModelLimits.MaxMoney, ErrorMessage = "Tổng tiền không hợp lệ.")]
        [DisplayFormat(DataFormatString = "{0:N0}", ApplyFormatInEditMode = false)]
        [Display(Name = "Tổng tiền")]
        public decimal TotalAmount { get; set; }

        public virtual ICollection<PurchaseReceiptDetail> Details { get; set; }
        public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; }

        /// <summary>Tính lại TotalAmount bằng decimal từ các dòng chi tiết.</summary>
        public decimal RecalculateTotal()
        {
            TotalAmount = Details == null ? 0m : Details.Sum(d => d.LineTotal);
            return TotalAmount;
        }
    }
}
