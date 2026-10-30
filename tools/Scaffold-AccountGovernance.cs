using System;
using System.IO;
using System.Resources;
using System.Linq;
using System.Configuration;
using System.Data.SqlClient;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Migrations;
using System.Data.Entity.Migrations.Design;
using System.Data.Entity.Migrations.Infrastructure;
using OnlineSalesManagement.Models;
public static class ScaffoldAccountGovernance
{
 public static int Main(string[] args) {
  var connection = new SqlConnectionStringBuilder(ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString);
  if(!connection.InitialCatalog.StartsWith("Dacn05_Test_",StringComparison.Ordinal)) throw new Exception("Migration tooling refuses real database.");
  var config=(DbMigrationsConfiguration)Activator.CreateInstance(typeof(ApplicationDbContext).Assembly.GetType("OnlineSalesManagement.Migrations.Configuration",true),true);
  config.TargetDatabase=new DbConnectionInfo(connection.ConnectionString,"System.Data.SqlClient");
  if(args[0]=="Scaffold") {
   // Bring only the isolated scaffolding database to the previous explicit migration.
   new DbMigrator(config).Update("202610151148216_AddShiftManagementTables");
   var scaffold=new MigrationScaffolder(config){Namespace="OnlineSalesManagement.Migrations"};
   var migration=scaffold.Scaffold("AccountGovernance");
   var dir=args[1];Directory.CreateDirectory(dir);
   File.WriteAllText(Path.Combine(dir,migration.MigrationId+".cs"),migration.UserCode,new System.Text.UTF8Encoding(true));
   File.WriteAllText(Path.Combine(dir,migration.MigrationId+".Designer.cs"),migration.DesignerCode,new System.Text.UTF8Encoding(true));
   using(var writer=new ResXResourceWriter(Path.Combine(dir,migration.MigrationId+".resx")))foreach(var resource in migration.Resources)writer.AddResource(resource.Key,resource.Value);
   Console.WriteLine("SCAFFOLDED: "+migration.MigrationId);
  } else {
   var migrator=new DbMigrator(config);
   var ids=migrator.GetLocalMigrations().OrderBy(x=>x).ToList();
   var script=new MigratorScriptingDecorator(migrator).ScriptUpdate(ids[ids.Count-2],ids.Last());
   File.WriteAllText(args[1],script,new System.Text.UTF8Encoding(true));
   Console.WriteLine("PREVIEW ONLY: "+args[1]);
  }
  return 0;
 }
}
