using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TimesheetApp.Data;
using TimesheetApp.Models.TimesheetModels;
using TimesheetApp.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using TimesheetApp.Helpers;
using System.Text.Json;

namespace TimesheetApp.Controllers
{
    /// <summary>
    /// Class that is used to manage Timesheets, and timesheet rows.
    /// </summary>
    [Authorize]
    public class TimesheetController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TimesheetController(ApplicationDbContext context)
        {
            _context = context;
        }
        public IActionResult Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userSheets = _context.Timesheets!.Where(t => t.UserId == userId).OrderBy(c => c.EndDate).ToList();
            int offset = (7 - (int)DateTime.Today.DayOfWeek + (int)DayOfWeek.Friday) % 7;
            DateTime nextFriday = DateTime.Today.AddDays(offset);

            if (userSheets.Count() == 0)
            {
                userSheets.Add(createUpdateTimesheetWithRows(DateTime.Now, userId ?? "0") ?? new Timesheet());
            }

            DateTime currentDate = DateTime.Today;

            // find the closes timesheet
            var timesheet = userSheets.AsEnumerable()
                .OrderBy(ts => Math.Abs((DateTime.Parse(ts.EndDate.ToString()!) - currentDate.Date).TotalDays))
                .FirstOrDefault();

            if (timesheet != null)
            {
                timesheet.CurrentlySelected = true;
            }

            createUpdateTimesheetWithRows(DateTime.Parse(timesheet!.EndDate.ToString()!), userId ?? "0");

            var rows = _context.TimesheetRows.Where(c => c.TimesheetId == timesheet!.TimesheetId).ToList();
            var model = new TimesheetViewModel()
            {
                Timesheets = userSheets,
                TimesheetRows = rows,
            };
            return View(model);
        }

        //update the rows on a timesheet to match thier wps, and create a timesheet if it doesnt exist.
        private Timesheet? createUpdateTimesheetWithRows(DateTime endDate, string userId)
        {

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
                _context.SaveChanges();
            }
            var currentUser = _context.Users.Where(c => c.Id == userId).First();
            var myWps = _context.EmployeeWorkPackages.Where(c => c.UserId == userId).Include(c => c.WorkPackage);
            var myExistingRows = _context.TimesheetRows.Where(c => c.Timesheet!.UserId == userId).Select(c => new TimesheetRow { WorkPackageId = c.WorkPackageId, WorkPackageProjectId = c.WorkPackageProjectId, TimesheetId = c.TimesheetId }).ToList();
            foreach (var wp in myWps)
            {
                if (!myExistingRows.Where(c => c.TimesheetId == sheet.TimesheetId).Any(r => r.WorkPackageId == wp.WorkPackageId && r.WorkPackageProjectId == wp.WorkPackage!.ProjectId))
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
            return sheet;
        }


        [HttpPost]
        [Authorize]
        public IActionResult CreateTimesheet([FromBody] string end)
        {
            if (string.IsNullOrWhiteSpace(end))
            {
                Response.StatusCode = 400;
                return Json("Please choose a date.");
            }
            if (Convert.ToDateTime(end) < DateTime.Now)
            {
                Response.StatusCode = 400;
                return Json("Date cannot be earlier then the present.");
            }
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Timesheet? createdTimesheet = createUpdateTimesheetWithRows(Convert.ToDateTime(end), userId!);
            if (createdTimesheet == null)
            {
                Response.StatusCode = 400;
                return Json("Timesheet already exists for this week.");
            }
            Timesheet returnTimesheet = new Timesheet
            {
                TotalHours = createdTimesheet.TotalHours,
                EndDate = createdTimesheet.EndDate,
                TimesheetId = createdTimesheet.TimesheetId
            };
            return Json(returnTimesheet);
        }

        [HttpPost]
        public async Task<IActionResult?> UpdateRow([FromBody] TimesheetRow timesheetRow)
        {
            string json = JsonSerializer.Serialize(timesheetRow);
            try
            {
                _context.Update(timesheetRow);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return NotFound();

            }
            return Ok();
        }

        [HttpPost]
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
            var timesheet = _context.Timesheets.Where(c => c.TimesheetId == tid).FirstOrDefault();
            createUpdateTimesheetWithRows(DateTime.Parse(timesheet!.EndDate.ToString()!), userId ?? "0");
            return Json(_context.TimesheetRows.Where(c => c.TimesheetId == tid).Select(c => new TimesheetRow
            {
                TimesheetRowId = c.TimesheetRowId,
                TimesheetId = c.TimesheetId,
                WorkPackageProjectId = c.WorkPackageProjectId,
                WorkPackageId = c.WorkPackageId,
                Notes = c.Notes,
                packedHours = c.packedHours,
                OriginalLabourCode = c.OriginalLabourCode
            }).ToList());
        }

        [HttpPost]
        public async Task<IActionResult> AddRow()
        {
            TimesheetRow row = new TimesheetRow()
            {
                WorkPackageId = "",
                ProjectId = 0,
                Sat = 0,
                Sun = 0,
                Mon = 0,
                Tue = 0,
                Wed = 0,
                Thu = 0,
                Fri = 0,
                Notes = "",
                TimesheetId = 1,
            };
            _context.TimesheetRows!.Add(row);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }
    }
}