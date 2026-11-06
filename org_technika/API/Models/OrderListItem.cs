using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

//===========================
// ПЕРЕДАЧА ДАННЫХ ЧЕРЕЗ API
//===========================

namespace API.Models
{
    public class OrderListItem
    {
        public int OrderId { get; set; }

        public string OrderNumber { get; set; }

        public string ClientName { get; set; }

        public string ClientPhone { get; set; }

        public string SerialNumber { get; set; }

        public string EquipmentType { get; set; }

        public string Manufacturer { get; set; }

        public string ModelName { get; set; }

        public string MasterName { get; set; }

        public string StatusName { get; set; }

        public DateTime CreatedAt { get; set; }

        public bool IsPaid { get; set; }

        public bool IsArchived { get; set; }
    }
}