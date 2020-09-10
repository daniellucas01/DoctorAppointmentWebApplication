using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace DoctorAppointmentWebApplication.Models
{
    public class AppointmentEntity:TableEntity
    {
        public AppointmentEntity()
        {
            //blank 
        }

        public AppointmentEntity(string CustomerID, string DoctorID)
        {
            this.PartitionKey = CustomerID;
            this.RowKey = DoctorID;
        }
        public DateTime AppointmentDate { get; set; }
        public DateTime AppointmentTime { get; set; }
        public string CustomerName { get; set; }
        public string DoctorName { get; set; }
        public string PatientNumber { get; set; }


    }
}
