using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DoctorAppointmentWebApplication.Models
{
    public class RoleViewModel
    {
        public string Role { get; set; }

        public List<SelectListItem> Roles { get; } = new List<SelectListItem>
        {
            new SelectListItem { Value = "doctor", Text = "Doctor" },
            new SelectListItem { Value = "patient", Text = "Patient" }
        };

    }
}
