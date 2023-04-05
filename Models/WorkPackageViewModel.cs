
using TimesheetApp.Models.TimesheetModels;

namespace TimesheetApp.Models
{
  public class WorkPackageViewModel
  {
    public List<TimesheetApp.Models.TimesheetModels.WorkPackage> wps { get; set; } = new List<WorkPackage>();
    public List<Budget>? budgets { get; set; } = new List<Budget>();
    public WorkPackage WorkPackage { get; set; } = new WorkPackage();
    public List<LabourGrade>? LabourGrades { get; set; }
  }
  public class LowestWorkPackageBAndEViewModel
  {
    public List<Budget>? budgets { get; set; }
    public List<ResponsibleEngineerEstimate>? estimates { get; set; }
  }

  public class CreateProjectViewModel
  {
    public Project project { get; set; } = new Project();
    public List<Budget>? budgets { get; set; } = new List<Budget>();
  }

  public class EmployeeWorkPackageViewModel
  {
    public ApplicationUser Employee { get; set; } = new ApplicationUser();
    public bool Assigned { get; set; }

  }

  public class SignTimesheetViewModel
  {
    public string? Password { get; set; }
    public int Timesheet { get; set; }
    public string? ApproverNotes { get; set; }

    public double? Overtime { get; set; }
    public double? FlexHours { get; set; }
  }
}