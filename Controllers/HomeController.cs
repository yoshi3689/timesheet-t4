using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimesheetApp.Data;
using TimesheetApp.Models;

namespace TimesheetApp.Controllers;
/// <summary>
/// This is the page that deals with the employee dashboard.
/// </summary>
[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;


    public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    [Authorize(Policy = "KeyRequirement")]
    public async Task<IActionResult> IndexAsync()
    {
        var user = (await _userManager.GetUserAsync(User));
        if (user != null)
        {
            var userId = user.Id;
            return View(_context.Notifications.Where(c => c.UserId == userId).ToList());
        }
        else
        {
            return View();
        }

    }

    [HttpPost]
    [Authorize(Policy = "KeyRequirement")]

    public async Task<IActionResult> SeeNotification([FromBody] string id)
    {
        int newID;
        try
        {
            newID = Convert.ToInt32(id);
        }
        catch (System.Exception)
        {
            return BadRequest();
        }
        var userId = (await _userManager.GetUserAsync(User))!.Id;
        var noti = await _context.Notifications.Where(c => c.Id == newID && c.UserId == userId).FirstOrDefaultAsync();
        if (noti != null)
        {
            _context.Remove(noti);
            await _context.SaveChangesAsync();
        }
        return Ok();
    }


    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
