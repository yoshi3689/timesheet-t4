using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using TimesheetApp.Data;
using TimesheetApp.Helpers;
using TimesheetApp.Models;
using TimesheetApp.Models.TimesheetModels;
using TimesheetApp.Services;
using Xunit;

namespace TimesheetApp.Tests;

// ─── helpers ────────────────────────────────────────────────────────────────

static class DbFactory
{
    public static ApplicationDbContext Create(string name)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: name)
            .Options;
        return new ApplicationDbContext(options);
    }
}

// ─── SignatureService ────────────────────────────────────────────────────────

public class SignatureServiceTests
{
    private readonly SignatureService _svc = new SignatureService();

    private static (byte[] pub, byte[] encPriv) GenerateKeyPair()
    {
        using var rsa = RSA.Create();
        var pub = rsa.ExportRSAPublicKey();
        var encPriv = KeyHelper.Encrypt(rsa.ExportRSAPrivateKey(), "testpassword");
        return (pub, encPriv);
    }

    private static Timesheet MakeTimesheet() => new Timesheet
    {
        TimesheetId = 1,
        EndDate = DateOnly.FromDateTime(DateTime.Today),
        TotalHours = 40,
        FlexHours = 0,
        Overtime = 0,
        UserId = "user1"
    };

    [Fact]
    public void HashTimesheet_ReturnsNonNull_WithValidKey()
    {
        var (_, encPriv) = GenerateKeyPair();
        var ts = MakeTimesheet();
        var hash = _svc.HashTimesheet(ts, "testpassword", encPriv);
        Assert.NotNull(hash);
    }

    [Fact]
    public void HashTimesheet_ReturnsNull_WithWrongPassword()
    {
        var (_, encPriv) = GenerateKeyPair();
        var ts = MakeTimesheet();
        var hash = _svc.HashTimesheet(ts, "wrongpassword", encPriv);
        Assert.Null(hash);
    }

    [Fact]
    public void VerifySignature_ReturnsTrueForValidSignature()
    {
        var (pub, encPriv) = GenerateKeyPair();
        var ts = MakeTimesheet();
        var hash = _svc.HashTimesheet(ts, "testpassword", encPriv)!;
        Assert.True(_svc.VerifySignature(ts, pub, hash));
    }

    [Fact]
    public void VerifySignature_ReturnsFalseForTamperedTimesheet()
    {
        var (pub, encPriv) = GenerateKeyPair();
        var ts = MakeTimesheet();
        var hash = _svc.HashTimesheet(ts, "testpassword", encPriv)!;

        ts.TotalHours = 999; // tamper
        Assert.False(_svc.VerifySignature(ts, pub, hash));
    }

    [Fact]
    public void VerifySignature_ReturnsFalseForWrongKey()
    {
        var (_, encPriv) = GenerateKeyPair();
        var (wrongPub, _) = GenerateKeyPair();
        var ts = MakeTimesheet();
        var hash = _svc.HashTimesheet(ts, "testpassword", encPriv)!;
        Assert.False(_svc.VerifySignature(ts, wrongPub, hash));
    }

    [Fact]
    public void HashVerify_RoundTrip_WithTimesheetRows()
    {
        var (pub, encPriv) = GenerateKeyPair();
        var ts = MakeTimesheet();
        var row = new TimesheetRow { WorkPackageId = "WP1", WorkPackageProjectId = 1, OriginalLabourCode = "P5", packedHours = 0, TimesheetId = 1 };
        ts.TimesheetRows.Add(row);

        var hash = _svc.HashTimesheet(ts, "testpassword", encPriv)!;
        Assert.NotNull(hash);
        Assert.True(_svc.VerifySignature(ts, pub, hash));
    }

    [Fact]
    public void VerifySignature_StillValid_WhenEfMaterializesRowsInDifferentOrder()
    {
        // Regression test for the EF Core nav-collection ordering bug:
        // CreateDataString must sort rows by TimesheetRowId so sign-time and verify-time
        // produce the same string regardless of how EF materialises the collection.
        var (pub, encPriv) = GenerateKeyPair();
        var ts = MakeTimesheet();

        // Add rows with IDs out of natural order to expose any ordering dependency.
        ts.TimesheetRows.Add(new TimesheetRow { TimesheetRowId = 3, WorkPackageId = "WP3", WorkPackageProjectId = 1, OriginalLabourCode = "P3", packedHours = 3000L });
        ts.TimesheetRows.Add(new TimesheetRow { TimesheetRowId = 1, WorkPackageId = "WP1", WorkPackageProjectId = 1, OriginalLabourCode = "P1", packedHours = 1000L });
        ts.TimesheetRows.Add(new TimesheetRow { TimesheetRowId = 2, WorkPackageId = "WP2", WorkPackageProjectId = 1, OriginalLabourCode = "P2", packedHours = 2000L });

        var hash = _svc.HashTimesheet(ts, "testpassword", encPriv)!;

        // Simulate EF returning rows in reversed order on the verify path.
        var reversed = ts.TimesheetRows.Reverse().ToList();
        ts.TimesheetRows.Clear();
        foreach (var r in reversed) ts.TimesheetRows.Add(r);

        Assert.True(_svc.VerifySignature(ts, pub, hash));
    }
}

// ─── NotificationService ────────────────────────────────────────────────────

public class NotificationServiceTests
{
    private static ApplicationDbContext Db(string name) => DbFactory.Create(name);

    [Fact]
    public void GetUserNotifications_ReturnsOnlyOwnedNotifications()
    {
        using var ctx = Db(nameof(GetUserNotifications_ReturnsOnlyOwnedNotifications));
        ctx.Notifications.AddRange(
            new Notification { UserId = "u1", Message = "A", For = "f1", Importance = 1 },
            new Notification { UserId = "u2", Message = "B", For = "f2", Importance = 1 }
        );
        ctx.SaveChanges();

        var svc = new NotificationService(ctx);
        var result = svc.GetUserNotifications("u1");
        Assert.Single(result);
        Assert.Equal("A", result[0].Message);
    }

