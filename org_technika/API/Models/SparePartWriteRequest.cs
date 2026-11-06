using System.ComponentModel.DataAnnotations;

namespace API.Models
{
    public class SparePartWriteRequest
    {
        [Required, StringLength(200, MinimumLength = 1)]
        public string PartName { get; set; }

        [Required, StringLength(30, MinimumLength = 1)]
        public string Unit { get; set; }

        [Range(typeof(decimal), "0", "9999999999")]
        public decimal UnitPrice { get; set; }

        [Range(typeof(decimal), "0", "999999999")]
        public decimal StockQuantity { get; set; }
    }
}
