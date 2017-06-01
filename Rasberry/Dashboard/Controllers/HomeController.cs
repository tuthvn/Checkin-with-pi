using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Dashboard.Models;

namespace Dashboard.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View(ListDataModal.setListDataSource());
        }

        //public IActionResult About()
        //{
        //    ViewData["Message"] = "Your application description page.";

        //    return View();
        //}

        //public IActionResult Contact()
        //{
        //    ViewData["Message"] = "Your contact page.";

        //    return View();
        //}

        //public IActionResult Error()
        //{
        //    return View();
        //}
    }
}
