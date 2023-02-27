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
    public virtual DbSet<Timesheet>? Timesheets { get; set; }
    public virtual DbSet<TimesheetRow>? TimesheetRows { get; set; }
    public virtual DbSet<Signature>? Signatures { get; set; }
    public virtual DbSet<ApprovedTimesheet>? ApprovedTimesheet { get; set; }
    public virtual DbSet<ApprovedTimesheetRow>? ApprovedTimesheetRows { get; set; }
    public virtual DbSet<EmployeeProject>? EmployeeProjects { get; set; }
    public virtual DbSet<EmployeeWorkPackage>? EmployeeWorkPackages { get; set; }
    public virtual DbSet<LabourGrade>? LabourGrades { get; set; }
    public virtual DbSet<Project>? Projects { get; set; }
    public virtual DbSet<WorkPackage>? WorkPackages { get; set; }

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


        // TODO: add a foreign key constraint to the Signature table
        // modelBuilder.Entity<Timesheet>(entity =>
        // {
        //     entity.ToTable("Signature");
        //     entity.HasOne(u => u.User).WithOne(p => p.)
        //         .HasForeignKey(d => d.UserId)
        //         .OnDelete(DeleteBehavior.ClientSetNull)
        //         .HasConstraintName("Signatures_ibfk_1");
        // });
    }

}