    [Fact]
    public async Task DismissNotificationAsync_RemovesCorrectNotification()
    {
        using var ctx = Db(nameof(DismissNotificationAsync_RemovesCorrectNotification));
        ctx.Notifications.Add(new Notification { Id = 1, UserId = "u1", Message = "X", For = "f", Importance = 1 });
        ctx.SaveChanges();

        var svc = new NotificationService(ctx);
        await svc.DismissNotificationAsync(1, "u1");

        Assert.Empty(ctx.Notifications.ToList());
    }

    [Fact]
    public async Task DismissNotificationAsync_DoesNotRemoveOtherUsersNotification()
    {
        using var ctx = Db(nameof(DismissNotificationAsync_DoesNotRemoveOtherUsersNotification));
        ctx.Notifications.Add(new Notification { Id = 2, UserId = "u1", Message = "Y", For = "f", Importance = 1 });
        ctx.SaveChanges();

        var svc = new NotificationService(ctx);
        await svc.DismissNotificationAsync(2, "u2"); // wrong user

        Assert.Single(ctx.Notifications.ToList());
    }

    [Fact]
    public void AddNotification_AddsToContext()
    {
        using var ctx = Db(nameof(AddNotification_AddsToContext));
        var svc = new NotificationService(ctx);
        svc.AddNotification("u1", "Hello", "field1", 2);
        ctx.SaveChanges();

        var n = ctx.Notifications.Single();
        Assert.Equal("u1", n.UserId);
        Assert.Equal("Hello", n.Message);
        Assert.Equal("field1", n.For);
        Assert.Equal(2, n.Importance);
    }

    [Fact]
    public void NotificationExistsFor_ReturnsTrueWhenExists()
    {
        using var ctx = Db(nameof(NotificationExistsFor_ReturnsTrueWhenExists));
        ctx.Notifications.Add(new Notification { UserId = "u1", For = "target", Importance = 1 });
        ctx.SaveChanges();

        var svc = new NotificationService(ctx);
        Assert.True(svc.NotificationExistsFor("u1", "target"));
        Assert.False(svc.NotificationExistsFor("u1", "missing"));
    }

    [Fact]
    public void NotificationExistsFor_IsScopedByUser_NotJustForField()
    {
        using var ctx = Db(nameof(NotificationExistsFor_IsScopedByUser_NotJustForField));
        ctx.Notifications.Add(new Notification { UserId = "u1", For = "target", Importance = 1 });
        ctx.SaveChanges();

        var svc = new NotificationService(ctx);
        // Regression check for the cross-user silent-drop bug: a notification
        // existing for u1 must not block the same "For" value for u2.
        Assert.False(svc.NotificationExistsFor("u2", "target"));
    }
}

// ─── TimesheetService ────────────────────────────────────────────────────────

public class TimesheetServiceTests
{
    private static ApplicationDbContext Db(string name) => DbFactory.Create(name);

    private static (byte[] pub, byte[] encPriv) GenKeys()
    {
        using var rsa = RSA.Create();
        return (rsa.ExportRSAPublicKey(), KeyHelper.Encrypt(rsa.ExportRSAPrivateKey(), "pw"));
    }

