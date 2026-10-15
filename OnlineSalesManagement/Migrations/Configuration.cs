namespace OnlineSalesManagement.Migrations
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Migrations;
    using System.Linq;

    internal sealed class Configuration : DbMigrationsConfiguration<OnlineSalesManagement.Models.ApplicationDbContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = false;
        }


        protected override void Seed(
            OnlineSalesManagement.Models.ApplicationDbContext context)
        {
            OnlineSalesManagement.SampleDataSeeder.Seed(context);
        }
    }
}
