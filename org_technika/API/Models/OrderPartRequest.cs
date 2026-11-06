using System.ComponentModel.DataAnnotations;

namespace API.Models
{
    public class OrderPartRequest
    {
        [Range(1, int.MaxValue)]
        public int SparePartId { get; set; }

        [Range(0.001, 999999999.999)]
        public decimal Quantity { get; set; }
    }
}
