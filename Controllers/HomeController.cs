using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.SqlServer.Storage.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using TimesheetApp.Data;
using TimesheetApp.Models;
using static Program;

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

    [Authorize(Policy = "KeyRequirement", Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> DownloadSql()
    {
        // Create a process to execute the mysqldump command
        var process = new Process();
        process.StartInfo.FileName = "mysqldump";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.Arguments = $"--user=root --password={GlobalData.DBPassword} --host={GlobalData.DBHost} --port={GlobalData.DBPort} --ssl-mode=DISABLE db";
        Console.WriteLine(process.StartInfo.Arguments);

        // Start the process and capture the output as a stream
        process.Start();
        var streamReader = process.StandardOutput;

        // Return the SQL dump as a file download
        var fileStream = new MemoryStream();
        await streamReader.BaseStream.CopyToAsync(fileStream);
        fileStream.Position = 0;
        return File(fileStream, "application/octet-stream", "mydb.sql");
    }



}
