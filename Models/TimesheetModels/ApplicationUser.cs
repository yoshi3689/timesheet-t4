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
    [Index(nameof(SupervisorId))]
    [Index(nameof(TimesheetApproverId))]
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [MaxLength(100), MinLength(2)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = null!;
        [Required]
        [MaxLength(100), MinLength(2)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = null!;
        [Required]
        [IntLength(5, 10)]
        [Range(0, long.MaxValue, ErrorMessage = "Only positive number allowed.")]
        [Display(Name = "Employee Number")]
        public long EmployeeNumber { get; set; }
        [Display(Name = "Sick Days")]
        [Range(0, double.MaxValue, ErrorMessage = "Only positive number allowed.")]
        public double SickDays { get; set; }
        [Range(0, double.MaxValue, ErrorMessage = "Only positive number allowed.")]
        [Display(Name = "Flex Time")]
        public double FlexTime { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Only positive number allowed.")]
        public double Overtime { get; set; }
        [Required]
        [Display(Name = "Job Title")]
        public string JobTitle { get; set; } = null!;
        public bool HasTempPassword { get; set; }
        [Display(Name = "Salary")]
        public double Salary { get; set; }
        public byte[]? PublicKey { get; set; }
        public byte[]? PrivateKey { get; set; }
        [Required]
        [Display(Name = "Labour Grade")]
        public string LabourGradeCode { get; set; } = null!;
        [Display(Name = "Supervisor")]
        public string? SupervisorId { get; set; }
        [Display(Name = "Timesheet Approver")]
        public string? TimesheetApproverId { get; set; }
        [Display(Name = "Two-Factor Override")]
        public bool? TwoFactorPolicyOverride { get; set; }

        [InverseProperty("User")]
        public virtual ICollection<Timesheet> Timesheets { get; } = new List<Timesheet>();

        [ForeignKey("SupervisorId")]
        public ApplicationUser? Supervisor { get; set; }

        [ForeignKey("TimesheetApproverId")]
        [Display(Name = "Timesheet Approver")]
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
        public virtual ICollection<EmployeeWorkPackage>? WorkPackages { get; set; }

        [InverseProperty("User")]
        public virtual ICollection<Notification>? Notifications { get; set; }

        [NotMapped]
        public bool Selected { get; set; }
    }
}