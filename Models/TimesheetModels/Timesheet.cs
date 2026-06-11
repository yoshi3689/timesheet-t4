using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TimesheetApp.Models.TimesheetModels;

// A timesheet covers one SAT–FRI week, identified by its Friday EndDate.
// Status is derived from the hash fields — there is no separate status column:
//   EmployeeHash == null                          → Draft
//   EmployeeHash != null && ApproverHash == null  → Submitted (awaiting approval)
//   ApproverHash != null                          → Approved
//   EmployeeHash == null && ApproverNotes != null → Declined (returned to employee)
[Index(nameof(UserId))]
public partial class Timesheet
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int TimesheetId { get; set; }

    // Always a Friday — the week boundary. Snapped by TimesheetService.CreateOrUpdateTimesheetWithRows.
    [Required]
    public DateOnly EndDate { get; set; }

    // Running sum of all row totals. Updated incrementally by UpdateRow, not recomputed from rows.
    [Range(0, double.MaxValue, ErrorMessage = "Only positive number allowed.")]
    public double TotalHours { get; set; }

    // Hours above 40 designated as banked flex time. Set on submit by the employee.
    [Range(0, double.MaxValue, ErrorMessage = "Only positive number allowed.")]
    public double FlexHours { get; set; }

    // RSA signature of this sheet signed by the employee using their private key.
    // Null = draft (not yet submitted). Set by SubmitTimesheetAsync.
    public byte[]? EmployeeHash { get; set; }

    // RSA signature signed by the approver. Null = not yet approved. Set by ApproveTimesheetAsync.
    public byte[]? ApproverHash { get; set; }

    // Hours above 40 designated as overtime pay. Set on submit by the employee.
    [Range(0, double.MaxValue, ErrorMessage = "Only positive number allowed.")]
    public double Overtime { get; set; }

    [Required]
    public string UserId { get; set; } = null!;

    // Populated when approved or declined — records which approver acted on this sheet.
    public string? TimesheetApproverId { get; set; }

    // Non-null (even a single space " ") signals the sheet was declined. See DeclineTimesheetAsync.
    public string? ApproverNotes { get; set; }

    [InverseProperty("Timesheet")]
    public virtual ICollection<TimesheetRow> TimesheetRows { get; } = new List<TimesheetRow>();

    [ForeignKey("UserId")]
    public ApplicationUser? User { get; set; } = null!;

    [ForeignKey("TimesheetApproverId")]
    public ApplicationUser? TimesheetApprover { get; set; }

    // UI-only flag — never persisted to DB.
    [NotMapped]
    public bool CurrentlySelected { get; set; }
}
