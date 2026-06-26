using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using TimesheetApp.Models.TimesheetModels;
using TimesheetApp.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using TimesheetApp.Services;

namespace TimesheetApp.Controllers
{
    [Authorize(Policy = "KeyRequirement")]
    public class TimesheetController : Controller
    {
        private readonly ITimesheetService _timesheetService;
        private readonly UserManager<ApplicationUser> _userManager;

        public TimesheetController(ITimesheetService timesheetService, UserManager<ApplicationUser> userManager)
        {
            _timesheetService = timesheetService;
            _userManager = userManager;
        }

        [Authorize(Policy = "KeyRequirement")]
        public IActionResult Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userSheets = _timesheetService.GetUnapprovedTimesheets(userId!);

            if (userSheets.Count == 0)
            {
                userSheets.Add(_timesheetService.CreateOrUpdateTimesheetWithRows(DateTime.Now, userId ?? "0") ?? new Timesheet());
            }

            DateTime currentDate = DateTime.Today;
            var timesheet = userSheets.AsEnumerable()
                .OrderBy(ts => Math.Abs((DateTime.Parse(ts.EndDate.ToString()!) - currentDate.Date).TotalDays))
                .FirstOrDefault();

            if (timesheet != null)
            {
                timesheet.CurrentlySelected = true;
            }

            _timesheetService.CreateOrUpdateTimesheetWithRows(DateTime.Parse(timesheet!.EndDate.ToString()!), userId ?? "0");

            var rows = _timesheetService.GetTimesheetRows(timesheet!.TimesheetId);

            var currentUser = _userManager.GetUserAsync(User).GetAwaiter().GetResult();
            var model = new TimesheetViewModel()
            {
                Timesheets = userSheets,
                TimesheetRows = rows,
                CurrentUser = currentUser
            };

            return View(model);
        }

        [Authorize(Policy = "KeyRequirement")]
        public IActionResult ToApprove()
        {
            var approverId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var (verifiedSheets, _) = _timesheetService.GetTimesheetsToApprove(approverId!);

            if (verifiedSheets.Count == 0)
            {
                return View(new TimesheetViewModel());
            }

            DateTime currentDate = DateTime.Today;
            var timesheet = verifiedSheets
                .OrderBy(ts => Math.Abs((DateTime.Parse(ts.EndDate.ToString()!) - currentDate.Date).TotalDays))
                .FirstOrDefault();

            if (timesheet != null)
            {
                timesheet.CurrentlySelected = true;
            }

            var rows = _timesheetService.GetTimesheetRows(timesheet!.TimesheetId)
                .Where(c => c.TimesheetId == timesheet.TimesheetId)
                .ToList();

            var model = new TimesheetViewModel()
            {
                Timesheets = verifiedSheets,
                TimesheetRows = rows,
            };
            return View(model);
        }

        [HttpPost]
        [Authorize(Policy = "KeyRequirement")]
        public IActionResult CreateTimesheet([FromBody] string end)
        {
            if (string.IsNullOrWhiteSpace(end))
            {
                Response.StatusCode = 400;
                return Json("Please choose a date.");
            }
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int offset = (7 - (int)Convert.ToDateTime(end).DayOfWeek + (int)DayOfWeek.Friday) % 7;
            DateTime nextFriday = Convert.ToDateTime(end).AddDays(offset);

            var existingSheets = _timesheetService.GetUnapprovedTimesheets(userId!);
            if (existingSheets.Any(s => s.EndDate == DateOnly.FromDateTime(nextFriday)))
            {
                Response.StatusCode = 400;
                return Json("Timesheet already exists for this week.");
            }

            Timesheet? createdTimesheet = _timesheetService.CreateOrUpdateTimesheetWithRows(Convert.ToDateTime(end), userId!);
            if (createdTimesheet == null)
            {
                Response.StatusCode = 400;
                return Json("Timesheet already exists for this week.");
            }

            return Json(new Timesheet
            {
                TotalHours = createdTimesheet.TotalHours,
                EndDate = createdTimesheet.EndDate,
                TimesheetId = createdTimesheet.TimesheetId,
                EmployeeHash = createdTimesheet.EmployeeHash,
                FlexHours = createdTimesheet.FlexHours,
                Overtime = createdTimesheet.Overtime
            });
        }

