using System;
using System.Collections.Generic;
using System.Linq;
namespace OnlineSalesManagement.ViewModels
{
    [Serializable] public sealed class CatalogImportRow { public int Number { get; set; } public string[] Cells { get; set; } }
    [Serializable] public sealed class CatalogImportIssue { public int Row { get; set; } public string Column { get; set; } public string Message { get; set; } public bool Duplicate { get; set; } }
    [Serializable] public sealed class CatalogImportSnapshot
    {
        public string Token { get; set; }
        public string OwnerId { get; set; }
        public string Kind { get; set; }
        public DateTime ExpiresUtc { get; set; }
        public List<CatalogImportRow> Rows { get; set; } = new List<CatalogImportRow>();
        public List<CatalogImportIssue> Issues { get; set; } = new List<CatalogImportIssue>();
        public int ValidCount { get { return Rows.Count(r => !Issues.Any(i => i.Row == r.Number)); } }
        public int DuplicateCount { get { return Issues.Where(i => i.Duplicate).Select(i => i.Row).Distinct().Count(); } }
    }
    [Serializable] public sealed class CatalogImportResult { public string Kind { get; set; } public int Added { get; set; } }
}
