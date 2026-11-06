using System.ComponentModel.DataAnnotations;

namespace API.Models
{
    public class CreateEquipmentRequest
    {
        [Range(1, int.MaxValue)]
        public int ClientId { get; set; }

        [Range(1, int.MaxValue)]
        public int EquipmentModelId { get; set; }

        [Required, StringLength(100, MinimumLength = 1)]
        public string SerialNumber { get; set; }
    }
}
