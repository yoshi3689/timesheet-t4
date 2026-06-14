using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
        await SeedWorkPackagesAsync(db, users);
        await SeedTimesheetsAsync(db, users);
    }

    // ── Users ─────────────────────────────────────────────────────────────────

    private static async Task<Dictionary<string, ApplicationUser>> SeedUsersAsync(
        ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        var result = new Dictionary<string, ApplicationUser>();

        async Task<ApplicationUser> Upsert(string email, Func<RSA, ApplicationUser> factory, string? role = null)
        {
            var existing = await userManager.FindByEmailAsync(email);
            if (existing != null)
            {
                // If the user exists but was never activated (no RSA keys), seed the keys now
                if (existing.PublicKey == null)
                {
                    using var seedRsa = RSA.Create();
                    existing.PublicKey = seedRsa.ExportRSAPublicKey();
                    existing.PrivateKey = KeyHelper.Encrypt(seedRsa.ExportRSAPrivateKey(), "Password123!");
                    await userManager.UpdateAsync(existing);
                }
                result[email] = existing;
                return existing;
            }

            using var rsa = RSA.Create();
            var user = factory(rsa);
            var createResult = await userManager.CreateAsync(user, "Password123!");
            if (!createResult.Succeeded)
                throw new InvalidOperationException(
                    $"Failed to seed user '{email}': {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
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

        // Sync supervisor/approver links in case engineers already existed with stale GUIDs
        foreach (var (email, _, _, _, _) in engDefs)
        {
            var user = result[email];
            if (user.SupervisorId != sup1.Id || user.TimesheetApproverId != sup1.Id)
            {
                user.SupervisorId = sup1.Id;
                user.TimesheetApproverId = sup1.Id;
                await userManager.UpdateAsync(user);
            }
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
            var existing = await db.Projects.FirstOrDefaultAsync(p => p.ProjectId == id);
            if (existing != null)
            {
                // Sync PM and IsClosed so tests see the expected project state after a re-seed
                var fresh = factory();
                existing.ProjectManagerId = fresh.ProjectManagerId;
                existing.IsClosed = fresh.IsClosed;
                await db.SaveChangesAsync();
                result[id] = existing;
                return existing;
            }
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

    // ── Work Packages ─────────────────────────────────────────────────────────

    private static async Task SeedWorkPackagesAsync(
        ApplicationDbContext db,
        Dictionary<string, ApplicationUser> users)
    {
        var existingWps = (await db.WorkPackages
            .Select(w => new { w.WorkPackageId, w.ProjectId })
            .ToListAsync())
            .Select(w => $"{w.ProjectId}:{w.WorkPackageId}")
            .ToHashSet();

        void Add(string id, int projectId, string title, string? parentId, bool isBottom, bool isClosed, string? eng = null)
        {
            if (existingWps.Contains($"{projectId}:{id}")) return;
            db.WorkPackages.Add(new WorkPackage
            {
                WorkPackageId = id, ProjectId = projectId, Title = title,
                ParentWorkPackageId = parentId,
                ParentWorkPackageProjectId = parentId != null ? projectId : 0,
                IsBottomLevel = isBottom, IsClosed = isClosed,
                ResponsibleUserId = eng != null ? users[eng].Id : null
            });
        }

        // Project 101 — Cloud Migration
        Add("A",  101, "Discovery",             null, false, false);
        Add("AA", 101, "Requirements Gathering", "A", true,  false, "eng1@sheet.dev");
        Add("AB", 101, "Architecture Design",    "A", true,  false, "eng6@sheet.dev");
        Add("B",  101, "Development",           null, false, false);
        Add("BA", 101, "Frontend",               "B", true,  false, "eng7@sheet.dev");
        Add("BB", 101, "API Layer",              "B", true,  false, "eng8@sheet.dev");
        Add("BC", 101, "Database Migration",     "B", true,  false, "eng4@sheet.dev");
        Add("C",  101, "Testing",               null, false, false);
        Add("CA", 101, "Unit Testing",           "C", true,  false, "eng2@sheet.dev");
        Add("CB", 101, "Integration Testing",    "C", true,  false, "eng3@sheet.dev");
        Add("D",  101, "Deployment",            null, true,  false, "eng6@sheet.dev");
        await db.SaveChangesAsync();

        // Project 102 — Mobile App
        Add("A",  102, "Planning",       null, false, false);
        Add("AA", 102, "Wireframes",      "A", true,  false, "eng5@sheet.dev");
        Add("AB", 102, "Sprint Planning", "A", true,  false, "eng5@sheet.dev");
        Add("B",  102, "Implementation", null, false, false);
        Add("BA", 102, "iOS",             "B", true,  false, "eng1@sheet.dev");
        Add("BB", 102, "Android",         "B", true,  false, "eng2@sheet.dev");
        Add("BC", 102, "Backend API",     "B", true,  false, "eng4@sheet.dev");
        Add("C",  102, "QA",             null, false, false);
        Add("CA", 102, "Testing",         "C", true,  false, "eng3@sheet.dev");
        await db.SaveChangesAsync();

        // Project 103 — Security Audit (all closed)
        Add("A",  103, "Assessment",        null, false, true);
        Add("AA", 103, "Vulnerability Scan", "A", true,  true, "eng6@sheet.dev");
        Add("AB", 103, "Penetration Test",   "A", true,  true, "eng6@sheet.dev");
        Add("B",  103, "Remediation",       null, true,  true, "eng4@sheet.dev");
        await db.SaveChangesAsync();

        // Sync ResponsibleUserId for existing WPs in case user GUIDs changed after a re-seed
        var respMap = new Dictionary<(string, int), string?>
        {
            { ("AA", 101), users["eng1@sheet.dev"].Id }, { ("AB", 101), users["eng6@sheet.dev"].Id },
            { ("BA", 101), users["eng7@sheet.dev"].Id }, { ("BB", 101), users["eng8@sheet.dev"].Id },
            { ("BC", 101), users["eng4@sheet.dev"].Id }, { ("CA", 101), users["eng2@sheet.dev"].Id },
            { ("CB", 101), users["eng3@sheet.dev"].Id }, { ("D",  101), users["eng6@sheet.dev"].Id },
            { ("AA", 102), users["eng5@sheet.dev"].Id }, { ("AB", 102), users["eng5@sheet.dev"].Id },
            { ("BA", 102), users["eng1@sheet.dev"].Id }, { ("BB", 102), users["eng2@sheet.dev"].Id },
            { ("BC", 102), users["eng4@sheet.dev"].Id }, { ("CA", 102), users["eng3@sheet.dev"].Id },
            { ("AA", 103), users["eng6@sheet.dev"].Id }, { ("AB", 103), users["eng6@sheet.dev"].Id },
            { ("B",  103), users["eng4@sheet.dev"].Id },
        };
        foreach (var ((wpId, projectId), userId) in respMap)
        {
            var wp = await db.WorkPackages.FindAsync(wpId, projectId);
            if (wp != null && wp.ResponsibleUserId != userId)
                wp.ResponsibleUserId = userId;
        }

        // Sync IsClosed — seed WPs may have been closed in the DB; reset the ones that should be open
        var shouldBeOpen = new (string, int)[]
        {
            ("A",  101), ("AA", 101), ("AB", 101),
            ("B",  101), ("BA", 101), ("BB", 101), ("BC", 101),
            ("C",  101), ("CA", 101), ("CB", 101), ("D",  101),
            ("A",  102), ("AA", 102), ("AB", 102),
            ("B",  102), ("BA", 102), ("BB", 102), ("BC", 102),
            ("C",  102), ("CA", 102),
        };
        foreach (var (wpId, projectId) in shouldBeOpen)
        {
            var wp = await db.WorkPackages.FindAsync(wpId, projectId);
            if (wp != null && wp.IsClosed)
                wp.IsClosed = false;
        }
        await db.SaveChangesAsync();
    }

    // ── Timesheets ────────────────────────────────────────────────────────────

    private static async Task SeedTimesheetsAsync(
        ApplicationDbContext db,
        Dictionary<string, ApplicationUser> users)
    {
        var sup1Id = users["sup1@sheet.dev"].Id;
        var dummy = new byte[] { 1 };

        async Task Add(string email, DateOnly endDate,
            byte[]? empHash, byte[]? appHash, string? appNotes,
            params (int proj, string wp, float sat, float sun, float mon, float tue, float wed, float thu, float fri)[] rowDefs)
        {
            var userId = users[email].Id;
            if (await db.Timesheets.AnyAsync(t => t.UserId == userId && t.EndDate == endDate)) return;

            var ts = new Timesheet
            {
                UserId = userId,
                TimesheetApproverId = sup1Id,
                EndDate = endDate,
                EmployeeHash = empHash,
                ApproverHash = appHash,
                ApproverNotes = appNotes,
            };

            foreach (var r in rowDefs)
            {
                var row = new TimesheetRow
                {
                    WorkPackageId = r.wp,
                    WorkPackageProjectId = r.proj,
                    packedHours = PackHours(r.sat, r.sun, r.mon, r.tue, r.wed, r.thu, r.fri)
                };
                row.TotalHoursRow = row.getSum();
                ts.TimesheetRows.Add(row);
            }

            ts.TotalHours = ts.TimesheetRows.Sum(r => r.TotalHoursRow);
            db.Timesheets.Add(ts);
            await db.SaveChangesAsync();
        }

        // approved — week ending May 16
        await Add("eng1@sheet.dev", new DateOnly(2026, 5, 16), dummy, dummy, null,
            (101, "BA", 0, 0, 8, 8, 4, 0, 0),
            (101, "BB", 0, 0, 0, 0, 4, 8, 8));

        await Add("eng2@sheet.dev", new DateOnly(2026, 5, 16), dummy, dummy, null,
            (101, "AA", 0, 0, 8, 8, 8, 0, 0),
            (101, "AB", 0, 0, 0, 0, 0, 8, 8));

        // approved — week ending May 23
        await Add("eng4@sheet.dev", new DateOnly(2026, 5, 23), dummy, dummy, null,
            (101, "BC", 0, 0, 8, 8, 8, 8, 0),
            (101, "CA", 0, 0, 0, 0, 0, 0, 8));

        await Add("eng3@sheet.dev", new DateOnly(2026, 5, 23), dummy, dummy, "Approved — short week noted",
            (101, "BA", 0, 0, 8, 8, 0, 0, 0),
            (101, "CB", 0, 0, 0, 0, 8, 8, 0));

        // submitted — week ending May 30
        await Add("eng6@sheet.dev", new DateOnly(2026, 5, 30), dummy, null, null,
            (101, "BB", 0, 0, 8, 8, 8, 0, 0),
            (101, "BC", 0, 0, 0, 0, 0, 8, 8));

        await Add("eng5@sheet.dev", new DateOnly(2026, 5, 30), dummy, null, null,
            (101, "CA", 0, 0, 8, 8, 0, 0, 0),
            (101, "CB", 0, 0, 0, 0, 8, 8, 8));

        // rejected — week ending May 30
        // status='rejected' because ApproverNotes set + both hashes null (see timesheetUtils.ts deriveStatus)
        await Add("eng8@sheet.dev", new DateOnly(2026, 5, 30), null, null,
            "Hours not matching project log — please revise",
            (101, "BA", 0, 0, 7, 7, 7, 7, 7));

        // draft — week ending Jun 6
        await Add("eng7@sheet.dev", new DateOnly(2026, 6, 6), null, null, null,
            (101, "AA", 0, 0, 8, 8, 8, 0, 0));

        await Add("eng3@sheet.dev", new DateOnly(2026, 6, 6), null, null, null,
            (101, "BB", 0, 0, 8, 8, 0, 0, 0),
            (101, "BA", 0, 0, 0, 0, 8, 8, 8));

        // submitted — week ending Jun 6
        await Add("eng4@sheet.dev", new DateOnly(2026, 6, 6), dummy, null, null,
            (101, "CB", 0, 0, 8, 8, 8, 0, 0),
            (101, "D",  0, 0, 0, 0, 0, 8, 8));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static long PackHours(float sat, float sun, float mon, float tue, float wed, float thu, float fri)
    {
        float[] hours = [sat, sun, mon, tue, wed, thu, fri];
        long packed = 0;
        for (int d = 0; d < 7; d++)
        {
            float normalized = MathF.Floor(hours[d]) + MathF.Round((hours[d] - MathF.Floor(hours[d])) / 0.25f) / 10f;
            int decihour = (int)Math.Round(normalized * 10);
            packed |= (long)decihour << (d * 8);
        }
        return packed;
    }
}
