using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

// report generation packages
using iText.Kernel.Colors;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Draw;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;

using TimesheetApp.Data;
using TimesheetApp.Models;
using TimesheetApp.Models.TimesheetModels;

namespace TimesheetApp.Services;

public class ProjectService : IProjectService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISignatureService _signatureService;
    private readonly INotificationService _notificationService;

    public ProjectService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ISignatureService signatureService,
        INotificationService notificationService)
    {
        _context = context;
        _userManager = userManager;
        _signatureService = signatureService;
        _notificationService = notificationService;
    }

    public IEnumerable<Project> GetProjectsForUser(string userId, bool isHrOrAdmin)
    {
        if (isHrOrAdmin)
        {
            return _context.Projects!
                .Where(p => p.ProjectId != 10)
                .Include(s => s.ProjectManager)
                .OrderBy(c => c.ProjectId)
                .ToList();
        }
        return _context.Projects!
            .Where(s => (s.ProjectManager!.Id == userId || s.AssistantProjectManagerId == userId) && s.ProjectId != 10)
            .OrderBy(c => c.ProjectId)
            .Include(s => s.ProjectManager)
            .ToList();
    }

    public (bool valid, string? error) ValidateNewProject(CreateProjectViewModel input)
    {
        if (_context.Projects.Find(input.project.ProjectId) != null)
        {
            return (false, "Project ID must be unique.");
        }
        return (true, null);
    }

    public void CreateProject(CreateProjectViewModel input)
    {
        _context.Projects!.Add(input.project);
        _context.SaveChanges();

        var newWP = new WorkPackage
        {
            WorkPackageId = "0",
            ProjectId = input.project.ProjectId,
            IsBottomLevel = true,
            Title = input.project.ProjectTitle
        };
        _context.WorkPackages!.Add(newWP);

        double totalBudget = 0;
        var grades = _context.LabourGrades.Where(c => c.Year == DateTime.Now.Year);

        if (input.budgets != null)
        {
            foreach (var budget in input.budgets)
            {
                Budget newBudget = new Budget
                {
                    WPProjectId = input.project.ProjectId + "~0",
                    People = budget.People,
                    Days = budget.Days,
                    LabourCode = budget.LabourCode,
                    UnallocatedDays = budget.Days,
                    UnallocatedPeople = budget.People
                };
                totalBudget += budget.BudgetAmount * grades.Where(c => budget.LabourCode == c.LabourCode).First().Rate;
                _context.Budgets!.Add(newBudget);
            }
        }

        input.project.TotalBudget = totalBudget;
        _notificationService.AddNotification(
            input.project.ProjectManagerId!,
            "You have been added to the project " + input.project.ProjectTitle + " as a Project Manager.",
            Convert.ToString(input.project.ProjectId) + " Add",
            1);
        _context.SaveChanges();
    }

    public async Task<bool> VerifyProjectManagerAsync(int projectId, string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return false;

        if (await _userManager.IsInRoleAsync(user, "Admin") || await _userManager.IsInRoleAsync(user, "HR"))
        {
            return true;
        }

        var project = _context.Projects.FirstOrDefault(c => c.ProjectId == projectId);
        if (project == null) return false;

        return user.Id == project.ProjectManagerId || user.Id == project.AssistantProjectManagerId;
    }

    public async Task<byte[]> GenerateReportAsync(int projectId)
    {
        MemoryStream ms = new MemoryStream();
        PdfWriter writer = new PdfWriter(ms);
        PdfDocument pdfDoc = new PdfDocument(writer);
        Document document = new Document(pdfDoc, PageSize.A4.Rotate(), false);
        writer.SetCloseStream(false);

        Paragraph header = new Paragraph("Project Cost Performace Report")
            .SetTextAlignment(TextAlignment.CENTER)
            .SetFontSize(15);
        document.Add(header);

        float fontSizeSH = 11.5F;
        Paragraph subheader = new Paragraph($"Created Date: {DateTime.Now.ToShortDateString()}").SetFontSize(fontSizeSH);
        document.Add(subheader);

        Project? prj = await _context.Projects!.FindAsync(projectId);
        if (prj == null) return Array.Empty<byte>();

        ApplicationUser? mgr = await _context.Users.FindAsync(prj!.ProjectManagerId);
        if (mgr == null) return Array.Empty<byte>();

        var startOfThisMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var lastDay = DateOnly.FromDateTime(startOfThisMonth.AddDays(-1));

        LineSeparator ls = new LineSeparator(new SolidLine());

        Paragraph details = new Paragraph();
        details.Add(new Text($"Project Title: {prj.ProjectTitle}"));
        details.Add(new Tab());
        details.Add(new Tab());
        details.Add(new Text($"Manager: {mgr.FirstName} {mgr.LastName} ({mgr.EmployeeNumber})"));
        details.SetFontSize(fontSizeSH);
        document.Add(details);

        DateTime previousFriday = startOfThisMonth.AddDays(-1).AddDays(-(int)startOfThisMonth.AddDays(-1).DayOfWeek - 2);

        Paragraph dates = new Paragraph();
        dates.Add(new Text($"End Date: {previousFriday.ToShortDateString()}"));
        dates.SetFontSize(fontSizeSH);
        document.Add(dates);
        document.Add(ls);

        Table wpTable = new Table(9);

        wpTable.AddCell(new Cell().SetBackgroundColor(ColorConstants.LIGHT_GRAY).SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).Add(new Paragraph("Work Package")));
        wpTable.AddCell(new Cell().SetBackgroundColor(ColorConstants.LIGHT_GRAY).SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).Add(new Paragraph("Engineers")));
        wpTable.AddCell(new Cell().SetBackgroundColor(ColorConstants.LIGHT_GRAY).SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).Add(new Paragraph("Stats")));
        wpTable.AddCell(new Cell().SetBackgroundColor(ColorConstants.LIGHT_GRAY).SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).Add(new Paragraph("Project Budget")));
        wpTable.AddCell(new Cell().SetBackgroundColor(ColorConstants.LIGHT_GRAY).SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).Add(new Paragraph("Engineer Planned")));
        wpTable.AddCell(new Cell().SetBackgroundColor(ColorConstants.LIGHT_GRAY).SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).Add(new Paragraph("Actual to date")));
        wpTable.AddCell(new Cell().SetBackgroundColor(ColorConstants.LIGHT_GRAY).SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).Add(new Paragraph("Estimate at Completion")));
        wpTable.AddCell(new Cell().SetBackgroundColor(ColorConstants.LIGHT_GRAY).SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).Add(new Paragraph("% Variance")));
        wpTable.AddCell(new Cell().SetBackgroundColor(ColorConstants.LIGHT_GRAY).SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).Add(new Paragraph("% Complete")));
        wpTable.SetWidth(UnitValue.CreatePercentValue(100));

        var labourGrades = _context.LabourGrades.ToList();
        var budgets = _context.Budgets.Where(c => c.WPProjectId.StartsWith(prj.ProjectId + "~")).ToList();
        var estimates = _context.ResponsibleEngineerEstimates.Where(c => c.WPProjectId!.StartsWith(prj.ProjectId + "~")).ToList();

        var employees = _context.EmployeeProjects.Where(c => c.ProjectId == prj.ProjectId).Select(c => c.UserId).ToList();
        var eWps = _context.EmployeeWorkPackages.Where(c => c.WorkPackageProjectId == prj.ProjectId).Include(c => c.User).ToList();
        var timesheets = _context.Timesheets
            .Where(c => c.TimesheetApproverId != null && c.EndDate <= lastDay && employees.Contains(c.UserId))
            .Include(c => c.TimesheetApprover)
            .Include(c => c.TimesheetRows);

        var timesheetRows = new List<TimesheetRow>();
        foreach (var timesheet in timesheets)
        {
            if (_signatureService.VerifySignature(timesheet, timesheet.TimesheetApprover!.PublicKey!, timesheet.ApproverHash!))
            {
                timesheetRows.AddRange(timesheet.TimesheetRows.Where(c => c.ProjectId == prj.ProjectId).ToList());
            }
        }

        foreach (var wp in _context.WorkPackages.Where(c => c.ProjectId == prj.ProjectId).OrderBy(c => c.WorkPackageId).ToList())
        {
            wpTable.AddCell(new Cell()
                .Add(new Paragraph(wp.WorkPackageId).SetFontSize(fontSizeSH))
                .Add(new Paragraph(wp.Title).SetFontSize(fontSizeSH)));

            Cell engineers = new Cell();
            foreach (var employee in eWps.Where(c => c.WorkPackageId == wp.WorkPackageId).Select(c => c.User))
            {
                if (employee != null)
                    engineers.Add(new Paragraph(employee.FirstName + " " + employee.LastName![0]));
            }
            wpTable.AddCell(engineers);

            wpTable.AddCell(new Cell()
                .Add(new Paragraph("Total P.D.").SetFontSize(fontSizeSH).SetTextAlignment(TextAlignment.RIGHT))
                .Add(new Paragraph("Labour $").SetFontSize(fontSizeSH).SetTextAlignment(TextAlignment.RIGHT)));

            double totalPDPM = 0;
            double totalCostPM = 0;
            foreach (var budget in budgets.Where(c => c.WPProjectId == prj.ProjectId + "~" + wp.WorkPackageId && c.isREBudget == false))
            {
                totalPDPM += budget.BudgetAmount;
                totalCostPM += budget.BudgetAmount * labourGrades.Where(c => c.LabourCode == budget.LabourCode && c.Year == DateTime.Now.Year).First().Rate;
            }
            wpTable.AddCell(new Cell()
                .Add(new Paragraph(Convert.ToString(Math.Round(totalPDPM, 2))).SetFontSize(fontSizeSH))
                .Add(new Paragraph("$" + Math.Round(totalCostPM, 2)).SetFontSize(fontSizeSH)));

            double totalPDRE = 0;
            double totalCostRE = 0;
            foreach (var budget in budgets.Where(c => c.WPProjectId == prj.ProjectId + "~" + wp.WorkPackageId && c.isREBudget == true))
            {
                totalPDRE += budget.BudgetAmount;
                totalCostRE += budget.BudgetAmount * labourGrades.Where(c => c.LabourCode == budget.LabourCode && c.Year == DateTime.Now.Year).First().Rate;
            }
            wpTable.AddCell(new Cell()
                .Add(new Paragraph(Convert.ToString(Math.Round(totalPDRE, 2))).SetFontSize(fontSizeSH))
                .Add(new Paragraph("$" + Math.Round(totalCostRE, 2)).SetFontSize(fontSizeSH)));

            double totalPDActual = 0;
            double totalCostActual = 0;
            foreach (var row in timesheetRows.Where(c => c.WorkPackageId == wp.WorkPackageId))
            {
                totalPDActual = totalPDActual + (row.TotalHoursRow / 8);
                totalCostActual += (row.TotalHoursRow / 8) * labourGrades.Where(c => c.LabourCode == row.OriginalLabourCode && c.Year == row.Timesheet!.EndDate!.Value.Year).First().Rate;
            }
            wpTable.AddCell(new Cell()
                .Add(new Paragraph(Convert.ToString(Math.Round(totalPDActual, 2))).SetFontSize(fontSizeSH))
                .Add(new Paragraph("$" + Math.Round(totalCostActual, 2)).SetFontSize(fontSizeSH)));

            double pDEstimate = totalPDActual;
            double costEstimate = totalCostActual;
            foreach (var estimate in estimates.Where(c => c.WPProjectId == prj.ProjectId + "~" + wp.WorkPackageId).ToList())
            {
                pDEstimate += estimate.EstimatedCost;
                costEstimate += estimate.EstimatedCost * labourGrades.Where(c => c.LabourCode == estimate.LabourCode && c.Year == DateTime.Now.Year).First().Rate;
            }
            wpTable.AddCell(new Cell()
                .Add(new Paragraph(Convert.ToString(Math.Round(pDEstimate, 2))).SetFontSize(fontSizeSH))
                .Add(new Paragraph("$" + Math.Round(costEstimate, 2)).SetFontSize(fontSizeSH)));

            double pdVariance = (pDEstimate - totalPDPM) / Math.Max(totalPDPM, pDEstimate) * 100;
            double costVariance = (costEstimate - totalCostPM) / Math.Max(totalCostPM, costEstimate) * 100;
            pdVariance = double.IsNaN(pdVariance) ? 0 : pdVariance;
            costVariance = double.IsNaN(costVariance) ? 0 : costVariance;

            wpTable.AddCell(new Cell()
                .Add(new Paragraph(Convert.ToString(Math.Round(pdVariance))).SetFontSize(fontSizeSH))
                .Add(new Paragraph(Convert.ToString(Math.Round(costVariance))).SetFontSize(fontSizeSH)));

            double percentComplete = totalCostActual / costEstimate * 100;
            wpTable.AddCell(new Cell()
                .Add(new Paragraph(Convert.ToString(double.IsNaN(percentComplete) ? 0 : Math.Round(percentComplete))).SetFontSize(fontSizeSH)));
        }

        document.Add(wpTable);

        int n = pdfDoc.GetNumberOfPages();
        for (int i = 1; i <= n; i++)
        {
            document.ShowTextAligned(new Paragraph(String.Format("Page " + i + " of " + n)), 559, 806, i, TextAlignment.RIGHT, VerticalAlignment.TOP, 0);
        }

        document.Close();
        byte[] byteInfo = ms.ToArray();
        ms.Write(byteInfo, 0, byteInfo.Length);
        ms.Position = 0;
        return byteInfo;
    }

    public async Task<byte[]> GenerateWeekReportAsync(int projectId)
    {
        MemoryStream ms = new MemoryStream();
        PdfWriter writer = new PdfWriter(ms);
        PdfDocument pdfDoc = new PdfDocument(writer);
        Document document = new Document(pdfDoc, PageSize.A4.Rotate(), false);
        writer.SetCloseStream(false);

        Paragraph header = new Paragraph("Week Details Report")
            .SetTextAlignment(TextAlignment.CENTER)
            .SetFontSize(15);
        document.Add(header);

        float fontSizeSH = 11.5F;
        Paragraph subheader = new Paragraph($"Created Date: {DateTime.Now.ToShortDateString()}").SetFontSize(fontSizeSH);
        document.Add(subheader);

        Project? prj = await _context.Projects!.FindAsync(projectId);
        if (prj == null) return Array.Empty<byte>();

        ApplicationUser? mgr = await _context.Users.FindAsync(prj!.ProjectManagerId);
        if (mgr == null) return Array.Empty<byte>();

        DateTime today = DateTime.Today;
        int daysUntilFriday = ((int)DayOfWeek.Friday - (int)today.DayOfWeek + 7) % 7;
        DateTime lastDay = today.AddDays(daysUntilFriday - 7);
        DateTime firstDay = lastDay.AddDays(-6);

        LineSeparator ls = new LineSeparator(new SolidLine());

        Paragraph details = new Paragraph();
        details.Add(new Text($"Project Title: {prj.ProjectTitle}"));
        details.Add(new Tab());
        details.Add(new Tab());
        details.Add(new Text($"Manager: {mgr.FirstName} {mgr.LastName} ({mgr.EmployeeNumber})"));
        details.SetFontSize(fontSizeSH);
        document.Add(details);

        Paragraph dates = new Paragraph();
        dates.Add(new Text($"Start Date: {firstDay.ToShortDateString()}"));
        dates.Add(new Tab());
        dates.Add(new Tab());
        dates.Add(new Text($"End Date: {lastDay.ToShortDateString()}"));
        dates.SetFontSize(fontSizeSH);
        document.Add(dates);
        document.Add(ls);

        Table wpTable = new Table(18);

        wpTable.AddCell(new Cell().SetBackgroundColor(ColorConstants.LIGHT_GRAY).SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).Add(new Paragraph("Work Package")));
        wpTable.AddCell(new Cell().SetBackgroundColor(ColorConstants.LIGHT_GRAY).SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).Add(new Paragraph("Employees")));
        wpTable.AddCell(new Cell(1, 2).SetBackgroundColor(ColorConstants.LIGHT_GRAY).SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).Add(new Paragraph("Sat")));
        wpTable.AddCell(new Cell(1, 2).SetBackgroundColor(ColorConstants.LIGHT_GRAY).SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).Add(new Paragraph("Sun")));
        wpTable.AddCell(new Cell(1, 2).SetBackgroundColor(ColorConstants.LIGHT_GRAY).SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).Add(new Paragraph("Mon")));
        wpTable.AddCell(new Cell(1, 2).SetBackgroundColor(ColorConstants.LIGHT_GRAY).SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).Add(new Paragraph("Tue")));
        wpTable.AddCell(new Cell(1, 2).SetBackgroundColor(ColorConstants.LIGHT_GRAY).SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).Add(new Paragraph("Wed")));
        wpTable.AddCell(new Cell(1, 2).SetBackgroundColor(ColorConstants.LIGHT_GRAY).SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).Add(new Paragraph("Thu")));
        wpTable.AddCell(new Cell(1, 2).SetBackgroundColor(ColorConstants.LIGHT_GRAY).SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).Add(new Paragraph("Fri")));
        wpTable.AddCell(new Cell(1, 2).SetBackgroundColor(ColorConstants.LIGHT_GRAY).SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).Add(new Paragraph("Total")));
        wpTable.AddCell(new Cell(1, 2).SetBackgroundColor(ColorConstants.LIGHT_GRAY).SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).Add(new Paragraph("")));

        for (int i = 0; i < 8; i++)
        {
            wpTable.AddCell(new Cell().SetBackgroundColor(ColorConstants.LIGHT_GRAY).SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).Add(new Paragraph("Hour")));
            wpTable.AddCell(new Cell().SetBackgroundColor(ColorConstants.LIGHT_GRAY).SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).Add(new Paragraph("$")));
        }

        wpTable.SetWidth(UnitValue.CreatePercentValue(100));

        var labourGrades = _context.LabourGrades.ToList();
        var employees = _context.EmployeeProjects.Where(c => c.ProjectId == prj.ProjectId).Select(c => c.UserId).ToList();
        var eWps = _context.EmployeeWorkPackages.Where(c => c.WorkPackageProjectId == prj.ProjectId).Include(c => c.User).ToList();
        var timesheets = _context.Timesheets
            .Where(c => c.TimesheetApproverId != null && c.EndDate == DateOnly.FromDateTime(lastDay) && employees.Contains(c.UserId))
            .Include(c => c.TimesheetApprover)
            .Include(c => c.TimesheetRows);

        var timesheetRows = new List<TimesheetRow>();
        foreach (var timesheet in timesheets)
        {
            if (_signatureService.VerifySignature(timesheet, timesheet.TimesheetApprover!.PublicKey!, timesheet.ApproverHash!))
            {
                timesheetRows.AddRange(timesheet.TimesheetRows.Where(c => c.ProjectId == prj.ProjectId).ToList());
            }
        }

        double[] dayTotals = { 0, 0, 0, 0, 0, 0, 0 };
        double[] dayTotalsMoney = { 0, 0, 0, 0, 0, 0, 0 };
        double grandTotal = 0;
        double grandTotalMoney = 0;

        foreach (var wp in _context.WorkPackages.Where(c => c.ProjectId == prj.ProjectId).Include(c => c.EmployeeWorkPackages!).ThenInclude(c => c.User).OrderBy(c => c.WorkPackageId).ToList())
        {
            if (wp.EmployeeWorkPackages != null && wp.EmployeeWorkPackages.Count() > 0)
            {
                wpTable.AddCell(new Cell(wp.EmployeeWorkPackages.Count(), 1)
                    .Add(new Paragraph(wp.WorkPackageId).SetFontSize(fontSizeSH))
                    .Add(new Paragraph(wp.Title).SetFontSize(fontSizeSH)));

                foreach (var ewp in wp.EmployeeWorkPackages)
                {
                    var user = ewp.User;
                    wpTable.AddCell(new Cell().Add(new Paragraph(user!.FirstName + " " + user.LastName).SetFontSize(fontSizeSH)));

                    double totalMoney = 0;
                    double totalHour = 0;
                    var row = timesheetRows.Where(c => c.WorkPackageId == ewp.WorkPackageId && c.Timesheet!.UserId == user.Id).FirstOrDefault();
                    for (int i = 0; i < 7; i++)
                    {
                        double hour = 0;
                        double money = 0;
                        if (row != null)
                        {
                            hour = row.getHour(i);
                            totalHour += hour;
                            grandTotal += hour;
                            dayTotals[i] += hour;
                            money = hour * labourGrades.Where(c => c.Year == DateTime.Now.Year && c.LabourCode == row.OriginalLabourCode).First().Rate / 8;
                            totalMoney += money;
                            dayTotalsMoney[i] += money;
                            grandTotalMoney += money;
                        }
                        wpTable.AddCell(new Cell().Add(new Paragraph(Convert.ToString(Math.Round(hour, 2))).SetFontSize(fontSizeSH)));
                        wpTable.AddCell(new Cell().Add(new Paragraph(Convert.ToString(Math.Round(money, 2))).SetFontSize(fontSizeSH)));
                    }
                    wpTable.AddCell(new Cell().Add(new Paragraph(Convert.ToString(Math.Round(totalHour, 2))).SetFontSize(fontSizeSH)));
                    wpTable.AddCell(new Cell().Add(new Paragraph(Convert.ToString(Math.Round(totalMoney, 2))).SetFontSize(fontSizeSH)));
                }
            }
        }

        wpTable.AddCell(new Cell(1, 2).SetBackgroundColor(ColorConstants.LIGHT_GRAY).SetTextAlignment(TextAlignment.RIGHT).SetFontSize(fontSizeSH).Add(new Paragraph("Total")));
        for (int i = 0; i < 7; i++)
        {
            wpTable.AddCell(new Cell().Add(new Paragraph(Convert.ToString(Math.Round(dayTotals[i], 2))).SetFontSize(fontSizeSH)).SetBackgroundColor(ColorConstants.LIGHT_GRAY));
            wpTable.AddCell(new Cell().Add(new Paragraph(Convert.ToString(Math.Round(dayTotalsMoney[i], 2))).SetFontSize(fontSizeSH)).SetBackgroundColor(ColorConstants.LIGHT_GRAY));
        }
        wpTable.AddCell(new Cell().Add(new Paragraph(Convert.ToString(Math.Round(grandTotal, 2))).SetFontSize(fontSizeSH)).SetBackgroundColor(ColorConstants.LIGHT_GRAY));
        wpTable.AddCell(new Cell().Add(new Paragraph(Convert.ToString(Math.Round(grandTotalMoney, 2))).SetFontSize(fontSizeSH)).SetBackgroundColor(ColorConstants.LIGHT_GRAY));
        document.Add(wpTable);

        int n = pdfDoc.GetNumberOfPages();
        for (int i = 1; i <= n; i++)
        {
            document.ShowTextAligned(new Paragraph(String.Format("Page " + i + " of " + n)), 559, 806, i, TextAlignment.RIGHT, VerticalAlignment.TOP, 0);
        }

        document.Close();
        byte[] byteInfo = ms.ToArray();
        ms.Write(byteInfo, 0, byteInfo.Length);
        ms.Position = 0;
        return byteInfo;
    }

    public async Task<byte[]> GeneratePCBACAsync(int projectId)
    {
        MemoryStream ms = new MemoryStream();
        PdfWriter writer = new PdfWriter(ms);
        PdfDocument pdfDoc = new PdfDocument(writer);
        Document document = new Document(pdfDoc, PageSize.A4.Rotate(), false);
        writer.SetCloseStream(false);

        Paragraph header = new Paragraph("Project Costing/Budget/Actual Comparison (PCBAC)")
            .SetTextAlignment(TextAlignment.CENTER)
            .SetFontSize(15);
        document.Add(header);

        float fontSizeSH = 11.5F;
        Paragraph subheader = new Paragraph($"Created Date: {DateTime.Now.ToShortDateString()}").SetFontSize(fontSizeSH);
        document.Add(subheader);

        Project? prj = await _context.Projects!.FindAsync(projectId);
        if (prj == null) return Array.Empty<byte>();

        ApplicationUser? mgr = await _context.Users.FindAsync(prj!.ProjectManagerId);
        if (mgr == null) return Array.Empty<byte>();

        LineSeparator ls = new LineSeparator(new SolidLine());

        Paragraph details = new Paragraph();
        details.Add(new Text($"Project Title: {prj.ProjectTitle}"));
        details.Add(new Tab());
        details.Add(new Tab());
        details.Add(new Text($"Manager: {mgr.FirstName} {mgr.LastName} ({mgr.EmployeeNumber})"));
        details.SetFontSize(fontSizeSH);
        document.Add(details);
        document.Add(ls);

        Table wpTable = new Table(8);

        wpTable.AddCell(new Cell(2, 2).SetBackgroundColor(ColorConstants.LIGHT_GRAY).SetVerticalAlignment(VerticalAlignment.MIDDLE).SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).Add(new Paragraph("Labour")));
        wpTable.AddCell(new Cell(1, 2).SetBackgroundColor(ColorConstants.LIGHT_GRAY).SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).Add(new Paragraph("Project Manager's Budget")));
        wpTable.AddCell(new Cell(1, 2).SetBackgroundColor(ColorConstants.LIGHT_GRAY).SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).Add(new Paragraph("Responsible Engineer's Budget")));
        wpTable.AddCell(new Cell(1, 2).SetBackgroundColor(ColorConstants.LIGHT_GRAY).SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).Add(new Paragraph("Actual Cost To Date")));

        for (int i = 0; i < 3; i++)
        {
            wpTable.AddCell(new Cell().SetBackgroundColor(ColorConstants.LIGHT_GRAY).SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).Add(new Paragraph("PD")));
            wpTable.AddCell(new Cell().SetBackgroundColor(ColorConstants.LIGHT_GRAY).SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).Add(new Paragraph("$")));
        }

        wpTable.SetWidth(UnitValue.CreatePercentValue(100));

        var labourGrades = _context.LabourGrades.Where(c => c.Year == DateTime.Now.Year).ToList();
        var budgets = _context.Budgets.Where(c => c.WPProjectId.StartsWith(prj.ProjectId + "~")).ToList();
        var estimates = _context.ResponsibleEngineerEstimates.Where(c => c.WPProjectId!.StartsWith(prj.ProjectId + "~")).ToList();

        var employees = _context.EmployeeProjects.Where(c => c.ProjectId == prj.ProjectId).Select(c => c.UserId).ToList();
        var timesheets = _context.Timesheets
            .Where(c => c.TimesheetApproverId != null && employees.Contains(c.UserId))
            .Include(c => c.TimesheetApprover)
            .Include(c => c.TimesheetRows);

        var timesheetRows = new List<TimesheetRow>();
        foreach (var timesheet in timesheets)
        {
            if (_signatureService.VerifySignature(timesheet, timesheet.TimesheetApprover!.PublicKey!, timesheet.ApproverHash!))
            {
                timesheetRows.AddRange(timesheet.TimesheetRows.Where(c => c.ProjectId == prj.ProjectId).ToList());
            }
        }

        double totalPM = 0, totalPMPD = 0, totalRE = 0, totalREPD = 0, totalActualPD = 0, totalActual = 0;

        foreach (var lg in labourGrades)
        {
            wpTable.AddCell(new Cell(1, 2).Add(new Paragraph(lg.LabourCode + " ($" + lg.Rate + ")")));

            double totalPDPM = 0, totalCostPM = 0;
            foreach (var budget in budgets.Where(c => c.isREBudget == false && c.LabourCode == lg.LabourCode))
            {
                totalPDPM += budget.BudgetAmount;
                totalCostPM += budget.BudgetAmount * labourGrades.Where(c => c.LabourCode == budget.LabourCode && c.Year == DateTime.Now.Year).First().Rate;
            }
            totalPM += totalCostPM;
            totalPMPD += totalPDPM;
            wpTable.AddCell(new Cell().Add(new Paragraph(Convert.ToString(Math.Round(totalPDPM, 2))).SetFontSize(fontSizeSH).SetTextAlignment(TextAlignment.CENTER)));
            wpTable.AddCell(new Cell().Add(new Paragraph("$" + Math.Round(totalCostPM, 2)).SetFontSize(fontSizeSH).SetTextAlignment(TextAlignment.CENTER)));

            double totalPDRE = 0, totalCostRE = 0;
            foreach (var budget in budgets.Where(c => c.isREBudget == true && c.LabourCode == lg.LabourCode))
            {
                totalPDRE += budget.BudgetAmount;
                totalCostRE += budget.BudgetAmount * labourGrades.Where(c => c.LabourCode == budget.LabourCode && c.Year == DateTime.Now.Year).First().Rate;
            }
            totalRE += totalCostRE;
            totalREPD += totalPDRE;
            wpTable.AddCell(new Cell().Add(new Paragraph(Convert.ToString(Math.Round(totalPDRE, 2))).SetFontSize(fontSizeSH).SetTextAlignment(TextAlignment.CENTER)));
            wpTable.AddCell(new Cell().Add(new Paragraph("$" + Math.Round(totalCostRE, 2)).SetFontSize(fontSizeSH).SetTextAlignment(TextAlignment.CENTER)));

            double totalPDActual = 0, totalCostActual = 0;
            foreach (var row in timesheetRows.Where(c => c.OriginalLabourCode == lg.LabourCode))
            {
                totalPDActual += row.TotalHoursRow / 8;
                totalCostActual += (row.TotalHoursRow / 8) * labourGrades.Where(c => c.LabourCode == row.OriginalLabourCode && c.Year == row.Timesheet!.EndDate!.Value.Year).First().Rate;
            }
            totalActualPD += totalPDActual;
            totalActual += totalCostActual;
            wpTable.AddCell(new Cell().Add(new Paragraph(Convert.ToString(Math.Round(totalPDActual, 2))).SetFontSize(fontSizeSH).SetTextAlignment(TextAlignment.CENTER)));
            wpTable.AddCell(new Cell().Add(new Paragraph("$" + Math.Round(totalCostActual, 2)).SetFontSize(fontSizeSH).SetTextAlignment(TextAlignment.CENTER)));
        }

        wpTable.AddCell(new Cell(1, 2).SetBackgroundColor(ColorConstants.LIGHT_GRAY).SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).Add(new Paragraph("Total")));
        wpTable.AddCell(new Cell().SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).SetBackgroundColor(ColorConstants.LIGHT_GRAY).Add(new Paragraph(Convert.ToString(Math.Round(totalPMPD, 2)))));
        wpTable.AddCell(new Cell().SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).SetBackgroundColor(ColorConstants.LIGHT_GRAY).Add(new Paragraph("$" + Convert.ToString(Math.Round(totalPM, 2)))));
        wpTable.AddCell(new Cell().SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).SetBackgroundColor(ColorConstants.LIGHT_GRAY).Add(new Paragraph(Convert.ToString(Math.Round(totalREPD, 2)))));
        wpTable.AddCell(new Cell().SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).SetBackgroundColor(ColorConstants.LIGHT_GRAY).Add(new Paragraph("$" + Convert.ToString(Math.Round(totalRE, 2)))));
        wpTable.AddCell(new Cell().SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).SetBackgroundColor(ColorConstants.LIGHT_GRAY).Add(new Paragraph(Convert.ToString(Math.Round(totalActualPD, 2)))));
        wpTable.AddCell(new Cell().SetTextAlignment(TextAlignment.CENTER).SetFontSize(fontSizeSH).SetBackgroundColor(ColorConstants.LIGHT_GRAY).Add(new Paragraph("$" + Convert.ToString(Math.Round(totalActual, 2)))));

        document.Add(wpTable);

        int n = pdfDoc.GetNumberOfPages();
        for (int i = 1; i <= n; i++)
        {
            document.ShowTextAligned(new Paragraph(String.Format("Page " + i + " of " + n)), 559, 806, i, TextAlignment.RIGHT, VerticalAlignment.TOP, 0);
        }

        document.Close();
        byte[] byteInfo = ms.ToArray();
        ms.Write(byteInfo, 0, byteInfo.Length);
        ms.Position = 0;
        return byteInfo;
    }

    public async Task<List<object>> GetAllProjectEmployeesAsync(int projectId, string currentUserId)
    {
        var employees = await _context.EmployeeProjects
            .Where(c => c.ProjectId == projectId && c.UserId != currentUserId)
            .Include(c => c.Project)
                .ThenInclude(p => p!.AssistantProjectManager)
            .Include(c => c.Project!.ProjectManager)
            .Include(c => c.User)
            .Select(c => (object)new
            {
                c!.User!.FirstName,
                c!.User!.LastName,
                c!.User!.EmployeeNumber,
                ManagerNumber = c.Project != null && c.Project.AssistantProjectManager != null ? c.Project.AssistantProjectManager.EmployeeNumber : 0,
                ProjectManagerNumber = c.Project != null && c.Project.ProjectManager != null ? c.Project.ProjectManager.EmployeeNumber : 0
            })
            .ToListAsync();

        return employees;
    }

    public async Task<bool> AssignAssistantProjectManagerAsync(int projectId, string employeeNumber, string currentPmId)
    {
        int asmEmployeeNum = Convert.ToInt32(employeeNumber);
        var proj = await _context.Projects.FindAsync(projectId);
        if (proj == null) return false;

        var user = _context.Users.Where(c => c.EmployeeNumber == asmEmployeeNum).Select(c => c.Id).FirstOrDefault();
        if (user == null || user == proj.ProjectManagerId) return false;

        var oldASM = proj.AssistantProjectManagerId;
        proj.AssistantProjectManagerId = user;

        if (oldASM != null)
        {
            _notificationService.AddNotification(oldASM, "You have been removed from the project " + proj.ProjectTitle + " as an Assistant Project Manager.", Convert.ToString(projectId) + " Remove", 2);
        }
        _notificationService.AddNotification(proj.AssistantProjectManagerId, "You have been added to the project " + proj.ProjectTitle + " as an Assistant Project Manager.", Convert.ToString(projectId) + " Add", 1);
        _context.SaveChanges();
        return true;
    }
}
