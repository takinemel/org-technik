using System;
using System.Collections.Generic;

namespace API.Models
{
    public class OrderDetails
    {
        public int OrderId { get; set; }

        public string OrderNumber { get; set; }

        public DateTime CreatedAt { get; set; }

        public string FaultDescription { get; set; }

        public string Diagnosis { get; set; }

        public DateTime? CompletedAt { get; set; }

        public DateTime? IssuedAt { get; set; }

        public bool IsPaid { get; set; }

        public bool IsArchived { get; set; }

        public string StatusName { get; set; }

        public ClientDetails Client { get; set; }

        public EquipmentDetails Equipment { get; set; }

        public MasterDetails Master { get; set; }

        public List<RepairWorkDetails> Works { get; set; }

        public List<OrderPartDetails> Parts { get; set; }

        public List<OrderStatusHistoryItem> StatusHistory { get; set; }

        public decimal TotalCost { get; set; }
    }

    public class ClientDetails
    {
        public int ClientId { get; set; }

        public string FullName { get; set; }

        public string Phone { get; set; }

        public string Email { get; set; }

        public string Address { get; set; }
    }

    public class EquipmentDetails
    {
        public int EquipmentId { get; set; }

        public string SerialNumber { get; set; }

        public string TypeName { get; set; }

        public string ManufacturerName { get; set; }

        public string ModelName { get; set; }
    }

    public class MasterDetails
    {
        public int? MasterId { get; set; }

        public string FullName { get; set; }

        public string Phone { get; set; }

        public string Specialization { get; set; }
    }

    public class RepairWorkDetails
    {
        public int RepairWorkId { get; set; }

        public string WorkName { get; set; }

        public DateTime WorkDate { get; set; }

        public decimal Price { get; set; }
    }

    public class OrderPartDetails
    {
        public int SparePartId { get; set; }

        public string PartName { get; set; }

        public string Unit { get; set; }

        public decimal Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal LineTotal { get; set; }
    }

    public class OrderStatusHistoryItem
    {
        public string StatusName { get; set; }

        public DateTime ChangedAt { get; set; }

        public string ChangedBy { get; set; }

        public string Comment { get; set; }
    }
}