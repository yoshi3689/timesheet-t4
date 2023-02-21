using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace TimesheetApp.Models.TimesheetModels
{
    /// <summary>
    /// Used to store a timesheet row once it is encryped, after being signed.
    /// DB Model
    /// </summary>
    public class ApprovedTimesheetRow
    {
        [Key]
        [Required]
        public int ApprovedTimesheetRowId { get; set; }
        [Required]
        public string? ProjectId { get; set; }
        public string? TotalHoursRow { get; set; }
        [Required]
        public string? WorkPackageId { get; set; }
        public string? Notes { get; set; }
        public string? Sat { get; set; }
        public string? Sun { get; set; }
        public string? Mon { get; set; }
        public string? Tue { get; set; }
        public string? Wed { get; set; }
        public string? Thu { get; set; }
        public string? Fri { get; set; }
        [Required]
        public int? ApprovedTimesheetId { get; set; }
        [ForeignKey("ApprovedTimesheetId")]
        public ApprovedTimesheet? ApprovedTimesheet { get; set; }
    }
}