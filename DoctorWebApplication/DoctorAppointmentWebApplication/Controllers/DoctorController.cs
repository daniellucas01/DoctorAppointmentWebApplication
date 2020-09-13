using System;
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
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Azure.ServiceBus;
using System.Text;
using System.Threading;

namespace DoctorAppointmentWebApplication.Controllers
{
    [Authorize(Roles = "Doctor")]
    public class DoctorController : Controller
    {
        const string ServiceBusConnectionString = "Endpoint=sb://azureservicebustp047067.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=27AeQCdxa6yeB3QzMTfmALnX+gdDWwvF/5sUiUdCgAs=";
        /*const string QueueName = "18e39f84-332c-4019-85ac-ecb669eeb0d7";*/
        public const string Namespace = "azureservicebustp047067";

        static IQueueClient queueClient;
        static List<string> items;
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

        public IActionResult Index()
        {
            return View();
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
        public IActionResult PublishingAppointment(DateTime myDate, DateTime myTime, string myUserID, string myUserName, string myPhoneNumber)
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
            insertTable.CoronaRisk = "None";
            try
            {
                TableOperation tableOperation = TableOperation.Insert(insertTable);
                TableResult result = table.ExecuteAsync(tableOperation).Result; // Toshows the result to the front-end
                table.ExecuteAsync(tableOperation);
                ViewBag.TableName = table.Name;
                ViewBag.msg = "Insert Success!";
                return RedirectToAction("ManageTimeSlots");
            }
            catch (Exception ex)
            {
                ViewBag.msg = "Unable to insert the data. Error :" + ex.ToString();
            }
            return View();
        }