    private static Mock<UserManager<ApplicationUser>> MockUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(store.Object, null, null, null, null, null, null, null, null);
    }

    [Fact]
    public void GetUnapprovedTimesheets_ReturnsOnlyUnapproved()
    {
        using var ctx = Db(nameof(GetUnapprovedTimesheets_ReturnsOnlyUnapproved));
        ctx.Timesheets.AddRange(
            new Timesheet { UserId = "u1", EndDate = DateOnly.FromDateTime(DateTime.Today), ApproverHash = null },
            new Timesheet { UserId = "u1", EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-7)), ApproverHash = new byte[] { 1 } }
        );
        ctx.SaveChanges();

        var svc = new TimesheetService(ctx, MockUserManager().Object, new SignatureService(), new NotificationService(ctx));
        var result = svc.GetUnapprovedTimesheets("u1");
        Assert.Single(result);
        Assert.Null(result[0].ApproverHash);
    }

    [Fact]
    public void GetApprovedTimesheets_ReturnsOnlyApproved()
    {
        using var ctx = Db(nameof(GetApprovedTimesheets_ReturnsOnlyApproved));
        ctx.Timesheets.AddRange(
            new Timesheet { UserId = "u1", EndDate = DateOnly.FromDateTime(DateTime.Today), ApproverHash = null },
            new Timesheet { UserId = "u1", EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-7)), ApproverHash = new byte[] { 1 } }
        );
        ctx.SaveChanges();

        var svc = new TimesheetService(ctx, MockUserManager().Object, new SignatureService(), new NotificationService(ctx));
        var result = svc.GetApprovedTimesheets("u1");
        Assert.Single(result);
        Assert.NotNull(result[0].ApproverHash);
    }

    [Fact]
    public void CreateOrUpdateTimesheetWithRows_CreatesNewTimesheetOnFirstCall()
    {
        using var ctx = Db(nameof(CreateOrUpdateTimesheetWithRows_CreatesNewTimesheetOnFirstCall));
        ctx.Users.Add(new ApplicationUser { Id = "u1", UserName = "u1@test.com", FirstName = "Test", LastName = "User", JobTitle = "Dev", LabourGradeCode = "P1" });
        ctx.SaveChanges();

        var svc = new TimesheetService(ctx, MockUserManager().Object, new SignatureService(), new NotificationService(ctx));
        var result = svc.CreateOrUpdateTimesheetWithRows(DateTime.Today, "u1");

        Assert.NotNull(result);
        Assert.Equal("u1", result!.UserId);
        Assert.Equal(1, ctx.Timesheets.Count());
    }

    [Fact]
    public void CreateOrUpdateTimesheetWithRows_ReturnsExistingIfAlreadySigned()
    {
        using var ctx = Db(nameof(CreateOrUpdateTimesheetWithRows_ReturnsExistingIfAlreadySigned));
        int offset = (7 - (int)DateTime.Today.DayOfWeek + (int)DayOfWeek.Friday) % 7;
        var friday = DateOnly.FromDateTime(DateTime.Today.AddDays(offset));
        var existing = new Timesheet { UserId = "u1", EndDate = friday, EmployeeHash = new byte[] { 1 } };
        ctx.Timesheets.Add(existing);
        ctx.Users.Add(new ApplicationUser { Id = "u1", UserName = "u1@test.com", FirstName = "Test", LastName = "User", JobTitle = "Dev", LabourGradeCode = "P1" });
        ctx.SaveChanges();

        var svc = new TimesheetService(ctx, MockUserManager().Object, new SignatureService(), new NotificationService(ctx));
        var result = svc.CreateOrUpdateTimesheetWithRows(DateTime.Today, "u1");

        // already signed — returned the existing sheet (not a new one)
        Assert.Equal(existing.TimesheetId, result!.TimesheetId);
        Assert.Equal(1, ctx.Timesheets.Count());
    }

    [Fact]
    public void AddCustomRow_ReturnsNull_WhenTimesheetNotFound()
    {
        using var ctx = Db(nameof(AddCustomRow_ReturnsNull_WhenTimesheetNotFound));
        var svc = new TimesheetService(ctx, MockUserManager().Object, new SignatureService(), new NotificationService(ctx));
        var row = svc.AddCustomRow(999, "u1", "SICK", "P1");
        Assert.Null(row);
    }

    [Fact]
    public void AddCustomRow_ReturnsNull_WhenWrongUser()
    {
        using var ctx = Db(nameof(AddCustomRow_ReturnsNull_WhenWrongUser));
        ctx.Timesheets.Add(new Timesheet { TimesheetId = 1, UserId = "u1", EndDate = DateOnly.FromDateTime(DateTime.Today) });
        ctx.SaveChanges();

        var svc = new TimesheetService(ctx, MockUserManager().Object, new SignatureService(), new NotificationService(ctx));
        var row = svc.AddCustomRow(1, "u2", "SICK", "P1"); // wrong user
        Assert.Null(row);
    }

    [Fact]
    public void AddCustomRow_AddsRow_WhenValid()
    {
        using var ctx = Db(nameof(AddCustomRow_AddsRow_WhenValid));
        // need Project 8 (010 octal) and WorkPackage SICK to exist for FK
        ctx.Projects.Add(new Project { ProjectId = 8, ProjectTitle = "Extras", ProjectManagerId = "admin" });
        ctx.WorkPackages.Add(new WorkPackage { WorkPackageId = "SICK", ProjectId = 8, Title = "Sick Time" });
        ctx.Timesheets.Add(new Timesheet { TimesheetId = 1, UserId = "u1", EndDate = DateOnly.FromDateTime(DateTime.Today) });
        ctx.SaveChanges();

        var svc = new TimesheetService(ctx, MockUserManager().Object, new SignatureService(), new NotificationService(ctx));
        var row = svc.AddCustomRow(1, "u1", "SICK", "P1");
        Assert.NotNull(row);
        Assert.Equal("SICK", row!.WorkPackageId);
        Assert.Equal("P1", row.OriginalLabourCode);
    }

    [Fact]
    public void UpdateRow_ReturnsBadRequest_WhenTimesheetAlreadySigned()
    {
        using var ctx = Db(nameof(UpdateRow_ReturnsBadRequest_WhenTimesheetAlreadySigned));
        var ts = new Timesheet { TimesheetId = 1, UserId = "u1", EndDate = DateOnly.FromDateTime(DateTime.Today), EmployeeHash = new byte[] { 1 } };
        var row = new TimesheetRow { TimesheetRowId = 1, TimesheetId = 1, WorkPackageId = "WP1", WorkPackageProjectId = 1, Timesheet = ts };
        ctx.Timesheets.Add(ts);
        ctx.Projects.Add(new Project { ProjectId = 1, ProjectTitle = "P", ProjectManagerId = "m" });
        ctx.WorkPackages.Add(new WorkPackage { WorkPackageId = "WP1", ProjectId = 1, Title = "T" });
        ctx.TimesheetRows.Add(row);
        ctx.SaveChanges();

        var svc = new TimesheetService(ctx, MockUserManager().Object, new SignatureService(), new NotificationService(ctx));
        var (errors, result) = svc.UpdateRow(new TimesheetRow { TimesheetRowId = 1, TimesheetId = 1 });
        Assert.Null(errors);
        Assert.Null(result); // bad request sentinel
    }

    [Fact]
    public void GetTimesheetRowDtos_ReturnsExpectedShape()
    {
        using var ctx = Db(nameof(GetTimesheetRowDtos_ReturnsExpectedShape));
        ctx.Projects.Add(new Project { ProjectId = 1, ProjectTitle = "P", ProjectManagerId = "m" });
        ctx.WorkPackages.Add(new WorkPackage { WorkPackageId = "WP1", ProjectId = 1, Title = "T" });
        ctx.Timesheets.Add(new Timesheet { TimesheetId = 1, UserId = "u1", EndDate = DateOnly.FromDateTime(DateTime.Today) });
        ctx.TimesheetRows.Add(new TimesheetRow { TimesheetId = 1, WorkPackageId = "WP1", WorkPackageProjectId = 1 });
        ctx.SaveChanges();

        var svc = new TimesheetService(ctx, MockUserManager().Object, new SignatureService(), new NotificationService(ctx));
        var dtos = svc.GetTimesheetRowDtos(1);
        Assert.Single(dtos);
        Assert.NotNull(dtos[0].WorkPackage);
        Assert.Equal("WP1", dtos[0].WorkPackage!.WorkPackageId);
    }

    [Fact]
    public async Task SubmitTimesheetAsync_NotifiesApprover_WhenApproverSet()
    {
        using var ctx = Db(nameof(SubmitTimesheetAsync_NotifiesApprover_WhenApproverSet));
        var (_, encPriv) = GenKeys();
        var user = new ApplicationUser
        {
            Id = "u1", UserName = "u1@t.com", FirstName = "Emp", LastName = "Loyee",
            JobTitle = "Dev", LabourGradeCode = "P1", PrivateKey = encPriv, TimesheetApproverId = "approver1"
        };
        ctx.Users.Add(user);
        ctx.Timesheets.Add(new Timesheet { TimesheetId = 1, UserId = "u1", EndDate = DateOnly.FromDateTime(DateTime.Today), TotalHours = 40 });
        ctx.SaveChanges();

        var userManagerMock = MockUserManager();
        userManagerMock.Setup(um => um.FindByIdAsync("u1")).ReturnsAsync(user);

        var svc = new TimesheetService(ctx, userManagerMock.Object, new SignatureService(), new NotificationService(ctx));
        var (success, error, hash) = await svc.SubmitTimesheetAsync(1, "u1", "pw", null, null);

        Assert.True(success);
        var notification = ctx.Notifications.Single(n => n.UserId == "approver1");
        Assert.Equal("timesheet-1 submitted", notification.For);
        Assert.Equal("/timesheets/approve/1", notification.TargetUrl);
    }
}

