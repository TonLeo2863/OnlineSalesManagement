
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineSalesManagement
{
    [Table("ShiftTemplates")]
    public class ShiftTemplate
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        public bool EndsNextDay { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsSystemDefault { get; set; }
    }
}
