using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimesheetApp.Models.TimesheetModels;

/// <summary>
/// Model for the timesheet table. Stores information for a specific sheet.
/// For the database.
/// </summary>
public partial class Timesheet
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int TimesheetId { get; set; }

    [Required]
    public DateOnly? EndDate { get; set; }

    public double TotalHours { get; set; }
    public double FlexHours { get; set; }
    public byte[]? EmployeeHash { get; set; }
    public byte[]? ApproverHash { get; set; }
    public double Overtime { get; set; }
    [Required]
    public string UserId { get; set; } = null!;
    public string? TimesheetApproverId { get; set; }
    public string? ApproverNotes { get; set; }

    [InverseProperty("Timesheet")]
    public virtual ICollection<TimesheetRow> TimesheetRows { get; } = new List<TimesheetRow>();

    [ForeignKey("UserId")]
    public ApplicationUser? User { get; set; } = null!;

    [ForeignKey("TimesheetApproverId")]
    public ApplicationUser? TimesheetApprover { get; set; }


    [NotMapped]
    public bool CurrentlySelected { get; set; }
}
