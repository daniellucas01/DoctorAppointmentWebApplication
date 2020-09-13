using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using DoctorAppointmentWebApplication.Areas.Identity.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Extensions.Configuration;
using System.IO;
using Microsoft.WindowsAzure.Storage;
using Microsoft.AspNetCore.Http;
using DoctorAppointmentWebApplication.Controllers;
using System.Diagnostics;
using Microsoft.Azure.ServiceBus.Management;

namespace DoctorAppointmentWebApplication.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {

        public string Role { get; set; }

        public List<SelectListItem> Roles { get; } = new List<SelectListItem>
        {
            new SelectListItem { Value = "doctor", Text = "Doctor" },
            new SelectListItem { Value = "patient", Text = "Patient" }
        };

        private readonly SignInManager<DoctorAppointmentWebApplicationUser> _signInManager;
        private readonly UserManager<DoctorAppointmentWebApplicationUser> _userManager;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;

        public RegisterModel(
            UserManager<DoctorAppointmentWebApplicationUser> userManager,
            SignInManager<DoctorAppointmentWebApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public IList<Microsoft.AspNetCore.Authentication.AuthenticationScheme> ExternalLogins { get; set; }

        public class InputModel
        {
            [Required]
            [DataType(DataType.Text)]
            [Display(Name = "Name")]
            public string Name { get; set; }

            [Required]
            [DataType(DataType.Date)]
            [Display(Name = "Date Of Birth")]
            public DateTime DateOfBirth { get; set; }

            [Required]
            [DataType(DataType.PhoneNumber)]
            [Display(Name = "Phone Number")]
            public string PhoneNumber { get; set; }

            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }

            [Required]
            [DataType(DataType.Text)]
            [Display(Name = "Role")]
            public string Role { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
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
            CloudBlockBlob blob = container.GetBlockBlobReference(fileName + ".jpg");

            //step 3: start to upload a static picture from pc to storage
            using (var fileStream = files.OpenReadStream())
            {
                blob.UploadFromStreamAsync(fileStream).Wait();
            }
        }

        public async Task<IActionResult> OnPostAsync(IFormFile files, string returnUrl = null)
        {
            string photoId = Guid.NewGuid().ToString("N");
            UploadBlob(files, photoId);
            string imageURLFromBlob = String.Concat("https://doctorappointmentwebappl.blob.core.windows.net/user-profile-picture/", photoId, ".jpg");
            returnUrl = returnUrl ?? Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            if (ModelState.IsValid)
            {
                var user = new DoctorAppointmentWebApplicationUser {
                    Name = Input.Name,
                    DateOfBirth = Input.DateOfBirth,
                    PhoneNumber = Input.PhoneNumber,
                    UserName = Input.Email,
                    Email = Input.Email,
                    Role = Input.Role,
                    ImageURL = imageURLFromBlob
                };
                var result = await _userManager.CreateAsync(user, Input.Password);
                if (result.Succeeded)
                {
                    if (Input.Role.Equals("Patient", StringComparison.InvariantCultureIgnoreCase))
                    {
                        _userManager.AddToRoleAsync(user, "Patient").Wait();
                    }
                    else
                    {
                        _userManager.AddToRoleAsync(user, "Doctor").Wait();
                    }
                    _logger.LogInformation("User created a new account with password.");

                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId = user.Id, code = code, returnUrl = returnUrl },
                        protocol: Request.Scheme);

                    await _emailSender.SendEmailAsync(Input.Email, "Confirm your email",
                        $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

                    if (_userManager.Options.SignIn.RequireConfirmedAccount)
                    {
                        return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl = returnUrl });
                    }
                    else
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        if (Input.Role.Equals("Doctor", StringComparison.InvariantCultureIgnoreCase))
                        {
                            Trace.WriteLine("INI USER IDNYA : " + user.Id + "    USING FUNCTION: " + _userManager.GetUserId(HttpContext.User));
                            await createServiceBusQueueAsync(user.Id);
                        }
                        return LocalRedirect(returnUrl);
                    }
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
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

        private async Task createServiceBusQueueAsync(string queueName)
        {
            var client = new ManagementClient(RetrieveAzureServiceBusConnection());

            if (!await client.QueueExistsAsync(queueName).ConfigureAwait(false))
            {
                await client.CreateQueueAsync(new QueueDescription(queueName)
                {
                    MaxDeliveryCount = 5,
                    LockDuration = TimeSpan.FromSeconds(30),
                    MaxSizeInMB = 1024,
                    EnableBatchedOperations = true
                }).ConfigureAwait(false);
            }
        }
    }
}
