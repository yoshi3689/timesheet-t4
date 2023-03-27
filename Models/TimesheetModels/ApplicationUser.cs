using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TimesheetApp.Controllers;
using TimesheetApp.Models.TimesheetModels;

namespace TimesheetApp.Models
{
    /// <summary>
    /// Class for storing information for the user table in the database.
    /// These fields are added to existing fields in the AspNetUsers table.
    /// </summary>
    [Index(nameof(EmployeeNumber), IsUnique = true)]
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [MaxLength(100), MinLength(2)]
        public string? FirstName { get; set; }
        [Required]
        [MaxLength(100), MinLength(2)]
        public string? LastName { get; set; }
        [Required]
        [IntLength(5, 10)]
        public int EmployeeNumber { get; set; }
        public double SickDays { get; set; }
        public double FlexTime { get; set; }
        [Required]
        public string? JobTitle { get; set; }
        public bool HasTempPassword { get; set; }
        public double Salary { get; set; }
        public byte[]? PublicKey { get; set; }
        public byte[]? PrivateKey { get; set; }
        [Required]
        public string? LabourGradeCode { get; set; }
        public string? SupervisorId { get; set; }
        public string? TimesheetApproverId { get; set; }

        [InverseProperty("User")]
        public virtual ICollection<Timesheet> Timesheets { get; } = new List<Timesheet>();

        [ForeignKey("LabourGradeCode")]
        public virtual LabourGrade? LabourGrade { get; set; }

        [ForeignKey("SupervisorId")]
        public ApplicationUser? Supervisor { get; set; }

        [ForeignKey("TimesheetApproverId")]
        public ApplicationUser? TimesheetApprover { get; set; }

        [InverseProperty("Supervisor")]
        public virtual ICollection<ApplicationUser> SupervisedUsers { get; } = new List<ApplicationUser>();

        [InverseProperty("TimesheetApprover")]
        public virtual ICollection<ApplicationUser> ApprovableUsers { get; } = new List<ApplicationUser>();

        [InverseProperty("ProjectManager")]
        public virtual ICollection<Project> ManagedProjects { get; } = new List<Project>();

        [InverseProperty("AssistantProjectManager")]
        public virtual ICollection<Project> AssistantManagedProjects { get; } = new List<Project>();

        [InverseProperty("ResponsibleUser")]
        public virtual ICollection<WorkPackage> SupervisedWorkPackage { get; } = new List<WorkPackage>();

        [InverseProperty("User")]
        public virtual ICollection<EmployeeProject> EmployeeProjects { get; } = new List<EmployeeProject>();

        [InverseProperty("User")]
        public virtual ICollection<EmployeeWorkPackage>? WorkPackages { get; set; }

        [InverseProperty("User")]
        public virtual ICollection<Notification>? Notifications { get; set; }
    }
}