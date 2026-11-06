using System.ComponentModel.DataAnnotations;

namespace API.Models
{
    public class ChangeOrderStatusRequest
    {
        [Required, StringLength(60, MinimumLength = 1)]
        public string StatusName { get; set; }

        [StringLength(500)]
        public string Comment { get; set; }
    }
}
