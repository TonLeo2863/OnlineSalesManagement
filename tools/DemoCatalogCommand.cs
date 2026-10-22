using System;
using System.Data.Entity;
using System.Linq;
using OnlineSalesManagement.Models;
using OnlineSalesManagement.Services;
public static class DemoCatalogCommand
{
    public static int Main(string[] args)
    {
        try {
            Database.SetInitializer<ApplicationDbContext>(null); // Preview never creates or migrates a database.
            using (var db = new ApplicationDbContext()) {
                var existingCategories = db.Categories.AsNoTracking().ToList();
                var existingProducts = db.Products.AsNoTracking().ToList();
                var planned = DemoCatalog.Products();
                var names = planned.Select(p => p.Category.Name).Distinct().ToList();
                var duplicate = planned.Where(p => existingProducts.Any(e => string.Equals(e.Sku.Trim(), p.Sku, StringComparison.OrdinalIgnoreCase))).ToList();
                Console.WriteLine("DATABASE: " + db.Database.Connection.Database);
                Console.WriteLine("EXISTING categories=" + existingCategories.Count + " products=" + existingProducts.Count);
                Console.WriteLine("PLAN categories=" + names.Count + " products=" + planned.Count);
                Console.WriteLine("ADD categories=" + names.Count(n => !existingCategories.Any(e => string.Equals(e.Name.Trim(), n, StringComparison.OrdinalIgnoreCase))) + " products=" + (planned.Count - duplicate.Count));
                Console.WriteLine("DUPLICATE_SKU_SKIPPED=" + duplicate.Count);
                foreach(var name in names) {
                    var category=existingCategories.FirstOrDefault(c => string.Equals(c.Name.Trim(),name,StringComparison.OrdinalIgnoreCase));
                    Console.WriteLine((category==null ? "CATEGORY ADD " : "CATEGORY REUSE ") + name + (category!=null && !category.IsActive ? " [INACTIVE: kept unchanged]" : ""));
                }
                foreach(var p in planned) Console.WriteLine((duplicate.Any(d => d.Sku == p.Sku) ? "SKIP " : "ADD ") + p.Sku + " | " + p.Category.Name + " | " + p.Name + " | cost=" + p.CostPrice + " sale=" + p.SalePrice + " | stock=0");
                Console.WriteLine("Existing records, users, orders and inventory are NOT updated. Images: original local Content/product-placeholder.svg. Prices are DEMO values.");
                if(args.Length > 0 && args[0] == "Apply") Console.WriteLine("APPLIED products=" + DemoCatalog.Apply(db));
                else Console.WriteLine("PREVIEW ONLY: no writes, no migrations.");
            }
            return 0;
        } catch(Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }
}
