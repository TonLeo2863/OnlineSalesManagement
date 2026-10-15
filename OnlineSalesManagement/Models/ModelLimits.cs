namespace OnlineSalesManagement
{
    /// <summary>
    /// Hằng số dùng chung cho validation.
    /// Giá trị tiền tối đa nhỏ hơn nhiều so với giới hạn của decimal(18,2) để tránh tràn khi cộng dồn.
    /// Dùng kiểu double cho RangeAttribute để KHÔNG phụ thuộc culture vi-VN khi phân tích chuỗi số.
    /// </summary>
    public static class ModelLimits
    {
        public const double MaxMoney = 999999999999.99; // gần 1.000 tỷ đồng
    }
}