        [HttpPost]
        [Authorize(Policy = "KeyRequirement")]
        public IActionResult? UpdateRow([FromBody] TimesheetRow timesheetRow)
        {
            if (timesheetRow.ValidationErrors != null)
            {
                Response.StatusCode = 400;
                return Json(timesheetRow.ValidationErrors);
            }

            var (errors, result) = _timesheetService.UpdateRow(timesheetRow);

            if (result == null && errors == null)
            {
                return BadRequest();
            }
            if (errors != null)
            {
                Response.StatusCode = 400;
                return Json(errors);
            }
            return Json(result);
        }

        [HttpPost]
        [Authorize(Policy = "KeyRequirement")]
        public IActionResult? GetTimesheet([FromBody] string timesheetId)
        {
            int tid;
            try
            {
                tid = Convert.ToInt32(timesheetId);
            }
            catch (System.Exception)
            {
                return BadRequest();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var timesheet = _timesheetService.GetTimesheetWithDetails(tid);
            if (timesheet == null || (timesheet.UserId != userId && timesheet.User!.TimesheetApproverId != userId))
            {
                return BadRequest();
            }

            _timesheetService.CreateOrUpdateTimesheetWithRows(DateTime.Parse(timesheet!.EndDate.ToString()!), timesheet.UserId ?? "0");

            return Json(_timesheetService.GetTimesheetRowDtos(tid));
        }

        [HttpPost]
        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult?> SubmitTimesheetAsync([FromBody] SignTimesheetViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || model.Password == null)
            {
                return BadRequest();
            }

            var (success, error, _) = await _timesheetService.SubmitTimesheetAsync(model.Timesheet, user.Id, model.Password, model.Flexhours, model.Overtime);

            if (!success)
            {
                if (error == "badrequest") return BadRequest();
                if (error == "unauthorized") return Unauthorized();
                Response.StatusCode = 400;
                return Json(error);
            }

            return Json(_timesheetService.GetTimesheetRowDtos(model.Timesheet));
        }

        [HttpPost]
        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult?> ApproveTimesheetAsync([FromBody] SignTimesheetViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || model.Password == null)
            {
                return BadRequest();
            }

            var (success, error, _) = await _timesheetService.ApproveTimesheetAsync(model.Timesheet, user.Id, model.Password);

            if (!success)
            {
                if (error == "badrequest") return BadRequest();
                if (error == "unauthorized") return Unauthorized();
                Response.StatusCode = 400;
                return Json(error);
            }

            return Json(_timesheetService.GetTimesheetRowDtos(model.Timesheet));
        }

        [HttpPost]
        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult?> DeclineTimesheetAsync([FromBody] SignTimesheetViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || model.Password == null)
            {
                return BadRequest();
            }

            bool success = await _timesheetService.DeclineTimesheetAsync(model.Timesheet, user.Id, model.Password, model.ApproverNotes);
            if (!success)
            {
                return BadRequest();
            }

            return Json(_timesheetService.GetTimesheetRowDtos(model.Timesheet));
        }

        [Authorize(Policy = "KeyRequirement")]
        [HttpPost]
        public async Task<IActionResult> AddCustomRowAsync([FromBody] CustomRowModel model)
        {
            int timesheetIdInt;
            try
            {
                timesheetIdInt = Convert.ToInt32(model.TimesheetId);
            }
            catch (System.Exception)
            {
                return BadRequest();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return BadRequest();
            }

            var row = _timesheetService.AddCustomRow(timesheetIdInt, user.Id, model.Type!, user.LabourGradeCode);
            if (row == null)
            {
                return BadRequest();
            }
            return Json(row);
        }

        [HttpGet]
        [Authorize(Policy = "KeyRequirement")]
        public IActionResult GetAll()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Json(_timesheetService.GetApprovedTimesheets(userId!).Select(t => new Timesheet
            {
                TotalHours = t.TotalHours,
                EndDate = t.EndDate,
                TimesheetId = t.TimesheetId,
                EmployeeHash = t.EmployeeHash,
                FlexHours = t.FlexHours,
                Overtime = t.Overtime
            }));
        }
    }
}
