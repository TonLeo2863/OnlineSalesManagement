using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineSalesManagement
{
    [Table("Suppliers")]
    public class Supplier
    {
        public Supplier()
        {
            IsActive = true;
            PurchaseReceipts = new HashSet<PurchaseReceipt>();
        }

        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã nhà cung cấp.")]
        [StringLength(20, ErrorMessage = "Mã nhà cung cấp tối đa {1} ký tự.")]
        [Index("IX_Supplier_Code", IsUnique = true)]
        [Display(Name = "Mã nhà cung cấp")]
        public string Code { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên nhà cung cấp.")]
        [StringLength(200, ErrorMessage = "Tên nhà cung cấp tối đa {1} ký tự.")]
        [Display(Name = "Tên nhà cung cấp")]
        public string Name { get; set; }

        [StringLength(150, ErrorMessage = "Tên người liên hệ tối đa {1} ký tự.")]
        [Display(Name = "Người liên hệ")]
        public string ContactPerson { get; set; }

        [StringLength(256, ErrorMessage = "Email tối đa {1} ký tự.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [StringLength(20, ErrorMessage = "Số điện thoại tối đa {1} ký tự.")]
        [RegularExpression(@"^(\+84|0)\d{9,10}$", ErrorMessage = "Số điện thoại không hợp lệ (ví dụ: 0281234567).")]
        [Display(Name = "Số điện thoại")]
        public string PhoneNumber { get; set; }

        [StringLength(300, ErrorMessage = "Địa chỉ tối đa {1} ký tự.")]
        [Display(Name = "Địa chỉ")]
        public string Address { get; set; }

        [StringLength(20, ErrorMessage = "Mã số thuế tối đa {1} ký tự.")]
        [Display(Name = "Mã số thuế")]
        public string TaxCode { get; set; }

        [Display(Name = "Đang hợp tác")]
        public bool IsActive { get; set; }

        public virtual ICollection<PurchaseReceipt> PurchaseReceipts { get; set; }
    }
}
