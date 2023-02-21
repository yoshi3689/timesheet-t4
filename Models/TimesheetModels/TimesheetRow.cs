using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimesheetApp.Models.TimesheetModels;

/// <summary>
/// Model for a specific row in a timesheet.
/// For the database.
/// </summary>
public partial class TimesheetRow
{
    [Key]
    [Required]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int TimesheetRowId { get; set; }

    [Required]
    [MaxLength(255)]
    public string? ProjectId { get; set; }

    public double TotalHoursRow { get; set; }
    [Required]
    [MaxLength(255)]
    public string? WorkPackageId { get; set; }

    [Required]
    [MaxLength(255)]
    public string? WorkPackageProjectId { get; set; }

    public string? Notes { get; set; }

    public double Sat { get; set; }

    public double Sun { get; set; }

    public double Mon { get; set; }

    public double Tue { get; set; }

    public double Wed { get; set; }

    public double Thu { get; set; }

    public double Fri { get; set; }
    [Required]
    public int TimesheetId { get; set; }
    [ForeignKey("TimesheetId")]
    public Timesheet? Timesheet { get; set; }
    [ForeignKey("ProjectId")]
    public Project? Project { get; set; }
    //set dbcontext for fk
    public WorkPackage? WorkPackage { get; set; }

}
