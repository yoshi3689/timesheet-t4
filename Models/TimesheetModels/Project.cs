using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using TimesheetApp.Controllers;

namespace TimesheetApp.Models.TimesheetModels
{
    /// <summary>
    /// Model of a project, in order to keep track of project specific information.
    /// For the database.
    /// </summary>
    public class Project
    {
        [Key]
        [Required]
        [Display(Name = "Id")]
        [Range(0, int.MaxValue, ErrorMessage = "Must be positive.")]
        public int ProjectId { get; set; }
        [Display(Name = "Name")]
        [Required]
        public string ProjectTitle { get; set; } = "";
        [Required]
        [Display(Name = "Manager")]
        public string? ProjectManagerId { get; set; }
        [Display(Name = "Asst. Manager")]
        public string? AssistantProjectManagerId { get; set; }
        [Display(Name = "Budget")]
        public double TotalBudget { get; set; }
        [Display(Name = "Actual Cost")]
        public double ActualCost { get; set; }
        public bool IsClosed { get; set; }

        [ForeignKey("ProjectManagerId")]
        [Display(Name = "Manager")]
        public virtual ApplicationUser? ProjectManager { get; set; }
        [ForeignKey("AssistantProjectManagerId")]
        [Display(Name = "Asst. Manager")]
        public virtual ApplicationUser? AssistantProjectManager { get; set; }

        [InverseProperty("Project")]
        public virtual ICollection<EmployeeProject> EmployeeProjects { get; } = new List<EmployeeProject>();
    }
}