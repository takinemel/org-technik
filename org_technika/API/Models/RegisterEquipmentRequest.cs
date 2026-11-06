using System.ComponentModel.DataAnnotations;

namespace API.Models
{
    public class RegisterEquipmentRequest
    {
        [Range(1, int.MaxValue)]
        public int EquipmentId { get; set; }
    }
}
