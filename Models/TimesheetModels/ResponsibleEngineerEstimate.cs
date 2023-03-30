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
        public string? ProjectId { get; set; }
        public string? WPProjectId { get; set; }
        public double EstimatedCost { get; set; }
        public DateOnly? Date { get; set; }
        public string? LabourCode { get; set; }
    }
}