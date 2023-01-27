using System;
using System.Collections.Generic;

namespace TimesheetApp.Models.TimesheetModels;

public partial class Timesheet
{
    public int TimesheetId { get; set; }

    public DateOnly? EndDate { get; set; }

    public double? TotalHours { get; set; }

    public string UserId { get; set; } = null!;

    public virtual ICollection<TimesheetRow> TimesheetRows { get; } = new List<TimesheetRow>();

    public virtual AspNetUser User { get; set; } = null!;
}
