using System.ComponentModel.DataAnnotations;

namespace API.Models
{
    public class ClientWriteRequest
    {
        [Required, StringLength(200, MinimumLength = 2)]
        public string FullName { get; set; }

        [Required, StringLength(30, MinimumLength = 5)]
        public string Phone { get; set; }

        [EmailAddress, StringLength(254)]
        public string Email { get; set; }

        [StringLength(300)]
        public string Address { get; set; }
    }
}
