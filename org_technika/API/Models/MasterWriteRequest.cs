using System.ComponentModel.DataAnnotations;

namespace API.Models
{
    public class MasterWriteRequest
    {
        [Required, StringLength(200, MinimumLength = 1)]
        public string FullName { get; set; }

        [Required, StringLength(30, MinimumLength = 1)]
        public string Phone { get; set; }

        [StringLength(150)]
        public string Specialization { get; set; }
    }
}
