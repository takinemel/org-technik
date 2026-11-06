using System.ComponentModel.DataAnnotations;

namespace API.Models
{
    public class LoginRequest
    {
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Login { get; set; }

        [Required]
        [StringLength(256, MinimumLength = 1)]
        public string Password { get; set; }
    }
}
