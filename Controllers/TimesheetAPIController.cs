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

namespace TimesheetApp.Controllers
{
    public class TimesheetAPIController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TimesheetAPIController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var applicationDbContext = _context.Timesheets.Where(t => t.UserId == userId);
            List<int> timesheetIDs = applicationDbContext.Select(t => t.TimesheetId).OrderBy(id => id).ToList();
            var rowContext = _context.TimesheetRows.Where(r => timesheetIDs.Contains((int)r.TimesheetId));

            var model = new TimesheetViewModel()
            {
                Timesheets = await applicationDbContext.ToListAsync(),
                TimesheetRows = await rowContext.ToListAsync(),
            };
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateRow(TimesheetRow timesheetRow)
        {
            try
            {
                _context.Update(timesheetRow);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return NotFound();

            }
            return RedirectToAction(nameof(Index));
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
            _context.TimesheetRows.Add(row);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }
    }
}