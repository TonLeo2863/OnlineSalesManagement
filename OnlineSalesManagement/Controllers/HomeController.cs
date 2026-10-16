using System;
using System.Collections.Generic;
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
            if (!DashboardService.IsStaff(User)) return View("Welcome");
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
