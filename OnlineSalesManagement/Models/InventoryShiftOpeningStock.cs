
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineSalesManagement
{
    [Table("InventoryShiftOpeningStocks")]
    public class InventoryShiftOpeningStock
    {
        [Key]
        public int Id { get; set; }

        [Index("IX_ShiftOpening_Product", 1, IsUnique = true)]
        public int WorkShiftId { get; set; }

        [ForeignKey("WorkShiftId")]
        public virtual WorkShift WorkShift { get; set; }

        [Index("IX_ShiftOpening_Product", 2, IsUnique = true)]
        public int ProductId { get; set; }

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }

        [Required]
        [StringLength(50)]
        public string ProductSku { get; set; }

        [Required]
        [StringLength(200)]
        public string ProductName { get; set; }

        [StringLength(30)]
        public string Unit { get; set; }

        public int OpeningQuantity { get; set; }

        [Column(TypeName = "decimal")]
        public decimal OpeningUnitCost { get; set; }

        public DateTime CapturedAt { get; set; }
    }
}
