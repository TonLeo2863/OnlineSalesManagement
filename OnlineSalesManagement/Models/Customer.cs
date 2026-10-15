using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using OnlineSalesManagement.Models;


namespace OnlineSalesManagement
{
    [Table("Customers")]
    public class Customer
    {
        public Customer()
        {
            IsActive = true;
            CreatedDate = DateTime.Now;
            SalesOrders = new HashSet<SalesOrder>();
        }

        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã khách hàng.")]
        [StringLength(20, ErrorMessage = "Mã khách hàng tối đa {1} ký tự.")]
        [Index("IX_Customer_Code", IsUnique = true)]
        [Display(Name = "Mã khách hàng")]
        public string Code { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên khách hàng.")]
        [StringLength(150, ErrorMessage = "Họ tên tối đa {1} ký tự.")]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; }

        [StringLength(256, ErrorMessage = "Email tối đa {1} ký tự.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
        [StringLength(20, ErrorMessage = "Số điện thoại tối đa {1} ký tự.")]
        [RegularExpression(@"^(\+84|0)\d{9,10}$", ErrorMessage = "Số điện thoại không hợp lệ (ví dụ: 0901234567).")]
        [Display(Name = "Số điện thoại")]
        public string PhoneNumber { get; set; }

        [StringLength(300, ErrorMessage = "Địa chỉ tối đa {1} ký tự.")]
        [Display(Name = "Địa chỉ")]
        public string Address { get; set; }

        [Display(Name = "Đang hoạt động")]
        public bool IsActive { get; set; }

        [Display(Name = "Ngày tạo")]
        public DateTime CreatedDate { get; set; }

        /// <summary>
        /// Giả định: liên kết tùy chọn tới tài khoản đăng nhập (AspNetUsers.Id) cho kênh bán hàng
        /// của khách trong tương lai. Có thể để null (khách mua tại quầy, khách lẻ).
        /// </summary>
        [StringLength(128)]
        public string UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }

        public virtual ICollection<SalesOrder> SalesOrders { get; set; }
    }
}
