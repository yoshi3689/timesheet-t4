using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace TimesheetApp.Models.TimesheetModels
{
    /// <summary>
    /// Model for storing Timesheets that are signed and encryped
    /// DB Model
    /// </summary>
    public class ApprovedTimesheet
    {
        [Key]
        [Required]
        public int ApprovedTimesheetId { get; set; }
        [Required]
        public string? EndDate { get; set; }
        public string? TotalHours { get; set; }
        [Required]
        public string? UserId { get; set; }
        public string? ApproverId { get; set; }
        [ForeignKey("ApproverId")]
        public ApplicationUser? Approver { get; set; }
    }
}