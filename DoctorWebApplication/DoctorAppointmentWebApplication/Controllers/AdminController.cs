using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using DoctorAppointmentWebApplication.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Extensions.Configuration;
using System.IO;
using Microsoft.WindowsAzure.Storage;

namespace DoctorAppointmentWebApplication.Controllers
{
    public class AdminController : Controller
    {
        private readonly RoleManager<IdentityRole> roleManager;
        public AdminController(RoleManager<IdentityRole> roleManager)
        {
            this.roleManager = roleManager;
        }

        public IActionResult Index()
        {
            return View();
        }
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(ProjectRole role)
        {
            var roleExist = await roleManager.RoleExistsAsync(role.RoleName);
            if (!roleExist)
            {
                var result = await roleManager.CreateAsync(new IdentityRole(role.RoleName));
            }
            return View();
        }

        // Blob Container Creation
        private CloudBlobContainer RetrieveBlobContainerInfo()
        {
            // Read JSON
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json");

            IConfigurationRoot configure = builder.Build();

            CloudStorageAccount objectaccount = CloudStorageAccount
                .Parse(configure["ConnectionStrings:BlobStorageConnectionString"]);

            CloudBlobClient blobclientagent = objectaccount.CreateCloudBlobClient();

            // Step 2: how to identify container, or create a new container in the blob storage account.
            CloudBlobContainer container = blobclientagent.GetContainerReference("user-profile-picture");

            return container;
        }

        public ActionResult CreateBlobContainer()
        {
            CloudBlobContainer container = RetrieveBlobContainerInfo();
            ViewBag.Success = container.CreateIfNotExistsAsync().Result;
            ViewBag.BlobContainerName = container.Name;
            return View();
        }
    }
}
