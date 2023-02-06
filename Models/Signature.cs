using System.ComponentModel.DataAnnotations;

namespace TimesheetApp.Models
{
    public class Signature
    {
        [Key]
        public int SignatureId { get; set; }
        public string? Name { get; set; }
        public string? SignatureImage { get; set; }

        public string? UserId { get; set; }
        public virtual ApplicationUser? User { get; set; }
    }
}