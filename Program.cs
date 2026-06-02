using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TimesheetApp.Data;
using TimesheetApp.Models;
using TimesheetApp.Models.TimesheetModels;
using System.Security.Cryptography;
using TimesheetApp.Helpers;
using TimesheetApp.Authorization;
using Microsoft.AspNetCore.Authorization;
using TimesheetApp.Services;

internal partial class Program
{
    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var isDev = builder.Environment.IsDevelopment();

        var host = builder.Configuration["DBHOST"] ?? (isDev ? "localhost" : throw new InvalidOperationException("DBHOST environment variable is required in production."));
        var port = builder.Configuration["DBPORT"] ?? (isDev ? "3333" : throw new InvalidOperationException("DBPORT environment variable is required in production."));
        var password = builder.Configuration["DBPASSWORD"] ?? (isDev ? "password123" : throw new InvalidOperationException("DBPASSWORD environment variable is required in production."));
        var db = builder.Configuration["DBNAME"] ?? (isDev ? "db" : throw new InvalidOperationException("DBNAME environment variable is required in production."));

        string connectionString = $"server={host}; userid=root; pwd={password};"
                + $"port={port}; database={db};SslMode=none;allowpublickeyretrieval=True;";
        // Add services to the container.
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
        }
        );

        builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();
        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.Events.OnRedirectToLogin = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                else
                    context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                else
                    context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };
        });
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("KeyRequirement", policy => policy.Requirements.Add(new KeyRequirement(true)));
        });
        builder.Services.AddScoped<IAuthorizationHandler, KeyRequirementHandler>();
        builder.Services.AddScoped<ISignatureService, SignatureService>();
        builder.Services.AddScoped<INotificationService, NotificationService>();
        builder.Services.AddScoped<IEmployeeService, EmployeeService>();
        builder.Services.AddScoped<ITimesheetService, TimesheetService>();
        builder.Services.AddScoped<IWorkPackageService, WorkPackageService>();
        builder.Services.AddScoped<IProjectService, ProjectService>();
        builder.Services.AddControllersWithViews()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.ReferenceHandler =
                    System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
            });
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromHours(12);
            options.Cookie.Name = ".ProjectManagement.Session";
            options.Cookie.IsEssential = true;
            options.Cookie.SecurePolicy = isDev ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.Cookie.HttpOnly = true;
        });

        builder.Services.AddHealthChecks();

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("Frontend", policy =>
            {
                policy.WithOrigins("http://localhost:3000")
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseMigrationsEndPoint();
            app.UseHsts();
            app.UseSwagger();
            app.UseSwaggerUI();
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

        app.UseCors("Frontend");

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");
        app.MapRazorPages();
        app.MapHealthChecks("/health");

        app.UseSession();
        // Seed initial data and apply pending migrations on startup
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

            // Define the number of HR users you want to create
            int numHRUsers = 6;

            // Create an array to store the HR users
            ApplicationUser[] hrUsers = new ApplicationUser[numHRUsers];

            // Create a loop to create the HR users
            RSA rsa2;
            for (int i = 1; i < numHRUsers; i++)
            {
                rsa2 = RSA.Create();

                ApplicationUser newHR = new ApplicationUser
                {
                    Email = $"hr{i}@hr.com",
                    UserName = $"hr{i}@hr.com",
                    FirstName = "HR",
                    LastName = $"Manager{i}",
                    JobTitle = "HR Manager",
                    EmailConfirmed = true,
                    LabourGradeCode = "P5",
                    EmployeeNumber = 1002342000 + i,
                    SupervisorId = admin.Id,
                    SickDays = 7,
                    TimesheetApproverId = admin.Id,
                    PublicKey = rsa.ExportRSAPublicKey(),
                    PrivateKey = KeyHelper.Encrypt(rsa.ExportRSAPrivateKey(), "Password123!")
                };

                hrUsers[i] = newHR;
            }

            // Save the HR users to the database
            foreach (var hrUser in hrUsers)
            {
                if (hrUser == null || hrUser.Email == null)
                {
                    continue;
                }
                var hrExists = await UserManager.FindByEmailAsync(hrUser.Email);
                if (hrExists == null)
                {
                    await UserManager.CreateAsync(hrUser, "Password123!");
                    var newHRExists = await UserManager.FindByEmailAsync(hrUser.Email);
                    if (newHRExists != null)
                    {
                        await UserManager.AddToRoleAsync(hrUser, "HR");
                        await UserManager.AddToRoleAsync(hrUser, "Supervisor");
                        admin.SupervisorId = newHRExists.Id;
                        admin.TimesheetApproverId = newHRExists.Id;
                    }
                }
            }

            // Save the changes to the database
            await DbContext.SaveChangesAsync();

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
            var flex = DbContext.WorkPackages.Where(c => c.WorkPackageId == "FLEX").FirstOrDefault();
            if (flex == null)
            {
                DbContext.WorkPackages.Add(new WorkPackage { WorkPackageId = "FLEX", ProjectId = project!.ProjectId, Title = "Flex time" });
            }
            DbContext.SaveChanges();

        }
        app.Run();
    }
}
