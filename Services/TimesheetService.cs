using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TimesheetApp.Data;
using TimesheetApp.Models;
using TimesheetApp.Models.TimesheetModels;

namespace TimesheetApp.Services;

public class TimesheetService : ITimesheetService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISignatureService _signatureService;

    public TimesheetService(ApplicationDbContext context, UserManager<ApplicationUser> userManager, ISignatureService signatureService)
    {
        _context = context;
        _userManager = userManager;
        _signatureService = signatureService;
    }

    // Returns sheets the employee has not yet submitted (EmployeeHash == null), newest first.
    public List<Timesheet> GetUnapprovedTimesheets(string userId)
    {
        return _context.Timesheets!
            .Where(t => t.UserId == userId && t.ApproverHash == null)
            .OrderByDescending(c => c.EndDate)
            .ToList();
    }

    // Finds or creates the timesheet for the week containing endDate, then adds one row per
    // assigned open WP that doesn't already have a row in this sheet.
    // Returns the newly created Timesheet, or null if the sheet already existed (or was already submitted).
    public Timesheet? CreateOrUpdateTimesheetWithRows(DateTime endDate, string userId)
    {
        Timesheet? result = null;
        // Snap the given date forward to the nearest Friday (the week's end boundary).
        int offset = (7 - (int)endDate.DayOfWeek + (int)DayOfWeek.Friday) % 7;
        DateTime nextFriday = endDate.AddDays(offset);
        var sheet = _context.Timesheets.Where(c => c.EndDate == DateOnly.FromDateTime(nextFriday) && c.UserId == userId).FirstOrDefault();
        if (sheet == null)
        {
            sheet = new Timesheet
            {
                EndDate = DateOnly.FromDateTime(nextFriday),
                UserId = userId,
            };
            _context.Timesheets.Add(sheet);
            result = sheet;
            _context.SaveChanges();
        }
        else if (sheet.EmployeeHash != null)
        {
            // Sheet is already submitted — don't add rows to a locked sheet.
            return sheet;
        }
        var currentUser = _context.Users.Where(c => c.Id == userId).FirstOrDefault();
        if (currentUser == null) return null;
        var myWps = _context.EmployeeWorkPackages.Where(c => c.UserId == userId).Include(c => c.WorkPackage);
        // Load existing rows across all sheets to check for duplicates efficiently.
        var myExistingRows = _context.TimesheetRows.Where(c => c.Timesheet!.UserId == userId).Select(c => new TimesheetRow { WorkPackageId = c.WorkPackageId, WorkPackageProjectId = c.WorkPackageProjectId, TimesheetId = c.TimesheetId }).ToList();
        foreach (var wp in myWps)
        {
            if (wp.WorkPackage != null && !wp.WorkPackage.IsClosed && !myExistingRows.Where(c => c.TimesheetId == sheet.TimesheetId).Any(r => r.WorkPackageId == wp.WorkPackageId && r.WorkPackageProjectId == wp.WorkPackage!.ProjectId))
            {
                TimesheetRow row = new TimesheetRow
                {
                    WorkPackageId = wp.WorkPackageId,
                    WorkPackageProjectId = wp.WorkPackage!.ProjectId,
                    OriginalLabourCode = currentUser.LabourGradeCode,
                    TimesheetId = sheet.TimesheetId
                };
                _context.TimesheetRows.Add(row);
            }
        }
        _context.SaveChanges();
        return result;
    }

    public List<TimesheetRow> GetTimesheetRows(int timesheetId)
    {
        return _context.TimesheetRows
            .Where(c => c.TimesheetId == timesheetId)
            .Include(c => c.WorkPackage)
            .ToList();
    }

    public Timesheet? GetTimesheetById(int timesheetId)
    {
        return _context.Timesheets.Find(timesheetId);
    }

    // Loads the sheet with its User navigation property. Needed when the approver's identity is required.
    public Timesheet? GetTimesheetWithDetails(int timesheetId)
    {
        return _context.Timesheets
            .Where(c => c.TimesheetId == timesheetId)
            .Include(c => c.User)
            .FirstOrDefault();
    }

    // Updates a single row's hours. Rejects if the sheet is already submitted (EmployeeHash != null).
    // Validates that no single day column exceeds 24h across all rows in the sheet.
    // Updates Timesheet.TotalHours incrementally (new total - old total) rather than recomputing the full sum.
    // Returns (errors, null) on validation failure, or (null, updatedRowFields) on success.
    public (Dictionary<int, string>? errors, object? result) UpdateRow(TimesheetRow timesheetRow)
    {
        var timesheetRows = _context.TimesheetRows
            .Where(c => c.TimesheetId == timesheetRow.TimesheetId)
            .Include(c => c.Timesheet)
            .Include(c => c.WorkPackage)
            .ToList();

        var oldRow = timesheetRows.Where(c => c.TimesheetRowId == timesheetRow.TimesheetRowId).FirstOrDefault();
        if (oldRow == null || oldRow.Timesheet == null || oldRow.Timesheet.EmployeeHash != null)
        {
            return (null, null);
        }

        Dictionary<int, string> validationErrors = new Dictionary<int, string>();
        oldRow.Timesheet.TotalHours += timesheetRow.TotalHoursRow - oldRow.TotalHoursRow;
        oldRow.packedHours = timesheetRow.packedHours;
        oldRow.Notes = timesheetRow.Notes;
        oldRow.TotalHoursRow = timesheetRow.TotalHoursRow;

        for (int i = 0; i < 7; i++)
        {
            float total = 0;
            foreach (var row in timesheetRows)
            {
                total += row.getHour(i);
            }
            if (total > 24)
            {
                validationErrors.Add(i, "Cannot have more then 24 hours in a column.");
            }
        }

        if (validationErrors.Count > 0)
        {
            return (validationErrors, null);
        }

        _context.SaveChanges();
        return (null, new { oldRow.Timesheet.TotalHours, oldRow.Sun, oldRow.Mon, oldRow.Tue, oldRow.Wed, oldRow.Thu, oldRow.Fri, oldRow.Sat, oldRow.TotalHoursRow, oldRow.ProjectId, oldRow.WorkPackageId, oldRow.TimesheetRowId, oldRow.Notes });
    }

    // Returns a lightweight projection with only the fields the frontend needs.
    // Avoids loading full entity graphs — WorkPackage is projected to 3 fields only.
    public List<TimesheetRow> GetTimesheetRowDtos(int timesheetId)
    {
        return _context.TimesheetRows
            .Where(c => c.TimesheetId == timesheetId)
            .Include(c => c.WorkPackage)
            .Select(c => new TimesheetRow
            {
                TimesheetRowId = c.TimesheetRowId,
                TimesheetId = c.TimesheetId,
                WorkPackageProjectId = c.WorkPackageProjectId,
                WorkPackageId = c.WorkPackageId,
                Notes = c.Notes,
                packedHours = c.packedHours,
                OriginalLabourCode = c.OriginalLabourCode,
                WorkPackage = new WorkPackage
                {
                    ProjectId = c.WorkPackage!.ProjectId,
                    WorkPackageId = c.WorkPackage.WorkPackageId,
                    IsClosed = c.WorkPackage.IsClosed,
                    Title = c.WorkPackage.Title
                }
            })
            .ToList();
    }

    // Returns all submitted-but-not-approved sheets for the approver's assigned employees.
    // Each sheet's RSA signature is verified before inclusion — sheets with invalid or missing
    // signatures are silently dropped. N+1 query: one DB round-trip per employee (known backlog item).
    public List<Timesheet> GetTimesheetsToApprove(string approverId)
    {
        var approveSheets = _context.Timesheets!
            .Where(t => t.User!.TimesheetApproverId == approverId &&
                        t.EmployeeHash != null &&
                        t.ApproverHash == null)
            .Include(c => c.User)
            .Include(c => c.TimesheetRows)
            .OrderBy(c => c.EndDate)
            .ToList();

        var verifiedSheets = new List<Timesheet>();
        foreach (var sheet in approveSheets)
        {
            if (sheet.User == null || sheet.EmployeeHash == null || sheet.User.PublicKey == null)
            {
                Console.WriteLine($"signature skip: timesheet {sheet.TimesheetId} missing user, hash, or public key");
                continue;
            }
            if (!_signatureService.VerifySignature(sheet, sheet.User.PublicKey, sheet.EmployeeHash))
            {
                Console.WriteLine($"signature invalid: timesheet {sheet.TimesheetId} failed RSA verification");
                continue;
            }
            verifiedSheets.Add(sheet);
        }
        return verifiedSheets;
    }

    public List<Timesheet> GetApprovedTimesheets(string userId)
    {
        return _context.Timesheets!
            .Where(t => t.UserId == userId && t.ApproverHash != null)
            .OrderByDescending(c => c.EndDate)
            .ToList();
    }

    // Signs the timesheet with the employee's RSA private key (decrypted using their password).
    // Also records flex and overtime designations on both the sheet and the user's running totals.
    // Validates that flex + overtime does not exceed hours worked above 40.
    public async Task<(bool success, string? error, byte[]? hash)> SubmitTimesheetAsync(int timesheetId, string userId, string password, double? flexhours, double? overtime)
    {
        var user = await _userManager.FindByIdAsync(userId);
        var timesheet = _context.Timesheets
            .Where(c => c.TimesheetId == timesheetId)
            .Include(c => c.TimesheetRows)
            .FirstOrDefault();

        if (user == null || timesheet == null || timesheet.UserId != user.Id || user.PrivateKey == null)
        {
            return (false, "badrequest", null);
        }

        timesheet.FlexHours = flexhours ?? 0;
        timesheet.Overtime = overtime ?? 0;
        user.Overtime += overtime ?? 0;
        user.FlexTime += flexhours ?? 0;

        // Float comparison — can be imprecise for values like 7.1 + 7.1 + ... See backlog.
        if (timesheet.TotalHours > 40 && timesheet.TotalHours != flexhours + overtime + 40)
        {
            return (false, "You cannot have more flexhours and overtime then you worked.", null);
        }

        byte[]? timesheetHash = _signatureService.HashTimesheet(timesheet, password, user.PrivateKey);
        if (timesheetHash == null)
        {
            return (false, "unauthorized", null);
        }

        timesheet.EmployeeHash = timesheetHash;
        _context.Update(timesheet);
        _context.SaveChanges();
        return (true, null, timesheetHash);
    }

    // Signs the sheet with the approver's RSA private key, marking it approved.
    // Also deducts SICK and FLEX special rows from the employee's running balances.
    public async Task<(bool success, string? error, byte[]? hash)> ApproveTimesheetAsync(int timesheetId, string approverId, string password)
    {
        var user = await _userManager.FindByIdAsync(approverId);
        var timesheet = _context.Timesheets
            .Where(c => c.TimesheetId == timesheetId)
            .Include(c => c.TimesheetRows)
            .Include(c => c.User)
            .FirstOrDefault();

        if (user == null || timesheet == null || user.PrivateKey == null || timesheet.User == null || timesheet.User.TimesheetApproverId != user.Id)
        {
            return (false, "badrequest", null);
        }

        byte[]? timesheetHash = _signatureService.HashTimesheet(timesheet, password, user.PrivateKey);
        if (timesheetHash == null)
        {
            return (false, "unauthorized", null);
        }

        foreach (var row in timesheet.TimesheetRows)
        {
            // Project 10 ("Extras") holds all special row types: SICK, FLEX, VACN, SHOL.
            if (row.WorkPackageProjectId == 10 && row.WorkPackageId == "SICK")
            {
                timesheet.User!.SickDays -= row.TotalHoursRow / 8;
            }
            if (row.WorkPackageProjectId == 10 && row.WorkPackageId == "FLEX")
            {
                timesheet.User!.FlexTime -= row.TotalHoursRow;
            }
        }

        timesheet.ApproverHash = timesheetHash;
        timesheet.TimesheetApproverId = user.Id;
        _context.Update(timesheet);
        _context.SaveChanges();
        return (true, null, timesheetHash);
    }

    // Clears both hashes, returning the sheet to Draft state.
    // ApproverNotes is set to a space " " when no message is provided — a non-null value
    // is what signals "declined" in status derivation (null = not yet acted on).
    public async Task<bool> DeclineTimesheetAsync(int timesheetId, string approverId, string password, string? approverNotes)
    {
        var user = await _userManager.FindByIdAsync(approverId);
        var timesheet = _context.Timesheets
            .Where(c => c.TimesheetId == timesheetId)
            .Include(c => c.TimesheetRows)
            .Include(c => c.User)
            .FirstOrDefault();

        if (user == null || timesheet == null || user.PrivateKey == null || timesheet.User == null || timesheet.User.TimesheetApproverId != user.Id)
        {
            return false;
        }

        timesheet.ApproverHash = null;
        timesheet.EmployeeHash = null;
        timesheet.ApproverNotes = approverNotes ?? " ";
        _context.Update(timesheet);
        _context.SaveChanges();
        return true;
    }

    // Adds a special "custom" row for non-project time: SICK, VACN, SHOL, or FLEX.
    // These rows always belong to Project 10 ("Extras") — the hardcoded special-purpose project.
    public TimesheetRow? AddCustomRow(int timesheetId, string userId, string type, string? labourGradeCode)
    {
        var timesheet = _context.Timesheets
            .Where(c => c.TimesheetId == timesheetId)
            .FirstOrDefault();

        if (timesheet == null || timesheet.UserId != userId)
        {
            return null;
        }

        TimesheetRow row = new TimesheetRow
        {
            WorkPackageId = type,
            WorkPackageProjectId = 10, // Project 10 = "Extras" (SICK/VACN/SHOL/FLEX rows live here)
            OriginalLabourCode = labourGradeCode,
            TimesheetId = timesheetId
        };

        try
        {
            _context.TimesheetRows.Add(row);
            _context.SaveChanges();
        }
        catch (Exception)
        {
            return null;
        }

        row.Timesheet = null;
        return row;
    }

    public bool DeleteTimesheet(int timesheetId)
    {
        var timesheet = _context.Timesheets
            .Include(t => t.TimesheetRows)
            .FirstOrDefault(t => t.TimesheetId == timesheetId);
        if (timesheet == null) return false;
        _context.TimesheetRows.RemoveRange(timesheet.TimesheetRows);
        _context.Timesheets.Remove(timesheet);
        _context.SaveChanges();
        return true;
    }
}
