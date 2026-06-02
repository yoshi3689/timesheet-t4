using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TimesheetApp.Models;
using TimesheetApp.Services;


namespace TimesheetApp.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly IWorkPackageService _workPackageService;
    private readonly INotificationService _notificationService;

    public HomeController(
        ILogger<HomeController> logger,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        IWorkPackageService workPackageService,
        INotificationService notificationService)
    {
        _userManager = userManager;
        _logger = logger;
        _configuration = configuration;
        _workPackageService = workPackageService;
        _notificationService = notificationService;
    }

    [Authorize(Policy = "KeyRequirement")]
    public async Task<IActionResult> IndexAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user != null)
        {
            var userId = user.Id;
            var workPackages = await _workPackageService.GetResponsibleWorkPackagesAsync(userId);
            var notifications = _notificationService.GetUserNotifications(userId);
            var model = new WorkPackageNotificationModel
            {
                Name = user.FirstName,
                Notifications = notifications,
                WorkPackages = workPackages
            };
            return View(model);
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
        await _notificationService.DismissNotificationAsync(newID, userId);
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
        var process = new System.Diagnostics.Process();
        process.StartInfo.FileName = "mysqldump";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        var dbPassword = _configuration["DBPASSWORD"];
        var dbHost = _configuration["DBHOST"];
        var dbPort = _configuration["DBPORT"];
        var dbName = _configuration["DBNAME"];
        process.StartInfo.Arguments = $"--user=root --password={dbPassword} --host={dbHost} --port={dbPort} {dbName}";

        process.Start();
        var streamReader = process.StandardOutput;

        var fileStream = new MemoryStream();
        await streamReader.BaseStream.CopyToAsync(fileStream);
        fileStream.Position = 0;
        return File(fileStream, "application/octet-stream", "mydb.sql");
    }
}
