﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DoctorAppointmentWebApplication.Areas.Identity.Data;
using DoctorAppointmentWebApplication.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using DoctorAppointmentWebApplication.Models;

namespace DoctorAppointmentWebApplication.Controllers
{
    public class DoctorController : Controller

        //INI CONTROLLER BUAT DOKTER (PUBLISHING ) NANTI KASIH AUTHORIZATION SAMA KAYAK HOME CONTROLLER --> ALGO PART
    {
        private readonly UserManager<DoctorAppointmentWebApplicationUser> userManager;
        private DoctorAppointmentWebApplicationContext _application;


        //Connection
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

        public DoctorController(UserManager<DoctorAppointmentWebApplicationUser> userManager, DoctorAppointmentWebApplicationContext application)
        {
            this.userManager = userManager;
            this._application = application;
        }

        public ActionResult PublishingAppointment()
        {
            if (userManager != null) //fetching the logged information 
            {
                var loggedID = userManager.GetUserId(HttpContext.User);
                ViewBag.userId = loggedID;
                var user = userManager.GetUserAsync(User);
                ViewBag.phoneNumber = user.Result.PhoneNumber;
                ViewBag.userName = user.Result.Name;
            }

            return View();
        }

        [HttpPost]
        public IActionResult PublishingAppointment (DateTime myDate, DateTime myTime, string myUserID, string myUserName, string myPhoneNumber)
        {
            CloudTable table = GetTableInformation();

            string uniqueRowKey = Guid.NewGuid().ToString("N");

            var utcDate = DateTime.SpecifyKind(myDate, DateTimeKind.Utc);
            var utcTime = DateTime.SpecifyKind(myTime, DateTimeKind.Utc);

            AppointmentEntity insertTable = new AppointmentEntity("Appointment", uniqueRowKey);
            insertTable.PatientID = "None";
            insertTable.DoctorID = myUserID;
            insertTable.PatientName = "None";
            insertTable.DoctorName = myUserName;
            insertTable.AppointmentDate = utcDate;
            insertTable.AppointmentTime = utcTime;
            insertTable.PatientNumber = "None";
            insertTable.DoctorNumber = myPhoneNumber;
            insertTable.CreatedBy = "Doctor";
            try
            {
                TableOperation tableOperation = TableOperation.Insert(insertTable);
                TableResult result = table.ExecuteAsync(tableOperation).Result; // Toshows the result to the front-end
                table.ExecuteAsync(tableOperation);
                ViewBag.TableName = table.Name;
                ViewBag.msg = "Insert Success!";
                return RedirectToAction("ListUsers", "Home");
            }
            catch (Exception ex)
            {
                ViewBag.msg = "Unable to insert the data. Error :" + ex.ToString();
            }


            return View();
        }

        public ActionResult ManageTimeSlots()
        {
            CloudTable table = GetTableInformation();

            CloudTable appointmentTable = GetTableInformation();
            List<AppointmentEntity> appointment = new List<AppointmentEntity>();
            var userId = userManager.GetUserId(HttpContext.User);
            var user = userManager.GetUserAsync(User);
            var userName = user.Result.Name;
            try
            {
                TableQuery<AppointmentEntity> query =
                    new TableQuery<AppointmentEntity>()
                    .Where(TableQuery.CombineFilters(TableQuery.GenerateFilterCondition("DcotorID", QueryComparisons.Equal, userId),
                           TableOperators.And,
                           TableQuery.GenerateFilterCondition("CreatedBy", QueryComparisons.Equal, "Doctor")));


                TableContinuationToken token = null;

                do
                {
                    TableQuerySegment<AppointmentEntity> result = appointmentTable.ExecuteQuerySegmentedAsync(query, token).Result;
                    token = result.ContinuationToken;

                    foreach (AppointmentEntity timeSlots in result.Results)
                    {
                        appointment.Add(timeSlots);
                    }
                }
                while (token != null);
            }
            catch (Exception e)
            {
                ViewBag.msg = "Error: " + e.ToString();
            }
            System.Diagnostics.Trace.WriteLine(appointment.ToString());
            return View(appointment);
        }
    }
}
