using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TimesheetApp.Data;
using TimesheetApp.Models;
using TimesheetApp.Models.TimesheetModels;
using System.Security.Cryptography;
using TimesheetApp.Helpers;
using TimesheetApp.Authorization;
using Microsoft.AspNetCore.Authorization;

internal partial class Program
{
    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var host = builder.Configuration["DBHOST"] ?? "localhost";
        var port = builder.Configuration["DBPORT"] ?? "3333";
        var password = builder.Configuration["DBPASSWORD"] ?? "password123";
        var db = builder.Configuration["DBNAME"] ?? "db";

        string connectionString = $"server={host}; userid=root; pwd={password};"
                + $"port={port}; database={db};SslMode=none;allowpublickeyretrieval=True;";
        GlobalData.DBHost = host;
        GlobalData.DBPassword = password;
        GlobalData.DBPort = port;
        GlobalData.DBName = db;
        // Add services to the container.
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
        }
        );

        builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("KeyRequirement", policy => policy.Requirements.Add(new KeyRequirement(true)));
        });
        builder.Services.AddScoped<IAuthorizationHandler, KeyRequirementHandler>();
        builder.Services.AddControllersWithViews();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromHours(12);
            options.Cookie.Name = ".ProjectManagement.Session";
            options.Cookie.IsEssential = true;
        });

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseMigrationsEndPoint();
            app.UseHsts();

        }
        else
        {
            app.UseExceptionHandler("/Home/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");
        app.MapRazorPages();

        app.UseSession();
        //get the needed services to add roles and update db
        var scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();
        using (var scope = scopeFactory.CreateScope())
        {
            var DbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var UserManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var RoleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            var services = scope.ServiceProvider;

            var context = services.GetRequiredService<ApplicationDbContext>();
            if (context.Database.GetPendingMigrations().Any())
            {
                context.Database.Migrate();
            }


            RSA rsa = RSA.Create();
            //create a default admin
            ApplicationUser admin = new ApplicationUser
            {
                Email = "admin@admin.com",
                UserName = "admin@admin.com",
                FirstName = "admin",
                LastName = "admin",
                JobTitle = "admin",
                EmailConfirmed = true,
                LabourGradeCode = "P5",
                SickDays = 7,
                EmployeeNumber = 1000000000,
                PublicKey = rsa.ExportRSAPublicKey(),
                PrivateKey = KeyHelper.Encrypt(rsa.ExportRSAPrivateKey(), "Password123!")
            };
            var adminExist = await UserManager.FindByEmailAsync(admin.Email);
            if (adminExist == null)
            {
                await UserManager.CreateAsync(admin, "Password123!");
                var newAdmin = await UserManager.FindByEmailAsync("admin@admin.com");
                if (newAdmin != null)
                {
                    await UserManager.AddToRoleAsync(newAdmin, "Admin");
                    await UserManager.AddToRoleAsync(newAdmin, "Supervisor");
                    await UserManager.AddToRoleAsync(newAdmin, "HR");
                }
            }
            else
            {
                admin = adminExist;
            }

            RSA rsa2 = RSA.Create();
            ApplicationUser newHR = new ApplicationUser
            {
                Email = "hr@hr.com",
                UserName = "hr@hr.com",
                FirstName = "HR",
                LastName = "Manager",
                JobTitle = "HR Manager",
                EmailConfirmed = true,
                LabourGradeCode = "P5",
                EmployeeNumber = 1000000001,
                SupervisorId = admin.Id,
                SickDays = 7,
                TimesheetApproverId = admin.Id,
                PublicKey = rsa2.ExportRSAPublicKey(),
                PrivateKey = KeyHelper.Encrypt(rsa2.ExportRSAPrivateKey(), "Password123!")
            };
            var hrExists = await UserManager.FindByEmailAsync(newHR.Email);
            if (hrExists == null)
            {
                await UserManager.CreateAsync(newHR, "Password123!");
                var newHRExists = await UserManager.FindByEmailAsync("hr@hr.com");
                if (newHRExists != null)
                {
                    await UserManager.AddToRoleAsync(newHR, "HR");
                    await UserManager.AddToRoleAsync(newHR, "Supervisor");
                }
                admin.SupervisorId = newHR.Id;
                admin.TimesheetApproverId = newHR.Id;
                DbContext.SaveChanges();
            }

            var project = DbContext.Projects.Where(c => c.ProjectId == 010).FirstOrDefault();
            if (project == null)
            {
                project = new Project { ProjectId = 010, ProjectTitle = "Extras", ProjectManagerId = admin.Id };
                DbContext.Projects.Add(project);
                DbContext.SaveChanges();
            }
            var sick = DbContext.WorkPackages.Where(c => c.WorkPackageId == "SICK").FirstOrDefault();
            if (sick == null)
            {
                DbContext.WorkPackages.Add(new WorkPackage { WorkPackageId = "SICK", ProjectId = project!.ProjectId, Title = "Sick Time" });
            }
            var vacn = DbContext.WorkPackages.Where(c => c.WorkPackageId == "VACN").FirstOrDefault();
            if (vacn == null)
            {
                DbContext.WorkPackages.Add(new WorkPackage { WorkPackageId = "VACN", ProjectId = project!.ProjectId, Title = "Vacation Time" });
            }
            var shol = DbContext.WorkPackages.Where(c => c.WorkPackageId == "SHOL").FirstOrDefault();
            if (shol == null)
            {
                DbContext.WorkPackages.Add(new WorkPackage { WorkPackageId = "SHOL", ProjectId = project!.ProjectId, Title = "Statutory Holiday" });
            }
            DbContext.SaveChanges();
        }
        app.Run();
    }

    public static class GlobalData
    {
        public static string? DBPassword { get; set; }
        public static string? DBHost { get; set; }
        public static string? DBPort { get; set; }
        public static string? DBName { get; set; }
    }
}
