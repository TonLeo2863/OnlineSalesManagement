using System;
using System.Linq;
using System.Web.Mvc;

namespace OnlineSalesManagement.Services
{
    public sealed class MenuProduct
    {
        public int CategoryId { get; set; }
        public string Name { get; set; }
        public string Sku { get; set; }
    }
    public static class StorePresentation
    {
        public static string ImageUrl(UrlHelper url, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                value = value.Trim();
                if ((value.StartsWith("~/") || (value.StartsWith("/") && !value.StartsWith("//"))) && !value.Contains("\\"))
                    return url.Content(value);
                Uri uri;
                if (Uri.TryCreate(value, UriKind.Absolute, out uri) && (uri.Scheme == "https" || uri.Scheme == "http"))
                    return uri.AbsoluteUri;
                if (!value.Contains(":") && !value.Contains("\\") && !value.StartsWith("//") && !value.Split('/').Contains(".."))
                    return url.Content("~/" + value.TrimStart('/'));
            }
            return url.Content("~/Content/product-placeholder.svg");
        }
    }
}