        public ActionResult ManageTimeSlots()
        {
            CloudTable appointmentTable = GetTableInformation();
            List<AppointmentEntity> appointment = new List<AppointmentEntity>();
            var userId = userManager.GetUserId(HttpContext.User);
            System.Diagnostics.Trace.WriteLine(userId.ToString());
            var user = userManager.GetUserAsync(User);
            try
            {
                TableQuery<AppointmentEntity> query =
                    new TableQuery<AppointmentEntity>()
                    .Where(TableQuery.CombineFilters(TableQuery.GenerateFilterCondition("DoctorID", QueryComparisons.Equal, userId),
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

        public IActionResult DeleteTimeSlots(string rowkey)
        {
            CloudTable appointmentTable = GetTableInformation();
            if (!String.IsNullOrEmpty(HttpContext.Request.Query["rowkey"]))
            {
                rowkey = HttpContext.Request.Query["rowkey"];
            }
            TableOperation deleteAction = TableOperation.Delete(new AppointmentEntity("Appointment", rowkey) { ETag = "*" });
            TableResult deleteResult = appointmentTable.ExecuteAsync(deleteAction).Result;
            return RedirectToAction("ManageTimeSlots", "Doctor");
        }

        public async Task<ActionResult> EditTimeSlotsAsync(string id)
        {
            string rowkey = "";
            if (!String.IsNullOrEmpty(HttpContext.Request.Query["rowkey"]))
            {
                rowkey = HttpContext.Request.Query["rowkey"];
            }

            CloudTable table = GetTableInformation();

            TableOperation retrieveTimeSlotDetails = TableOperation.Retrieve<AppointmentEntity>(id, rowkey);

            TableResult retrievedResult = await table.ExecuteAsync(retrieveTimeSlotDetails);

            AppointmentEntity updateEntity = (AppointmentEntity)retrievedResult.Result;

            return View(updateEntity);
        }

        [HttpPost]
        public async Task<IActionResult> SaveTimeSlotAsync(string PartitionKey, string RowKey, DateTime AppointmentDate, DateTime AppointmentTime)
        {
            bool hasChanged = false;
            CloudTable table = GetTableInformation();

            TableOperation retrieveOperation = TableOperation.Retrieve<AppointmentEntity>(PartitionKey, RowKey);
            TableResult retrievedResult = await table.ExecuteAsync(retrieveOperation);

            AppointmentEntity updateEntity = (AppointmentEntity)retrievedResult.Result;

            if (updateEntity != null)
            {
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
                }
                else
                {
                    Trace.WriteLine("No Changes");
                }
            }
            return RedirectToAction("ManageTimeSlots", "Doctor");
        }


        //============================================

        // View Appointment of the Booked
        public ActionResult ViewBookedAppointment()
        {
            CloudTable appointmentTable = GetTableInformation();
            List<AppointmentEntity> appointments = new List<AppointmentEntity>();
            var userId = userManager.GetUserId(HttpContext.User);
            var user = userManager.GetUserAsync(User);
            var userName = user.Result.Name;
            try
            {
                TableQuery<AppointmentEntity> query =
                    new TableQuery<AppointmentEntity>()
                    .Where(TableQuery.CombineFilters(TableQuery.GenerateFilterCondition("PatientID", QueryComparisons.NotEqual, "None"),
                           TableOperators.And,
                           TableQuery.GenerateFilterCondition("DoctorID", QueryComparisons.Equal, userId)));
                TableContinuationToken token = null;

                do
                {
                    TableQuerySegment<AppointmentEntity> result = appointmentTable.ExecuteQuerySegmentedAsync(query, token).Result;
                    token = result.ContinuationToken;

                    foreach (AppointmentEntity patient in result.Results)
                    {
                        appointments.Add(patient);
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

        public ActionResult DeleteAppointment(string rowkey)
        {
            Trace.WriteLine("Delete is clicked" + rowkey);
            /*if (!String.IsNullOrEmpty(HttpContext.Request.Query["rowkey"]))
            {
                rowkey = HttpContext.Request.Query["rowkey"];
            }*/

            CloudTable appointmentTable = GetTableInformation();
            var msg = "";
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
            return RedirectToAction("ViewBookedAppointment", "Doctor", new { msg });
        }

        public async Task<ActionResult> EditAppointment(string rowkey) // id = PartitionKey
        {
            CloudTable table = GetTableInformation();

            // Create a retrieve operation that takes a item entity
            TableOperation retrieveOperation = TableOperation.Retrieve<AppointmentEntity>("Appointment", rowkey);
            //Execute the operation
            TableResult retrievedResult = await table.ExecuteAsync(retrieveOperation);

            // Assign the result to a Item object.
            AppointmentEntity updateEntity = (AppointmentEntity)retrievedResult.Result;

            /*if (updateEntity != null)
            {
                //Change the description
                updateEntity.Description = "in nos";

                // Create the InsertOrReplace TableOperation
                TableOperation insertOrReplaceOperation = TableOperation.InsertOrReplace(updateEntity);

                // Execute the operation.
                table.Execute(insertOrReplaceOperation);
                Console.WriteLine("Entity was updated.");
            }*/


            return View(updateEntity);
        }

        [HttpPost]
        public async Task<IActionResult> EditAppointment(string PartitionKey, string RowKey, DateTime AppointmentDate, DateTime AppointmentTime)
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
            return RedirectToAction("ViewBookedAppointment", "Doctor");
        }

        public async Task<IActionResult> ReceiveNotification(string QueueName)
        {
            QueueName = "18e39f84-332c-4019-85ac-ecb669eeb0d7";
            queueClient = new QueueClient(ServiceBusConnectionString, QueueName, ReceiveMode.PeekLock);
            items = new List<string>();
            await Task.Factory.StartNew(() =>
            {
                queueClient = new QueueClient(ServiceBusConnectionString, QueueName, ReceiveMode.PeekLock);
                var options = new MessageHandlerOptions(ExceptionMethod)
                {
                    MaxConcurrentCalls = 1,
                    AutoComplete = false
                };
                queueClient.RegisterMessageHandler(ExecuteMessageProcessing, options);
            });
            return RedirectToAction("ReceiveNotificationResult");
        }

        //Part 2: Received Message from the Service Bus - get data step
        private static async Task ExecuteMessageProcessing(Message message, CancellationToken arg2)
        {
            //var result = JsonConvert.DeserializeObject<Ostring>(Encoding.UTF8.GetString(message.Body));
            // Console.WriteLine($"Order Id is {result.OrderId}, Order name is {result.OrderName} and quantity is {result.OrderQuantity}");
            Console.WriteLine($"Received message: SequenceNumber:{message.SystemProperties.SequenceNumber} Body:{Encoding.UTF8.GetString(message.Body)}");
            await queueClient.CompleteAsync(message.SystemProperties.LockToken);

            items.Add(Encoding.UTF8.GetString(message.Body));
        }

        //Part 2: Received Message from the Service Bus
        private static async Task ExceptionMethod(ExceptionReceivedEventArgs arg)
        {
            await Task.Run(() =>
           Console.WriteLine($"Error occured. Error is {arg.Exception.Message}")
           );
        }
        public IActionResult ReceiveNotificationResult()
        {
            if (items == null)
            {
                return RedirectToAction("ReceiveNotificationResult");
            }
            else
            {
                return View(items);
            }
        }
    }
}