// ─── NotificationService (additional) ────────────────────────────────────────

// (already tested above)

// ─── WorkPackageService ──────────────────────────────────────────────────────

public class WorkPackageServiceTests
{
    private static ApplicationDbContext Db(string name) => DbFactory.Create(name);

    private static Mock<UserManager<ApplicationUser>> MockUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(store.Object, null, null, null, null, null, null, null, null);
    }

    private static WorkPackageService MakeService(ApplicationDbContext ctx) =>
        new WorkPackageService(
            ctx,
            new NotificationService(ctx),
            new ProjectService(ctx, MockUserManager().Object, new SignatureService(), new NotificationService(ctx)),
            new SignatureService());

    [Fact]
    public async Task GetResponsibleWorkPackagesAsync_ReturnsOnlyREWPs()
    {
        using var ctx = Db(nameof(GetResponsibleWorkPackagesAsync_ReturnsOnlyREWPs));
        ctx.Projects.Add(new Project { ProjectId = 1, ProjectTitle = "P", ProjectManagerId = "m" });
        ctx.WorkPackages.AddRange(
            new WorkPackage { WorkPackageId = "A", ProjectId = 1, Title = "T1", ResponsibleUserId = "u1", IsBottomLevel = true },
            new WorkPackage { WorkPackageId = "B", ProjectId = 1, Title = "T2", ResponsibleUserId = "u2", IsBottomLevel = true },
            new WorkPackage { WorkPackageId = "C", ProjectId = 1, Title = "T3", ResponsibleUserId = "u1", IsBottomLevel = false }
        );
        ctx.SaveChanges();

        var svc = MakeService(ctx);
        var result = await svc.GetResponsibleWorkPackagesAsync("u1");
        Assert.Single(result); // only bottom-level WP A
        Assert.Equal("A", result[0].WorkPackageId);
    }

    [Fact]
    public void CloseWorkPackage_SetsIsClosedTrue()
    {
        using var ctx = Db(nameof(CloseWorkPackage_SetsIsClosedTrue));
        ctx.Projects.Add(new Project { ProjectId = 1, ProjectTitle = "P", ProjectManagerId = "m" });
        ctx.WorkPackages.Add(new WorkPackage { WorkPackageId = "WP1", ProjectId = 1, Title = "T", IsClosed = false });
        ctx.SaveChanges();

        var svc = MakeService(ctx);
        svc.CloseWorkPackage("WP1", 1);

        var wp = ctx.WorkPackages.Find("WP1", 1);
        Assert.True(wp!.IsClosed);
    }

    [Fact]
    public void CloseWorkPackage_NotifiesAssignedEmployees()
    {
        using var ctx = Db(nameof(CloseWorkPackage_NotifiesAssignedEmployees));
        ctx.Projects.Add(new Project { ProjectId = 1, ProjectTitle = "P", ProjectManagerId = "m" });
        ctx.WorkPackages.Add(new WorkPackage { WorkPackageId = "WP1", ProjectId = 1, Title = "T", IsClosed = false });
        ctx.EmployeeWorkPackages.Add(new EmployeeWorkPackage { UserId = "u1", WorkPackageId = "WP1", WorkPackageProjectId = 1 });
        ctx.SaveChanges();

        var svc = MakeService(ctx);
        svc.CloseWorkPackage("WP1", 1);

        var notification = ctx.Notifications.Single(n => n.UserId == "u1");
        Assert.Equal("1~WP1 closed", notification.For);
    }

    [Fact]
    public void CloseProject_ClosesProjectAndAllWPs()
    {
        using var ctx = Db(nameof(CloseProject_ClosesProjectAndAllWPs));
        ctx.Projects.Add(new Project { ProjectId = 2, ProjectTitle = "P2", ProjectManagerId = "m" });
        ctx.WorkPackages.AddRange(
            new WorkPackage { WorkPackageId = "WP1", ProjectId = 2, Title = "T1", IsClosed = false },
            new WorkPackage { WorkPackageId = "WP2", ProjectId = 2, Title = "T2", IsClosed = false }
        );
        ctx.SaveChanges();

        var svc = MakeService(ctx);
        svc.CloseProject(2);

        var proj = ctx.Projects.Find(2);
        Assert.True(proj!.IsClosed);
        Assert.All(ctx.WorkPackages.Where(w => w.ProjectId == 2).ToList(), w => Assert.True(w.IsClosed));
    }

    [Fact]
    public void CloseProject_NotifiesEachAffectedEmployeeOnce_NotPerWP()
    {
        using var ctx = Db(nameof(CloseProject_NotifiesEachAffectedEmployeeOnce_NotPerWP));
        ctx.Projects.Add(new Project { ProjectId = 3, ProjectTitle = "P3", ProjectManagerId = "m" });
        ctx.WorkPackages.AddRange(
            new WorkPackage { WorkPackageId = "WP1", ProjectId = 3, Title = "T1", IsClosed = false },
            new WorkPackage { WorkPackageId = "WP2", ProjectId = 3, Title = "T2", IsClosed = false }
        );
        // u1 assigned to both WPs — must only be notified once for the whole-project close.
        ctx.EmployeeWorkPackages.AddRange(
            new EmployeeWorkPackage { UserId = "u1", WorkPackageId = "WP1", WorkPackageProjectId = 3 },
            new EmployeeWorkPackage { UserId = "u1", WorkPackageId = "WP2", WorkPackageProjectId = 3 }
        );
        ctx.SaveChanges();

        var svc = MakeService(ctx);
        svc.CloseProject(3);

        Assert.Single(ctx.Notifications.Where(n => n.UserId == "u1"));
    }

    [Fact]
    public void GetProjectBudgets_ReturnsAllBudgetsForProject()
    {
        using var ctx = Db(nameof(GetProjectBudgets_ReturnsAllBudgetsForProject));
        ctx.Budgets.AddRange(
            new Budget { WPProjectId = "1~WP1", LabourCode = "P1" },
            new Budget { WPProjectId = "1~WP2", LabourCode = "P2" },
            new Budget { WPProjectId = "2~WP1", LabourCode = "P1" }
        );
        ctx.SaveChanges();

        var svc = MakeService(ctx);
        var result = svc.GetProjectBudgets(1);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void GetCurrentYearLabourGrades_ReturnsCurrentYearOnly()
    {
        using var ctx = Db(nameof(GetCurrentYearLabourGrades_ReturnsCurrentYearOnly));
        ctx.LabourGrades.AddRange(
            new LabourGrade { LabourCode = "P1", Rate = 100, Year = DateTime.Now.Year },
            new LabourGrade { LabourCode = "P2", Rate = 200, Year = DateTime.Now.Year },
            new LabourGrade { LabourCode = "P1", Rate = 90, Year = 2020 }
        );
        ctx.SaveChanges();

        var svc = MakeService(ctx);
        var result = svc.GetCurrentYearLabourGrades();
        Assert.Equal(2, result.Count);
        Assert.All(result, lg => Assert.Equal(DateTime.Now.Year, lg.Year));
    }

    [Fact]
    public void IsUserResponsibleForWorkPackage_TrueForExactMatch()
    {
        using var ctx = Db(nameof(IsUserResponsibleForWorkPackage_TrueForExactMatch));
        ctx.Projects.Add(new Project { ProjectId = 1, ProjectTitle = "P", ProjectManagerId = "m" });
        ctx.WorkPackages.Add(new WorkPackage { WorkPackageId = "BA", ProjectId = 1, Title = "T", ResponsibleUserId = "re1" });
        ctx.SaveChanges();

        var svc = MakeService(ctx);
        Assert.True(svc.IsUserResponsibleForWorkPackage("BA", 1, "re1"));
    }

    [Fact]
    public void IsUserResponsibleForWorkPackage_TrueForDescendantOfMidLevelAssignment()
    {
        using var ctx = Db(nameof(IsUserResponsibleForWorkPackage_TrueForDescendantOfMidLevelAssignment));
        ctx.Projects.Add(new Project { ProjectId = 1, ProjectTitle = "P", ProjectManagerId = "m" });
        ctx.WorkPackages.AddRange(
            new WorkPackage { WorkPackageId = "BA", ProjectId = 1, Title = "Frontend", ResponsibleUserId = "re1", IsBottomLevel = false },
            new WorkPackage { WorkPackageId = "child1", ProjectId = 1, Title = "modal creation", ParentWorkPackageId = "BA", IsBottomLevel = true }
        );
        ctx.SaveChanges();

        var svc = MakeService(ctx);
        Assert.True(svc.IsUserResponsibleForWorkPackage("child1", 1, "re1"));
    }

    [Fact]
    public void IsUserResponsibleForWorkPackage_FalseForUnrelatedUser()
    {
        using var ctx = Db(nameof(IsUserResponsibleForWorkPackage_FalseForUnrelatedUser));
        ctx.Projects.Add(new Project { ProjectId = 1, ProjectTitle = "P", ProjectManagerId = "m" });
        ctx.WorkPackages.AddRange(
            new WorkPackage { WorkPackageId = "BA", ProjectId = 1, Title = "Frontend", ResponsibleUserId = "re1", IsBottomLevel = false },
            new WorkPackage { WorkPackageId = "child1", ProjectId = 1, Title = "modal creation", ParentWorkPackageId = "BA", IsBottomLevel = true }
        );
        ctx.SaveChanges();

        var svc = MakeService(ctx);
        Assert.False(svc.IsUserResponsibleForWorkPackage("child1", 1, "someone-else"));
    }
}

