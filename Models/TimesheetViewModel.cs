namespace TimesheetApp.Models.TimesheetModels
{
    public class TimesheetViewModel
    {
        public IEnumerable<Timesheet>? Timesheets { get; set; }
        public IEnumerable<TimesheetRow>? TimesheetRows { get; set; }
        public ApplicationUser? CurrentUser { get; set; }
    }

    public class CustomRowModel
    {
        public string? Type { get; set; }
        public string? TimesheetId { get; set; }
    }
}