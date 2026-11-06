using System.ComponentModel.DataAnnotations;

namespace API.Models
{
    public class OrderWorkRequest
    {
        [Required, StringLength(200, MinimumLength = 1)]
        public string WorkName { get; set; }

        [Range(typeof(decimal), "0", "9999999999")]
        public decimal Price { get; set; }
    }
}
