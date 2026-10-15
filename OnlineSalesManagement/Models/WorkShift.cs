
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineSalesManagement
{
    public enum WorkShiftStatus
    {
        Scheduled = 0,
        Open = 1,
        Closed = 2,
        Cancelled = 3
    }

    [Table("WorkShifts")]
    public class WorkShift
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        // Null nếu ca được cấu hình hoàn toàn tùy chỉnh.
        public int? ShiftTemplateId { get; set; }

        [ForeignKey("ShiftTemplateId")]
        public virtual ShiftTemplate ShiftTemplate { get; set; }

        [Required]
        public DateTime ScheduledStart { get; set; }

        [Required]
        public DateTime ScheduledEnd { get; set; }

        public DateTime? ActualStart { get; set; }

        public DateTime? ActualEnd { get; set; }

        [StringLength(128)]
        public string AssignedUserId { get; set; }

        [StringLength(128)]
        public string OpenedByUserId { get; set; }

        [StringLength(128)]
        public string ClosedByUserId { get; set; }

        public WorkShiftStatus Status { get; set; }
            = WorkShiftStatus.Scheduled;

        [StringLength(500)]
        public string Note { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; }
    }
}
