using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TimesheetApp.Data;
using TimesheetApp.Models.TimesheetModels;

namespace TimesheetApp.Controllers
{
    public class TimesheetRowController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TimesheetRowController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: TimesheetRow
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.TimesheetRows.Include(t => t.Timesheet);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: TimesheetRow/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var timesheetRow = await _context.TimesheetRows
                .Include(t => t.Timesheet)
                .FirstOrDefaultAsync(m => m.TimesheetRowId == id);
            if (timesheetRow == null)
            {
                return NotFound();
            }

            return View(timesheetRow);
        }

        // GET: TimesheetRow/Create
        public IActionResult Create()
        {
            ViewData["TimesheetId"] = new SelectList(_context.Timesheets, "TimesheetId", "TimesheetId");
            return View();
        }

        // POST: TimesheetRow/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("TimesheetRowId,ProjectId,TotalHoursRow,WorkPackageId,Notes,Sat,Sun,Mon,Tue,Wed,Thu,Fri,TimesheetId")] TimesheetRow timesheetRow)
        {
            if (ModelState.IsValid)
            {
                _context.Add(timesheetRow);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["TimesheetId"] = new SelectList(_context.Timesheets, "TimesheetId", "TimesheetId", timesheetRow.TimesheetId);
            return View(timesheetRow);
        }

        // GET: TimesheetRow/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var timesheetRow = await _context.TimesheetRows.FindAsync(id);
            if (timesheetRow == null)
            {
                return NotFound();
            }
            ViewData["TimesheetId"] = new SelectList(_context.Timesheets, "TimesheetId", "TimesheetId", timesheetRow.TimesheetId);
            return View(timesheetRow);
        }

        // POST: TimesheetRow/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("TimesheetRowId,ProjectId,TotalHoursRow,WorkPackageId,Notes,Sat,Sun,Mon,Tue,Wed,Thu,Fri,TimesheetId")] TimesheetRow timesheetRow)
        {
            if (id != timesheetRow.TimesheetRowId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(timesheetRow);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TimesheetRowExists(timesheetRow.TimesheetRowId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["TimesheetId"] = new SelectList(_context.Timesheets, "TimesheetId", "TimesheetId", timesheetRow.TimesheetId);
            return View(timesheetRow);
        }

        // GET: TimesheetRow/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var timesheetRow = await _context.TimesheetRows
                .Include(t => t.Timesheet)
                .FirstOrDefaultAsync(m => m.TimesheetRowId == id);
            if (timesheetRow == null)
            {
                return NotFound();
            }

            return View(timesheetRow);
        }

        // POST: TimesheetRow/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var timesheetRow = await _context.TimesheetRows.FindAsync(id);
            _context.TimesheetRows.Remove(timesheetRow);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TimesheetRowExists(int id)
        {
            return _context.TimesheetRows.Any(e => e.TimesheetRowId == id);
        }
    }
}
