﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TimesheetApp.Data;

#nullable disable

namespace TimesheetApp.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    partial class ApplicationDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.3")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRole", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("varchar(255)");

                    b.Property<string>("ConcurrencyStamp")
                        .IsConcurrencyToken()
                        .HasColumnType("longtext");

                    b.Property<string>("Name")
                        .HasMaxLength(256)
                        .HasColumnType("varchar(256)");

                    b.Property<string>("NormalizedName")
                        .HasMaxLength(256)
                        .HasColumnType("varchar(256)");

                    b.HasKey("Id");

                    b.HasIndex("NormalizedName")
                        .IsUnique()
                        .HasDatabaseName("RoleNameIndex");

                    b.ToTable("AspNetRoles", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("ClaimType")
                        .HasColumnType("longtext");

                    b.Property<string>("ClaimValue")
                        .HasColumnType("longtext");

                    b.Property<string>("RoleId")
                        .IsRequired()
                        .HasColumnType("varchar(255)");

                    b.HasKey("Id");

                    b.HasIndex("RoleId");

                    b.ToTable("AspNetRoleClaims", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("ClaimType")
                        .HasColumnType("longtext");

                    b.Property<string>("ClaimValue")
                        .HasColumnType("longtext");

                    b.Property<string>("UserId")
                        .IsRequired()
                        .HasColumnType("varchar(255)");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("AspNetUserClaims", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<string>", b =>
                {
                    b.Property<string>("LoginProvider")
                        .HasMaxLength(128)
                        .HasColumnType("varchar(128)");

                    b.Property<string>("ProviderKey")
                        .HasMaxLength(128)
                        .HasColumnType("varchar(128)");

                    b.Property<string>("ProviderDisplayName")
                        .HasColumnType("longtext");

                    b.Property<string>("UserId")
                        .IsRequired()
                        .HasColumnType("varchar(255)");

                    b.HasKey("LoginProvider", "ProviderKey");

                    b.HasIndex("UserId");

                    b.ToTable("AspNetUserLogins", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>", b =>
                {
                    b.Property<string>("UserId")
                        .HasColumnType("varchar(255)");

                    b.Property<string>("RoleId")
                        .HasColumnType("varchar(255)");

                    b.HasKey("UserId", "RoleId");

                    b.HasIndex("RoleId");

                    b.ToTable("AspNetUserRoles", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<string>", b =>
                {
                    b.Property<string>("UserId")
                        .HasColumnType("varchar(255)");

                    b.Property<string>("LoginProvider")
                        .HasMaxLength(128)
                        .HasColumnType("varchar(128)");

                    b.Property<string>("Name")
                        .HasMaxLength(128)
                        .HasColumnType("varchar(128)");

                    b.Property<string>("Value")
                        .HasColumnType("longtext");

                    b.HasKey("UserId", "LoginProvider", "Name");

                    b.ToTable("AspNetUserTokens", (string)null);
                });

            modelBuilder.Entity("TimesheetApp.Models.ApplicationUser", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("varchar(255)");

                    b.Property<int>("AccessFailedCount")
                        .HasColumnType("int");

                    b.Property<string>("ConcurrencyStamp")
                        .IsConcurrencyToken()
                        .HasColumnType("longtext");

                    b.Property<string>("Email")
                        .HasMaxLength(256)
                        .HasColumnType("varchar(256)");

                    b.Property<bool>("EmailConfirmed")
                        .HasColumnType("tinyint(1)");

                    b.Property<int>("EmployeeNumber")
                        .HasMaxLength(100)
                        .HasColumnType("int");

                    b.Property<string>("FirstName")
                        .IsRequired()
                        .HasMaxLength(250)
                        .HasColumnType("varchar(250)");

                    b.Property<double>("FlexTime")
                        .HasColumnType("double");

                    b.Property<bool>("HasTempPassword")
                        .HasColumnType("tinyint(1)");

                    b.Property<string>("JobTitle")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<string>("LabourGradeCode")
                        .IsRequired()
                        .HasColumnType("varchar(2)");

                    b.Property<string>("LastName")
                        .IsRequired()
                        .HasMaxLength(250)
                        .HasColumnType("varchar(250)");

                    b.Property<bool>("LockoutEnabled")
                        .HasColumnType("tinyint(1)");

                    b.Property<DateTimeOffset?>("LockoutEnd")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("NormalizedEmail")
                        .HasMaxLength(256)
                        .HasColumnType("varchar(256)");

                    b.Property<string>("NormalizedUserName")
                        .HasMaxLength(256)
                        .HasColumnType("varchar(256)");

                    b.Property<string>("PasswordHash")
                        .HasColumnType("longtext");

                    b.Property<string>("PhoneNumber")
                        .HasColumnType("longtext");

                    b.Property<bool>("PhoneNumberConfirmed")
                        .HasColumnType("tinyint(1)");

                    b.Property<byte[]>("PrivateKey")
                        .HasColumnType("longblob");

                    b.Property<byte[]>("PublicKey")
                        .HasColumnType("longblob");

                    b.Property<double>("Salary")
                        .HasColumnType("double");

                    b.Property<string>("SecurityStamp")
                        .HasColumnType("longtext");

                    b.Property<double>("SickDays")
                        .HasColumnType("double");

                    b.Property<string>("SupervisorId")
                        .HasColumnType("varchar(255)");

                    b.Property<string>("TimesheetApproverId")
                        .HasColumnType("varchar(255)");

                    b.Property<bool>("TwoFactorEnabled")
                        .HasColumnType("tinyint(1)");

                    b.Property<string>("UserName")
                        .HasMaxLength(256)
                        .HasColumnType("varchar(256)");

                    b.HasKey("Id");

                    b.HasIndex("EmployeeNumber")
                        .IsUnique();

                    b.HasIndex("LabourGradeCode");

                    b.HasIndex("NormalizedEmail")
                        .HasDatabaseName("EmailIndex");

                    b.HasIndex("NormalizedUserName")
                        .IsUnique()
                        .HasDatabaseName("UserNameIndex");

                    b.HasIndex("SupervisorId");

                    b.HasIndex("TimesheetApproverId");

                    b.ToTable("AspNetUsers", (string)null);
                });

            modelBuilder.Entity("TimesheetApp.Models.Signature", b =>
                {
                    b.Property<int>("SignatureId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("Name")
                        .HasColumnType("longtext");

                    b.Property<string>("SignatureImage")
                        .HasColumnType("longtext");

                    b.Property<string>("UserId")
                        .HasColumnType("varchar(255)");

                    b.HasKey("SignatureId");

                    b.HasIndex("UserId");

                    b.ToTable("Signatures");
                });

            modelBuilder.Entity("TimesheetApp.Models.TimesheetModels.Budget", b =>
                {
                    b.Property<int>("BudgetId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<double>("BudgetAmount")
                        .HasColumnType("double");

                    b.Property<string>("LabourCode")
                        .HasColumnType("varchar(2)");

                    b.Property<string>("WPProjectId")
                        .HasColumnType("longtext");

                    b.Property<bool>("isREBudget")
                        .HasColumnType("tinyint(1)");

                    b.HasKey("BudgetId");

                    b.HasIndex("LabourCode");

                    b.ToTable("Budgets");
                });

            modelBuilder.Entity("TimesheetApp.Models.TimesheetModels.EmployeeProject", b =>
                {
                    b.Property<string>("UserId")
                        .HasColumnType("varchar(255)");

                    b.Property<string>("ProjectId")
                        .HasColumnType("varchar(255)");

                    b.HasKey("UserId", "ProjectId");

                    b.HasIndex("ProjectId");

                    b.ToTable("EmployeeProjects");
                });

            modelBuilder.Entity("TimesheetApp.Models.TimesheetModels.EmployeeWorkPackage", b =>
                {
                    b.Property<string>("UserId")
                        .HasColumnType("varchar(255)");

                    b.Property<string>("WorkPackageId")
                        .HasColumnType("varchar(255)");

                    b.Property<string>("WorkPackageProjectId")
                        .IsRequired()
                        .HasColumnType("varchar(255)");

                    b.HasKey("UserId", "WorkPackageId");

                    b.HasIndex("WorkPackageId", "WorkPackageProjectId");

                    b.ToTable("EmployeeWorkPackages");
                });

            modelBuilder.Entity("TimesheetApp.Models.TimesheetModels.LabourGrade", b =>
                {
                    b.Property<string>("LabourCode")
                        .HasMaxLength(2)
                        .HasColumnType("varchar(2)");

                    b.Property<double>("Rate")
                        .HasColumnType("double");

                    b.HasKey("LabourCode");

                    b.ToTable("LabourGrades");
                });

            modelBuilder.Entity("TimesheetApp.Models.TimesheetModels.Project", b =>
                {
                    b.Property<string>("ProjectId")
                        .HasColumnType("varchar(255)");

                    b.Property<double>("ActualCost")
                        .HasColumnType("double");

                    b.Property<string>("AssistantProjectManagerId")
                        .HasColumnType("varchar(255)");

                    b.Property<string>("ProjectManagerId")
                        .IsRequired()
                        .HasColumnType("varchar(255)");

                    b.Property<double>("TotalBudget")
                        .HasColumnType("double");

                    b.HasKey("ProjectId");

                    b.HasIndex("AssistantProjectManagerId");

                    b.HasIndex("ProjectManagerId");

                    b.ToTable("Projects");
                });

            modelBuilder.Entity("TimesheetApp.Models.TimesheetModels.ResponsibleEngineerEstimate", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<DateOnly?>("Date")
                        .HasColumnType("date");

                    b.Property<double>("EstimatedCost")
                        .HasColumnType("double");

                    b.Property<string>("LabourCode")
                        .HasColumnType("varchar(2)");

                    b.Property<string>("ProjectId")
                        .HasColumnType("longtext");

                    b.Property<string>("WPProjectId")
                        .HasColumnType("longtext");

                    b.HasKey("Id");

                    b.HasIndex("LabourCode");

                    b.ToTable("ResponsibleEngineerEstimates");
                });

            modelBuilder.Entity("TimesheetApp.Models.TimesheetModels.Timesheet", b =>
                {
                    b.Property<int>("TimesheetId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<DateOnly?>("EndDate")
                        .IsRequired()
                        .HasColumnType("date");

                    b.Property<double>("FlexHours")
                        .HasColumnType("double");

                    b.Property<double>("Overtime")
                        .HasColumnType("double");

                    b.Property<double?>("TotalHours")
                        .HasColumnType("double");

                    b.Property<string>("UserId")
                        .IsRequired()
                        .HasColumnType("varchar(255)");

                    b.HasKey("TimesheetId");

                    b.HasIndex("UserId");

                    b.ToTable("Timesheets");
                });

            modelBuilder.Entity("TimesheetApp.Models.TimesheetModels.TimesheetRow", b =>
                {
                    b.Property<int>("TimesheetRowId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<byte[]>("Hash")
                        .HasColumnType("longblob");

                    b.Property<string>("Notes")
                        .HasColumnType("longtext");

                    b.Property<string>("OriginalLabourCode")
                        .HasColumnType("varchar(2)");

                    b.Property<string>("ProjectId")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("varchar(255)");

                    b.Property<int>("TimesheetId")
                        .HasColumnType("int");

                    b.Property<double>("TotalHoursRow")
                        .HasColumnType("double");

                    b.Property<string>("WorkPackageId")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("varchar(255)");

                    b.Property<string>("WorkPackageProjectId")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("varchar(255)");

                    b.Property<long>("packedHours")
                        .HasColumnType("bigint");

                    b.HasKey("TimesheetRowId");

                    b.HasIndex("OriginalLabourCode");

                    b.HasIndex("ProjectId");

                    b.HasIndex("TimesheetId");

                    b.HasIndex("WorkPackageId", "WorkPackageProjectId");

                    b.ToTable("TimesheetRows");
                });

            modelBuilder.Entity("TimesheetApp.Models.TimesheetModels.WorkPackage", b =>
                {
                    b.Property<string>("WorkPackageId")
                        .HasColumnType("varchar(255)");

                    b.Property<string>("ProjectId")
                        .HasColumnType("varchar(255)");

                    b.Property<double>("ActualCost")
                        .HasColumnType("double");

                    b.Property<bool>("IsBottomLevel")
                        .HasColumnType("tinyint(1)");

                    b.Property<bool>("IsClosed")
                        .HasColumnType("tinyint(1)");

                    b.Property<string>("ParentWorkPackageId")
                        .HasColumnType("varchar(255)");

                    b.Property<string>("ParentWorkPackageProjectId")
                        .HasColumnType("varchar(255)");

                    b.Property<string>("ResponsibleUserId")
                        .HasColumnType("varchar(255)");

                    b.HasKey("WorkPackageId", "ProjectId");

                    b.HasIndex("ProjectId");

                    b.HasIndex("ResponsibleUserId");

                    b.HasIndex("ParentWorkPackageId", "ParentWorkPackageProjectId");

                    b.ToTable("WorkPackages");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>", b =>
                {
                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityRole", null)
                        .WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>", b =>
                {
                    b.HasOne("TimesheetApp.Models.ApplicationUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<string>", b =>
                {
                    b.HasOne("TimesheetApp.Models.ApplicationUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>", b =>
                {
                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityRole", null)
                        .WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("TimesheetApp.Models.ApplicationUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<string>", b =>
                {
                    b.HasOne("TimesheetApp.Models.ApplicationUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("TimesheetApp.Models.ApplicationUser", b =>
                {
                    b.HasOne("TimesheetApp.Models.TimesheetModels.LabourGrade", "LabourGrade")
                        .WithMany("ApplicationUsers")
                        .HasForeignKey("LabourGradeCode")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("TimesheetApp.Models.ApplicationUser", "Supervisor")
                        .WithMany("SupervisedUsers")
                        .HasForeignKey("SupervisorId");

                    b.HasOne("TimesheetApp.Models.ApplicationUser", "TimesheetApprover")
                        .WithMany("ApprovableUsers")
                        .HasForeignKey("TimesheetApproverId");

                    b.Navigation("LabourGrade");

                    b.Navigation("Supervisor");

                    b.Navigation("TimesheetApprover");
                });

            modelBuilder.Entity("TimesheetApp.Models.Signature", b =>
                {
                    b.HasOne("TimesheetApp.Models.ApplicationUser", "User")
                        .WithMany()
                        .HasForeignKey("UserId");

                    b.Navigation("User");
                });

            modelBuilder.Entity("TimesheetApp.Models.TimesheetModels.Budget", b =>
                {
                    b.HasOne("TimesheetApp.Models.TimesheetModels.LabourGrade", "LabourGrade")
                        .WithMany("Budgets")
                        .HasForeignKey("LabourCode");

                    b.Navigation("LabourGrade");
                });

            modelBuilder.Entity("TimesheetApp.Models.TimesheetModels.EmployeeProject", b =>
                {
                    b.HasOne("TimesheetApp.Models.TimesheetModels.Project", "Project")
                        .WithMany("EmployeeProjects")
                        .HasForeignKey("ProjectId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("TimesheetApp.Models.ApplicationUser", "User")
                        .WithMany("EmployeeProjects")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Project");

                    b.Navigation("User");
                });

            modelBuilder.Entity("TimesheetApp.Models.TimesheetModels.EmployeeWorkPackage", b =>
                {
                    b.HasOne("TimesheetApp.Models.ApplicationUser", "User")
                        .WithMany("WorkPackages")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("TimesheetApp.Models.TimesheetModels.WorkPackage", "WorkPackage")
                        .WithMany("EmployeeWorkPackages")
                        .HasForeignKey("WorkPackageId", "WorkPackageProjectId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");

                    b.Navigation("WorkPackage");
                });

            modelBuilder.Entity("TimesheetApp.Models.TimesheetModels.Project", b =>
                {
                    b.HasOne("TimesheetApp.Models.ApplicationUser", "AssistantProjectManager")
                        .WithMany("AssistantManagedProjects")
                        .HasForeignKey("AssistantProjectManagerId");

                    b.HasOne("TimesheetApp.Models.ApplicationUser", "ProjectManager")
                        .WithMany("ManagedProjects")
                        .HasForeignKey("ProjectManagerId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("AssistantProjectManager");

                    b.Navigation("ProjectManager");
                });

            modelBuilder.Entity("TimesheetApp.Models.TimesheetModels.ResponsibleEngineerEstimate", b =>
                {
                    b.HasOne("TimesheetApp.Models.TimesheetModels.LabourGrade", "LabourGrade")
                        .WithMany("ResponsibleEngineerEstimates")
                        .HasForeignKey("LabourCode");

                    b.Navigation("LabourGrade");
                });

            modelBuilder.Entity("TimesheetApp.Models.TimesheetModels.Timesheet", b =>
                {
                    b.HasOne("TimesheetApp.Models.ApplicationUser", "User")
                        .WithMany("Timesheets")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("TimesheetApp.Models.TimesheetModels.TimesheetRow", b =>
                {
                    b.HasOne("TimesheetApp.Models.TimesheetModels.LabourGrade", "OriginalLabourGrade")
                        .WithMany()
                        .HasForeignKey("OriginalLabourCode");

                    b.HasOne("TimesheetApp.Models.TimesheetModels.Project", "Project")
                        .WithMany()
                        .HasForeignKey("ProjectId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("TimesheetApp.Models.TimesheetModels.Timesheet", "Timesheet")
                        .WithMany("TimesheetRows")
                        .HasForeignKey("TimesheetId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("TimesheetApp.Models.TimesheetModels.WorkPackage", "WorkPackage")
                        .WithMany("TimesheetRows")
                        .HasForeignKey("WorkPackageId", "WorkPackageProjectId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("OriginalLabourGrade");

                    b.Navigation("Project");

                    b.Navigation("Timesheet");

                    b.Navigation("WorkPackage");
                });

            modelBuilder.Entity("TimesheetApp.Models.TimesheetModels.WorkPackage", b =>
                {
                    b.HasOne("TimesheetApp.Models.TimesheetModels.Project", "Project")
                        .WithMany()
                        .HasForeignKey("ProjectId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("TimesheetApp.Models.ApplicationUser", "ResponsibleUser")
                        .WithMany("SupervisedWorkPackage")
                        .HasForeignKey("ResponsibleUserId");

                    b.HasOne("TimesheetApp.Models.TimesheetModels.WorkPackage", "ParentWorkPackage")
                        .WithMany("ChildWorkPackages")
                        .HasForeignKey("ParentWorkPackageId", "ParentWorkPackageProjectId");

                    b.Navigation("ParentWorkPackage");

                    b.Navigation("Project");

                    b.Navigation("ResponsibleUser");
                });

            modelBuilder.Entity("TimesheetApp.Models.ApplicationUser", b =>
                {
                    b.Navigation("ApprovableUsers");

                    b.Navigation("AssistantManagedProjects");

                    b.Navigation("EmployeeProjects");

                    b.Navigation("ManagedProjects");

                    b.Navigation("SupervisedUsers");

                    b.Navigation("SupervisedWorkPackage");

                    b.Navigation("Timesheets");

                    b.Navigation("WorkPackages");
                });

            modelBuilder.Entity("TimesheetApp.Models.TimesheetModels.LabourGrade", b =>
                {
                    b.Navigation("ApplicationUsers");

                    b.Navigation("Budgets");

                    b.Navigation("ResponsibleEngineerEstimates");
                });

            modelBuilder.Entity("TimesheetApp.Models.TimesheetModels.Project", b =>
                {
                    b.Navigation("EmployeeProjects");
                });

            modelBuilder.Entity("TimesheetApp.Models.TimesheetModels.Timesheet", b =>
                {
                    b.Navigation("TimesheetRows");
                });

            modelBuilder.Entity("TimesheetApp.Models.TimesheetModels.WorkPackage", b =>
                {
                    b.Navigation("ChildWorkPackages");

                    b.Navigation("EmployeeWorkPackages");

                    b.Navigation("TimesheetRows");
                });
#pragma warning restore 612, 618
        }
    }
}
