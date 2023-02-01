using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimesheetApp.Models.TimesheetModels;

public partial class TimesheetRow
{
    public int TimesheetRowId { get; set; }

    public int ProjectId { get; set; }

    public double TotalHoursRow { get; set; }

    public string? WorkPackageId { get; set; }

    public string? Notes { get; set; }

    public double Sat { get; set; }

    public double Sun { get; set; }

    public double Mon { get; set; }

    public double Tue { get; set; }

    public double Wed { get; set; }

    public double Thu { get; set; }

    public double Fri { get; set; }

    public int TimesheetId { get; set; }

    [ForeignKey("TimesheetId")]
    public Timesheet? Timesheet { get; set; }
}
