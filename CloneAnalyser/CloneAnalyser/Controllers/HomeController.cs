using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace CloneAnalyser.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            ViewBag.Title = "Clone Analyzer";

            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Clone analyser description";

            return View();
        }

        public ActionResult Backend()
        {
            ViewBag.Message = "Backend stuff comes here";

            return View();
        }
    }
}
