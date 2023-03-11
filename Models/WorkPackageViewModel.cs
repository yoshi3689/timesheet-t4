
using TimesheetApp.Models.TimesheetModels;

namespace TimesheetApp.Models
{
    public class WorkPackageViewModel
    {
        public List<TimesheetApp.Models.TimesheetModels.WorkPackage> wps { get; set; } = new List<WorkPackage>();
        public List<Budget>? budgets { get; set; } = new List<Budget>();
        public WorkPackage WorkPackage { get; set; } = new WorkPackage();
    }

    public class CreateProjectViewModel
    {
        public Project project { get; set; } = new Project();
        public List<Budget>? budgets { get; set; } = new List<Budget>();
        public CreateProjectViewModel()
        {
            budgets = new List<Budget>();
        }
    }
}