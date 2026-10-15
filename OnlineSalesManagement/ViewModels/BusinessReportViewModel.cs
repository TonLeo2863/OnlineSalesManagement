
using System;
using System.Collections.Generic;

namespace OnlineSalesManagement.ViewModels
{
    public class BusinessReportRow
    {
        public int DocumentId { get; set; }
        public string DocumentNumber { get; set; }
        public DateTime Date { get; set; }
        public string PartnerName { get; set; }
        public string ProductSku { get; set; }
        public string ProductName { get; set; }
        public string Channel { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }
    }

    public class BusinessReportViewModel
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        public int DocumentCount { get; set; }
        public long TotalQuantity { get; set; }
        public decimal TotalAmount { get; set; }

        public decimal CounterRevenue { get; set; }
        public decimal OnlineRevenue { get; set; }

        public List<BusinessReportRow> Rows { get; set; }
            = new List<BusinessReportRow>();
    }
}
