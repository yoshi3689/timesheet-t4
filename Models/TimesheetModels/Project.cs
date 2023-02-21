using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

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
        public string? ProjectId { get; set; }
        [Required]
        public string? ProjectManagerId { get; set; }
        public double MasterBudget { get; set; }
        public double ActualCost { get; set; }
        [ForeignKey("ProjectManagerId")]
        public virtual ApplicationUser? ProjectManager { get; set; }

        [InverseProperty("Project")]
        public virtual ICollection<EmployeeProject> EmployeeProjects { get; } = new List<EmployeeProject>();
    }
}