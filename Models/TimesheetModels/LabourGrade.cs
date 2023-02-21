using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace TimesheetApp.Models.TimesheetModels
{
    /// <summary>
    /// Moel for the different labor grades and the rate that they charge as.
    /// for the database;
    /// </summary>
    public class LabourGrade
    {
        [Key]
        [Required]
        [MaxLength(2), MinLength(2)]
        public string? LabourCode { get; set; }
        [Required]
        public double Rate { get; set; }
        [InverseProperty("LabourGrade")]
        public virtual ICollection<ApplicationUser> ApplicationUsers { get; } = new List<ApplicationUser>();

        [InverseProperty("OriginalLabourGrade")]
        public virtual ICollection<EmployeeWorkPackage>? OriginalEmployees { get; set; }


    }
}