using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TimesheetApp.Data;
using TimesheetApp.Models;

namespace TimesheetApp.Controllers
{
    public class SignatureController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SignatureController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Signature
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Signatures.Include(s => s.User);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: Signature/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var signature = await _context.Signatures
                .Include(s => s.User)
                .FirstOrDefaultAsync(m => m.SignatureId == id);
            if (signature == null)
            {
                return NotFound();
            }

            return View(signature);
        }

        // GET: Signature/Create
        public IActionResult Create()
        {
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Id");
            return View(new Signature());
        }

        // POST: Signature/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("SignatureId,Name,SignatureImage,UserId")] Signature signature)
        {
            // Console.WriteLine("Hello " + signature.SignatureImage + signature.Name);
                signature.SignatureImage = signature.SignatureImage!.Replace("=","").Replace(" ", "");
            if (ModelState.IsValid) {   
                string base64String = signature.SignatureImage!.Split(",")[1];
                Console.WriteLine(base64String);
                byte[] bytes = Convert.FromBase64String(base64String);
                var binarySignature = Convert.FromBase64String(base64String);
                System.IO.File.WriteAllBytes("Signature.png", binarySignature);
                
                _context.Add(signature);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Id", signature.UserId);
            return View(signature);
        }

        // GET: Signature/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var signature = await _context.Signatures.FindAsync(id);
            if (signature == null)
            {
                return NotFound();
            }
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Id", signature.UserId);
            return View(signature);
        }

        // POST: Signature/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("SignatureId,Name,SignatureImage,UserId")] Signature signature)
        {
            if (id != signature.SignatureId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(signature);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SignatureExists(signature.SignatureId))
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
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Id", signature.UserId);
            return View(signature);
        }

        // GET: Signature/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var signature = await _context.Signatures
                .Include(s => s.User)
                .FirstOrDefaultAsync(m => m.SignatureId == id);
            if (signature == null)
            {
                return NotFound();
            }

            return View(signature);
        }

        // POST: Signature/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var signature = await _context.Signatures.FindAsync(id);
            _context.Signatures.Remove(signature);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool SignatureExists(int id)
        {
            return _context.Signatures.Any(e => e.SignatureId == id);
        }
    }
}
