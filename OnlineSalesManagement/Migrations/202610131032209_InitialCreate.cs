namespace OnlineSalesManagement.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Categories",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 100),
                        Description = c.String(maxLength: 500),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.Name, unique: true, name: "IX_Category_Name");
            
            CreateTable(
                "dbo.Products",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Sku = c.String(nullable: false, maxLength: 50),
                        Name = c.String(nullable: false, maxLength: 200),
                        Description = c.String(maxLength: 1000),
                        CategoryId = c.Int(nullable: false),
                        Unit = c.String(nullable: false, maxLength: 30),
                        CostPrice = c.Decimal(nullable: false, precision: 18, scale: 2),
                        SalePrice = c.Decimal(nullable: false, precision: 18, scale: 2),
                        QuantityInStock = c.Int(nullable: false),
                        ReorderLevel = c.Int(nullable: false),
                        ImageUrl = c.String(maxLength: 500),
                        IsActive = c.Boolean(nullable: false),
                        CreatedDate = c.DateTime(nullable: false),
                        RowVersion = c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Categories", t => t.CategoryId)
                .Index(t => t.Sku, unique: true, name: "IX_Product_Sku")
                .Index(t => t.CategoryId);
            
            CreateTable(
                "dbo.InventoryTransactions",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ProductId = c.Int(nullable: false),
                        TransactionDate = c.DateTime(nullable: false),
                        TransactionType = c.Int(nullable: false),
                        QuantityChange = c.Int(nullable: false),
                        StockAfter = c.Int(nullable: false),
                        UnitPrice = c.Decimal(nullable: false, precision: 18, scale: 2),
                        PurchaseReceiptId = c.Int(),
                        SalesOrderId = c.Int(),
                        Note = c.String(maxLength: 500),
                        CreatedBy = c.String(maxLength: 256),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Products", t => t.ProductId)
                .ForeignKey("dbo.PurchaseReceipts", t => t.PurchaseReceiptId)
                .ForeignKey("dbo.SalesOrders", t => t.SalesOrderId)
                .Index(t => new { t.ProductId, t.TransactionDate }, name: "IX_InventoryTransaction_Product_Date")
                .Index(t => t.PurchaseReceiptId)
                .Index(t => t.SalesOrderId);
            
            CreateTable(
                "dbo.PurchaseReceipts",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ReceiptNumber = c.String(nullable: false, maxLength: 30),
                        ReceiptDate = c.DateTime(nullable: false),
                        SupplierId = c.Int(nullable: false),
                        EmployeeId = c.Int(nullable: false),
                        Status = c.Int(nullable: false),
                        Note = c.String(maxLength: 500),
                        TotalAmount = c.Decimal(nullable: false, precision: 18, scale: 2),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Employees", t => t.EmployeeId)
                .ForeignKey("dbo.Suppliers", t => t.SupplierId)
                .Index(t => t.ReceiptNumber, unique: true, name: "IX_PurchaseReceipt_ReceiptNumber")
                .Index(t => t.ReceiptDate, name: "IX_PurchaseReceipt_ReceiptDate")
                .Index(t => t.SupplierId)
                .Index(t => t.EmployeeId);
            
            CreateTable(
                "dbo.PurchaseReceiptDetails",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        PurchaseReceiptId = c.Int(nullable: false),
                        ProductId = c.Int(nullable: false),
                        Quantity = c.Int(nullable: false),
                        UnitCost = c.Decimal(nullable: false, precision: 18, scale: 2),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Products", t => t.ProductId)
                .ForeignKey("dbo.PurchaseReceipts", t => t.PurchaseReceiptId, cascadeDelete: true)
                .Index(t => new { t.PurchaseReceiptId, t.ProductId }, unique: true, name: "IX_PurchaseReceiptDetail_Receipt_Product");
            
            CreateTable(
                "dbo.Employees",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Code = c.String(nullable: false, maxLength: 20),
                        FullName = c.String(nullable: false, maxLength: 150),
                        Email = c.String(maxLength: 256),
                        PhoneNumber = c.String(maxLength: 20),
                        Address = c.String(maxLength: 300),
                        Position = c.String(maxLength: 100),
                        HireDate = c.DateTime(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                        UserId = c.String(maxLength: 128),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.AspNetUsers", t => t.UserId)
                .Index(t => t.Code, unique: true, name: "IX_Employee_Code")
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.SalesOrders",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        OrderNumber = c.String(nullable: false, maxLength: 30),
                        OrderDate = c.DateTime(nullable: false),
                        CustomerId = c.Int(nullable: false),
                        EmployeeId = c.Int(),
                        Channel = c.Int(nullable: false),
                        Status = c.Int(nullable: false),
                        PaymentMethod = c.Int(nullable: false),
                        PaymentStatus = c.Int(nullable: false),
                        ShippingAddress = c.String(maxLength: 300),
                        Note = c.String(maxLength: 500),
                        SubTotal = c.Decimal(nullable: false, precision: 18, scale: 2),
                        DiscountAmount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        TotalAmount = c.Decimal(nullable: false, precision: 18, scale: 2),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Customers", t => t.CustomerId)
                .ForeignKey("dbo.Employees", t => t.EmployeeId)
                .Index(t => t.OrderNumber, unique: true, name: "IX_SalesOrder_OrderNumber")
                .Index(t => t.OrderDate, name: "IX_SalesOrder_OrderDate")
                .Index(t => t.CustomerId)
                .Index(t => t.EmployeeId);
            
            CreateTable(
                "dbo.Customers",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Code = c.String(nullable: false, maxLength: 20),
                        FullName = c.String(nullable: false, maxLength: 150),
                        Email = c.String(maxLength: 256),
                        PhoneNumber = c.String(nullable: false, maxLength: 20),
                        Address = c.String(maxLength: 300),
                        IsActive = c.Boolean(nullable: false),
                        CreatedDate = c.DateTime(nullable: false),
                        UserId = c.String(maxLength: 128),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.AspNetUsers", t => t.UserId)
                .Index(t => t.Code, unique: true, name: "IX_Customer_Code")
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.AspNetUsers",
                c => new
                    {
                        Id = c.String(nullable: false, maxLength: 128),
                        Email = c.String(maxLength: 256),
                        EmailConfirmed = c.Boolean(nullable: false),
                        PasswordHash = c.String(),
                        SecurityStamp = c.String(),
                        PhoneNumber = c.String(),
                        PhoneNumberConfirmed = c.Boolean(nullable: false),
                        TwoFactorEnabled = c.Boolean(nullable: false),
                        LockoutEndDateUtc = c.DateTime(),
                        LockoutEnabled = c.Boolean(nullable: false),
                        AccessFailedCount = c.Int(nullable: false),
                        UserName = c.String(nullable: false, maxLength: 256),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.UserName, unique: true, name: "UserNameIndex");
            
            CreateTable(
                "dbo.AspNetUserClaims",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        UserId = c.String(nullable: false, maxLength: 128),
                        ClaimType = c.String(),
                        ClaimValue = c.String(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.AspNetUsers", t => t.UserId, cascadeDelete: true)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.AspNetUserLogins",
                c => new
                    {
                        LoginProvider = c.String(nullable: false, maxLength: 128),
                        ProviderKey = c.String(nullable: false, maxLength: 128),
                        UserId = c.String(nullable: false, maxLength: 128),
                    })
                .PrimaryKey(t => new { t.LoginProvider, t.ProviderKey, t.UserId })
                .ForeignKey("dbo.AspNetUsers", t => t.UserId, cascadeDelete: true)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.AspNetUserRoles",
                c => new
                    {
                        UserId = c.String(nullable: false, maxLength: 128),
                        RoleId = c.String(nullable: false, maxLength: 128),
                    })
                .PrimaryKey(t => new { t.UserId, t.RoleId })
                .ForeignKey("dbo.AspNetUsers", t => t.UserId, cascadeDelete: true)
                .ForeignKey("dbo.AspNetRoles", t => t.RoleId, cascadeDelete: true)
                .Index(t => t.UserId)
                .Index(t => t.RoleId);
            
            CreateTable(
                "dbo.SalesOrderDetails",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        SalesOrderId = c.Int(nullable: false),
                        ProductId = c.Int(nullable: false),
                        Quantity = c.Int(nullable: false),
                        UnitPrice = c.Decimal(nullable: false, precision: 18, scale: 2),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Products", t => t.ProductId)
                .ForeignKey("dbo.SalesOrders", t => t.SalesOrderId, cascadeDelete: true)
                .Index(t => new { t.SalesOrderId, t.ProductId }, unique: true, name: "IX_SalesOrderDetail_Order_Product");
            
            CreateTable(
                "dbo.Suppliers",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Code = c.String(nullable: false, maxLength: 20),
                        Name = c.String(nullable: false, maxLength: 200),
                        ContactPerson = c.String(maxLength: 150),
                        Email = c.String(maxLength: 256),
                        PhoneNumber = c.String(maxLength: 20),
                        Address = c.String(maxLength: 300),
                        TaxCode = c.String(maxLength: 20),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.Code, unique: true, name: "IX_Supplier_Code");
            
            CreateTable(
                "dbo.AspNetRoles",
                c => new
                    {
                        Id = c.String(nullable: false, maxLength: 128),
                        Name = c.String(nullable: false, maxLength: 256),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.Name, unique: true, name: "RoleNameIndex");
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.AspNetUserRoles", "RoleId", "dbo.AspNetRoles");
            DropForeignKey("dbo.InventoryTransactions", "SalesOrderId", "dbo.SalesOrders");
            DropForeignKey("dbo.InventoryTransactions", "PurchaseReceiptId", "dbo.PurchaseReceipts");
            DropForeignKey("dbo.PurchaseReceipts", "SupplierId", "dbo.Suppliers");
            DropForeignKey("dbo.PurchaseReceipts", "EmployeeId", "dbo.Employees");
            DropForeignKey("dbo.Employees", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.SalesOrders", "EmployeeId", "dbo.Employees");
            DropForeignKey("dbo.SalesOrderDetails", "SalesOrderId", "dbo.SalesOrders");
            DropForeignKey("dbo.SalesOrderDetails", "ProductId", "dbo.Products");
            DropForeignKey("dbo.SalesOrders", "CustomerId", "dbo.Customers");
            DropForeignKey("dbo.Customers", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.AspNetUserRoles", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.AspNetUserLogins", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.AspNetUserClaims", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.PurchaseReceiptDetails", "PurchaseReceiptId", "dbo.PurchaseReceipts");
            DropForeignKey("dbo.PurchaseReceiptDetails", "ProductId", "dbo.Products");
            DropForeignKey("dbo.InventoryTransactions", "ProductId", "dbo.Products");
            DropForeignKey("dbo.Products", "CategoryId", "dbo.Categories");
            DropIndex("dbo.AspNetRoles", "RoleNameIndex");
            DropIndex("dbo.Suppliers", "IX_Supplier_Code");
            DropIndex("dbo.SalesOrderDetails", "IX_SalesOrderDetail_Order_Product");
            DropIndex("dbo.AspNetUserRoles", new[] { "RoleId" });
            DropIndex("dbo.AspNetUserRoles", new[] { "UserId" });
            DropIndex("dbo.AspNetUserLogins", new[] { "UserId" });
            DropIndex("dbo.AspNetUserClaims", new[] { "UserId" });
            DropIndex("dbo.AspNetUsers", "UserNameIndex");
            DropIndex("dbo.Customers", new[] { "UserId" });
            DropIndex("dbo.Customers", "IX_Customer_Code");
            DropIndex("dbo.SalesOrders", new[] { "EmployeeId" });
            DropIndex("dbo.SalesOrders", new[] { "CustomerId" });
            DropIndex("dbo.SalesOrders", "IX_SalesOrder_OrderDate");
            DropIndex("dbo.SalesOrders", "IX_SalesOrder_OrderNumber");
            DropIndex("dbo.Employees", new[] { "UserId" });
            DropIndex("dbo.Employees", "IX_Employee_Code");
            DropIndex("dbo.PurchaseReceiptDetails", "IX_PurchaseReceiptDetail_Receipt_Product");
            DropIndex("dbo.PurchaseReceipts", new[] { "EmployeeId" });
            DropIndex("dbo.PurchaseReceipts", new[] { "SupplierId" });
            DropIndex("dbo.PurchaseReceipts", "IX_PurchaseReceipt_ReceiptDate");
            DropIndex("dbo.PurchaseReceipts", "IX_PurchaseReceipt_ReceiptNumber");
            DropIndex("dbo.InventoryTransactions", new[] { "SalesOrderId" });
            DropIndex("dbo.InventoryTransactions", new[] { "PurchaseReceiptId" });
            DropIndex("dbo.InventoryTransactions", "IX_InventoryTransaction_Product_Date");
            DropIndex("dbo.Products", new[] { "CategoryId" });
            DropIndex("dbo.Products", "IX_Product_Sku");
            DropIndex("dbo.Categories", "IX_Category_Name");
            DropTable("dbo.AspNetRoles");
            DropTable("dbo.Suppliers");
            DropTable("dbo.SalesOrderDetails");
            DropTable("dbo.AspNetUserRoles");
            DropTable("dbo.AspNetUserLogins");
            DropTable("dbo.AspNetUserClaims");
            DropTable("dbo.AspNetUsers");
            DropTable("dbo.Customers");
            DropTable("dbo.SalesOrders");
            DropTable("dbo.Employees");
            DropTable("dbo.PurchaseReceiptDetails");
            DropTable("dbo.PurchaseReceipts");
            DropTable("dbo.InventoryTransactions");
            DropTable("dbo.Products");
            DropTable("dbo.Categories");
        }
    }
}
