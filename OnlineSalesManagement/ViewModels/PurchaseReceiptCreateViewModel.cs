
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace OnlineSalesManagement.ViewModels
{
    public class PurchaseReceiptLineViewModel
    {
        public int? ProductId { get; set; }

        public int? Quantity { get; set; }

        public decimal? UnitCost { get; set; }
    }

    public class PurchaseReceiptCreateViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập số phiếu.")]
        [StringLength(30)]
        [Display(Name = "Số phiếu")]
        public string ReceiptNumber { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn nhà cung cấp.")]
        [Display(Name = "Nhà cung cấp")]
        public int? SupplierId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn nhân viên.")]
        [Display(Name = "Nhân viên")]
        public int? EmployeeId { get; set; }

        [StringLength(500)]
        [Display(Name = "Ghi chú")]
        public string Note { get; set; }

        public List<PurchaseReceiptLineViewModel> Details
        { get; set; } =
            new List<PurchaseReceiptLineViewModel>();

        public IEnumerable<SelectListItem> Suppliers
        { get; set; } = new List<SelectListItem>();

        public IEnumerable<SelectListItem> Employees
        { get; set; } = new List<SelectListItem>();

        public IEnumerable<SelectListItem> Products
        { get; set; } = new List<SelectListItem>();
    }
}

