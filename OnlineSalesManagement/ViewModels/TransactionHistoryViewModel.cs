using System;
using System.Collections.Generic;
namespace OnlineSalesManagement.ViewModels
{
    public sealed class TransactionHistoryRow
    {
        public int Id { get; set; }
        public string Number { get; set; }
        public DateTime Date { get; set; }
        public int Status { get; set; }
        public int Channel { get; set; }
        public string Partner { get; set; }
        public decimal Amount { get; set; }
    }
    public sealed class TransactionHistoryViewModel
    {
        public int OwnerId { get; set; }
        public string Title { get; set; }
        public string Kind { get; set; }
        public string PartnerTitle { get; set; }
        public string PageParameter { get; set; }
        public int Page { get; set; }
        public int PageCount { get; set; }
        public int TotalCount { get; set; }
        public decimal ConfirmedAmount { get; set; }
        public List<TransactionHistoryRow> Rows { get; set; }
    }
}
