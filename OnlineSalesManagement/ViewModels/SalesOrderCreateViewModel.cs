
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using OnlineSalesManagement;

namespace OnlineSalesManagement.ViewModels
{
    public class SalesOrderLineViewModel
    {
        public int? ProductId { get; set; }

        public int? Quantity { get; set; }
    }

    public class SalesOrderCreateViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập số đơn hàng.")]
        [StringLength(30)]
        [Display(Name = "Số đơn hàng")]
        public string OrderNumber { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn khách hàng.")]
        [Display(Name = "Khách hàng")]
        public int? CustomerId { get; set; }

        [Display(Name = "Nhân viên")]
        public int? EmployeeId { get; set; }

        [Display(Name = "Kênh bán hàng")]
        public SalesChannel Channel { get; set; }

        [Display(Name = "Thanh toán")]
        public PaymentMethod PaymentMethod { get; set; }

        [StringLength(300)]
        [Display(Name = "Địa chỉ giao hàng")]
        public string ShippingAddress { get; set; }

        [StringLength(500)]
        [Display(Name = "Ghi chú")]
        public string Note { get; set; }

        [Range(0, 9999999999999999.99,
            ErrorMessage = "Giảm giá không hợp lệ.")]
        [Display(Name = "Giảm giá")]
        public decimal DiscountAmount { get; set; }

        public List<SalesOrderLineViewModel> Details
        { get; set; } = new List<SalesOrderLineViewModel>();

        public IEnumerable<SelectListItem> Customers
        { get; set; } = new List<SelectListItem>();

        public IEnumerable<SelectListItem> Employees
        { get; set; } = new List<SelectListItem>();

        public IEnumerable<SelectListItem> Products
        { get; set; } = new List<SelectListItem>();
    }
}
