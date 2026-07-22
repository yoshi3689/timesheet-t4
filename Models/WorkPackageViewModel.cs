
using System.ComponentModel.DataAnnotations;
using TimesheetApp.Models.TimesheetModels;

namespace TimesheetApp.Models
{
    public class WorkPackageIdRequest
    {
        public string WorkPackageId { get; set; } = "";
    }

    // Create-time request shape for POST .../split. Deliberately excludes WorkPackageId —
    // the server always generates it (Guid.NewGuid()) — so client-side omission of a dead
    // placeholder field can't trip [Required] validation the way it does on the raw WorkPackage entity.
    public class CreateWorkPackageFields
    {
        [Required]
        public string Title { get; set; } = "";
        public string? ParentWorkPackageId { get; set; }
        public string? ResponsibleUserId { get; set; }
    }

    public class CreateWorkPackageRequest
    {
        public CreateWorkPackageFields WorkPackage { get; set; } = new();
        public List<Budget>? budgets { get; set; } = new List<Budget>();
    }

    public class WorkPackageUpdateRequest
    {
        public string WorkPackageId { get; set; } = "";
        public string Title { get; set; } = "";
        public string? ResponsibleUserId { get; set; }
    }

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

    public class EstimateEntry
    {
        public string LabourCode { get; set; } = "";
        public double EstimatedCost { get; set; }
    }

    public class SubmitEstimateRequest
    {
        public int ProjectId { get; set; }
        public List<EstimateEntry> Entries { get; set; } = new();
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
        public double? Flexhours { get; set; }
    }
}