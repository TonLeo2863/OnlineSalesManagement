namespace OnlineSalesManagement.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddShiftManagementTables : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.InventoryShiftAuditLogs",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        WorkShiftId = c.Int(nullable: false),
                        SnapshotId = c.Int(),
                        Action = c.Int(nullable: false),
                        PerformedByUserId = c.String(nullable: false, maxLength: 128),
                        ActionTime = c.DateTime(nullable: false),
                        FieldName = c.String(maxLength: 100),
                        OldValue = c.String(),
                        NewValue = c.String(),
                        Reason = c.String(maxLength: 1000),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.InventoryShiftSnapshots", t => t.SnapshotId)
                .ForeignKey("dbo.WorkShifts", t => t.WorkShiftId, cascadeDelete: true)
                .Index(t => t.WorkShiftId)
                .Index(t => t.SnapshotId);
            
            CreateTable(
                "dbo.InventoryShiftSnapshots",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        WorkShiftId = c.Int(nullable: false),
                        Version = c.Int(nullable: false),
                        PeriodStart = c.DateTime(nullable: false),
                        PeriodEnd = c.DateTime(nullable: false),
                        OpeningTransactionId = c.Int(nullable: false),
                        ClosingTransactionId = c.Int(nullable: false),
                        TotalProducts = c.Int(nullable: false),
                        TotalSystemQuantity = c.Long(nullable: false),
                        TotalActualQuantity = c.Long(),
                        TotalStockValue = c.Decimal(nullable: false, precision: 18, scale: 2),
                        CreatedByUserId = c.String(nullable: false, maxLength: 128),
                        CreatedAt = c.DateTime(nullable: false),
                        Note = c.String(maxLength: 1000),
                        PreviousSnapshotId = c.Int(),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.InventoryShiftSnapshots", t => t.PreviousSnapshotId)
                .ForeignKey("dbo.WorkShifts", t => t.WorkShiftId, cascadeDelete: true)
                .Index(t => new { t.WorkShiftId, t.Version }, unique: true, name: "IX_ShiftSnapshot_Version")
                .Index(t => t.PreviousSnapshotId);
            
            CreateTable(
                "dbo.InventoryShiftSnapshotDetails",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        SnapshotId = c.Int(nullable: false),
                        ProductId = c.Int(nullable: false),
                        ProductSku = c.String(nullable: false, maxLength: 50),
                        ProductName = c.String(nullable: false, maxLength: 200),
                        Unit = c.String(maxLength: 30),
                        OpeningQuantity = c.Int(nullable: false),
                        ImportedQuantity = c.Int(nullable: false),
                        ExportedQuantity = c.Int(nullable: false),
                        AdjustmentQuantity = c.Int(nullable: false),
                        ClosingSystemQuantity = c.Int(nullable: false),
                        ActualQuantity = c.Int(),
                        DifferenceQuantity = c.Int(),
                        UnitCost = c.Decimal(nullable: false, precision: 18, scale: 2),
                        StockValue = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Note = c.String(maxLength: 500),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Products", t => t.ProductId, cascadeDelete: true)
                .ForeignKey("dbo.InventoryShiftSnapshots", t => t.SnapshotId, cascadeDelete: true)
                .Index(t => new { t.SnapshotId, t.ProductId }, unique: true, name: "IX_Snapshot_Product");
            
            CreateTable(
                "dbo.WorkShifts",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 100),
                        ShiftTemplateId = c.Int(),
                        ScheduledStart = c.DateTime(nullable: false),
                        ScheduledEnd = c.DateTime(nullable: false),
                        ActualStart = c.DateTime(),
                        ActualEnd = c.DateTime(),
                        AssignedUserId = c.String(maxLength: 128),
                        OpenedByUserId = c.String(maxLength: 128),
                        ClosedByUserId = c.String(maxLength: 128),
                        Status = c.Int(nullable: false),
                        Note = c.String(maxLength: 500),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                        OpeningTransactionId = c.Int(),
                        ClosingTransactionId = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.ShiftTemplates", t => t.ShiftTemplateId)
                .Index(t => t.ShiftTemplateId);
            
            CreateTable(
                "dbo.ShiftTemplates",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 100),
                        StartTime = c.Time(nullable: false, precision: 7),
                        EndTime = c.Time(nullable: false, precision: 7),
                        EndsNextDay = c.Boolean(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                        IsSystemDefault = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.InventoryShiftOpeningStocks",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        WorkShiftId = c.Int(nullable: false),
                        ProductId = c.Int(nullable: false),
                        ProductSku = c.String(nullable: false, maxLength: 50),
                        ProductName = c.String(nullable: false, maxLength: 200),
                        Unit = c.String(maxLength: 30),
                        OpeningQuantity = c.Int(nullable: false),
                        OpeningUnitCost = c.Decimal(nullable: false, precision: 18, scale: 2),
                        CapturedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Products", t => t.ProductId)
                .ForeignKey("dbo.WorkShifts", t => t.WorkShiftId)
                .Index(t => new { t.WorkShiftId, t.ProductId }, unique: true, name: "IX_ShiftOpening_Product");
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.InventoryShiftOpeningStocks", "WorkShiftId", "dbo.WorkShifts");
            DropForeignKey("dbo.InventoryShiftOpeningStocks", "ProductId", "dbo.Products");
            DropForeignKey("dbo.InventoryShiftAuditLogs", "WorkShiftId", "dbo.WorkShifts");
            DropForeignKey("dbo.InventoryShiftSnapshots", "WorkShiftId", "dbo.WorkShifts");
            DropForeignKey("dbo.WorkShifts", "ShiftTemplateId", "dbo.ShiftTemplates");
            DropForeignKey("dbo.InventoryShiftSnapshots", "PreviousSnapshotId", "dbo.InventoryShiftSnapshots");
            DropForeignKey("dbo.InventoryShiftSnapshotDetails", "SnapshotId", "dbo.InventoryShiftSnapshots");
            DropForeignKey("dbo.InventoryShiftSnapshotDetails", "ProductId", "dbo.Products");
            DropForeignKey("dbo.InventoryShiftAuditLogs", "SnapshotId", "dbo.InventoryShiftSnapshots");
            DropIndex("dbo.InventoryShiftOpeningStocks", "IX_ShiftOpening_Product");
            DropIndex("dbo.WorkShifts", new[] { "ShiftTemplateId" });
            DropIndex("dbo.InventoryShiftSnapshotDetails", "IX_Snapshot_Product");
            DropIndex("dbo.InventoryShiftSnapshots", new[] { "PreviousSnapshotId" });
            DropIndex("dbo.InventoryShiftSnapshots", "IX_ShiftSnapshot_Version");
            DropIndex("dbo.InventoryShiftAuditLogs", new[] { "SnapshotId" });
            DropIndex("dbo.InventoryShiftAuditLogs", new[] { "WorkShiftId" });
            DropTable("dbo.InventoryShiftOpeningStocks");
            DropTable("dbo.ShiftTemplates");
            DropTable("dbo.WorkShifts");
            DropTable("dbo.InventoryShiftSnapshotDetails");
            DropTable("dbo.InventoryShiftSnapshots");
            DropTable("dbo.InventoryShiftAuditLogs");
        }
    }
}
