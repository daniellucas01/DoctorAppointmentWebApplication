using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace DoctorAppointmentWebApplication.Areas.Identity.Data
{
    // Add profile data for application users by adding properties to the DoctorAppointmentWebApplicationUser class
    public class DoctorAppointmentWebApplicationUser : IdentityUser
    {
        [PersonalData]
        public string Name { get; set; }
        [PersonalData]
        public DateTime DateOfBirth { get; set; }
        [PersonalData]
        public string PhoneNumber { get; set; }
        [PersonalData]
        public string Role { get; set; }
    }
}
