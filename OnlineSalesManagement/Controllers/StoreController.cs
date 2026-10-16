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
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal SalePrice { get; set; }
        public string Token { get; set; }
        [Range(1, 1000)] public int Quantity { get; set; }
        [Required, StringLength(150)] public string FullName { get; set; }
        [Required, RegularExpression(@"^(\+84|0)\d{9,10}$")] public string PhoneNumber { get; set; }
        [Required, StringLength(300)] public string ShippingAddress { get; set; }
        [StringLength(500)] public string Note { get; set; }
    }
    [Authorize]
    public class StoreController : Controller
    {
        private readonly ApplicationDbContext db = new ApplicationDbContext();
        [AllowAnonymous]
        public ActionResult Index(string q)
        {
            var products = db.Products.AsNoTracking().Where(p => p.IsActive && p.Category.IsActive);
            if (!string.IsNullOrWhiteSpace(q)) products = products.Where(p => p.Name.Contains(q) || p.Sku.Contains(q));
            return View(products.OrderBy(p => p.Name).Take(200).ToList());
        }
        public ActionResult Checkout(int id)
        {
            var uid = User.Identity.GetUserId();
            if (string.IsNullOrWhiteSpace(uid)) return new HttpUnauthorizedResult();
            var product = db.Products.SingleOrDefault(p => p.Id == id && p.IsActive && p.Category.IsActive);
            if (product == null) return HttpNotFound();
            var token = "WEB" + Guid.NewGuid().ToString("N").Substring(0, 26);
            Session["OnlineCheckoutToken"] = token;
            Session["OnlineCheckoutOwner"] = uid;
            Session["OnlineCheckoutProductId"] = id;
            var customer = db.Customers.FirstOrDefault(c => c.UserId == uid);
            return View(new OnlineCheckoutModel {
                ProductId = id, ProductName = product.Name, SalePrice = product.SalePrice,
                Quantity = 1, Token = token, FullName = customer == null ? null : customer.FullName,
                PhoneNumber = customer == null ? null : customer.PhoneNumber,
                ShippingAddress = customer == null ? null : customer.Address
            });
        }
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Checkout([Bind(Include = "ProductId,Token,Quantity,FullName,PhoneNumber,ShippingAddress,Note")] OnlineCheckoutModel model)
        {
            var uid = User.Identity.GetUserId();
            if (string.IsNullOrWhiteSpace(uid)) return new HttpUnauthorizedResult();
            if (string.IsNullOrEmpty(model.Token) || model.Token != Session["OnlineCheckoutToken"] as string
                || uid != Session["OnlineCheckoutOwner"] as string
                || model.ProductId != (int?)Session["OnlineCheckoutProductId"])
                return new HttpStatusCodeResult(400, "Checkout expired. Reload the product page.");
            var product = db.Products.SingleOrDefault(p => p.Id == model.ProductId && p.IsActive && p.Category.IsActive);
            if (product == null) return HttpNotFound();
            model.ProductName = product.Name;
            model.SalePrice = product.SalePrice;
            if (model.Quantity > product.QuantityInStock)
                ModelState.AddModelError("Quantity", "Số lượng vượt tồn hiện tại. Tồn kho sẽ được kiểm tra lại khi nhân viên xác nhận.");
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
                        customer = new Customer {
                            Code = "ON" + Guid.NewGuid().ToString("N").Substring(0, 18),
                            UserId = uid, Email = User.Identity.Name,
                            FullName = model.FullName, PhoneNumber = model.PhoneNumber,
                            Address = model.ShippingAddress
                        };
                        db.Customers.Add(customer);
                        db.SaveChanges();
                    }
                    if (!customer.IsActive) throw new InvalidOperationException("Hồ sơ khách hàng đã ngừng hoạt động.");
                    customerId = customer.Id;
                    tx.Commit();
                }
                var existing = db.SalesOrders.FirstOrDefault(o => o.OrderNumber == model.Token && o.CustomerId == customerId);
                var orderId = existing == null ? new SalesOrderService().CreatePending(new SalesOrderPendingInput {
                    OrderNumber = model.Token, CustomerId = customerId, Channel = SalesChannel.Online,
                    PaymentMethod = PaymentMethod.CashOnDelivery, DiscountAmount = 0,
                    ShippingAddress = model.ShippingAddress,
                    Note = "Người nhận: " + model.FullName + "; ĐT: " + model.PhoneNumber,
                    Details = new List<SalesOrderLineInput> { new SalesOrderLineInput { ProductId = product.Id, Quantity = model.Quantity } }
                }) : existing.Id;
                Session.Remove("OnlineCheckoutToken");
                Session.Remove("OnlineCheckoutOwner");
                Session.Remove("OnlineCheckoutProductId");
                return RedirectToAction("Details", new { id = orderId });
            }
            catch (InvalidOperationException ex) { ModelState.AddModelError("", ex.Message); }
            catch (ArgumentException) { ModelState.AddModelError("", "Thông tin đặt hàng không hợp lệ. Vui lòng kiểm tra lại."); }
            catch (DbUpdateException) { ModelState.AddModelError("", "Dữ liệu vừa thay đổi. Vui lòng kiểm tra đơn của tôi trước khi đặt lại."); }
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
