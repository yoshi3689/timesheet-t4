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
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(2), MinLength(2)]
        public string? LabourCode { get; set; }
        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Only positive number allowed.")]
        public double Rate { get; set; }
        [Range(0, int.MaxValue, ErrorMessage = "Only positive number allowed.")]
        public int Year { get; set; }
    }
}