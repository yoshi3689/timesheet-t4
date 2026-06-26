using TimesheetApp.Models.TimesheetModels;

namespace TimesheetApp.Services;

public interface ITimesheetService
{
    List<Timesheet> GetUnapprovedTimesheets(string userId);
    Timesheet? CreateOrUpdateTimesheetWithRows(DateTime endDate, string userId);
    List<TimesheetRow> GetTimesheetRows(int timesheetId);
    Timesheet? GetTimesheetById(int timesheetId);
    Timesheet? GetTimesheetWithDetails(int timesheetId);
    (Dictionary<int, string>? errors, object? result) UpdateRow(TimesheetRow timesheetRow);
    List<TimesheetRow> GetTimesheetRowDtos(int timesheetId);
    (List<Timesheet> items, int total) GetTimesheetsToApprove(
        string approverId,
        string status = "submitted",
        DateOnly? from = null,
        DateOnly? to = null,
        int page = 0,
        int pageSize = 15);
    List<Timesheet> GetApprovedTimesheets(string userId);
    Task<(bool success, string? error, byte[]? hash)> SubmitTimesheetAsync(int timesheetId, string userId, string password, double? flexhours, double? overtime);
    Task<(bool success, string? error, byte[]? hash)> ApproveTimesheetAsync(int timesheetId, string approverId, string password);
    Task<bool> DeclineTimesheetAsync(int timesheetId, string approverId, string password, string? approverNotes);
    TimesheetRow? AddCustomRow(int timesheetId, string userId, string type, string? labourGradeCode);
    bool DeleteTimesheet(int timesheetId);
}
