
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineSalesManagement
{
    public enum InventoryShiftAuditAction
    {
        Created = 1,
        Closed = 2,
        CorrectionRequested = 3,
        CorrectionApproved = 4,
        CorrectionRejected = 5,
        NoteUpdated = 6,
        Opened = 7
    }

    [Table("InventoryShiftAuditLogs")]
    public class InventoryShiftAuditLog
    {
        public InventoryShiftAuditLog()
        {
            ActionTime = DateTime.Now;
        }

        [Key]
        public int Id { get; set; }

        public int WorkShiftId { get; set; }

        [ForeignKey("WorkShiftId")]
        public virtual WorkShift WorkShift { get; set; }

        public int? SnapshotId { get; set; }

        [ForeignKey("SnapshotId")]
        public virtual InventoryShiftSnapshot Snapshot { get; set; }

        public InventoryShiftAuditAction Action { get; set; }

        [Required]
        [StringLength(128)]
        public string PerformedByUserId { get; set; }

        public DateTime ActionTime { get; set; }

        [StringLength(100)]
        public string FieldName { get; set; }

        // Chuỗi được lưu bởi Service, không nhận trực tiếp
        // từ dữ liệu tự do do client gửi lên.
        public string OldValue { get; set; }
        public string NewValue { get; set; }

        [StringLength(1000)]
        public string Reason { get; set; }
    }
}
