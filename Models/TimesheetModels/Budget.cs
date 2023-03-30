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
        public double Days { get; set; }
        public int People { get; set; }
        [Display(Name = "Labour Grade")]
        public string? LabourCode { get; set; }
        public double UnallocatedDays { get; set; }
        public double UnallocatedPeople { get; set; }
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