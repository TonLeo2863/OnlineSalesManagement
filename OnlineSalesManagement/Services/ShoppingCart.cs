using System;
using System.Collections.Generic;
using System.Linq;

namespace OnlineSalesManagement.Services
{
    // Server-owned state only. MVC's exclusive session lock serializes mutations.
    [Serializable]
    public sealed class ShoppingCart
    {
        public string OwnerId { get; private set; }
        public string Version { get; private set; }
        public string CheckoutToken { get; set; }
        public Dictionary<int, int> Items { get; private set; }
        public Dictionary<string, int> SubmittedOrders { get; private set; }

        public ShoppingCart(string ownerId)
        {
            OwnerId = ownerId;
            Items = new Dictionary<int, int>();
            SubmittedOrders = new Dictionary<string, int>();
            Changed();
        }
        public bool Matches(string version) { return !string.IsNullOrEmpty(version) && version == Version; }
        public void SetQuantity(int productId, int quantity)
        {
            if (productId <= 0 || quantity < 0 || quantity > 1000) throw new ArgumentException("Số lượng không hợp lệ.");
            if (quantity == 0) Items.Remove(productId);
            else
            {
                if (!Items.ContainsKey(productId) && Items.Count >= 100) throw new ArgumentException("Giỏ tối đa 100 sản phẩm.");
                Items[productId] = quantity;
            }
            Changed();
        }
        public bool ApplyReorder(string expectedVersion, IDictionary<int, int> quantities)
        {
            if (!Matches(expectedVersion)) return false;
            if (quantities.Any(p => p.Key <= 0 || p.Value < 1 || p.Value > 1000) || Items.Keys.Union(quantities.Keys).Count() > 100)
                throw new ArgumentException("Giỏ tối đa 100 mặt hàng, 1–1000 đơn vị mỗi mặt hàng.");
            if (quantities.Count == 0) return true;
            foreach (var item in quantities) Items[item.Key] = item.Value;
            Changed(); // One atomic cart version transition; invalidates any older checkout intent.
            return true;
        }
        public void Clear() { Items.Clear(); Changed(); }
        public void Submitted(string token, int orderId)
        {
            if (SubmittedOrders.Count >= 20) SubmittedOrders.Remove(SubmittedOrders.Keys.First());
            SubmittedOrders[token] = orderId;
            Clear();
        }
        private void Changed() { Version = Guid.NewGuid().ToString("N"); CheckoutToken = null; }
    }

    public sealed class CartLine
    {
        public int ProductId { get; set; }
        public string Name { get; set; }
        public string Sku { get; set; }
        public string ImageUrl { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public bool Available { get; set; }
        public decimal Total { get { return Price * Quantity; } }
        public bool Valid { get { return Available && Quantity > 0 && Quantity <= Stock && Price >= 0; } }
    }
    public sealed class CartViewModel
    {
        public string Version { get; set; }
        public IList<CartLine> Lines { get; set; }
        public decimal Total { get { return Lines.Sum(l => l.Total); } }
        public bool CanCheckout { get { return Lines.Count > 0 && Lines.All(l => l.Valid); } }
    }
}