// ─── ProjectService ──────────────────────────────────────────────────────────

public class ProjectServiceTests
{
    private static ApplicationDbContext Db(string name) => DbFactory.Create(name);

    private static Mock<UserManager<ApplicationUser>> MockUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(store.Object, null, null, null, null, null, null, null, null);
    }

    [Fact]
    public void GetProjectsForUser_HrOrAdmin_ReturnsAllProjects()
    {
        using var ctx = Db(nameof(GetProjectsForUser_HrOrAdmin_ReturnsAllProjects));
        ctx.Users.Add(new ApplicationUser { Id = "pm1", UserName = "pm@test.com", FirstName = "PM", LastName = "One", JobTitle = "PM", LabourGradeCode = "P5" });
        ctx.Projects.AddRange(
            new Project { ProjectId = 1, ProjectTitle = "A", ProjectManagerId = "pm1" },
            new Project { ProjectId = 2, ProjectTitle = "B", ProjectManagerId = "pm1" },
            new Project { ProjectId = 10, ProjectTitle = "Extras", ProjectManagerId = "pm1" } // excluded
        );
        ctx.SaveChanges();

        var svc = new ProjectService(ctx, MockUserManager().Object, new SignatureService(), new NotificationService(ctx));
        var result = svc.GetProjectsForUser("anyone", isHrOrAdmin: true).ToList();
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, p => p.ProjectId == 10);
    }

    [Fact]
    public void GetProjectsForUser_NonAdmin_ReturnsOnlyManagedProjects()
    {
        using var ctx = Db(nameof(GetProjectsForUser_NonAdmin_ReturnsOnlyManagedProjects));
        ctx.Users.Add(new ApplicationUser { Id = "pm1", UserName = "pm@test.com", FirstName = "PM", LastName = "One", JobTitle = "PM", LabourGradeCode = "P5" });
        ctx.Projects.AddRange(
            new Project { ProjectId = 1, ProjectTitle = "A", ProjectManagerId = "pm1" },
            new Project { ProjectId = 2, ProjectTitle = "B", ProjectManagerId = "other" }
        );
        ctx.SaveChanges();

        var svc = new ProjectService(ctx, MockUserManager().Object, new SignatureService(), new NotificationService(ctx));
        var result = svc.GetProjectsForUser("pm1", isHrOrAdmin: false).ToList();
        Assert.Single(result);
        Assert.Equal(1, result[0].ProjectId);
    }

    [Fact]
    public void ValidateNewProject_FailsForDuplicateId()
    {
        using var ctx = Db(nameof(ValidateNewProject_FailsForDuplicateId));
        ctx.Projects.Add(new Project { ProjectId = 5, ProjectTitle = "X", ProjectManagerId = "m" });
        ctx.SaveChanges();

        var svc = new ProjectService(ctx, MockUserManager().Object, new SignatureService(), new NotificationService(ctx));
        var input = new CreateProjectViewModel { project = new Project { ProjectId = 5, ProjectTitle = "Y", ProjectManagerId = "m" } };
        var (valid, error) = svc.ValidateNewProject(input);
        Assert.False(valid);
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateNewProject_SucceedsForNewId()
    {
        using var ctx = Db(nameof(ValidateNewProject_SucceedsForNewId));
        var svc = new ProjectService(ctx, MockUserManager().Object, new SignatureService(), new NotificationService(ctx));
        var input = new CreateProjectViewModel { project = new Project { ProjectId = 99, ProjectTitle = "New", ProjectManagerId = "m" } };
        var (valid, error) = svc.ValidateNewProject(input);
        Assert.True(valid);
        Assert.Null(error);
    }

    [Fact]
    public async Task CreateProject_AddsProjectAndTopLevelWP()
    {
        using var ctx = Db(nameof(CreateProject_AddsProjectAndTopLevelWP));
        ctx.Users.Add(new ApplicationUser { Id = "pm1", UserName = "pm@test.com", FirstName = "PM", LastName = "One", JobTitle = "PM", LabourGradeCode = "P5" });
        ctx.LabourGrades.Add(new LabourGrade { LabourCode = "P1", Rate = 100, Year = DateTime.Now.Year });
        ctx.SaveChanges();

        var svc = new ProjectService(ctx, MockUserManager().Object, new SignatureService(), new NotificationService(ctx));
        var input = new CreateProjectViewModel
        {
            project = new Project { ProjectId = 42, ProjectTitle = "TestProject", ProjectManagerId = "pm1" },
            budgets = new List<Budget> { new Budget { LabourCode = "P1", Days = 10, People = 2 } }
        };
        await svc.CreateProjectAsync(input);

        Assert.Equal(1, ctx.Projects.Count());
        Assert.Equal(1, ctx.WorkPackages.Count());
        var wp = ctx.WorkPackages.Single();
        Assert.Equal("0", wp.WorkPackageId);
        Assert.Equal(42, wp.ProjectId);
        Assert.Equal(1, ctx.Notifications.Count());
    }

    [Fact]
    public async Task VerifyProjectManagerAsync_ReturnsTrueForPM()
    {
        using var ctx = Db(nameof(VerifyProjectManagerAsync_ReturnsTrueForPM));
        ctx.Users.Add(new ApplicationUser { Id = "pm1", UserName = "pm@test.com", FirstName = "PM", LastName = "One", JobTitle = "PM", LabourGradeCode = "P5" });
        ctx.Projects.Add(new Project { ProjectId = 1, ProjectTitle = "P", ProjectManagerId = "pm1" });
        ctx.SaveChanges();

        var umMock = MockUserManager();
        umMock.Setup(m => m.FindByIdAsync("pm1")).ReturnsAsync(new ApplicationUser { Id = "pm1" });
        umMock.Setup(m => m.IsInRoleAsync(It.IsAny<ApplicationUser>(), "Admin")).ReturnsAsync(false);
        umMock.Setup(m => m.IsInRoleAsync(It.IsAny<ApplicationUser>(), "HR")).ReturnsAsync(false);

        var svc = new ProjectService(ctx, umMock.Object, new SignatureService(), new NotificationService(ctx));
        var result = await svc.VerifyProjectManagerAsync(1, "pm1");
        Assert.True(result);
    }

    [Fact]
    public async Task VerifyProjectManagerAsync_ReturnsFalseForNonPM()
    {
        using var ctx = Db(nameof(VerifyProjectManagerAsync_ReturnsFalseForNonPM));
        ctx.Users.Add(new ApplicationUser { Id = "pm1", UserName = "pm@test.com", FirstName = "PM", LastName = "One", JobTitle = "PM", LabourGradeCode = "P5" });
        ctx.Projects.Add(new Project { ProjectId = 1, ProjectTitle = "P", ProjectManagerId = "pm1" });
        ctx.SaveChanges();

        var umMock = MockUserManager();
        umMock.Setup(m => m.FindByIdAsync("other")).ReturnsAsync(new ApplicationUser { Id = "other" });
        umMock.Setup(m => m.IsInRoleAsync(It.IsAny<ApplicationUser>(), "Admin")).ReturnsAsync(false);
        umMock.Setup(m => m.IsInRoleAsync(It.IsAny<ApplicationUser>(), "HR")).ReturnsAsync(false);

        var svc = new ProjectService(ctx, umMock.Object, new SignatureService(), new NotificationService(ctx));
        var result = await svc.VerifyProjectManagerAsync(1, "other");
        Assert.False(result);
    }

    [Fact]
    public void CalculateWpCostRollup_SumsPmAndReBudgetsSeparately()
    {
        using var ctx = Db(nameof(CalculateWpCostRollup_SumsPmAndReBudgetsSeparately));
        var svc = new ProjectService(ctx, MockUserManager().Object, new SignatureService(), new NotificationService(ctx));
        var labourGrades = new List<LabourGrade> { new LabourGrade { LabourCode = "P1", Rate = 100, Year = DateTime.Now.Year } };
        var budgets = new List<Budget>
        {
            new Budget { WPProjectId = "1~WP1", LabourCode = "P1", Days = 5, People = 2, isREBudget = false },
            new Budget { WPProjectId = "1~WP1", LabourCode = "P1", Days = 3, People = 1, isREBudget = true },
        };

        var rollup = svc.CalculateWpCostRollup(budgets, new List<ResponsibleEngineerEstimate>(), new List<TimesheetRow>(), labourGrades);

        Assert.Equal(10, rollup.PmPlannedPD); // 5 days * 2 people
        Assert.Equal(1000, rollup.PmPlannedCost); // 10 PD * $100
        Assert.Equal(3, rollup.REPlannedPD);
        Assert.Equal(300, rollup.REPlannedCost);
    }

    [Fact]
    public void CalculateWpCostRollup_EacIsActualPlusEstimatedRemaining()
    {
        using var ctx = Db(nameof(CalculateWpCostRollup_EacIsActualPlusEstimatedRemaining));
        var svc = new ProjectService(ctx, MockUserManager().Object, new SignatureService(), new NotificationService(ctx));
        var labourGrades = new List<LabourGrade> { new LabourGrade { LabourCode = "P1", Rate = 100, Year = DateTime.Now.Year } };
        var estimates = new List<ResponsibleEngineerEstimate> { new ResponsibleEngineerEstimate { WPProjectId = "1~WP1", LabourCode = "P1", EstimatedCost = 2 } };
        var rows = new List<TimesheetRow>
        {
            new TimesheetRow { WorkPackageId = "WP1", WorkPackageProjectId = 1, OriginalLabourCode = "P1", TotalHoursRow = 8, Timesheet = new Timesheet { EndDate = DateOnly.FromDateTime(DateTime.Today) } },
        };

        var rollup = svc.CalculateWpCostRollup(new List<Budget>(), estimates, rows, labourGrades);

        Assert.Equal(1, rollup.ActualPD); // 8 hours / 8
        Assert.Equal(100, rollup.ActualCost);
        Assert.Equal(3, rollup.EacPD); // 1 actual + 2 estimated
        Assert.Equal(300, rollup.EacCost);
    }

    [Fact]
    public void CalculateWpCostRollup_VarianceAndPercentComplete_ZeroWhenNaN()
    {
        using var ctx = Db(nameof(CalculateWpCostRollup_VarianceAndPercentComplete_ZeroWhenNaN));
        var svc = new ProjectService(ctx, MockUserManager().Object, new SignatureService(), new NotificationService(ctx));
        var labourGrades = new List<LabourGrade>();

        var rollup = svc.CalculateWpCostRollup(new List<Budget>(), new List<ResponsibleEngineerEstimate>(), new List<TimesheetRow>(), labourGrades);

        Assert.Equal(0, rollup.PdVariance);
        Assert.Equal(0, rollup.CostVariance);
        Assert.Equal(0, rollup.PercentComplete);
    }
}

