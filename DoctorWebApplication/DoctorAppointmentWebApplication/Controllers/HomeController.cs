﻿using System;
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
using Microsoft.Azure.ServiceBus;
using System.Text;

namespace DoctorAppointmentWebApplication.Controllers
{

    [Authorize(Roles = "Patient")]
    public class HomeController : Controller
    {
        static IQueueClient queueClient;
        private readonly UserManager<DoctorAppointmentWebApplicationUser> userManager;
        private DoctorAppointmentWebApplicationContext _application;

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
        public IActionResult DoctorList(string DoctorName)
        {
            var users = userManager.Users.Where(x => x.Role.Equals("doctor"));
            if (DoctorName != null)
            {
                users = userManager.Users.Where(x => x.Name.Contains(DoctorName) && x.Role.Equals("doctor"));
            }
            return View(users);
        }

        public ActionResult BookAppointment(string id)
        {
            string text = "Doctor Data = ";
            string name = "";
            string doctorPhoneNumber = "";
            if (!String.IsNullOrEmpty(HttpContext.Request.Query["name"]))
            {
                name = HttpContext.Request.Query["name"];
            }
            if (!String.IsNullOrEmpty(HttpContext.Request.Query["phone-number"]))
            {
                doctorPhoneNumber = HttpContext.Request.Query["phone-number"];
            }
            ViewBag.DoctorData = text + " " + id + " name = " + name + " Phone Number : " + doctorPhoneNumber;

            ViewBag.DoctorName = name;
            ViewBag.DoctorId = id;
            ViewBag.DoctorPhoneNumber = doctorPhoneNumber;
            if (userManager != null)
            {
                var userId = userManager.GetUserId(HttpContext.User);
                ViewBag.userId = userId;
                var user = userManager.GetUserAsync(User);
                ViewBag.phoneNumber = user.Result.PhoneNumber;
                ViewBag.userName = user.Result.Name;
            }
            return View();
        }

        [HttpPost]
        public IActionResult BookAppointment(string myDoctorName,string myDoctorID, string myUserName, string myUserID, string myPhoneNumber, DateTime myDate, DateTime myTime, string myDoctorPhoneNumber, string coronaRisk)
        {
            CloudTable table = GetTableInformation();

            string uniqueRowKey = Guid.NewGuid().ToString("N");
            AppointmentEntity patient = new AppointmentEntity("Appointment", uniqueRowKey);
            patient.PatientID = myUserID;
            patient.DoctorID = myDoctorID;
            patient.PatientName = myUserName;
            patient.DoctorName = myDoctorName;
            patient.DoctorNumber = myDoctorPhoneNumber;
            patient.PatientNumber = myPhoneNumber;
            patient.CreatedBy = "Patient";
            patient.CoronaRisk = coronaRisk;

            // Specify the Time Zone to prevent Table Storage to convert the Date Time to UTC
            var utcDate = DateTime.SpecifyKind(myDate, DateTimeKind.Utc); 
            var utcTime = DateTime.SpecifyKind(myTime, DateTimeKind.Utc);
            patient.AppointmentDate = utcDate;
            patient.AppointmentTime = utcTime;
            try
            {
                TableOperation tableOperation = TableOperation.Insert(patient);

                TableResult result = table.ExecuteAsync(tableOperation).Result;//toshows the result to the front-end
                table.ExecuteAsync(tableOperation);
                ViewBag.TableName = table.Name;
                ViewBag.msg = "Insert Success!";
                sendMessageAsync(myDoctorID, myUserName, utcDate.ToString(), utcTime.ToString(), coronaRisk);
                return RedirectToAction("DoctorList", "Home");
            }
            catch (Exception ex)
            {
                ViewBag.msg = "Unable to insert the data. Error :" + ex.ToString();
            }
            return View();
        }

        public string RetrieveAzureServiceBusConnection()
        {
            //read json
            var builder = new ConfigurationBuilder()
                            .SetBasePath(Directory.GetCurrentDirectory())
                            .AddJsonFile("appsettings.json");
            IConfigurationRoot configure = builder.Build();

            //to get key access
            //once link, time to read the content to get the connectiontring
            return configure["Connectionstrings:ServiceBusConnection"];
        }

