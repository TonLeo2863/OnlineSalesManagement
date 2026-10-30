using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using OnlineSalesManagement.ViewModels;
namespace OnlineSalesManagement.Services
{
    // A deliberately small, scalar-only XLSX reader/writer. No Office process, formulas or links are evaluated.
    public static class CatalogWorkbook
    {
        public const int MaxBytes = 2 * 1024 * 1024;
        public const int MaxRows = 500;
        private static readonly XNamespace X = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        public static string[] Headers(string kind) { return kind == "products" ? new[] { "SKU", "Tên sản phẩm", "Danh mục", "Mô tả", "Đơn vị tính", "Giá nhập", "Giá bán", "Mức tồn tối thiểu", "Đường dẫn ảnh", "Đang kinh doanh" } : new[] { "Tên danh mục", "Mô tả", "Đang sử dụng" }; }
        public static void EnsureKind(string kind) { if (kind != "products" && kind != "categories") throw new ArgumentException("Loại nhập không hợp lệ."); }
        private static XDocument ReadXml(ZipArchiveEntry entry, long[] budget)
        {
            if (entry == null || entry.Length > 4 * 1024 * 1024) throw new ArgumentException("Cấu trúc Excel thiếu hoặc vượt giới hạn.");
            using (var source = entry.Open()) using (var buffer = new MemoryStream())
            {
                var bytes = new byte[8192]; int length;
                while ((length = source.Read(bytes, 0, bytes.Length)) != 0) { if ((budget[0] += length) > 16 * 1024 * 1024 || buffer.Length + length > 4 * 1024 * 1024) throw new ArgumentException("XML Excel quá lớn."); buffer.Write(bytes, 0, length); }
                buffer.Position = 0;
                using (var reader = XmlReader.Create(buffer, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4 * 1024 * 1024 })) return XDocument.Load(reader);
            }
        }
        public static CatalogImportSnapshot Read(Stream input, string kind)
        {
            EnsureKind(kind);
            using (var memory = new MemoryStream())
            {
                var block = new byte[8192]; int size;
                while ((size = input.Read(block, 0, block.Length)) > 0) { if (memory.Length + size > MaxBytes) throw new ArgumentException("File tối đa 2 MB."); memory.Write(block, 0, size); }
                memory.Position = 0;
                using (var zip = new ZipArchive(memory, ZipArchiveMode.Read, true))
                {
                    var budget = new long[1]; Func<ZipArchiveEntry, XDocument> read = entry => ReadXml(entry, budget);
                    if (zip.Entries.Count > 100 || zip.Entries.Sum(e => e.Length) > 16 * 1024 * 1024 || zip.Entries.GroupBy(e => e.FullName, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1)) throw new ArgumentException("Gói Excel vượt giới hạn hoặc có phần trùng.");
                    if (zip.Entries.Any(e => e.FullName.IndexOf("vba", StringComparison.OrdinalIgnoreCase) >= 0 || e.FullName.IndexOf("externalLinks", StringComparison.OrdinalIgnoreCase) >= 0 || e.FullName.Contains("..") || e.FullName.Contains("\\"))) throw new ArgumentException("Không chấp nhận macro/liên kết hoặc đường dẫn không hợp lệ.");
                    foreach (var rel in zip.Entries.Where(e => e.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)))
                        if (read(rel).Descendants().Any(e => string.Equals((string)e.Attribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Không chấp nhận liên kết bên ngoài.");
                    var types = read(zip.GetEntry("[Content_Types].xml"));
                    if (!types.Descendants().Any(e => (string)e.Attribute("ContentType") == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml") || types.ToString().IndexOf("macroEnabled", StringComparison.OrdinalIgnoreCase) >= 0) throw new ArgumentException("Chỉ nhận workbook .xlsx không macro.");
                    var workbook = read(zip.GetEntry("xl/workbook.xml"));
                    var sheets = workbook.Descendants(X + "sheet").ToList();
                    if (sheets.Count != 1 || zip.Entries.Count(e => e.FullName.StartsWith("xl/worksheets/") && e.FullName.EndsWith(".xml")) != 1) throw new ArgumentException("File mẫu chỉ dùng một worksheet.");
                    var relationId = (string)sheets[0].Attribute(XNamespace.Get("http://schemas.openxmlformats.org/officeDocument/2006/relationships") + "id");
                    var relationship = read(zip.GetEntry("xl/_rels/workbook.xml.rels")).Descendants().SingleOrDefault(e => (string)e.Attribute("Id") == relationId);
                    var target = relationship == null ? "" : (string)relationship.Attribute("Target");
                    if (string.IsNullOrEmpty(target) || target.Contains("..") || target.Contains(":")) throw new ArgumentException("Worksheet không hợp lệ.");
                    var path = target.StartsWith("/") ? target.TrimStart('/') : "xl/" + target;
                    var sheet = read(zip.GetEntry(path));
                    if (sheet.Descendants(X + "f").Any() || sheet.Descendants(X + "hyperlink").Any()) throw new ArgumentException("Không chấp nhận công thức hoặc hyperlink. Hãy dán giá trị thuần.");
                    var shared = zip.GetEntry("xl/sharedStrings.xml");
                    var strings = shared == null ? new List<string>() : read(shared).Descendants(X + "si").Select(e => string.Concat(e.Descendants(X + "t").Select(t => t.Value))).ToList();
                    if (strings.Count > 6000 || strings.Any(v => v.Length > 2000)) throw new ArgumentException("Bảng chuỗi Excel vượt giới hạn.");
                    var snapshot = new CatalogImportSnapshot { Kind = kind }; var headers = Headers(kind); int previousRow = 0;
                    foreach (var row in sheet.Descendants(X + "row"))
                    {
                        int number; if (!int.TryParse((string)row.Attribute("r") ?? (previousRow + 1).ToString(), out number) || number <= previousRow || number > MaxRows + 1) throw new ArgumentException("Tối đa 500 dòng dữ liệu, số dòng phải hợp lệ và tăng dần.");
                        previousRow = number; var values = Enumerable.Repeat("", headers.Length).ToArray(); var used = new HashSet<int>(); int position = 0;
                        foreach (var cell in row.Elements(X + "c"))
                        {
                            var address = (string)cell.Attribute("r"); int column = 0;
                            if (address == null) column = ++position;
                            else { foreach (char c in address.TakeWhile(char.IsLetter)) { if (c < 'A' || c > 'Z') throw new ArgumentException("Địa chỉ ô không hợp lệ."); column = checked(column * 26 + c - 'A' + 1); } position = column; }
                            var type = (string)cell.Attribute("t"); var value = (string)cell.Element(X + "v") ?? "";
                            if (type == "inlineStr") value = string.Concat(cell.Descendants(X + "t").Select(t => t.Value));
                            if (type == "s") { int index; if (!int.TryParse(value, out index) || index < 0 || index >= strings.Count) throw new ArgumentException("Chuỗi Excel không hợp lệ."); value = strings[index]; }
                            if (type == "e") throw new ArgumentException("File có ô lỗi Excel.");
                            if (value.Length > 2000 || column < 1 || !used.Add(column)) throw new ArgumentException("Ô quá dài hoặc cột trùng/không hợp lệ.");
                            if (column > headers.Length) { if (!string.IsNullOrWhiteSpace(value)) throw new ArgumentException("File có cột ngoài mẫu; không nhập QuantityInStock."); continue; }
                            values[column - 1] = value.Trim();
                        }
                        if (number == 1) { if (!values.SequenceEqual(headers, StringComparer.OrdinalIgnoreCase)) throw new ArgumentException("Tiêu đề cột không khớp file mẫu."); }
                        else if (values.Any(v => !string.IsNullOrWhiteSpace(v))) snapshot.Rows.Add(new CatalogImportRow { Number = number, Cells = values });
                    }
                    if (!snapshot.Rows.Any() || !sheet.Descendants(X + "row").Any(e => (string)e.Attribute("r") == "1")) throw new ArgumentException("Thiếu tiêu đề hoặc không có dòng dữ liệu.");
                    return snapshot;
                }
            }
        }
        public static byte[] Template(string kind, string categoryName = "Danh mục hiện có")
        {
            EnsureKind(kind);
            var rows = new List<string[]> { Headers(kind), kind == "products" ? new[] { "EXCEL-DEMO-001", "Sản phẩm minh họa", categoryName, "Dữ liệu demo, sửa hoặc xóa dòng này trước khi nhập", "Cái", "100000", "150000", "3", "~/Content/product-placeholder.svg", "TRUE" } : new[] { "Danh mục minh họa Excel", "Sửa hoặc xóa dòng ví dụ trước khi nhập", "TRUE" } };
            return WriteRows(kind, rows);
        }
        public static byte[] WriteRows(string kind, IEnumerable<string[]> rows)
        {
            EnsureKind(kind);
            using (var output = new MemoryStream())
            {
                using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
                {
                    Write(zip, "[Content_Types].xml", new XDocument(new XElement(XNamespace.Get("http://schemas.openxmlformats.org/package/2006/content-types") + "Types", new XElement(XNamespace.Get("http://schemas.openxmlformats.org/package/2006/content-types") + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")), new XElement(XNamespace.Get("http://schemas.openxmlformats.org/package/2006/content-types") + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")), new XElement(XNamespace.Get("http://schemas.openxmlformats.org/package/2006/content-types") + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")), new XElement(XNamespace.Get("http://schemas.openxmlformats.org/package/2006/content-types") + "Override", new XAttribute("PartName", "/xl/worksheets/sheet1.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")))));
                    var rel = XNamespace.Get("http://schemas.openxmlformats.org/package/2006/relationships");
                    Write(zip, "_rels/.rels", new XDocument(new XElement(rel + "Relationships", new XElement(rel + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"), new XAttribute("Target", "xl/workbook.xml")))));
                    Write(zip, "xl/workbook.xml", new XDocument(new XElement(X + "workbook", new XAttribute(XNamespace.Xmlns + "r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships"), new XElement(X + "sheets", new XElement(X + "sheet", new XAttribute("name", "Import"), new XAttribute("sheetId", 1), new XAttribute(XNamespace.Get("http://schemas.openxmlformats.org/officeDocument/2006/relationships") + "id", "rId1"))))));
                    Write(zip, "xl/_rels/workbook.xml.rels", new XDocument(new XElement(rel + "Relationships", new XElement(rel + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"), new XAttribute("Target", "worksheets/sheet1.xml")))));
                    int number = 0; var data = new XElement(X + "sheetData");
                    foreach (var row in rows) { number++; var element = new XElement(X + "row", new XAttribute("r", number)); for (int i = 0; i < row.Length; i++) element.Add(new XElement(X + "c", new XAttribute("r", ((char)('A' + i)).ToString() + number), new XAttribute("t", "inlineStr"), new XElement(X + "is", new XElement(X + "t", row[i] ?? "")))); data.Add(element); }
                    Write(zip, "xl/worksheets/sheet1.xml", new XDocument(new XElement(X + "worksheet", data)));
                }
                return output.ToArray();
            }
        }
        private static void Write(ZipArchive zip, string name, XDocument document) { using (var stream = zip.CreateEntry(name).Open()) document.Save(stream); }
    }
}
