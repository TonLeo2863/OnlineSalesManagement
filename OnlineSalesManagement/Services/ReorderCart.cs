using System;
using System.Linq;
using System.Collections.Generic;
namespace OnlineSalesManagement.Services
{
    public sealed class ReorderResult
    {
        public int AddedUnits { get; set; }
        public List<string> Notices { get; set; } = new List<string>();
    }
    public static class ReorderCart
    {
        public static ReorderResult Add(ShoppingCart cart, SalesOrder order, string version)
        {
            if (order.Customer == null || order.Customer.UserId != cart.OwnerId) throw new UnauthorizedAccessException();
            var result = new ReorderResult();
            if (!cart.Matches(version)) { result.Notices.Add("Giỏ đã thay đổi hoặc yêu cầu đã xử lý. Hãy kiểm tra giỏ trước khi mua lại."); return result; }
            var plan = new Dictionary<int, int>(); int newLines = 0;
            foreach (var group in order.Details.GroupBy(d => d.ProductId))
            {
                var product = group.First().Product; var name = product == null ? "Sản phẩm #" + group.Key : product.Name;
                if (product == null || !product.IsActive || product.Category == null || !product.Category.IsActive || product.SalePrice < 0)
                { result.Notices.Add("Không thêm " + name + ": sản phẩm/danh mục ngừng bán hoặc giá không hợp lệ."); continue; }
                int existing; cart.Items.TryGetValue(product.Id, out existing);
                if (!cart.Items.ContainsKey(product.Id) && cart.Items.Count + newLines >= 100)
                { result.Notices.Add("Chưa thêm " + name + ": giỏ đã đạt giới hạn mặt hàng."); continue; }
                var requested = group.Sum(d => (long)d.Quantity);
                var available = Math.Max(0, Math.Min(1000 - existing, product.QuantityInStock - existing));
                var incoming = (int)Math.Min(Math.Max(0, requested), available);
                if (incoming == 0) { result.Notices.Add("Không thêm " + name + ": hết hàng hoặc số lượng trong giỏ đã đạt tồn/giới hạn."); continue; }
                plan.Add(product.Id, existing + incoming); if (!cart.Items.ContainsKey(product.Id)) newLines++;
                result.AddedUnits += incoming;
                if (incoming < requested) result.Notices.Add(name + ": chỉ thêm được " + incoming + "/" + requested + " theo tồn hiện tại.");
            }
            if (!cart.ApplyReorder(version, plan)) { result.AddedUnits = 0; result.Notices.Clear(); result.Notices.Add("Giỏ vừa thay đổi. Chưa thêm sản phẩm, hãy thử lại từ chi tiết đơn."); }
            return result;
        }
    }
}
