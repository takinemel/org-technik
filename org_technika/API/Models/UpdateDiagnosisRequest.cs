using System.ComponentModel.DataAnnotations;

namespace API.Models
{
    public class UpdateDiagnosisRequest
    {
        [Required, StringLength(4000, MinimumLength = 1)]
        public string Diagnosis { get; set; }
    }
}
