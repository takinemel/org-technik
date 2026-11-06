using System.ComponentModel.DataAnnotations;

namespace API.Models
{
    public class AssignMasterRequest
    {
        [Range(1, int.MaxValue)]
        public int MasterId { get; set; }
    }
}
