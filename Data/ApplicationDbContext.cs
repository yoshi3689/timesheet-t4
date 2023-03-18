using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TimesheetApp.Models;
using TimesheetApp.Models.TimesheetModels;

namespace TimesheetApp.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
    public virtual DbSet<Timesheet> Timesheets => Set<Timesheet>();
    public virtual DbSet<TimesheetRow> TimesheetRows => Set<TimesheetRow>();
    public virtual DbSet<EmployeeProject> EmployeeProjects => Set<EmployeeProject>();
    public virtual DbSet<EmployeeWorkPackage> EmployeeWorkPackages => Set<EmployeeWorkPackage>();
    public virtual DbSet<LabourGrade> LabourGrades => Set<LabourGrade>();
    public virtual DbSet<Project> Projects => Set<Project>();
    public virtual DbSet<Budget> Budgets => Set<Budget>();
    public virtual DbSet<ResponsibleEngineerEstimate> ResponsibleEngineerEstimates => Set<ResponsibleEngineerEstimate>();
    public virtual DbSet<WorkPackage> WorkPackages => Set<WorkPackage>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<WorkPackage>()
            .HasOne(p => p.ParentWorkPackage)
            .WithMany(c => c.ChildWorkPackages)
            .HasForeignKey(p => new { p.ParentWorkPackageId, p.ParentWorkPackageProjectId });

        modelBuilder.Entity<EmployeeWorkPackage>()
            .HasOne(p => p.WorkPackage)
            .WithMany(c => c.EmployeeWorkPackages)
            .HasForeignKey(p => new { p.WorkPackageId, p.WorkPackageProjectId });

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

    }

}
