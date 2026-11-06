using System.ComponentModel.DataAnnotations;

namespace API.Models
{
    public class NameRequest
    {
        [Required, StringLength(100, MinimumLength = 1)]
        public string Name { get; set; }
    }

    public class EquipmentModelRequest
    {
        [Range(1, int.MaxValue)]
        public int EquipmentTypeId { get; set; }

        [Range(1, int.MaxValue)]
        public int ManufacturerId { get; set; }

        [Required, StringLength(100, MinimumLength = 1)]
        public string ModelName { get; set; }
    }
}
