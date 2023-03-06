using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TimesheetApp.Data;
using TimesheetApp.Models.TimesheetModels;

namespace TimesheetApp.Controllers
{
    [Authorize(Roles = "HR,Admin")]
    public class LabourGradeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LabourGradeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: LabourGrade
        public async Task<IActionResult> Index()
        {
            return View(await _context.LabourGrades!.ToListAsync());
        }

        // GET: LabourGrade/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var labourGrade = await _context.LabourGrades!
                .FirstOrDefaultAsync(m => m.LabourCode == id);
            if (labourGrade == null)
            {
                return NotFound();
            }

            return View(labourGrade);
        }

        // GET: LabourGrade/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: LabourGrade/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("LabourCode,Rate")] LabourGrade labourGrade)
        {
            if (ModelState.IsValid)
            {
                _context.Add(labourGrade);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(labourGrade);
        }

        // GET: LabourGrade/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var labourGrade = await _context.LabourGrades!.FindAsync(id);
            if (labourGrade == null)
            {
                return NotFound();
            }
            return View(labourGrade);
        }

        // POST: LabourGrade/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("LabourCode,Rate")] LabourGrade labourGrade)
        {
            if (id != labourGrade.LabourCode)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(labourGrade);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!LabourGradeExists(labourGrade.LabourCode))
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
            return View(labourGrade);
        }

        // GET: LabourGrade/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var labourGrade = await _context.LabourGrades!
                .FirstOrDefaultAsync(m => m.LabourCode == id);
            if (labourGrade == null)
            {
                return NotFound();
            }

            return View(labourGrade);
        }

        // POST: LabourGrade/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var labourGrade = await _context.LabourGrades!.FindAsync(id);
            _context.LabourGrades!.Remove(labourGrade);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool LabourGradeExists(string id)
        {
            return _context.LabourGrades!.Any(e => e.LabourCode == id);
        }
    }
}
