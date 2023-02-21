namespace TimesheetApp.Models.TimesheetModels
{
    public class TimesheetViewModel
    {
        public IEnumerable<Timesheet>? Timesheets { get; set; }
        public IEnumerable<TimesheetRow>? TimesheetRows { get; set; }
    }
}