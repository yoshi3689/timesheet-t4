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
    [Authorize(Policy = "KeyRequirement")]

    public class TimesheetController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;


        public TimesheetController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        /// <summary>
        /// Index page of the timesheet. Gets all the users timesheets that aren't approved.
        /// </summary>
        /// <returns>a page for using timesheets.</returns>
        [Authorize(Policy = "KeyRequirement")]
        public IActionResult Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userSheets = _context.Timesheets!.Where(t => t.UserId == userId && t.ApproverHash == null).OrderByDescending(c => c.EndDate).ToList();
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

            //update the timesheet to add rows for new wps
            createUpdateTimesheetWithRows(DateTime.Parse(timesheet!.EndDate.ToString()!), userId ?? "0");

            var rows = _context.TimesheetRows
                .Where(c => c.TimesheetId == timesheet!.TimesheetId)
                .Include(c => c.WorkPackage)
                .ToList();

            var model = new TimesheetViewModel()
            {
                Timesheets = userSheets,
                TimesheetRows = rows,
                CurrentUser = _context.Users.Where(c => c.Id == userId).First()
            };

            return View(model);
        }

        /// <summary>
        /// Gets the page of timesheets to approve if the user is a timesheet approver. Also verifies that the timesheet signatures are legit.
        /// </summary>
        /// <returns>the page to approve timesheets.</returns>
        [Authorize(Policy = "KeyRequirement")]
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
                approveSheets.AddRange(_context.Timesheets!.Where(t => t.UserId == emp.Id && t.EmployeeHash != null && t.ApproverHash == null).Include(c => c.User).Include(c => c.TimesheetRows).OrderBy(c => c.EndDate));
            }
            if (approveSheets.Count() == 0)
            {
                return View(new TimesheetViewModel());
            }

            var timesheet = approveSheets.AsEnumerable()
                .OrderBy(ts => Math.Abs((DateTime.Parse(ts.EndDate.ToString()!) - currentDate.Date).TotalDays))
                .FirstOrDefault();

            if (timesheet != null)
            {
                timesheet.CurrentlySelected = true;
            }

            var verifiedSheets = new List<Timesheet>();
            foreach (var sheet in approveSheets)
            {
                if (sheet.User == null || sheet.EmployeeHash == null || sheet.User.PublicKey == null || !verifySignature(sheet, sheet.User!.PublicKey, sheet.EmployeeHash))
                {
                    Console.WriteLine("signature fail");
                }
                else
                {
                    verifiedSheets.Add(sheet);
                }
            }

            var rows = _context.TimesheetRows.Where(c => c.TimesheetId == timesheet!.TimesheetId).ToList();
            var model = new TimesheetViewModel()
            {
                Timesheets = verifiedSheets,
                TimesheetRows = rows,
            };
            foreach (var sheet in model.Timesheets)
            {

                Console.WriteLine(sheet.Overtime);
                Console.WriteLine(sheet.FlexHours);
                Console.WriteLine("****************************************************************************************");
            }
            return View(model);
        }

        /// <summary>
        /// Create a new timesheet, or update an existing timesheet. Add a row for each work package that the user is a part of.
        /// </summary>
        /// <param name="endDate">Date of the timesheet.</param>
        /// <param name="userId">id of the owner</param>
        /// <returns>The new/updated timesheet object,</returns>
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

        /// <summary>
        /// Create a new timesheet when you click the new timesheet button
        /// </summary>
        /// <param name="end">end date of the timesheet.</param>
        /// <returns>the json of the new timesheet, to display it on the page</returns>
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
            var sheet = _context.Timesheets.Where(c => c.EndDate == DateOnly.FromDateTime(nextFriday) && c.UserId == userId).FirstOrDefault();
            if (sheet != null)
            {
                Response.StatusCode = 400;
                return Json("Timesheet already exists for this week.");
            }
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
                EmployeeHash = createdTimesheet.EmployeeHash,
                FlexHours = createdTimesheet.FlexHours,
                Overtime = createdTimesheet.Overtime
            };
            return Json(returnTimesheet);
        }

        /// <summary>
        /// update a row in the timesheet, and verify that the fields are all correct. Rounds to the nearest 1/4 of an hour.
        /// </summary>
        /// <param name="timesheetRow">the updated row</param>
        /// <returns>an error in the form of a map if there is any, otherwise json of the new row.</returns>
        [HttpPost]
        [Authorize(Policy = "KeyRequirement")]
        public IActionResult? UpdateRow([FromBody] TimesheetRow timesheetRow)
        {
            if (timesheetRow.ValidationErrors != null)
            {
                Response.StatusCode = 400;
                return Json(timesheetRow.ValidationErrors);
            }

            var timesheetRows = _context.TimesheetRows.Where(c => c.TimesheetId == timesheetRow.TimesheetId).Include(c => c.Timesheet).Include(c => c.WorkPackage).ToList();
            var oldRow = timesheetRows.Where(c => c.TimesheetRowId == timesheetRow.TimesheetRowId).FirstOrDefault();
            if (oldRow == null || oldRow.Timesheet == null || oldRow.Timesheet.EmployeeHash != null)
            {
                return BadRequest();
            }
            Dictionary<int, string> validationErrors = new Dictionary<int, string>();
            if (oldRow.WorkPackage != null && oldRow.WorkPackage.IsClosed == true)
            {
                validationErrors.Add(0, "Row can no longer be edited, this work package is closed.");
                Response.StatusCode = 400;
                return Json(validationErrors);
            }
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
            if (validationErrors.Count() > 0)
            {
                Response.StatusCode = 400;
                return Json(validationErrors);
            }
            _context.SaveChanges();
            return Json(new { oldRow.Timesheet.TotalHours, oldRow.Sun, oldRow.Mon, oldRow.Tue, oldRow.Wed, oldRow.Thu, oldRow.Fri, oldRow.Sat, oldRow.TotalHoursRow, oldRow.ProjectId, oldRow.WorkPackageId, oldRow.TimesheetRowId, oldRow.Notes });
        }

        /// <summary>
        /// Get the rows for a specific timesheet.
        /// </summary>
        /// <param name="timesheetId">the id you need rows for.</param>
        /// <returns>json data of all the rows.</returns>
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
            var timesheet = _context.Timesheets.Where(c => c.TimesheetId == tid).Include(c => c.User).FirstOrDefault();
            if (timesheet == null || (timesheet.UserId != userId && timesheet.User!.TimesheetApproverId != userId))
            {
                return BadRequest();
            }

            createUpdateTimesheetWithRows(DateTime.Parse(timesheet!.EndDate.ToString()!), timesheet.UserId ?? "0");

            return Json(_context.TimesheetRows.Where(c => c.TimesheetId == tid).Include(c => c.WorkPackage).Select(c => new TimesheetRow
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
            }).ToList());
        }

        /// <summary>
        /// submit a timesheet and create a signature using the password they gave. it dehashes the private key and creates it.
        /// </summary>
        /// <param name="model">takes in a model that contains the password and the timesheet</param>
        /// <returns>the rows of the timesheet again</returns>
        [HttpPost]
        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult?> SubmitTimesheetAsync([FromBody] SignTimesheetViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            var timesheet = _context.Timesheets.Where(c => c.TimesheetId == model.Timesheet).Include(c => c.TimesheetRows).FirstOrDefault();
            if (user == null || timesheet == null || timesheet.UserId != user.Id || model.Password == null || user.PrivateKey == null)
            {
                return BadRequest();
            }
            timesheet.FlexHours = model.Flexhours ?? 0;
            timesheet.Overtime = model.Overtime ?? 0;
            user.Overtime += model.Overtime ?? 0;
            user.FlexTime += model.Flexhours ?? 0;
            if (timesheet.TotalHours != model.Flexhours + model.Overtime + 40)
            {
                Response.StatusCode = 400;
                return Json("You cannot have more flexhours and overtime then you worked.");
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

        /// <summary>
        /// Timesheet approver calls this to accept a timesheet.
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost]
        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult?> ApproveTimesheetAsync([FromBody] SignTimesheetViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            var timesheet = _context.Timesheets.Where(c => c.TimesheetId == model.Timesheet).Include(c => c.TimesheetRows).Include(c => c.User).FirstOrDefault();
            if (user == null || timesheet == null || model.Password == null || user.PrivateKey == null || timesheet.User == null || timesheet.User.TimesheetApproverId != user.Id)
            {
                return BadRequest();
            }
            byte[]? timesheetHash = hashTimesheet(timesheet, model.Password, user.PrivateKey);
            if (timesheetHash == null)
            {
                return Unauthorized();
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
            return GetTimesheet(Convert.ToString(timesheet.TimesheetId));
        }

        /// <summary>
        /// used to decline a timesheet. Sets the approver note to the reason why, and removes all the signatures.
        /// </summary>
        /// <param name="model">takes the notes in the model</param>
        /// <returns>Get the rows of the timesheet</returns>
        [HttpPost]
        [Authorize(Policy = "KeyRequirement")]
        public async Task<IActionResult?> DeclineTimesheetAsync([FromBody] SignTimesheetViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            var timesheet = _context.Timesheets.Where(c => c.TimesheetId == model.Timesheet).Include(c => c.TimesheetRows).Include(c => c.User).FirstOrDefault();
            if (user == null || timesheet == null || model.Password == null || user.PrivateKey == null || timesheet.User == null || timesheet.User.TimesheetApproverId != user.Id)
            {
                return BadRequest();
            }
            timesheet.ApproverHash = null;
            timesheet.EmployeeHash = null;
            timesheet.ApproverNotes = model.ApproverNotes ?? " ";
            _context.Update(timesheet);
            _context.SaveChanges();
            return GetTimesheet(Convert.ToString(timesheet.TimesheetId));
        }

        /// <summary>
        /// Adds a custom row to the timesheet. For things like sick time, holidays, etc.
        /// </summary>
        /// <param name="model">takes the type and the timesheet it is for</param>
        /// <returns>the json of the row</returns>
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
            var timesheet = _context.Timesheets.Where(c => c.TimesheetId == timesheetIdInt).FirstOrDefault();
            if (user == null || timesheet == null || timesheet.UserId != user.Id)
            {
                return BadRequest();
            }
            TimesheetRow row = new TimesheetRow { WorkPackageId = model.Type, WorkPackageProjectId = 010, OriginalLabourCode = user.LabourGradeCode, TimesheetId = timesheetIdInt };
            try
            {
                _context.TimesheetRows.Add(row);
                _context.SaveChanges();
            }
            catch (System.Exception)
            {
                return BadRequest();
            }
            row.Timesheet = null;
            return Json(row);
        }

        /// <summary>
        /// get all the timesheets for the logged in user.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Authorize(Policy = "KeyRequirement")]
        public IActionResult GetAll()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userSheets = _context.Timesheets!.Where(t => t.UserId == userId && t.ApproverHash != null).OrderByDescending(c => c.EndDate).ToList();
            return Json(userSheets.Select(createdTimesheet => new Timesheet
            {
                TotalHours = createdTimesheet.TotalHours,
                EndDate = createdTimesheet.EndDate,
                TimesheetId = createdTimesheet.TimesheetId,
                EmployeeHash = createdTimesheet.EmployeeHash,
                FlexHours = createdTimesheet.FlexHours,
                Overtime = createdTimesheet.Overtime
            }));
        }

        /// <summary>
        /// creates a signature of the timesheet. Requires a password to decrypt the users private key to create the hash.
        /// </summary>
        /// <param name="timesheet">timesheet to sign</param>
        /// <param name="password">password to decrypt the private key</param>
        /// <param name="encryptedPrivateKey">the stored key to decrypt</param>
        /// <returns></returns>
        [Authorize(Policy = "KeyRequirement")]
        public byte[]? hashTimesheet(Timesheet timesheet, string password, byte[] encryptedPrivateKey)
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

        /// <summary>
        /// Verifies a signature using the public key of the signer.
        /// </summary>
        /// <param name="timesheet">timesheet to match</param>
        /// <param name="publicKey">public key of signer</param>
        /// <param name="hashedSignature">signature to verify</param>
        /// <returns></returns>
        [Authorize(Policy = "KeyRequirement")]
        public bool verifySignature(Timesheet timesheet, byte[] publicKey, byte[] hashedSignature)
        {
            using (RSA rsa = RSA.Create())
            {
                rsa.ImportRSAPublicKey(publicKey, out _);
                string data = createDataString(timesheet);
                byte[] dataBytes = Encoding.UTF8.GetBytes(data);
                return rsa.VerifyData(dataBytes, hashedSignature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
        }

        /// <summary>
        /// Creates a string using a timesheet, required in creating the hash.
        /// </summary>
        /// <param name="timesheet">timeshee to make string for</param>
        /// <returns>string of all data</returns>
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