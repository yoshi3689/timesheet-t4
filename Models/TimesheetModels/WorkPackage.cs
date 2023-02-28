using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimesheetApp.Controllers;

namespace TimesheetApp.Models.TimesheetModels
{
    /// <summary>
    /// Model for a workpackage.
    /// For the database.
    /// </summary>
    [PrimaryKey(nameof(WorkPackageId), nameof(ProjectId))]
    public class WorkPackage
    {
        [Required]
        [Remote(action: "CheckWorkPackage", controller: "Project", ErrorMessage = "Work Package must be unique for this project")]
        public string? WorkPackageId { get; set; }
        [Required]
        public string? ProjectId { get; set; }
        public string? ResponsibleUserId { get; set; }
        public string? ParentWorkPackageId { get; set; }
        public string? ParentWorkPackageProjectId { get; set; }
        public bool IsBottomLevel { get; set; }
        public double EstimatedCost { get; set; }
        public bool IsClosed { get; set; }

        [ForeignKey("ProjectId")]
        public Project? Project { get; set; }

        public WorkPackage? ParentWorkPackage { get; set; }

        [ForeignKey("ResponsibleUserId")]
        public ApplicationUser? ResponsibleUser { get; set; }

        [InverseProperty("ParentWorkPackage")]
        public virtual ICollection<WorkPackage> ChildWorkPackages { get; } = new List<WorkPackage>();
        public virtual ICollection<EmployeeWorkPackage>? EmployeeWorkPackages { get; set; }
        public virtual ICollection<TimesheetRow>? TimesheetRows { get; set; }

    }
}