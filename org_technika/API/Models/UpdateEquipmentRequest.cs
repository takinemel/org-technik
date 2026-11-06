using System.ComponentModel.DataAnnotations;

namespace API.Models
{
    public class UpdateEquipmentRequest
    {
        [Range(1, int.MaxValue)]
        public int EquipmentModelId { get; set; }

        [Required, StringLength(100, MinimumLength = 1)]
        public string SerialNumber { get; set; }
    }
}
