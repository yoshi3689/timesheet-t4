using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TimesheetApp.Models;
using TimesheetApp.Models.TimesheetModels;

namespace TimesheetApp.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext()
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
    public virtual DbSet<Timesheet> Timesheets => Set<Timesheet>();
    public virtual DbSet<TimesheetRow> TimesheetRows => Set<TimesheetRow>();
    public virtual DbSet<EmployeeWorkPackage> EmployeeWorkPackages => Set<EmployeeWorkPackage>();
    public virtual DbSet<LabourGrade> LabourGrades => Set<LabourGrade>();
    public virtual DbSet<Project> Projects => Set<Project>();
    public virtual DbSet<Budget> Budgets => Set<Budget>();
    public virtual DbSet<ResponsibleEngineerEstimate> ResponsibleEngineerEstimates => Set<ResponsibleEngineerEstimate>();
    public virtual DbSet<WorkPackage> WorkPackages => Set<WorkPackage>();
    public virtual DbSet<Notification> Notifications => Set<Notification>();
    public virtual DbSet<ApplicationUser> ApplicationUsers => Set<ApplicationUser>();
    public virtual DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public virtual DbSet<SystemSettings> SystemSettings => Set<SystemSettings>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<WorkPackage>()
            .HasOne(p => p.ParentWorkPackage)
            .WithMany(c => c.ChildWorkPackages)
            .HasForeignKey(p => new { p.ParentWorkPackageId, p.ProjectId });

        modelBuilder.Entity<EmployeeWorkPackage>()
            .HasOne(p => p.WorkPackage)
            .WithMany(c => c.EmployeeWorkPackages)
            .HasForeignKey(p => new { p.WorkPackageId, p.WorkPackageProjectId })
            .HasPrincipalKey(p => new { p.WorkPackageId, p.ProjectId });

        modelBuilder.Entity<TimesheetRow>()
            .Property(e => e.WorkPackageId)
            .HasMaxLength(255);

        modelBuilder.Entity<TimesheetRow>()
            .HasOne(p => p.WorkPackage)
            .WithMany(c => c.TimesheetRows)
            .HasForeignKey(p => new { p.WorkPackageId, p.WorkPackageProjectId });

        modelBuilder.Entity<ApplicationUser>()
            .Property(e => e.FirstName)
            .HasMaxLength(250);

        modelBuilder.Entity<ApplicationUser>()
            .Property(e => e.LastName)
            .HasMaxLength(250);


        // IDs pinned to match production's current AspNetRoles rows (verified 2026-07-05).
        // Without an explicit Id, EF regenerates a random Guid via IdentityRole's default
        // constructor on every `dotnet ef migrations add`, scaffolding a spurious
        // delete-and-reinsert of these rows on every future migration — which would break
        // every AspNetUserRoles FK row pointing at the old Ids (verified in prod: HR/Admin/
        // Supervisor are referenced by 8 live user-role rows; PM/Employee are seeded
        // elsewhere via RoleManager, not HasData, so they're unaffected by this).
        modelBuilder.Entity<IdentityRole>().HasData(
            new IdentityRole { Id = "3aa3ac9c-2ea8-4f91-a13c-3e98476fac1b", Name = "HR", NormalizedName = "HR" },
            new IdentityRole { Id = "c930ec34-3a30-482b-9df2-00b27bb7c0b2", Name = "Admin", NormalizedName = "ADMIN" },
            new IdentityRole { Id = "488c0c63-7db3-4dcb-9cea-225890c4368e", Name = "Supervisor", NormalizedName = "SUPERVISOR" }
        );

        List<LabourGrade> labourGrades = new List<LabourGrade>();
        List<double> rates = new List<double> { 223.74, 246.81, 265.26, 339.07, 412.88, 518.98, 613.55 };
        double averageInflation = 0.038;
        int counter = 0;
        for (int i = 0; i < 10; i++)
        {
            labourGrades.Add(new LabourGrade { Id = counter + i + 1, LabourCode = "DS", Rate = Math.Round(rates[0] * Math.Pow(1 + averageInflation, i + 1), 2), Year = i + 2023 });
            labourGrades.Add(new LabourGrade { Id = counter + i + 2, LabourCode = "SS", Rate = Math.Round(rates[1] * Math.Pow(1 + averageInflation, i + 1), 2), Year = i + 2023 });
            labourGrades.Add(new LabourGrade { Id = counter + i + 3, LabourCode = "P1", Rate = Math.Round(rates[2] * Math.Pow(1 + averageInflation, i + 1), 2), Year = i + 2023 });
            labourGrades.Add(new LabourGrade { Id = counter + i + 4, LabourCode = "P2", Rate = Math.Round(rates[3] * Math.Pow(1 + averageInflation, i + 1), 2), Year = i + 2023 });
            labourGrades.Add(new LabourGrade { Id = counter + i + 5, LabourCode = "P3", Rate = Math.Round(rates[4] * Math.Pow(1 + averageInflation, i + 1), 2), Year = i + 2023 });
            labourGrades.Add(new LabourGrade { Id = counter + i + 6, LabourCode = "P4", Rate = Math.Round(rates[5] * Math.Pow(1 + averageInflation, i + 1), 2), Year = i + 2023 });
            labourGrades.Add(new LabourGrade { Id = counter + i + 7, LabourCode = "P5", Rate = Math.Round(rates[6] * Math.Pow(1 + averageInflation, i + 1), 2), Year = i + 2023 });
            counter += 7;
        }
        modelBuilder.Entity<LabourGrade>().HasData(labourGrades);

        modelBuilder.Entity<SystemSettings>().HasData(
            new SystemSettings { Id = 1, Require2FA = false }
        );
    }
}