// ─── EmployeeService ─────────────────────────────────────────────────────────

public class EmployeeServiceTests
{
    private static ApplicationDbContext Db(string name) => DbFactory.Create(name);

    private static Mock<UserManager<ApplicationUser>> MockUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(store.Object, null, null, null, null, null, null, null, null);
    }

    [Fact]
    public async Task GetAllEmployeesPaginatedAsync_ReturnsCorrectPage()
    {
        using var ctx = Db(nameof(GetAllEmployeesPaginatedAsync_ReturnsCorrectPage));
        for (int i = 1; i <= 5; i++)
        {
            ctx.Users.Add(new ApplicationUser { Id = $"u{i}", UserName = $"u{i}@t.com", FirstName = $"User{i}", LastName = "Test", JobTitle = "Dev", LabourGradeCode = "P1" });
        }
        ctx.SaveChanges();

        var svc = new EmployeeService(ctx, MockUserManager().Object, new NotificationService(ctx));
        var page1 = await svc.GetAllEmployeesPaginatedAsync(1, 3);
        var page2 = await svc.GetAllEmployeesPaginatedAsync(2, 3);
        Assert.Equal(3, page1.Count);
        Assert.Equal(2, page2.Count);
    }

    [Fact]
    public void GetTotalEmployeeCount_ReturnsCorrectCount()
    {
        using var ctx = Db(nameof(GetTotalEmployeeCount_ReturnsCorrectCount));
        ctx.Users.Add(new ApplicationUser { Id = "u1", UserName = "u1@t.com", FirstName = "A", LastName = "B", JobTitle = "Dev", LabourGradeCode = "P1" });
        ctx.Users.Add(new ApplicationUser { Id = "u2", UserName = "u2@t.com", FirstName = "C", LastName = "D", JobTitle = "Dev", LabourGradeCode = "P1" });
        ctx.SaveChanges();

        var svc = new EmployeeService(ctx, MockUserManager().Object, new NotificationService(ctx));
        Assert.Equal(2, svc.GetTotalEmployeeCount());
    }

    [Fact]
    public void EmployeeExists_ReturnsTrueForExisting()
    {
        using var ctx = Db(nameof(EmployeeExists_ReturnsTrueForExisting));
        ctx.Users.Add(new ApplicationUser { Id = "u1", UserName = "u1@t.com", FirstName = "A", LastName = "B", JobTitle = "Dev", LabourGradeCode = "P1" });
        ctx.SaveChanges();

        var svc = new EmployeeService(ctx, MockUserManager().Object, new NotificationService(ctx));
        Assert.True(svc.EmployeeExists("u1"));
        Assert.False(svc.EmployeeExists("nope"));
    }

    [Fact]
    public void GetTimesheetApprovers_ReturnsUsersUnderSupervisor()
    {
        using var ctx = Db(nameof(GetTimesheetApprovers_ReturnsUsersUnderSupervisor));
        ctx.Users.AddRange(
            new ApplicationUser { Id = "sup", UserName = "sup@t.com", FirstName = "Sup", LastName = "er", JobTitle = "Sup", LabourGradeCode = "P5" },
            new ApplicationUser { Id = "emp", UserName = "emp@t.com", FirstName = "Emp", LastName = "ee", JobTitle = "Dev", LabourGradeCode = "P1", SupervisorId = "sup" },
            new ApplicationUser { Id = "oth", UserName = "oth@t.com", FirstName = "Oth", LastName = "er", JobTitle = "Dev", LabourGradeCode = "P1", SupervisorId = "other" }
        );
        ctx.SaveChanges();

        var svc = new EmployeeService(ctx, MockUserManager().Object, new NotificationService(ctx));
        var result = svc.GetTimesheetApprovers("sup").ToList();
        // returns supervisor themselves + their subordinates
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void AssignTimesheetApprover_NotifiesNewApproverAndChangedSupervisedUsers()
    {
        using var ctx = Db(nameof(AssignTimesheetApprover_NotifiesNewApproverAndChangedSupervisedUsers));
        var supervisor = new ApplicationUser { Id = "sup", UserName = "sup@t.com", FirstName = "Sup", LastName = "Er", JobTitle = "Sup", LabourGradeCode = "P5" };
        ctx.Users.AddRange(
            supervisor,
            new ApplicationUser { Id = "newTsa", UserName = "tsa@t.com", FirstName = "New", LastName = "Tsa", JobTitle = "Dev", LabourGradeCode = "P1" },
            // already approved by newTsa — should NOT get a "changed" notification
            new ApplicationUser { Id = "emp1", UserName = "emp1@t.com", FirstName = "E", LastName = "1", JobTitle = "Dev", LabourGradeCode = "P1", SupervisorId = "sup", TimesheetApproverId = "newTsa" },
            // approved by someone else — SHOULD get a "changed" notification
            new ApplicationUser { Id = "emp2", UserName = "emp2@t.com", FirstName = "E", LastName = "2", JobTitle = "Dev", LabourGradeCode = "P1", SupervisorId = "sup", TimesheetApproverId = "oldTsa" }
        );
        ctx.SaveChanges();

        var svc = new EmployeeService(ctx, MockUserManager().Object, new NotificationService(ctx));
        svc.AssignTimesheetApprover(supervisor, "newTsa");

        Assert.Single(ctx.Notifications.Where(n => n.UserId == "newTsa" && n.For == "tsa-sup assign"));
        Assert.Single(ctx.Notifications.Where(n => n.UserId == "emp2" && n.For == "tsa-emp2 changed"));
        Assert.Empty(ctx.Notifications.Where(n => n.UserId == "emp1"));
    }

    [Fact]
    public void GetCurrentYearLabourGrades_ReturnsCorrectYear()
    {
        using var ctx = Db(nameof(GetCurrentYearLabourGrades_ReturnsCorrectYear));
        ctx.LabourGrades.AddRange(
            new LabourGrade { LabourCode = "P1", Rate = 100, Year = DateTime.Now.Year },
            new LabourGrade { LabourCode = "P1", Rate = 90, Year = 2010 }
        );
        ctx.SaveChanges();

        var svc = new EmployeeService(ctx, MockUserManager().Object, new NotificationService(ctx));
        var result = svc.GetCurrentYearLabourGrades();
        Assert.Single(result);
        Assert.Equal(DateTime.Now.Year, result[0].Year);
    }
}
