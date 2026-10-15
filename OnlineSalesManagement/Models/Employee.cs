using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using OnlineSalesManagement.Models;


namespace OnlineSalesManagement
{
    [Table("Employees")]
    public class Employee
    {
        public Employee()
        {
            IsActive = true;
            HireDate = DateTime.Today;
            PurchaseReceipts = new HashSet<PurchaseReceipt>();
            SalesOrders = new HashSet<SalesOrder>();
        }

        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã nhân viên.")]
        [StringLength(20, ErrorMessage = "Mã nhân viên tối đa {1} ký tự.")]
        [Index("IX_Employee_Code", IsUnique = true)]
        [Display(Name = "Mã nhân viên")]
        public string Code { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên nhân viên.")]
        [StringLength(150, ErrorMessage = "Họ tên tối đa {1} ký tự.")]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; }

        [StringLength(256, ErrorMessage = "Email tối đa {1} ký tự.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [StringLength(20, ErrorMessage = "Số điện thoại tối đa {1} ký tự.")]
        [RegularExpression(@"^(\+84|0)\d{9,10}$", ErrorMessage = "Số điện thoại không hợp lệ (ví dụ: 0901234567).")]
        [Display(Name = "Số điện thoại")]
        public string PhoneNumber { get; set; }

        [StringLength(300, ErrorMessage = "Địa chỉ tối đa {1} ký tự.")]
        [Display(Name = "Địa chỉ")]
        public string Address { get; set; }

        [StringLength(100, ErrorMessage = "Chức vụ tối đa {1} ký tự.")]
        [Display(Name = "Chức vụ")]
        public string Position { get; set; }

        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:dd/MM/yyyy}", ApplyFormatInEditMode = true)]
        [Display(Name = "Ngày vào làm")]
        public DateTime HireDate { get; set; }

        [Display(Name = "Đang làm việc")]
        public bool IsActive { get; set; }

        /// <summary>
        /// Giả định: liên kết tùy chọn tới tài khoản đăng nhập (AspNetUsers.Id)
        /// để biết nhân viên nào đang thao tác. Có thể để null.
        /// </summary>
        [StringLength(128)]
        public string UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }

        public virtual ICollection<PurchaseReceipt> PurchaseReceipts { get; set; }
        public virtual ICollection<SalesOrder> SalesOrders { get; set; }
    }
}
