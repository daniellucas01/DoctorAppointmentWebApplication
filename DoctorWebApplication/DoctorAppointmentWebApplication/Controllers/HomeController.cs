using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using DoctorAppointmentWebApplication.Areas.Identity.Data;
using DoctorAppointmentWebApplication.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System.IO;
using DoctorAppointmentWebApplication.Models;
using Microsoft.VisualStudio.Web.CodeGeneration.Contracts.Messaging;

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

        public ActionResult BookAppointment(string id)
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

            ViewBag.DoctorName = name;
            ViewBag.DoctorId = id;
            if (userManager != null)
            {
                var userId = userManager.GetUserId(HttpContext.User);
                ViewBag.userId = userId;
                var user = userManager.GetUserAsync(User);
                ViewBag.phoneNumber = user.Result.PhoneNumber;
                ViewBag.userName = user.Result.Name;
            }

            return View();
            // Container
        }

        [HttpPost]
        public IActionResult BookAppointment(string myDoctorName, string myUserName, string myUserID, string myPhoneNumber, DateTime myDate, DateTime myTime)
        {
            CloudTable table = GetTableInformation();

            string uniqueRowKey = Guid.NewGuid().ToString("N");
            AppointmentEntity patient = new AppointmentEntity(myUserID, uniqueRowKey);
            patient.CustomerName = myUserName;
            patient.DoctorName = myDoctorName;
            patient.AppointmentDate = myDate;
            patient.AppointmentTime = myTime;
            patient.PatientNumber = myPhoneNumber;
            try
            {
                TableOperation tableOperation = TableOperation.Insert(patient);

                TableResult result = table.ExecuteAsync(tableOperation).Result;//toshows the result to the front-end
                table.ExecuteAsync(tableOperation);
                ViewBag.TableName = table.Name;
                ViewBag.msg = "Insert Success!";
                return RedirectToAction("ListUsers", "Home");
                /*RedirectToAction("Home", "Index");*/ //Problem. Redirect to Home.
            }
            catch (Exception ex)
            {
                ViewBag.msg = "Unable to insert the data. Error :" + ex.ToString();
            }
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        //-------------- Storage account set up ---------------------

        private CloudTable GetTableInformation()
        {

            //link the appsettings.json to get the access key
            var builder = new ConfigurationBuilder()
                                 .SetBasePath(Directory.GetCurrentDirectory())
                                 .AddJsonFile("appsettings.json");
            IConfigurationRoot configure = builder.Build();

            //link storage account with access key
            CloudStorageAccount storageaccount =
                CloudStorageAccount.Parse(configure["ConnectionStrings:tablestorageconnection"]);

            CloudTableClient tableClient = storageaccount.CreateCloudTableClient();


            //create the table
            CloudTable table = tableClient.GetTableReference("AppointmentTable");

            return table;

        }

        public ActionResult ViewAppointment()
        {
            CloudTable appointmentTable = GetTableInformation();
            List<AppointmentEntity> patients = new List<AppointmentEntity>();
            var userId = userManager.GetUserId(HttpContext.User);
            var user = userManager.GetUserAsync(User);
            var userName = user.Result.Name;
            try
            {
                TableQuery<AppointmentEntity> query =
                    new TableQuery<AppointmentEntity>()
                    .Where(TableQuery.GenerateFilterCondition(("PartitionKey"), QueryComparisons.Equal, userId));
                TableContinuationToken token = null;

                do
                {
                    TableQuerySegment<AppointmentEntity> result = appointmentTable.ExecuteQuerySegmentedAsync(query, token).Result;
                    token = result.ContinuationToken;

                    foreach (AppointmentEntity patient in result.Results)
                    {
                        patients.Add(patient);
                    }
                }
                while (token != null);
            }
            catch (Exception e)
            {
                ViewBag.msg = "Error: " + e.ToString();
            }
            return View(patients);
        }

        
        public ActionResult DeleteAppointment(string userid, string rowkey) 
        {
            Trace.WriteLine("Delete is clicked");
            if (!String.IsNullOrEmpty(HttpContext.Request.Query["rowkey"]))
            {
                rowkey = HttpContext.Request.Query["rowkey"];
            }
            Trace.WriteLine(userid + " " + rowkey);
            CloudTable appointmentTable = GetTableInformation();
            TableOperation deleteAction = TableOperation.Delete(new AppointmentEntity(userid, rowkey) {ETag = "*"});
            TableResult deleteResult = appointmentTable.ExecuteAsync(deleteAction).Result;
            var msg = "";

            if (deleteResult.HttpStatusCode == 204)
            {
                msg = "Delete Succesfully";
            }
            else 
            {
                msg = "Delete Failed";
            }
            return RedirectToAction("ViewAppointment", "Home", new {msg});
        }



        public ActionResult CreateTable()
        {
            // link the table information
            CloudTable table = GetTableInformation();
            //create table with the mentioned name if not yet exist in storage
            ViewBag.success = table.CreateIfNotExistsAsync().Result; // return false and true. if false then the table denied, else its created
            //Store the table name in the ViewBag to show in the front-end
            ViewBag.TableName = table.Name;
            return View(); // comes out with the interface
        }

        //inputting dynamically
        public ActionResult CreateTableProcess(string myDoctorID, string myDoctorName, string myUserName, string myUserID,string myPhoneNumber, DateTime myDate, DateTime myTime)
        {
            CloudTable table = GetTableInformation();

            AppointmentEntity patient = new AppointmentEntity(myUserID, myDoctorID);
            patient.CustomerName = myUserName;
            patient.DoctorName = myDoctorName;
            patient.AppointmentDate = myDate;
            patient.AppointmentTime = myTime;
            patient.PatientNumber = myPhoneNumber;
            try
            {
                TableOperation tableOperation = TableOperation.Insert(patient);

                TableResult result = table.ExecuteAsync(tableOperation).Result;//toshows the result to the front-end
                table.ExecuteAsync(tableOperation);
                ViewBag.TableName = table.Name;
                ViewBag.msg = "Insert Success!";
            }
            catch (Exception ex)
            {
                ViewBag.msg = "Unable to insert the data. Error :" + ex.ToString();
            }



            return View();
        }
        







    }
}
