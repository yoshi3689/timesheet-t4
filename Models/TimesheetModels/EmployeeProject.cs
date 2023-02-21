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
    /// Model of the relationship between the employees and the projects that they are a part of.
    /// for the database.
    /// </summary>
    [PrimaryKey(nameof(UserId), nameof(ProjectId))]
    public class EmployeeProject
    {
        [Required]
        public string? UserId { get; set; }
        [Required]
        public string? ProjectId { get; set; }
        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }
        [ForeignKey("ProjectId")]
        public Project? Project { get; set; }
    }
}