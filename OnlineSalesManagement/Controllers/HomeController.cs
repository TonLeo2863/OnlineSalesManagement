using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using OnlineSalesManagement.Models;
using OnlineSalesManagement.Services;

namespace OnlineSalesManagement.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            if (!DashboardService.IsStaff(User))
            {
                using (var db = new ApplicationDbContext())
                    return View("Welcome", db.Products.AsNoTracking().Include(p => p.Category)
                        .Where(p => p.IsActive && p.Category.IsActive).OrderByDescending(p => p.QuantityInStock > 0)
                        .ThenByDescending(p => p.CreatedDate).Take(4).ToList());
            }
            using (var db = new ApplicationDbContext())
                return View(DashboardService.Load(db, User));
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }
    }
}
