using System;
using DoctorAppointmentWebApplication.Areas.Identity.Data;
using DoctorAppointmentWebApplication.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[assembly: HostingStartup(typeof(DoctorAppointmentWebApplication.Areas.Identity.IdentityHostingStartup))]
namespace DoctorAppointmentWebApplication.Areas.Identity
{
    public class IdentityHostingStartup : IHostingStartup
    {
        public void Configure(IWebHostBuilder builder)
        {
            builder.ConfigureServices((context, services) => {
                services.AddDbContext<DoctorAppointmentWebApplicationContext>(options =>
                    options.UseSqlServer(
                        context.Configuration.GetConnectionString("DoctorAppointmentWebApplicationContextConnection"),
                        x => x.MigrationsAssembly("DoctorAppointmentWebApplication")
                        )
                    );

                /*services.AddDefaultIdentity<DoctorAppointmentWebApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
                    .AddEntityFrameworkStores<DoctorAppointmentWebApplicationContext>();*/
            });
        }
    }
}