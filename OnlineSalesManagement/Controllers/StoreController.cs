using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using OnlineSalesManagement.Models;
using OnlineSalesManagement.Services;

namespace OnlineSalesManagement.Controllers
{
    public class OnlineCheckoutModel
    {
        public string Token { get; set; }
        public CartViewModel Cart { get; set; }
        [Required, StringLength(150)] public string FullName { get; set; }
        [Required, RegularExpression(@"^(\+84|0)\d{9,10}$")] public string PhoneNumber { get; set; }
        [Required, StringLength(300)] public string ShippingAddress { get; set; }
        [StringLength(500)] public string Note { get; set; }
    }
    [Authorize]
    [OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
    public class StoreController : Controller
    {
        private readonly ApplicationDbContext db = new ApplicationDbContext();
        [AllowAnonymous]
        public ActionResult Index(string q, int? categoryId = null)
        {
            var products = db.Products.AsNoTracking().Include(p => p.Category).Where(p => p.IsActive && p.Category.IsActive);
            if (categoryId.HasValue) products = products.Where(p => p.CategoryId == categoryId.Value);
            ViewBag.StoreCategories = db.Categories.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Name).ToList();
            ViewBag.MenuProducts = db.Products.AsNoTracking().Where(p => p.IsActive && p.Category.IsActive)
                .Select(p => new MenuProduct { CategoryId = p.CategoryId, Name = p.Name, Sku = p.Sku }).ToList();
            if (!string.IsNullOrWhiteSpace(q)) products = products.Where(p => p.Name.Contains(q) || p.Sku.Contains(q));
            if (User.Identity.IsAuthenticated && !string.IsNullOrWhiteSpace(User.Identity.GetUserId())) ViewBag.CartVersion = CurrentCart().Version;
            return View(products.OrderBy(p => p.Name).Take(200).ToList());
        }
        private ShoppingCart CurrentCart()
        {
            var uid = User.Identity.GetUserId();
            if (string.IsNullOrWhiteSpace(uid)) throw new InvalidOperationException("Thiếu tài khoản đăng nhập.");
            var key = "ShoppingCart:" + uid;
            var cart = Session[key] as ShoppingCart;
            if (cart == null || cart.OwnerId != uid) Session[key] = cart = new ShoppingCart(uid);
            return cart;
        }
        private CartViewModel ReadCart(ShoppingCart cart)
        {
            var ids = cart.Items.Keys.ToList();
            var products = db.Products.AsNoTracking().Include(p => p.Category)
                .Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id);
            return new CartViewModel { Version = cart.Version, Lines = cart.Items.Select(item => {
                Product p;
                products.TryGetValue(item.Key, out p);
                return new CartLine { ProductId = item.Key, Quantity = item.Value,
                    Name = p == null ? "Sản phẩm không còn tồn tại" : p.Name,
                    Sku = p == null ? "" : p.Sku, Price = p == null ? 0 : p.SalePrice,
                    ImageUrl = p == null ? null : p.ImageUrl,
                    Stock = p == null ? 0 : p.QuantityInStock,
                    Available = p != null && p.IsActive && p.Category != null && p.Category.IsActive };
            }).ToList() };
        }
        public ActionResult Cart()
        {
            if (string.IsNullOrWhiteSpace(User.Identity.GetUserId())) return new HttpUnauthorizedResult();
            return View(ReadCart(CurrentCart()));
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult AddToCart(int productId, int quantity, string version)
        {
            return ChangeCart(productId, quantity, version, true);
        }
        [AllowAnonymous, HttpGet]
        public ActionResult ProductDetails(int id)
        {
            var product = db.Products.AsNoTracking().Include(p => p.Category)
                .SingleOrDefault(p => p.Id == id && p.IsActive && p.Category.IsActive);
            if (product == null)
            {
                Response.StatusCode = 404;
                Response.TrySkipIisCustomErrors = true;
                return Json(new { success = false, message = "Sản phẩm không còn kinh doanh hoặc không tồn tại." }, JsonRequestBehavior.AllowGet);
            }
            var signedIn = User.Identity.IsAuthenticated && !string.IsNullOrWhiteSpace(User.Identity.GetUserId());
            return Json(new { success = true, product = new {
                id = product.Id, name = product.Name, sku = product.Sku, category = product.Category.Name,
                unit = product.Unit, description = product.Description, price = product.SalePrice.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) + " ₫",
                stock = product.QuantityInStock, imageUrl = StorePresentation.ImageUrl(Url, product.ImageUrl)
            }, signedIn, version = signedIn ? CurrentCart().Version : null }, JsonRequestBehavior.AllowGet);
        }
        private ActionResult CartResponse(bool success, string message, int status = 200, string productName = null)
        {
            if (Request.IsAjaxRequest())
            {
                var cart = CurrentCart();
                Response.StatusCode = status;
                Response.TrySkipIisCustomErrors = true;
                return Json(new { success, message, count = cart.Items.Values.Sum(), version = cart.Version, productName });
            }
            if (!success)
            {
                if (status == 400) return new HttpStatusCodeResult(400, "Invalid cart input");
                TempData["CartError"] = message;
            }
            return RedirectToAction("Cart");
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult UpdateCart(int productId, int quantity, string version)
        {
            return ChangeCart(productId, quantity, version, false);
        }
        private ActionResult ChangeCart(int productId, int quantity, string version, bool add)
        {
            if (string.IsNullOrWhiteSpace(User.Identity.GetUserId())) return new HttpUnauthorizedResult();
            var cart = CurrentCart();
            if (!cart.Matches(version)) return CartResponse(false, "Giỏ đã thay đổi hoặc yêu cầu đã xử lý. Vui lòng kiểm tra giỏ rồi thử lại.", 409);
            if (!ModelState.IsValid || productId <= 0 || quantity < (add ? 1 : 0) || quantity > 1000)
                return CartResponse(false, "Số lượng sản phẩm không hợp lệ.", 400);
            int old;
            cart.Items.TryGetValue(productId, out old);
            if (!add && old == 0) return CartResponse(false, "Sản phẩm không có trong giỏ.", 400);
            var desired = add ? (long)old + quantity : quantity;
            if (desired > 1000) return CartResponse(false, "Tối đa 1000 đơn vị mỗi sản phẩm.", 409);
            string productName = null;
            if (desired > 0)
            {
                var product = db.Products.AsNoTracking().SingleOrDefault(p => p.Id == productId && p.IsActive && p.Category.IsActive);
                if (product == null || product.SalePrice < 0 || desired > product.QuantityInStock)
                    return CartResponse(false, "Sản phẩm đã ngừng bán hoặc số lượng vượt số lượng còn lại.", 409);
                productName = product.Name;
            }
            try { cart.SetQuantity(productId, (int)desired); }
            catch (ArgumentException ex) { return CartResponse(false, ex.Message, 409); }
            return CartResponse(true, add ? "Đã thêm " + productName + " vào giỏ hàng" : "Đã cập nhật giỏ hàng.", 200, productName);
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ClearCart(string version)
        {
            if (string.IsNullOrWhiteSpace(User.Identity.GetUserId())) return new HttpUnauthorizedResult();
            var cart = CurrentCart();
            if (cart.Matches(version)) cart.Clear();
            else TempData["CartError"] = "Giỏ đã thay đổi. Vui lòng kiểm tra lại.";
            return RedirectToAction("Cart");
        }
        public ActionResult Checkout(int? id = null)
        {
            var uid = User.Identity.GetUserId();
            if (string.IsNullOrWhiteSpace(uid)) return new HttpUnauthorizedResult();
            var cart = CurrentCart();
            var summary = ReadCart(cart);
            if (!summary.CanCheckout) { TempData["CartError"] = "Vui lòng kiểm tra sản phẩm và tồn kho trong giỏ."; return RedirectToAction("Cart"); }
            if (cart.CheckoutToken == null) cart.CheckoutToken = "WEB" + Guid.NewGuid().ToString("N").Substring(0, 26);
            var customer = db.Customers.FirstOrDefault(c => c.UserId == uid);
            return View(new OnlineCheckoutModel {
                Token = cart.CheckoutToken, Cart = summary,
                FullName = customer == null ? null : customer.FullName,
                PhoneNumber = customer == null ? null : customer.PhoneNumber,
                ShippingAddress = customer == null ? null : customer.Address
            });
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Checkout([Bind(Include = "Token,FullName,PhoneNumber,ShippingAddress,Note")] OnlineCheckoutModel model)
        {
            var uid = User.Identity.GetUserId();
            if (string.IsNullOrWhiteSpace(uid)) return new HttpUnauthorizedResult();
            var cart = CurrentCart();
            int submittedId;
            if (model.Token != null && cart.SubmittedOrders.TryGetValue(model.Token, out submittedId))
                return RedirectToAction("Details", new { id = submittedId });
            if (string.IsNullOrEmpty(model.Token) || model.Token != cart.CheckoutToken)
                return new HttpStatusCodeResult(400, "Checkout expired. Reload cart.");
            // Recover a committed order if the response/session update was interrupted.
            var existing = db.SalesOrders.AsNoTracking().SingleOrDefault(o => o.OrderNumber == model.Token && o.Customer.UserId == uid);
            if (existing != null) { cart.Submitted(model.Token, existing.Id); return RedirectToAction("Details", new { id = existing.Id }); }
            model.Cart = ReadCart(cart);
            if (!model.Cart.CanCheckout) ModelState.AddModelError("", "Giỏ rỗng, sản phẩm ngừng bán hoặc số lượng vượt tồn. Vui lòng cập nhật giỏ.");
            var note = "Người nhận: " + model.FullName + "; ĐT: " + model.PhoneNumber + (string.IsNullOrWhiteSpace(model.Note) ? "" : "; " + model.Note);
            if (note.Length > 500) ModelState.AddModelError("Note", "Thông tin người nhận và ghi chú tối đa 500 ký tự.");
            if (!ModelState.IsValid) return View(model);
            try
            {
                int customerId;
                using (var tx = db.Database.BeginTransaction(IsolationLevel.Serializable))
                {
                    var profiles = db.Customers.Where(c => c.UserId == uid).ToList();
                    if (profiles.Count > 1) throw new InvalidOperationException("Tài khoản có nhiều hồ sơ khách. Vui lòng liên hệ quản trị.");
                    var customer = profiles.SingleOrDefault();
                    if (customer == null)
                    {
                        customer = new Customer { Code = "ON" + Guid.NewGuid().ToString("N").Substring(0, 18),
                            UserId = uid, Email = User.Identity.Name, FullName = model.FullName,
                            PhoneNumber = model.PhoneNumber, Address = model.ShippingAddress };
                        db.Customers.Add(customer); db.SaveChanges();
                    }
                    if (!customer.IsActive) throw new InvalidOperationException("Hồ sơ khách hàng đã ngừng hoạt động.");
                    customerId = customer.Id;
                    tx.Commit();
                }
                var orderId = new SalesOrderService().CreatePending(new SalesOrderPendingInput {
                    OrderNumber = model.Token, CustomerId = customerId, Channel = SalesChannel.Online,
                    PaymentMethod = PaymentMethod.CashOnDelivery, DiscountAmount = 0,
                    ShippingAddress = model.ShippingAddress, Note = note,
                    Details = cart.Items.OrderBy(i => i.Key).Select(i => new SalesOrderLineInput { ProductId = i.Key, Quantity = i.Value }).ToList()
                });
                cart.Submitted(model.Token, orderId);
                return RedirectToAction("Details", new { id = orderId });
            }
            catch (InvalidOperationException ex) { ModelState.AddModelError("", ex.Message); }
            catch (ArgumentException) { ModelState.AddModelError("", "Thông tin đặt hàng không hợp lệ. Vui lòng kiểm tra lại."); }
            catch (DbUpdateException) { ModelState.AddModelError("", "Dữ liệu vừa thay đổi. Vui lòng kiểm tra Đơn của tôi trước khi đặt lại."); }
            return View(model);
        }
        public ActionResult Orders()
        {
            var uid = User.Identity.GetUserId();
            if (string.IsNullOrWhiteSpace(uid)) return new HttpUnauthorizedResult();
            return View(db.SalesOrders.AsNoTracking().Where(o => o.Customer.UserId == uid)
                .OrderByDescending(o => o.Id).ToList());
        }
        public ActionResult Details(int id)
        {
            var uid = User.Identity.GetUserId();
            if (string.IsNullOrWhiteSpace(uid)) return new HttpUnauthorizedResult();
            var order = db.SalesOrders.Include(o => o.Details.Select(d => d.Product))
                .SingleOrDefault(o => o.Id == id && o.Customer.UserId == uid);
            return order == null ? (ActionResult)HttpNotFound() : View(order);
        }
        protected override void Dispose(bool disposing) { if (disposing) db.Dispose(); base.Dispose(disposing); }
    }
}