        public async Task sendMessageAsync(string QueueName, string myUserName, string utcDate, string utcTime, string coronaRisk)
        {
            queueClient = new QueueClient(RetrieveAzureServiceBusConnection(), QueueName);
            try
            {
                    string messageBody = $"Message {myUserName + " has request for an appointment on " + utcDate + " " + utcTime + ". This patient has " + coronaRisk + "Corona Risk"}";
                    var message = new Microsoft.Azure.ServiceBus.Message(Encoding.UTF8.GetBytes(messageBody));

                    // Write the body of the message to the console.
                    Trace.WriteLine($"Sending message: {messageBody}");

                    // Send the message to the queue.
                    await queueClient.SendAsync(message);
            }
            catch (Exception exception)
            {
                ViewBag.msg = exception.ToString();
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        // -------------- Storage account set up --------------------- //
        private CloudTable GetTableInformation()
        {
            //link the appsettings.json to get the access key
            var builder = new ConfigurationBuilder()
                                 .SetBasePath(Directory.GetCurrentDirectory())
                                 .AddJsonFile("appsettings.json");
            IConfigurationRoot configure = builder.Build();

            //link storage account with access key
            CloudStorageAccount storageaccount =
                CloudStorageAccount.Parse(configure["ConnectionStrings:AzureStorageConnection"]);

            CloudTableClient tableClient = storageaccount.CreateCloudTableClient();

            // Create the table
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
                    .Where(TableQuery.GenerateFilterCondition(("PatientID"), QueryComparisons.Equal, userId));
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

        public async Task<ActionResult> DeleteAppointmentAsync(string userid) 
        {
            string rowkey = "";
            string createdBy = "";
            if (!String.IsNullOrEmpty(HttpContext.Request.Query["rowkey"]))
            {
                rowkey = HttpContext.Request.Query["rowkey"];
            }
            if (!String.IsNullOrEmpty(HttpContext.Request.Query["created-by"]))
            {
                createdBy = HttpContext.Request.Query["created-by"];
            }
            Trace.WriteLine("Used Id = "+userid + " Rowkey = " + rowkey);

            CloudTable appointmentTable = GetTableInformation();
            var msg = "";
            if (createdBy.Equals("Patient", StringComparison.InvariantCultureIgnoreCase))
            {
                TableOperation deleteAction = TableOperation.Delete(new AppointmentEntity("Appointment", rowkey) { ETag = "*" });
                TableResult deleteResult = appointmentTable.ExecuteAsync(deleteAction).Result;

                if (deleteResult.HttpStatusCode == 204)
                {
                    msg = "Delete Succesfully";
                }
                else
                {
                    msg = "Delete Failed Created By Patient";
                }
            }

            else
            {
                // Create a retrieve operation that takes a item entity
                TableOperation retrieveOperation = TableOperation.Retrieve<AppointmentEntity>("Appointment", rowkey);
                //Execute the operation
                TableResult retrievedResult = await appointmentTable.ExecuteAsync(retrieveOperation);

                // Assign the result to a Item object.
                AppointmentEntity updateEntity = (AppointmentEntity)retrievedResult.Result;

                if (updateEntity != null)
                {
                    //Change the Id, Name, and Telephone Number of the Patient
                    updateEntity.PatientID = "None";
                    updateEntity.PatientName = "None";
                    updateEntity.PatientNumber = "None";
                    // Create the InsertOrReplace TableOperation
                    TableOperation insertOrReplaceOperation = TableOperation.InsertOrReplace(updateEntity);

                    // Execute the operation.
                    await appointmentTable.ExecuteAsync(insertOrReplaceOperation);
                    Trace.WriteLine("Entity was updated.");
                    msg = "Delete Succesfully";
                }
                else
                {
                    msg = "Delete Failed By Doctor";
                }
            }
            return RedirectToAction("ViewAppointment", "Home", new {msg});
        }



        public ActionResult CreateTable()
        {
            CloudTable tableReference = GetTableInformation();
            ViewBag.success = tableReference.CreateIfNotExistsAsync().Result;
            ViewBag.TableName = tableReference.Name;
            return View();
        }

        public ActionResult ViewPublishAppointment(string id) // id = DoctorId
        {
            CloudTable appointmentTable = GetTableInformation();
            string doctorName = "";
            string phoneNumber = "";
            if (!String.IsNullOrEmpty(HttpContext.Request.Query["name"]))
            {
                doctorName = HttpContext.Request.Query["name"];
            }
            if (!String.IsNullOrEmpty(HttpContext.Request.Query["phone-number"]))
            {
                phoneNumber = HttpContext.Request.Query["phone-number"];
            }
            ViewBag.doctorId = id;
            ViewBag.doctorName = doctorName;
            ViewBag.phoneNumber = phoneNumber;
            Trace.WriteLine(id);
            Trace.WriteLine(doctorName);
            Trace.WriteLine(phoneNumber);
            List<AppointmentEntity> appointments = new List<AppointmentEntity>();
            var userId = userManager.GetUserId(HttpContext.User);
            var user = userManager.GetUserAsync(User);
            var userName = user.Result.Name;
            try
            {
                TableQuery<AppointmentEntity> query =
                    new TableQuery<AppointmentEntity>()
                    .Where(TableQuery.CombineFilters(TableQuery.GenerateFilterCondition("PatientID", QueryComparisons.Equal, "None"),
                           TableOperators.And,
                           TableQuery.GenerateFilterCondition("DoctorID", QueryComparisons.Equal, id)));

                TableContinuationToken token = null;

                do
                {
                    TableQuerySegment<AppointmentEntity> result = appointmentTable.ExecuteQuerySegmentedAsync(query, token).Result;
                    token = result.ContinuationToken;

                    foreach (AppointmentEntity appointment in result.Results)
                    {
                        appointments.Add(appointment);
                    }
                }
                while (token != null);
            }
            catch (Exception e)
            {
                ViewBag.msg = "Error: " + e.ToString();
            }
            return View(appointments);
        }

        [HttpPost]
        public async Task<IActionResult> UpdatePublishedAsync(string id)
        {
            string rowkey = "";
            string coronaRisk = "";
            string doctorId = "";
            if (!String.IsNullOrEmpty(HttpContext.Request.Query["rowkey"]))
            {
                rowkey = HttpContext.Request.Query["rowkey"];
            }
            if (!String.IsNullOrEmpty(HttpContext.Request.Query["coronaRisk"]))
            {
                coronaRisk = HttpContext.Request.Query["coronaRisk"];
            }
            if (!String.IsNullOrEmpty(HttpContext.Request.Query["doctorId"]))
            {
                doctorId = HttpContext.Request.Query["doctorId"];
            }
            Trace.WriteLine("PartitionKey" + id);
            Trace.WriteLine("Rowkey" + rowkey);
            Trace.WriteLine("Risk" + coronaRisk);


            CloudTable table = GetTableInformation();

            // Create a retrieve operation that takes a item entity
            TableOperation retrieveOperation = TableOperation.Retrieve<AppointmentEntity>(id, rowkey);
            //Execute the operation
            TableResult retrievedResult = await table.ExecuteAsync(retrieveOperation);

            // Assign the result to a Item object.
            AppointmentEntity updateEntity = (AppointmentEntity)retrievedResult.Result;

            if (updateEntity != null)
            {
                var userId = userManager.GetUserId(HttpContext.User);
                ViewBag.userId = userId;
                var user = userManager.GetUserAsync(User);
                ViewBag.phoneNumber = user.Result.PhoneNumber;
                //Change the description
                updateEntity.PatientName = user.Result.Name;
                updateEntity.PatientID = userId;
                updateEntity.PatientNumber = user.Result.PhoneNumber;
                updateEntity.CoronaRisk = coronaRisk;

                // Create the InsertOrReplace TableOperation
                TableOperation insertOrReplaceOperation = TableOperation.InsertOrReplace(updateEntity);

                // Execute the operation.
                await table.ExecuteAsync(insertOrReplaceOperation);
                sendMessageAsync(doctorId, user.Result.Name,updateEntity.AppointmentDate.ToString(), updateEntity.AppointmentTime.ToString() ,coronaRisk);
            }
            Console.WriteLine("Appointment is booked");
            return RedirectToAction("ViewAppointment", "Home");
        }

        public async Task<ActionResult> EditAppointmentAsync(string id) // id = PartitionKey
        {
            string rowkey = "";
            if (!String.IsNullOrEmpty(HttpContext.Request.Query["rowkey"]))
            {
                rowkey = HttpContext.Request.Query["rowkey"];
            }

            CloudTable table = GetTableInformation();

            // Create a retrieve operation that takes a item entity
            TableOperation retrieveOperation = TableOperation.Retrieve<AppointmentEntity>(id, rowkey);

            //Execute the operation
            TableResult retrievedResult = await table.ExecuteAsync(retrieveOperation);

            // Assign the result to a Item object.
            AppointmentEntity updateEntity = (AppointmentEntity)retrievedResult.Result;

            return View(updateEntity);
        }

        [HttpPost]
        public async Task<IActionResult> EditAppointmentAsync(string PartitionKey, string RowKey, DateTime AppointmentDate, DateTime AppointmentTime)
        {
            bool hasChanged = false;
            CloudTable table = GetTableInformation();

            // Create a retrieve operation that takes a item entity
            TableOperation retrieveOperation = TableOperation.Retrieve<AppointmentEntity>(PartitionKey, RowKey);
            //Execute the operation
            TableResult retrievedResult = await table.ExecuteAsync(retrieveOperation);

            // Assign the result to a Item object.
            AppointmentEntity updateEntity = (AppointmentEntity)retrievedResult.Result;

            if (updateEntity != null)
            {
                //Change the description
                if (updateEntity.AppointmentDate != AppointmentDate || updateEntity.AppointmentTime != AppointmentTime)
                {
                    hasChanged = true;
                }
                if (hasChanged)
                {
                    var appointmentDate = DateTime.SpecifyKind(AppointmentDate, DateTimeKind.Utc);
                    updateEntity.AppointmentDate = appointmentDate;
                    var appointmentTime = DateTime.SpecifyKind(AppointmentTime, DateTimeKind.Utc);
                    updateEntity.AppointmentTime = appointmentTime;
                    // Create the InsertOrReplace TableOperation
                    TableOperation insertOrReplaceOperation = TableOperation.InsertOrReplace(updateEntity);

                    // Execute the operation.
                    await table.ExecuteAsync(insertOrReplaceOperation);
                    Trace.WriteLine("Entity was updated.");
                }
                else
                {
                    Trace.WriteLine("No Changes");
                }
            }
            return View();
        }
    }
}
