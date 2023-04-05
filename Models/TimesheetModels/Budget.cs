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
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BudgetId { get; set; }
        public string WPProjectId { get; set; } = "";
        [Display(Name = "Effort in Hours")]
        public double BudgetAmount
        {
            get
            {
                return Days * People;
            }
        }
        [Range(0, double.MaxValue, ErrorMessage = "Must be positive.")]
        public double Days { get; set; }
        [Range(0, int.MaxValue, ErrorMessage = "Must be positive.")]
        public int People { get; set; }
        [Display(Name = "Labour Grade")]
        public string? LabourCode { get; set; }
        public double UnallocatedDays { get; set; }
        public int UnallocatedPeople { get; set; }
        public double RemainingPDs
        {
            get
            {
                return UnallocatedDays * UnallocatedPeople;
            }
        }
        public bool isREBudget { get; set; }

        [NotMapped]
        public double Rate { get; set; }

    }
}