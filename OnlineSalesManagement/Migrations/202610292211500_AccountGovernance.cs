namespace OnlineSalesManagement.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AccountGovernance : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.AccountAdministrationAudits",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        TargetUserId = c.String(nullable: false, maxLength: 128),
                        ActorUserId = c.String(nullable: false, maxLength: 128),
                        Action = c.String(nullable: false, maxLength: 20),
                        Reason = c.String(nullable: false, maxLength: 500),
                        OccurredUtc = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            AddColumn("dbo.AspNetUsers", "IsAdministrativelySuspended", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            throw new NotSupportedException("Không tự động xóa audit/trạng thái tài khoản. Cần kế hoạch rollback dữ liệu được duyệt.");
        }
    }
}
