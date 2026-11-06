using System.ComponentModel.DataAnnotations;

namespace API.Models
{
    public class CreateOrderRequest
    {
        [Required, StringLength(30, MinimumLength = 1)]
        public string OrderNumber { get; set; }

        [Range(1, int.MaxValue)]
        public int ClientId { get; set; }

        [Range(1, int.MaxValue)]
        public int EquipmentId { get; set; }

        public int? MasterId { get; set; }

        [Required, StringLength(4000, MinimumLength = 3)]
        public string FaultDescription { get; set; }
    }
}
