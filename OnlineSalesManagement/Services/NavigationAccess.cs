using System;
using System.Linq;
using System.Security.Principal;
using System.Web.Mvc;

namespace OnlineSalesManagement.Services
{
    public static class NavigationAccess
    {
        public static bool CanAccess(IPrincipal user, string controller)
        {
            var type = typeof(Controllers.HomeController).Assembly.GetType("OnlineSalesManagement.Controllers." + controller + "Controller");
            if (type == null || !typeof(Controller).IsAssignableFrom(type)) return false;
            var action = type.GetMethods().FirstOrDefault(m => m.Name == "Index" && !m.IsDefined(typeof(HttpPostAttribute), true));
            if (action == null) return false;
            if (action.IsDefined(typeof(AllowAnonymousAttribute), true) || type.IsDefined(typeof(AllowAnonymousAttribute), true)) return true;
            var rules = type.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>()
                .Concat(action.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
            return rules.All(a => user != null && user.Identity != null && user.Identity.IsAuthenticated
                && (string.IsNullOrWhiteSpace(a.Users) || a.Users.Split(',').Any(n => string.Equals(n.Trim(), user.Identity.Name, StringComparison.OrdinalIgnoreCase)))
                && (string.IsNullOrWhiteSpace(a.Roles) || a.Roles.Split(',').Any(r => user.IsInRole(r.Trim()))));
        }
    }
}
