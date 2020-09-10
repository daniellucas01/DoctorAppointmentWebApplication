using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using DoctorAppointmentWebApplication.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using DoctorAppointmentWebApplication.Areas.Identity.Data;
using DoctorAppointmentWebApplication.Data;

namespace DoctorAppointmentWebApplication.Controllers
{
    [Authorize(Roles = "Patient")]
    public class HomeController : Controller
    {
        private readonly UserManager<DoctorAppointmentWebApplicationUser> userManager;
        private DoctorAppointmentWebApplicationContext _application;
        /*private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }*/

        public HomeController(UserManager<DoctorAppointmentWebApplicationUser> userManager, DoctorAppointmentWebApplicationContext application)
        {
            this.userManager = userManager;
            this._application = application;
        }

        /*public HomeController(DoctorAppointmentWebApplicationContext application)
        {
            _application = application;
        }*/

        public IActionResult Index()
        {
            if (userManager != null)
            {
                var userId = userManager.GetUserId(HttpContext.User);
                ViewBag.userId = userId;
                var user = userManager.GetUserAsync(User);
                ViewBag.phoneNumber = user.Result.PhoneNumber;
            }
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [HttpGet]
        public IActionResult ListUsers()
        {
            var users = userManager.Users;
            return View(users);
        }

        public IActionResult BookAppointment(string id)
        {
            string text = "Doctor Data = ";
            /*string id = "";*/
            string name = "";
            /*if (!String.IsNullOrEmpty(RouteData.Values["id"].ToString()))
            {
                id = HttpContext.Request.Query["id"];
            }*/
            if (!String.IsNullOrEmpty(HttpContext.Request.Query["name"]))
            {
                name = HttpContext.Request.Query["name"];
            }
            ViewBag.DoctorData = text + " " + id + " name = " + name; 
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
