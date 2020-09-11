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

        public AppointmentEntity(string AppointmentType, string UniqueID)
        {
            this.PartitionKey = AppointmentType;
            this.RowKey = UniqueID;
        }
        public DateTime AppointmentDate { get; set; }
        public DateTime AppointmentTime { get; set; }
        public string PatientID { get; set; }
        public string DoctorID { get; set; }
        public string PatientName { get; set; }
        public string DoctorName { get; set; }
        public string PatientNumber { get; set; }
        public string DoctorNumber { get; set; }
        public string CreatedBy { get; set; }

    }
}
