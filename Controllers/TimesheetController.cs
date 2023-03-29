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
using System.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;

namespace TimesheetApp.Controllers
{
    /// <summary>
    /// Class that is used to manage Timesheets, and timesheet rows.
    /// </summary>
    [Authorize]
    public class TimesheetController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;


        public TimesheetController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
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

        //Sends to page displaying list of timesheets to approve for the current user.
        public IActionResult ToApprove()
        {
            //get the current user
            var approverId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            //get all the employees that the current user is an approver for.
            var empsApproving = _context.ApplicationUsers!.Where(c => c.TimesheetApproverId == approverId).ToList();
            //get the timesheets for each employee
            var approveSheets = new List<Timesheet>();
            int offset = (7 - (int)DateTime.Today.DayOfWeek + (int)DayOfWeek.Friday) % 7;
            DateTime nextFriday = DateTime.Today.AddDays(offset);
            DateTime currentDate = DateTime.Today;

            //get the timesheets for each employee
            foreach (var emp in empsApproving)
            {
                approveSheets.Add(_context.Timesheets!.Where(t => t.UserId == emp.Id && t.EmployeeHash != null && t.ApproverHash == null).OrderBy(c => c.EndDate).FirstOrDefault() ?? new Timesheet());
            }

            var timesheet = approveSheets.AsEnumerable()
                .OrderBy(ts => Math.Abs((DateTime.Parse(ts.EndDate.ToString()!) - currentDate.Date).TotalDays))
                .FirstOrDefault();

            if (timesheet != null)
            {
                timesheet.CurrentlySelected = true;
            }

            var rows = _context.TimesheetRows.Where(c => c.TimesheetId == timesheet!.TimesheetId).ToList();
            var model = new TimesheetViewModel()
            {
                Timesheets = approveSheets,
                TimesheetRows = rows,
            };

            return View(model);
        }

        //update the rows on a timesheet to match thier wps, and create a timesheet if it doesnt exist.
        private Timesheet? createUpdateTimesheetWithRows(DateTime endDate, string userId)
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
            return result;
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
                return Json("Date cannot be earlier than the present.");
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
                TimesheetId = createdTimesheet.TimesheetId,
                EmployeeHash = createdTimesheet.EmployeeHash
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
                OriginalLabourCode = c.OriginalLabourCode,
            }).ToList());
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult?> SubmitTimesheetAsync([FromBody] SignTimesheetViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            var timesheet = _context.Timesheets.Where(c => c.TimesheetId == model.Timesheet).FirstOrDefault();
            if (user == null || timesheet == null || timesheet.UserId != user.Id || model.Password == null || user.PrivateKey == null)
            {
                return BadRequest();
            }
            byte[]? timesheetHash = hashTimesheet(timesheet, model.Password, user.PrivateKey);
            if (timesheetHash == null)
            {
                return Unauthorized();
            }
            timesheet.EmployeeHash = timesheetHash;
            _context.Update(timesheet);
            _context.SaveChanges();
            return GetTimesheet(Convert.ToString(timesheet.TimesheetId));
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult?> ApproveTimesheetAsync([FromBody] SignTimesheetViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            var timesheet = _context.Timesheets.Where(c => c.TimesheetId == model.Timesheet).FirstOrDefault();
            if (user == null || timesheet == null || model.Password == null || user.PrivateKey == null)
            {
                return BadRequest();
            }
            byte[]? timesheetHash = hashTimesheet(timesheet, model.Password, user.PrivateKey);
            if (timesheetHash == null)
            {
                return Unauthorized();
            }
            timesheet.ApproverHash = timesheetHash;
            timesheet.TimesheetApproverId = user.Id;
            _context.Update(timesheet);
            _context.SaveChanges();
            return GetTimesheet(Convert.ToString(timesheet.TimesheetId));
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
        private byte[]? hashTimesheet(Timesheet timesheet, string password, byte[] encryptedPrivateKey)
        {
            using (RSA rsa = RSA.Create())
            {
                byte[]? unencrypt = KeyHelper.Decrypt(encryptedPrivateKey, password);
                if (unencrypt == null)
                {
                    return null;
                }
                rsa.ImportRSAPrivateKey(unencrypt, out _);
                string data = createDataString(timesheet);
                byte[] dataBytes = Encoding.UTF8.GetBytes(data);
                return rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
        }

        private bool verifySignature(Timesheet timesheet, byte[] publicKey, byte[] hashedSignature)
        {
            using (RSA rsa = RSA.Create())
            {
                rsa.ImportRSAPublicKey(publicKey, out _);
                string data = createDataString(timesheet);
                byte[] dataBytes = Encoding.UTF8.GetBytes(data);
                return rsa.VerifyData(dataBytes, hashedSignature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
        }

        private string createDataString(Timesheet timesheet)
        {
            StringBuilder dataBuilder = new StringBuilder();
            dataBuilder.Append(timesheet.EndDate);
            dataBuilder.Append(timesheet.TotalHours);
            dataBuilder.Append(timesheet.FlexHours);
            dataBuilder.Append(timesheet.Overtime);
            foreach (TimesheetRow row in timesheet.TimesheetRows)
            {
                dataBuilder.Append(row.WorkPackageId);
                dataBuilder.Append(row.WorkPackageProjectId);
                dataBuilder.Append(row.OriginalLabourCode);
                dataBuilder.Append(row.packedHours);
            }
            return dataBuilder.ToString();
        }
    }
}