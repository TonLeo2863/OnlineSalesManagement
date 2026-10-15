
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineSalesManagement
{
    [Table("InventoryShiftSnapshotDetails")]
    public class InventoryShiftSnapshotDetail
    {
        [Key]
        public int Id { get; set; }

        [Index("IX_Snapshot_Product", 1, IsUnique = true)]
        public int SnapshotId { get; set; }

        [ForeignKey("SnapshotId")]
        public virtual InventoryShiftSnapshot Snapshot { get; set; }

        [Index("IX_Snapshot_Product", 2, IsUnique = true)]
        public int ProductId { get; set; }

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }

        // Lưu lại thông tin để xem báo cáo lịch sử.
        [Required]
        [StringLength(50)]
        public string ProductSku { get; set; }

        [Required]
        [StringLength(200)]
        public string ProductName { get; set; }

        [StringLength(30)]
        public string Unit { get; set; }

        public int OpeningQuantity { get; set; }

        public int ImportedQuantity { get; set; }

        public int ExportedQuantity { get; set; }

        // Bao gồm các giao dịch điều chỉnh/hoàn kho
        // nếu không xếp vào nhập và xuất.
        public int AdjustmentQuantity { get; set; }

        public int ClosingSystemQuantity { get; set; }

        // Null khi chưa kiểm đếm thực tế.
        public int? ActualQuantity { get; set; }

        // ActualQuantity - ClosingSystemQuantity.
        public int? DifferenceQuantity { get; set; }

        [Column(TypeName = "decimal")]
        public decimal UnitCost { get; set; }

        [Column(TypeName = "decimal")]
        public decimal StockValue { get; set; }

        [StringLength(500)]
        public string Note { get; set; }
    }
}
