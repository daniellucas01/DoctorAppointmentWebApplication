using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DoctorAppointmentWebApplication.Areas.Identity.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace DoctorAppointmentWebApplication.Areas.Identity.Pages.Account.Manage
{
    public partial class IndexModel : PageModel
    {
        private readonly UserManager<DoctorAppointmentWebApplicationUser> _userManager;
        private readonly SignInManager<DoctorAppointmentWebApplicationUser> _signInManager;

        public IndexModel(
            UserManager<DoctorAppointmentWebApplicationUser> userManager,
            SignInManager<DoctorAppointmentWebApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public string Username { get; set; }
        public string Name { get; set; }
        public string DateOfBirth { get; set; }
        public string Role { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Phone]
            [Display(Name = "Phone number")]
            public string PhoneNumber { get; set; }
            [Display(Name = "Photo Profile")]
            public string ImageURL { get; set; }
        }

        private async Task LoadAsync(DoctorAppointmentWebApplicationUser user)
        {
            var userName = await _userManager.GetUserNameAsync(user);
            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
            Trace.WriteLine("User DOB: "+user.DateOfBirth.Date);
            Username = userName;
            Name = user.Name;
            DateOfBirth = user.DateOfBirth.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
            Role = user.Role;
            

            Input = new InputModel
            {
                PhoneNumber = user.PhoneNumber
            };
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            await LoadAsync(user);
            return Page();
        }

        private CloudBlobContainer GetBlobStorageInformation()
        {
            //read json
            var builder = new ConfigurationBuilder()
                            .SetBasePath(Directory.GetCurrentDirectory())
                            .AddJsonFile("appsettings.json");
            IConfigurationRoot configure = builder.Build();

            //to get key access
            //once link, time to read the content to get the connectiontring
            CloudStorageAccount objectaccount =
                CloudStorageAccount.Parse(configure["Connectionstrings:AzureStorageConnection"]);

            CloudBlobClient blobclientagent = objectaccount.CreateCloudBlobClient();

            //step 2 : how to create a new container in the blob storage account
            CloudBlobContainer container = blobclientagent.GetContainerReference("user-profile-picture");

            return container;
        }
        private void UploadBlob(IFormFile files, string fileName)
        {
            //step 1: grab the storage account and container information
            CloudBlobContainer container = GetBlobStorageInformation();

            //step 2: give a name for the blob
            CloudBlockBlob blob = container.GetBlockBlobReference(fileName);

            //step 3: start to upload a static picture from pc to storage
            using (var fileStream = files.OpenReadStream())
            {
                blob.UploadFromStreamAsync(fileStream).Wait();
            }
        }

        public async Task<IActionResult> OnPostAsync(IFormFile files)
        {
            var user = await _userManager.GetUserAsync(User);
            bool hasChanged = false;
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            if (!ModelState.IsValid)
            {
                await LoadAsync(user);
                return Page();
            }

            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
            if (Input.PhoneNumber != user.PhoneNumber)
            {
                user.PhoneNumber = Input.PhoneNumber;
                hasChanged = true;
                /*var setPhoneResult = await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
                if (!setPhoneResult.Succeeded)
                {
                    StatusMessage = "Unexpected error when trying to set phone number.";
                    return RedirectToPage();
                }*/
            }


            if (files != null)
            {
                hasChanged = true;
                string[] splittedImageURL = user.ImageURL.Split("/");
                Trace.WriteLine(user.ImageURL);
                Trace.WriteLine("This is the Image Name : " + splittedImageURL[4]);
                UploadBlob(files, splittedImageURL[4]);
                foreach (string Img in splittedImageURL)
                {
                    Trace.WriteLine(Img);
                }
            }

            if (hasChanged)
            {
                await _userManager.UpdateAsync(user);
                await _signInManager.RefreshSignInAsync(user);
                StatusMessage = "Your profile has been updated";
            }
            else
            {
                StatusMessage = "You have not updated any information on your profile";
            }
            return RedirectToPage();
        }
    }
}
