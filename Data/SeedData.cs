using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;
using TimesheetApp.Helpers;
using TimesheetApp.Models;
using TimesheetApp.Models.TimesheetModels;

namespace TimesheetApp.Data;

public static class SeedData
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        var users = await SeedUsersAsync(db, userManager);
        var projects = await SeedProjectsAsync(db, users);
        await SeedWorkPackagesAsync(db, users, projects);
        await SeedTimesheetsAsync(db, users, projects);
    }

    // ── Users ─────────────────────────────────────────────────────────────────

    private static async Task<Dictionary<string, ApplicationUser>> SeedUsersAsync(
        ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        var result = new Dictionary<string, ApplicationUser>();

        async Task<ApplicationUser> Upsert(string email, Func<RSA, ApplicationUser> factory, string? role = null)
        {
            var existing = await userManager.FindByEmailAsync(email);
            if (existing != null) { result[email] = existing; return existing; }

            using var rsa = RSA.Create();
            var user = factory(rsa);
            await userManager.CreateAsync(user, "Password123!");
            var created = (await userManager.FindByEmailAsync(email))!;
            if (role != null) await userManager.AddToRoleAsync(created, role);
            result[email] = created;
            return created;
        }

        var sup1 = await Upsert("sup1@sheet.dev", rsa => new ApplicationUser
        {
            Email = "sup1@sheet.dev", UserName = "sup1@sheet.dev",
            FirstName = "James", LastName = "Thornton", JobTitle = "Lead Engineer",
            EmailConfirmed = true, LabourGradeCode = "P5", SickDays = 7,
            EmployeeNumber = 1000000103,
            PublicKey = rsa.ExportRSAPublicKey(),
            PrivateKey = KeyHelper.Encrypt(rsa.ExportRSAPrivateKey(), "Password123!")
        }, "Supervisor");

        await Upsert("pm1@sheet.dev", rsa => new ApplicationUser
        {
            Email = "pm1@sheet.dev", UserName = "pm1@sheet.dev",
            FirstName = "Sarah", LastName = "Chen", JobTitle = "Project Manager",
            EmailConfirmed = true, LabourGradeCode = "P4", SickDays = 7,
            EmployeeNumber = 1000000101,
            PublicKey = rsa.ExportRSAPublicKey(),
            PrivateKey = KeyHelper.Encrypt(rsa.ExportRSAPrivateKey(), "Password123!")
        });

        await Upsert("pm2@sheet.dev", rsa => new ApplicationUser
        {
            Email = "pm2@sheet.dev", UserName = "pm2@sheet.dev",
            FirstName = "Marcus", LastName = "Webb", JobTitle = "Project Manager",
            EmailConfirmed = true, LabourGradeCode = "P4", SickDays = 7,
            EmployeeNumber = 1000000102,
            PublicKey = rsa.ExportRSAPublicKey(),
            PrivateKey = KeyHelper.Encrypt(rsa.ExportRSAPrivateKey(), "Password123!")
        });

        var engDefs = new (string email, string first, string last, string grade, long num)[]
        {
            ("eng1@sheet.dev", "Aiko",   "Tanaka",   "P2", 1000000201L),
            ("eng2@sheet.dev", "Luca",   "Ferretti", "P1", 1000000202L),
            ("eng3@sheet.dev", "Priya",  "Nair",     "P2", 1000000203L),
            ("eng4@sheet.dev", "Omar",   "Khalil",   "P3", 1000000204L),
            ("eng5@sheet.dev", "Hannah", "Müller",   "P3", 1000000205L),
            ("eng6@sheet.dev", "Dev",    "Singh",    "SS", 1000000206L),
            ("eng7@sheet.dev", "Chloe",  "Dubois",   "DS", 1000000207L),
            ("eng8@sheet.dev", "Ryo",    "Matsuda",  "DS", 1000000208L),
        };

        foreach (var (email, first, last, grade, num) in engDefs)
        {
            await Upsert(email, rsa => new ApplicationUser
            {
                Email = email, UserName = email,
                FirstName = first, LastName = last, JobTitle = "Software Engineer",
                EmailConfirmed = true, LabourGradeCode = grade, SickDays = 7,
                EmployeeNumber = num,
                SupervisorId = sup1.Id,
                TimesheetApproverId = sup1.Id,
                PublicKey = rsa.ExportRSAPublicKey(),
                PrivateKey = KeyHelper.Encrypt(rsa.ExportRSAPrivateKey(), "Password123!")
            });
        }

        return result;
    }

    // ── Projects ──────────────────────────────────────────────────────────────

    private static async Task<Dictionary<int, Project>> SeedProjectsAsync(
        ApplicationDbContext db, Dictionary<string, ApplicationUser> users)
    {
        var result = new Dictionary<int, Project>();

        async Task<Project> Upsert(int id, Func<Project> factory)
        {
            var existing = db.Projects.FirstOrDefault(p => p.ProjectId == id);
            if (existing != null) { result[id] = existing; return existing; }
            var p = factory();
            db.Projects.Add(p);
            await db.SaveChangesAsync();
            result[id] = p;
            return p;
        }

        await Upsert(101, () => new Project
        {
            ProjectId = 101, ProjectTitle = "Cloud Migration",
            ProjectManagerId = users["pm1@sheet.dev"].Id,
            TotalBudget = 2_500_000, ActualCost = 843_200, IsClosed = false
        });

        await Upsert(102, () => new Project
        {
            ProjectId = 102, ProjectTitle = "Mobile App",
            ProjectManagerId = users["pm2@sheet.dev"].Id,
            TotalBudget = 800_000, ActualCost = 312_500, IsClosed = false
        });

        await Upsert(103, () => new Project
        {
            ProjectId = 103, ProjectTitle = "Security Audit",
            ProjectManagerId = users["pm1@sheet.dev"].Id,
            TotalBudget = 200_000, ActualCost = 197_400, IsClosed = true
        });

        return result;
    }

    // ── Work Packages (stub — filled in Task 2) ───────────────────────────────

    private static Task SeedWorkPackagesAsync(
        ApplicationDbContext db,
        Dictionary<string, ApplicationUser> users,
        Dictionary<int, Project> projects) => Task.CompletedTask;

    // ── Timesheets (stub — filled in Task 2) ──────────────────────────────────

    private static Task SeedTimesheetsAsync(
        ApplicationDbContext db,
        Dictionary<string, ApplicationUser> users,
        Dictionary<int, Project> projects) => Task.CompletedTask;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static long PackHours(float sat, float sun, float mon, float tue, float wed, float thu, float fri)
    {
        float[] hours = [sat, sun, mon, tue, wed, thu, fri];
        long packed = 0;
        for (int d = 0; d < 7; d++)
        {
            int decihour = (int)Math.Round(hours[d] * 10);
            packed |= (long)decihour << (d * 8);
        }
        return packed;
    }
}
