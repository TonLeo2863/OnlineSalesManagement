using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OnlineSalesManagement
{
    [Table("Products")]
    public class Product
    {
        public Product()
        {
            Unit = "Cái";
            IsActive = true;
            CreatedDate = DateTime.Now;
            PurchaseReceiptDetails = new HashSet<PurchaseReceiptDetail>();
            SalesOrderDetails = new HashSet<SalesOrderDetail>();
            InventoryTransactions = new HashSet<InventoryTransaction>();
        }

        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã sản phẩm.")]
        [StringLength(50, ErrorMessage = "Mã sản phẩm tối đa {1} ký tự.")]
        [Index("IX_Product_Sku", IsUnique = true)]
        [Display(Name = "Mã sản phẩm (SKU)")]
        public string Sku { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên sản phẩm.")]
        [StringLength(200, ErrorMessage = "Tên sản phẩm tối đa {1} ký tự.")]
        [Display(Name = "Tên sản phẩm")]
        public string Name { get; set; }

        [StringLength(1000, ErrorMessage = "Mô tả tối đa {1} ký tự.")]
        [Display(Name = "Mô tả")]
        public string Description { get; set; }

        [Display(Name = "Danh mục")]
        public int CategoryId { get; set; }

        [ForeignKey("CategoryId")]
        public virtual Category Category { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập đơn vị tính.")]
        [StringLength(30, ErrorMessage = "Đơn vị tính tối đa {1} ký tự.")]
        [Display(Name = "Đơn vị tính")]
        public string Unit { get; set; }

        /// <summary>Giá nhập gần nhất (giả định: không tính giá vốn bình quân).</summary>
        [Range(0, ModelLimits.MaxMoney, ErrorMessage = "Giá nhập phải lớn hơn hoặc bằng 0.")]
        [DisplayFormat(DataFormatString = "{0:N0}", ApplyFormatInEditMode = false)]
        [Display(Name = "Giá nhập")]
        public decimal CostPrice { get; set; }

        [Range(0, ModelLimits.MaxMoney, ErrorMessage = "Giá bán phải lớn hơn hoặc bằng 0.")]
        [DisplayFormat(DataFormatString = "{0:N0}", ApplyFormatInEditMode = false)]
        [Display(Name = "Giá bán")]
        public decimal SalePrice { get; set; }

        /// <summary>
        /// Số lượng tồn hiện tại. Chỉ được thay đổi qua IncreaseStock/DecreaseStock
        /// và luôn đi kèm một dòng InventoryTransaction trong cùng transaction.
        /// </summary>
        [Range(0, int.MaxValue, ErrorMessage = "Số lượng tồn kho không được âm.")]
        [Display(Name = "Tồn kho")]
        public int QuantityInStock { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Mức tồn tối thiểu không được âm.")]
        [Display(Name = "Mức tồn tối thiểu")]
        public int ReorderLevel { get; set; }

        [StringLength(500, ErrorMessage = "Đường dẫn ảnh tối đa {1} ký tự.")]
        [Display(Name = "Hình ảnh")]
        public string ImageUrl { get; set; }

        [Display(Name = "Đang kinh doanh")]
        public bool IsActive { get; set; }

        [Display(Name = "Ngày tạo")]
        public DateTime CreatedDate { get; set; }

        /// <summary>Kiểm soát xung đột khi hai người cùng cập nhật tồn một sản phẩm (optimistic concurrency).</summary>
        [Timestamp]
        public byte[] RowVersion { get; set; }

        public virtual ICollection<PurchaseReceiptDetail> PurchaseReceiptDetails { get; set; }
        public virtual ICollection<SalesOrderDetail> SalesOrderDetails { get; set; }
        public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; }

        // ----- Thuộc tính tính toán (không lưu DB; KHÔNG dùng trong truy vấn LINQ to Entities) -----

        [NotMapped]
        [DisplayFormat(DataFormatString = "{0:N0}", ApplyFormatInEditMode = false)]
        [Display(Name = "Giá trị tồn (theo giá nhập)")]
        public decimal StockValue
        {
            get { return QuantityInStock * CostPrice; }
        }

        [NotMapped]
        [Display(Name = "Sắp hết hàng")]
        public bool IsLowStock
        {
            get { return QuantityInStock <= ReorderLevel; }
        }

        // ----- Quy tắc nghiệp vụ về tồn kho -----

        /// <summary>Kiểm tra còn đủ hàng để bán. Dùng trước khi xác nhận xuất bán.</summary>
        public bool HasEnoughStock(int quantity)
        {
            return quantity > 0 && QuantityInStock >= quantity;
        }

        /// <summary>Tăng tồn (nhập kho, hoàn kho khi hủy đơn).</summary>
        public void IncreaseStock(int quantity)
        {
            if (quantity <= 0)
                throw new ArgumentOutOfRangeException("quantity", "Số lượng nhập phải lớn hơn 0.");

            checked
            {
                QuantityInStock += quantity;
            }
        }

        /// <summary>Giảm tồn (xuất bán, hủy phiếu nhập). Ném InvalidOperationException nếu không đủ hàng.</summary>
        public void DecreaseStock(int quantity)
        {
            if (quantity <= 0)
                throw new ArgumentOutOfRangeException("quantity", "Số lượng xuất phải lớn hơn 0.");

            if (QuantityInStock < quantity)
                throw new InvalidOperationException(
                    string.Format("Sản phẩm '{0}' không đủ tồn kho (còn {1}, cần {2}).", Name, QuantityInStock, quantity));

            QuantityInStock -= quantity;
        }
    }
}
