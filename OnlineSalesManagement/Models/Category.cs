using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using OnlineSalesManagement.Models;


namespace OnlineSalesManagement
{
    [Table("Categories")]
    public class Category
    {
        public Category()
        {
            IsActive = true;
            Products = new HashSet<Product>();
        }

        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên danh mục.")]
        [StringLength(100, ErrorMessage = "Tên danh mục tối đa {1} ký tự.")]
        [Index("IX_Category_Name", IsUnique = true)]
        [Display(Name = "Tên danh mục")]
        public string Name { get; set; }

        [StringLength(500, ErrorMessage = "Mô tả tối đa {1} ký tự.")]
        [Display(Name = "Mô tả")]
        public string Description { get; set; }

        [Display(Name = "Đang sử dụng")]
        public bool IsActive { get; set; }

        public virtual ICollection<Product> Products { get; set; }
    }
}
