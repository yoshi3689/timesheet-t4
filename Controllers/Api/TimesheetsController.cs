using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TimesheetApp.Models;
using TimesheetApp.Models.TimesheetModels;
using TimesheetApp.Services;

namespace TimesheetApp.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "KeyRequirement")]
    public class TimesheetsController : ControllerBase
    {
        private readonly ITimesheetService _timesheetService;
        private readonly UserManager<ApplicationUser> _userManager;

        public TimesheetsController(
            ITimesheetService timesheetService,
            UserManager<ApplicationUser> userManager)
        {
            _timesheetService = timesheetService;
            _userManager = userManager;
        }

        // GET /api/timesheets/unapproved
        [HttpGet("unapproved")]
        public IActionResult GetUnapproved()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Ok(_timesheetService.GetUnapprovedTimesheets(userId!));
        }

        // GET /api/timesheets/approved
        [HttpGet("approved")]
        public IActionResult GetApproved()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Ok(_timesheetService.GetApprovedTimesheets(userId!).Select(t => new
            {
                t.TotalHours,
                t.EndDate,
                t.TimesheetId,
                t.EmployeeHash,
                t.ApproverHash,
                t.FlexHours,
                t.Overtime
            }));
        }

        // GET /api/timesheets/to-approve
        [HttpGet("to-approve")]
        public IActionResult GetToApprove()
        {
            var approverId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Ok(_timesheetService.GetTimesheetsToApprove(approverId!));
        }

        // POST /api/timesheets
        [HttpPost]
        public IActionResult CreateTimesheet([FromBody] string end)
        {
            if (string.IsNullOrWhiteSpace(end))
                return BadRequest("Please choose a date.");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int offset = (7 - (int)Convert.ToDateTime(end).DayOfWeek + (int)DayOfWeek.Friday) % 7;
            DateTime nextFriday = Convert.ToDateTime(end).AddDays(offset);

            var existingSheets = _timesheetService.GetUnapprovedTimesheets(userId!);
            if (existingSheets.Any(s => s.EndDate == DateOnly.FromDateTime(nextFriday)))
                return BadRequest("Timesheet already exists for this week.");

            var created = _timesheetService.CreateOrUpdateTimesheetWithRows(Convert.ToDateTime(end), userId!);
            if (created == null)
                return BadRequest("Timesheet already exists for this week.");

            return Ok(new Timesheet
            {
                TotalHours = created.TotalHours,
                EndDate = created.EndDate,
                TimesheetId = created.TimesheetId,
                EmployeeHash = created.EmployeeHash,
                FlexHours = created.FlexHours,
                Overtime = created.Overtime
            });
        }

        // POST /api/timesheets/rows/update
        [HttpPost("rows/update")]
        public IActionResult UpdateRow([FromBody] TimesheetRow timesheetRow)
        {
            var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var timesheet = _timesheetService.GetTimesheetById(timesheetRow.TimesheetId);
            if (timesheet == null || timesheet.UserId != callerId)
                return Forbid();

            var (errors, result) = _timesheetService.UpdateRow(timesheetRow);
            if (result == null && errors == null) return BadRequest();
            if (errors != null) return BadRequest(errors);
            return Ok(result);
        }

        // POST /api/timesheets/get
        [HttpPost("get")]
        public IActionResult GetTimesheet([FromBody] string timesheetId)
        {
            if (!int.TryParse(timesheetId, out int tid)) return BadRequest();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var timesheet = _timesheetService.GetTimesheetWithDetails(tid);
            if (timesheet == null ||
                (timesheet.UserId != userId && timesheet.User?.TimesheetApproverId != userId))
                return BadRequest();

            return Ok(_timesheetService.GetTimesheetRowDtos(tid));
        }

        // POST /api/timesheets/submit
        [HttpPost("submit")]
        public async Task<IActionResult> SubmitTimesheet([FromBody] SignTimesheetViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || model.Password == null) return BadRequest();

            var (success, error, _) = await _timesheetService.SubmitTimesheetAsync(
                model.Timesheet, user.Id, model.Password, model.Flexhours, model.Overtime);

            if (!success)
            {
                if (error == "badrequest") return BadRequest();
                if (error == "unauthorized") return Unauthorized();
                return BadRequest(error);
            }
            return Ok(_timesheetService.GetTimesheetRowDtos(model.Timesheet));
        }

        // POST /api/timesheets/approve
        [HttpPost("approve")]
        public async Task<IActionResult> ApproveTimesheet([FromBody] SignTimesheetViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || model.Password == null) return BadRequest();

            var (success, error, _) = await _timesheetService.ApproveTimesheetAsync(
                model.Timesheet, user.Id, model.Password);

            if (!success)
            {
                if (error == "badrequest") return BadRequest();
                if (error == "unauthorized") return Unauthorized();
                return BadRequest(error);
            }
            return Ok(_timesheetService.GetTimesheetRowDtos(model.Timesheet));
        }

        // POST /api/timesheets/decline
        [HttpPost("decline")]
        public async Task<IActionResult> DeclineTimesheet([FromBody] SignTimesheetViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || model.Password == null) return BadRequest();

            bool success = await _timesheetService.DeclineTimesheetAsync(
                model.Timesheet, user.Id, model.Password, model.ApproverNotes);
            if (!success) return BadRequest();
            return Ok(_timesheetService.GetTimesheetRowDtos(model.Timesheet));
        }

        // DELETE /api/timesheets/{id} — admin only, for test teardown
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public IActionResult DeleteTimesheet(int id)
        {
            var deleted = _timesheetService.DeleteTimesheet(id);
            if (!deleted) return NotFound();
            return Ok();
        }

        // POST /api/timesheets/rows/custom
        [HttpPost("rows/custom")]
        public async Task<IActionResult> AddCustomRow([FromBody] CustomRowModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return BadRequest();

            var row = _timesheetService.AddCustomRow(
                model.TimesheetId, user.Id, model.Type!, user.LabourGradeCode);
            if (row == null) return BadRequest();
            return Ok(row);
        }
    }
}
