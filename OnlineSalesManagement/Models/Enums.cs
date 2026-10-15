using System.ComponentModel.DataAnnotations;
using OnlineSalesManagement.Models;

namespace OnlineSalesManagement
{
    /// <summary>Trạng thái phiếu nhập kho. Chỉ trạng thái Confirmed làm tăng tồn kho.</summary>
    public enum ReceiptStatus
    {
        [Display(Name = "Nháp")]
        Draft = 0,

        [Display(Name = "Đã nhập kho")]
        Confirmed = 1,

        [Display(Name = "Đã hủy")]
        Cancelled = 2
    }

    /// <summary>
    /// Trạng thái đơn hàng bán.
    /// Pending: khách vừa đặt, CHƯA trừ tồn.
    /// Confirmed và Completed: ĐÃ trừ tồn (tính vào doanh thu).
    /// Cancelled: đơn bị hủy; nếu trước đó đã Confirmed thì phải hoàn tồn.
    /// </summary>
    public enum OrderStatus
    {
        [Display(Name = "Chờ xác nhận")]
        Pending = 0,

        [Display(Name = "Đã xác nhận")]
        Confirmed = 1,

        [Display(Name = "Hoàn tất")]
        Completed = 2,

        [Display(Name = "Đã hủy")]
        Cancelled = 3
    }

    /// <summary>Phương thức thanh toán (chỉ mô phỏng, không tích hợp cổng thanh toán thật).</summary>
    public enum PaymentMethod
    {
        [Display(Name = "Tiền mặt")]
        Cash = 0,

        [Display(Name = "Chuyển khoản")]
        BankTransfer = 1,

        [Display(Name = "Thanh toán khi nhận hàng")]
        CashOnDelivery = 2
    }

    /// <summary>Trạng thái thanh toán của đơn hàng (mô phỏng).</summary>
    public enum PaymentStatus
    {
        [Display(Name = "Chưa thanh toán")]
        Unpaid = 0,

        [Display(Name = "Đã thanh toán")]
        Paid = 1,

        [Display(Name = "Đã hoàn tiền")]
        Refunded = 2
    }

    /// <summary>Kênh bán hàng của đơn.</summary>
    public enum SalesChannel
    {
        [Display(Name = "Tại quầy")]
        Counter = 0,

        [Display(Name = "Trực tuyến")]
        Online = 1
    }

    /// <summary>
    /// Loại giao dịch kho. Dấu của InventoryTransaction.QuantityChange phải khớp loại:
    /// PurchaseReceipt (+), SalesOrder (-), PurchaseReceiptCancellation (-), SalesOrderCancellation (+), Adjustment (+ hoặc -).
    /// </summary>
    public enum InventoryTransactionType
    {
        [Display(Name = "Nhập kho")]
        PurchaseReceipt = 1,

        [Display(Name = "Xuất bán")]
        SalesOrder = 2,

        [Display(Name = "Hủy phiếu nhập")]
        PurchaseReceiptCancellation = 3,

        [Display(Name = "Hoàn kho do hủy đơn")]
        SalesOrderCancellation = 4,

        [Display(Name = "Điều chỉnh tồn kho")]
        Adjustment = 5
    }
}
