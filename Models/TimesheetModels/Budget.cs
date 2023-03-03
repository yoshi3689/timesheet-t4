using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace TimesheetApp.Models.TimesheetModels
{
    public class Budget
    {
        [Key]
        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BudgetID { get; set; }

        public string? WPProjectId { get; set; }
        public double BudgetAmount { get; set; }
        public string? LabourCode { get; set; }
        [ForeignKey("LabourCode")]
        public LabourGrade? LabourGrade { get; set; }
    }
}