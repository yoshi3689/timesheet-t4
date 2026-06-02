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

    public List<Timesheet> GetUnapprovedTimesheets(string userId)
    {
        return _context.Timesheets!
            .Where(t => t.UserId == userId && t.ApproverHash == null)
            .OrderByDescending(c => c.EndDate)
            .ToList();
    }

    public Timesheet? CreateOrUpdateTimesheetWithRows(DateTime endDate, string userId)
    {
        Timesheet? result = null;
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
            return sheet;
        }
        var currentUser = _context.Users.Where(c => c.Id == userId).First();
        var myWps = _context.EmployeeWorkPackages.Where(c => c.UserId == userId).Include(c => c.WorkPackage);
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

    public Timesheet? GetTimesheetWithDetails(int timesheetId)
    {
        return _context.Timesheets
            .Where(c => c.TimesheetId == timesheetId)
            .Include(c => c.User)
            .FirstOrDefault();
    }

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
                    IsClosed = c.WorkPackage.IsClosed
                }
            })
            .ToList();
    }

    public List<Timesheet> GetTimesheetsToApprove(string approverId)
    {
        var empsApproving = _context.ApplicationUsers!
            .Where(c => c.TimesheetApproverId == approverId)
            .ToList();

        var approveSheets = new List<Timesheet>();
        foreach (var emp in empsApproving)
        {
            approveSheets.AddRange(
                _context.Timesheets!
                    .Where(t => t.UserId == emp.Id && t.EmployeeHash != null && t.ApproverHash == null)
                    .Include(c => c.User)
                    .Include(c => c.TimesheetRows)
                    .OrderBy(c => c.EndDate)
            );
        }

        var verifiedSheets = new List<Timesheet>();
        foreach (var sheet in approveSheets)
        {
            if (sheet.User == null || sheet.EmployeeHash == null || sheet.User.PublicKey == null)
            {
                Console.WriteLine("signature fail");
                continue;
            }
            if (!_signatureService.VerifySignature(sheet, sheet.User.PublicKey, sheet.EmployeeHash))
            {
                Console.WriteLine("signature fail");
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
            if (row.WorkPackageProjectId == 010 && row.WorkPackageId == "SICK")
            {
                timesheet.User!.SickDays -= row.TotalHoursRow / 8;
            }
            if (row.WorkPackageProjectId == 010 && row.WorkPackageId == "FLEX")
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
            WorkPackageProjectId = 010,
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
}
