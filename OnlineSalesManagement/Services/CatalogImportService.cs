using System;
using System.Linq;
using System.Data;
using System.Data.Entity;
using System.Globalization;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using OnlineSalesManagement.Models;
using OnlineSalesManagement.ViewModels;
namespace OnlineSalesManagement.Services
{
    public static class CatalogImportService
    {
        private static decimal Decimal(string value) { decimal number; return decimal.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out number) ? number : -1; }
        private static bool Active(string value) { return string.IsNullOrEmpty(value) || value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase); }
        private static void Issue(CatalogImportSnapshot snapshot, CatalogImportRow row, int column, string message, bool duplicate = false) { snapshot.Issues.Add(new CatalogImportIssue { Row = row.Number, Column = CatalogWorkbook.Headers(snapshot.Kind)[column], Message = message, Duplicate = duplicate }); }
        private static Product Product(CatalogImportRow row, int categoryId)
        {
            int minimum; int.TryParse(row.Cells[7], NumberStyles.None, CultureInfo.InvariantCulture, out minimum);
            return new Product { Sku = row.Cells[0], Name = row.Cells[1], CategoryId = categoryId, Description = row.Cells[3], Unit = row.Cells[4], CostPrice = Decimal(row.Cells[5]), SalePrice = Decimal(row.Cells[6]), ReorderLevel = minimum, ImageUrl = row.Cells[8], IsActive = Active(row.Cells[9]) };
        }
        public static void Validate(ApplicationDbContext db, CatalogImportSnapshot snapshot)
        {
            CatalogWorkbook.EnsureKind(snapshot.Kind); snapshot.Issues.Clear();
            if (snapshot.Rows.Count < 1 || snapshot.Rows.Count > CatalogWorkbook.MaxRows) throw new ArgumentException("Số dòng ngoài giới hạn.");
            var categories = db.Categories.AsNoTracking().ToList();
            var skus = new HashSet<string>(db.Products.AsNoTracking().Select(p => p.Sku).ToList().Select(s => s.Trim()), StringComparer.OrdinalIgnoreCase);
            var repeated = new HashSet<string>(snapshot.Rows.GroupBy(row => row.Cells[0], StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key), StringComparer.OrdinalIgnoreCase);
            foreach (var row in snapshot.Rows)
            {
                var values = row.Cells; int activeColumn = snapshot.Kind == "products" ? 9 : 2;
                if (values.Length != CatalogWorkbook.Headers(snapshot.Kind).Length) throw new ArgumentException("Dòng không khớp mẫu.");
                if (!string.IsNullOrWhiteSpace(values[0]) && (repeated.Contains(values[0]) || (snapshot.Kind == "products" ? skus.Contains(values[0]) : categories.Any(c => string.Equals(c.Name.Trim(), values[0], StringComparison.OrdinalIgnoreCase))))) Issue(snapshot, row, 0, "Trùng trong file hoặc database; chỉ tạo mới, không ghi đè.", true);
                if (!string.IsNullOrEmpty(values[activeColumn]) && !new[] { "true", "false", "0", "1" }.Contains(values[activeColumn].ToLowerInvariant())) Issue(snapshot, row, activeColumn, "Chỉ nhập TRUE/FALSE hoặc 1/0.");
                object model;
                var fieldColumns = new Dictionary<string, int>();
                if (snapshot.Kind == "products")
                {
                    var category = categories.FirstOrDefault(c => string.Equals(c.Name.Trim(), values[2], StringComparison.OrdinalIgnoreCase));
                    if (category == null || !category.IsActive) Issue(snapshot, row, 2, "Danh mục chưa tồn tại hoặc đã ngừng sử dụng. Hãy nhập danh mục trước.");
                    for (int index = 5; index <= 6; index++) if (Decimal(values[index]) < 0 || decimal.Round(Decimal(values[index]), 2) != Decimal(values[index])) Issue(snapshot, row, index, "Giá không âm, tối đa 2 chữ số thập phân; dùng ô số hoặc dấu chấm thập phân.");
                    int minimum; if (!int.TryParse(values[7], NumberStyles.None, CultureInfo.InvariantCulture, out minimum)) Issue(snapshot, row, 7, "Mức tồn phải là số nguyên không âm.");
                    var image = values[8]; Uri uri;
                    if (!string.IsNullOrEmpty(image) && !((image.StartsWith("~/Content/") && !image.Contains("..") && !image.Contains("\\")) || (Uri.TryCreate(image, UriKind.Absolute, out uri) && uri.Scheme == "https"))) Issue(snapshot, row, 8, "Ảnh dùng ~/Content/... hoặc HTTPS; không dùng đường dẫn máy chủ/script.");
                    model = Product(row, category == null ? 0 : category.Id);
                    var properties = new[] { "Sku", "Name", "CategoryId", "Description", "Unit", "CostPrice", "SalePrice", "ReorderLevel", "ImageUrl", "IsActive" };
                    for (int i = 0; i < properties.Length; i++) fieldColumns[properties[i]] = i;
                }
                else { model = new Category { Name = values[0], Description = values[1], IsActive = Active(values[2]) }; fieldColumns["Name"] = 0; fieldColumns["Description"] = 1; fieldColumns["IsActive"] = 2; }
                var errors = new List<ValidationResult>();
                Validator.TryValidateObject(model, new ValidationContext(model), errors, true);
                foreach (var error in errors) foreach (var field in error.MemberNames.DefaultIfEmpty("Name")) Issue(snapshot, row, fieldColumns.ContainsKey(field) ? fieldColumns[field] : 0, error.ErrorMessage);
            }
        }
        public static int Confirm(ApplicationDbContext db, CatalogImportSnapshot snapshot)
        {
            using (var tx = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                Validate(db, snapshot); // Recheck current DB inside the write transaction; never trust a stale preview.
                if (snapshot.Issues.Any()) return 0;
                var categories = db.Categories.ToList();
                foreach (var row in snapshot.Rows)
                {
                    if (snapshot.Kind == "products") db.Products.Add(Product(row, categories.Single(c => string.Equals(c.Name.Trim(), row.Cells[2], StringComparison.OrdinalIgnoreCase)).Id));
                    else db.Categories.Add(new Category { Name = row.Cells[0], Description = row.Cells[1], IsActive = Active(row.Cells[2]) });
                }
                db.SaveChanges(); tx.Commit(); return snapshot.Rows.Count;
            }
        }
    }
}
