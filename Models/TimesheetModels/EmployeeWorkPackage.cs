using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace TimesheetApp.Models.TimesheetModels
{
    /// <summary>
    /// Models the relationship between an employee and thier workpackage. used to store inforamtion that is specific to thier workpackage, such as the starting code and if they can approve timesheets in it.
    /// Used for the database.
    /// </summary>
    [PrimaryKey(nameof(UserId), nameof(WorkPackageId), nameof(WorkPackageProjectId))]
    public class EmployeeWorkPackage
    {
        [Required]
        public string? UserId { get; set; }
        [Required]
        public string? WorkPackageId { get; set; }
        [Required]
        public int? WorkPackageProjectId { get; set; }

        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }

        //see dbcontext for fk definition
        public virtual WorkPackage? WorkPackage { get; set; }
    }
}