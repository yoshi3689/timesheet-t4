using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TimesheetApp.Models.TimesheetModels;

namespace TimesheetApp.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
    public virtual DbSet<Timesheet> Timesheets { get; set; }

    public virtual DbSet<TimesheetRow> TimesheetRows { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<AspNetRole>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.HasIndex(e => e.NormalizedName, "RoleNameIndex").IsUnique();

            entity.Property(e => e.Id).HasMaxLength(250);
            entity.Property(e => e.ConcurrencyStamp).HasColumnType("text");
            entity.Property(e => e.Name).HasColumnType("text");
            entity.Property(e => e.NormalizedName).HasMaxLength(256);
        });

        modelBuilder.Entity<AspNetRoleClaim>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.HasIndex(e => e.RoleId, "IX_AspNetRoleClaims_RoleId");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.ClaimType).HasColumnType("text");
            entity.Property(e => e.ClaimValue).HasColumnType("text");
            entity.Property(e => e.RoleId).HasMaxLength(250);

            entity.HasOne(d => d.Role).WithMany(p => p.AspNetRoleClaims).HasForeignKey(d => d.RoleId);
        });

        modelBuilder.Entity<AspNetUser>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.HasIndex(e => e.NormalizedEmail, "EmailIndex");

            entity.HasIndex(e => e.NormalizedUserName, "UserNameIndex").IsUnique();

            entity.Property(e => e.Id).HasMaxLength(250);
            entity.Property(e => e.ConcurrencyStamp).HasColumnType("text");
            entity.Property(e => e.Email).HasColumnType("text");
            entity.Property(e => e.LockoutEnd).HasColumnType("text");
            entity.Property(e => e.NormalizedEmail).HasMaxLength(256);
            entity.Property(e => e.NormalizedUserName).HasMaxLength(256);
            entity.Property(e => e.PasswordHash).HasColumnType("text");
            entity.Property(e => e.PhoneNumber).HasColumnType("text");
            entity.Property(e => e.SecurityStamp).HasColumnType("text");
            entity.Property(e => e.UserName).HasColumnType("text");

            entity.HasMany(d => d.Roles).WithMany(p => p.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "AspNetUserRole",
                    r => r.HasOne<AspNetRole>().WithMany().HasForeignKey("RoleId"),
                    l => l.HasOne<AspNetUser>().WithMany().HasForeignKey("UserId"),
                    j =>
                    {
                        j.HasKey("UserId", "RoleId")
                            .HasName("PRIMARY")
                            .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });
                        j.HasIndex(new[] { "RoleId" }, "IX_AspNetUserRoles_RoleId");
                    });
        });

        modelBuilder.Entity<AspNetUserClaim>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.HasIndex(e => e.UserId, "IX_AspNetUserClaims_UserId");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.ClaimType).HasColumnType("text");
            entity.Property(e => e.ClaimValue).HasColumnType("text");
            entity.Property(e => e.UserId).HasMaxLength(250);

            entity.HasOne(d => d.User).WithMany(p => p.AspNetUserClaims).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<AspNetUserLogin>(entity =>
        {
            entity.HasKey(e => new { e.LoginProvider, e.ProviderKey })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0 });

            entity.HasIndex(e => e.UserId, "IX_AspNetUserLogins_UserId");

            entity.Property(e => e.LoginProvider).HasMaxLength(128);
            entity.Property(e => e.ProviderKey).HasMaxLength(128);
            entity.Property(e => e.ProviderDisplayName).HasColumnType("text");
            entity.Property(e => e.UserId).HasMaxLength(250);

            entity.HasOne(d => d.User).WithMany(p => p.AspNetUserLogins).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<AspNetUserToken>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.LoginProvider, e.Name })
                .HasName("PRIMARY")
                .HasAnnotation("MySql:IndexPrefixLength", new[] { 0, 0, 0 });

            entity.Property(e => e.UserId).HasMaxLength(250);
            entity.Property(e => e.LoginProvider).HasMaxLength(128);
            entity.Property(e => e.Name).HasMaxLength(128);
            entity.Property(e => e.Value).HasColumnType("text");

            entity.HasOne(d => d.User).WithMany(p => p.AspNetUserTokens).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<DeviceCode>(entity =>
        {
            entity.HasKey(e => e.UserCode).HasName("PRIMARY");

            entity.HasIndex(e => e.DeviceCode1, "IX_DeviceCodes_DeviceCode").IsUnique();

            entity.HasIndex(e => e.Expiration, "IX_DeviceCodes_Expiration");

            entity.Property(e => e.UserCode).HasMaxLength(200);
            entity.Property(e => e.ClientId).HasColumnType("text");
            entity.Property(e => e.CreationTime).HasColumnType("text");
            entity.Property(e => e.Data).HasColumnType("text");
            entity.Property(e => e.Description).HasColumnType("text");
            entity.Property(e => e.DeviceCode1)
                .HasMaxLength(200)
                .HasColumnName("DeviceCode");
            entity.Property(e => e.Expiration).HasMaxLength(250);
            entity.Property(e => e.SessionId).HasColumnType("text");
            entity.Property(e => e.SubjectId).HasColumnType("text");
        });

        modelBuilder.Entity<EfmigrationsHistory>(entity =>
        {
            entity.HasKey(e => e.MigrationId).HasName("PRIMARY");

            entity.ToTable("__EFMigrationsHistory");

            entity.Property(e => e.MigrationId).HasMaxLength(150);
            entity.Property(e => e.ProductVersion).HasMaxLength(32);
        });

        modelBuilder.Entity<Key>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.HasIndex(e => e.Use, "IX_Keys_Use");

            entity.Property(e => e.Id).HasMaxLength(250);
            entity.Property(e => e.Algorithm).HasColumnType("text");
            entity.Property(e => e.Created).HasColumnType("text");
            entity.Property(e => e.Data).HasColumnType("text");
            entity.Property(e => e.IsX509certificate).HasColumnName("IsX509Certificate");
            entity.Property(e => e.Use).HasMaxLength(450);
        });

        modelBuilder.Entity<PersistedGrant>(entity =>
        {
            entity.HasKey(e => e.Key).HasName("PRIMARY");

            entity.HasIndex(e => e.ConsumedTime, "IX_PersistedGrants_ConsumedTime");

            entity.HasIndex(e => e.Expiration, "IX_PersistedGrants_Expiration");

            entity.HasIndex(e => new { e.SubjectId, e.ClientId, e.Type }, "IX_PersistedGrants_SubjectId_ClientId_Type");

            entity.HasIndex(e => new { e.SubjectId, e.SessionId, e.Type }, "IX_PersistedGrants_SubjectId_SessionId_Type");

            entity.Property(e => e.Key).HasMaxLength(200);
            entity.Property(e => e.ClientId).HasMaxLength(200);
            entity.Property(e => e.ConsumedTime).HasMaxLength(250);
            entity.Property(e => e.CreationTime).HasColumnType("text");
            entity.Property(e => e.Data).HasColumnType("text");
            entity.Property(e => e.Description).HasColumnType("text");
            entity.Property(e => e.Expiration).HasMaxLength(250);
            entity.Property(e => e.SessionId).HasMaxLength(100);
            entity.Property(e => e.SubjectId).HasMaxLength(200);
            entity.Property(e => e.Type).HasMaxLength(50);
        });

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

        base.OnModelCreating(modelBuilder);

    }

}
