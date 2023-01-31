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
    public virtual DbSet<Timesheet> Timesheets { get; set; }

    public virtual DbSet<TimesheetRow> TimesheetRows { get; set; }

    public virtual DbSet<Signature> Signatures { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>()
            .Property(e => e.FirstName)
            .HasMaxLength(250);

        modelBuilder.Entity<ApplicationUser>()
            .Property(e => e.LastName)
            .HasMaxLength(250);

        modelBuilder.Entity<Timesheet>(entity =>
        {
            entity.HasKey(e => e.TimesheetId).HasName("PRIMARY");

            entity.HasIndex(e => e.UserId, "UserId");

            entity.Property(e => e.UserId).HasMaxLength(250);

            entity.HasOne(d => d.User).WithMany(p => p.Timesheets)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("Timesheets_ibfk_1");
        });

        modelBuilder.Entity<TimesheetRow>(entity =>
        {
            entity.HasKey(e => e.TimesheetRowId).HasName("PRIMARY");

            entity.ToTable("TimesheetRow");

            entity.HasIndex(e => e.TimesheetId, "FK_TimesheetRow_Timesheet_TimesheetId");

            entity.Property(e => e.Notes)
                .HasMaxLength(200)
                .IsFixedLength();
            entity.Property(e => e.WorkPackageId).HasColumnType("tinytext");

            entity.HasOne(d => d.Timesheet).WithMany(p => p.TimesheetRows)
                .HasForeignKey(d => d.TimesheetId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_TimesheetRow_Timesheet_TimesheetId");
        });
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
