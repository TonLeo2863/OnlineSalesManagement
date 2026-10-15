
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineSalesManagement
{
    [Table("InventoryShiftSnapshots")]
    public class InventoryShiftSnapshot
    {
        public InventoryShiftSnapshot()
        {
            CreatedAt = DateTime.Now;
            Version = 1;
            Details = new HashSet<InventoryShiftSnapshotDetail>();
            AuditLogs = new HashSet<InventoryShiftAuditLog>();
        }

        [Key]
        public int Id { get; set; }

        [Index("IX_ShiftSnapshot_Version", 1, IsUnique = true)]
        public int WorkShiftId { get; set; }

        [ForeignKey("WorkShiftId")]
        public virtual WorkShift WorkShift { get; set; }

        // V1: bản chốt gốc; V2, V3: bản hiệu chỉnh.
        [Index("IX_ShiftSnapshot_Version", 2, IsUnique = true)]
        public int Version { get; set; }

        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }

        // Số giao dịch kho cuối cùng được tính vào báo cáo.
        public int OpeningTransactionId { get; set; }
        public int ClosingTransactionId { get; set; }

        public int TotalProducts { get; set; }
        public long TotalSystemQuantity { get; set; }
        public long? TotalActualQuantity { get; set; }

        [Column(TypeName = "decimal")]
        public decimal TotalStockValue { get; set; }

        [Required]
        [StringLength(128)]
        public string CreatedByUserId { get; set; }

        public DateTime CreatedAt { get; set; }

        [StringLength(1000)]
        public string Note { get; set; }

        // Phiên bản sửa phải tham chiếu đến phiên bản trước.
        public int? PreviousSnapshotId { get; set; }

        [ForeignKey("PreviousSnapshotId")]
        public virtual InventoryShiftSnapshot PreviousSnapshot
        {
            get;
            set;
        }

        [Timestamp]
        public byte[] RowVersion { get; set; }

        public virtual ICollection<InventoryShiftSnapshotDetail>
            Details
        { get; set; }

        public virtual ICollection<InventoryShiftAuditLog>
            AuditLogs
        { get; set; }
    }
}
