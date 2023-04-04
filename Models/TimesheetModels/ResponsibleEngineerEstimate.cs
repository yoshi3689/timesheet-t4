using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace TimesheetApp.Models.TimesheetModels
{
    public class ResponsibleEngineerEstimate
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string? WPProjectId { get; set; }
        [Range(0, double.MaxValue, ErrorMessage = "Only positive number allowed.")]
        [Display(Name = "Estimated Remaining P.D.")]
        public double EstimatedCost { get; set; }
        [Display(Name = "End Date")]
        public DateOnly? Date { get; set; }
        public string? LabourCode { get; set; }
    }
}