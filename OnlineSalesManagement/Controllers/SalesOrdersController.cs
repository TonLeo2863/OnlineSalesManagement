using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace OnlineSalesManagement.Controllers
{
    public class SalesOrdersController : Controller
    {
        // GET: SalesOrders
        public ActionResult Index()
        {
            return View();
        }
    }
}