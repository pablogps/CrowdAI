using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace INM.Controllers
{
    public class HomeController : Controller
    {        
        public ActionResult Index()
        {
            return RedirectToAction("HomePage", "Candidates");
        }

        public ActionResult About()
        {
            System.Diagnostics.Debug.WriteLine("/n/nWELCOME TO ABOUT/n/n");
            ViewBag.Message = "Your application description page.";
            return View();
        }

        public ActionResult Contact()
        {
            System.Diagnostics.Debug.WriteLine("The contact page");

            ViewBag.Message = "Your contact page.";
            return View();
        }
    }
}