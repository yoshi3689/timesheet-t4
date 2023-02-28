using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TimesheetApp.Data;
using TimesheetApp.Models;
using TimesheetApp.Models.TimesheetModels;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var host = builder.Configuration["DBHOST"] ?? "localhost";
        var port = builder.Configuration["DBPORT"] ?? "3333";
        var password = builder.Configuration["DBPASSWORD"] ?? "password123";
        var db = builder.Configuration["DBNAME"] ?? "testdb";

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

            //create basic roles
            List<IdentityRole> roles = new List<IdentityRole>();
            roles.Add(new IdentityRole { Name = "HR", NormalizedName = "HR" });
            roles.Add(new IdentityRole { Name = "Admin", NormalizedName = "ADMIN" });
            roles.Add(new IdentityRole { Name = "Supervisor", NormalizedName = "SUPERVISOR" });

            foreach (var role in roles)
            {
                var roleExist = await RoleManager.RoleExistsAsync(role.NormalizedName ?? "");
                if (!roleExist)
                {
                    DbContext.Roles.Add(role);
                    DbContext.SaveChanges();
                }
            }

            //try create labour grades
            var grades = DbContext.LabourGrades;
            LabourGrade adminGrade;
            if (grades != null && grades.Count() == 0)
            {
                //Default labour grades
                List<LabourGrade> labourGrades = new List<LabourGrade>();
                adminGrade = new LabourGrade { LabourCode = "JS", Rate = 1 };
                labourGrades.Add(adminGrade);
                labourGrades.Add(new LabourGrade { LabourCode = "DS", Rate = 1 });
                labourGrades.Add(new LabourGrade { LabourCode = "P1", Rate = 1 });
                labourGrades.Add(new LabourGrade { LabourCode = "P2", Rate = 2 });
                labourGrades.Add(new LabourGrade { LabourCode = "P3", Rate = 3 });
                labourGrades.Add(new LabourGrade { LabourCode = "P4", Rate = 4 });
                labourGrades.Add(new LabourGrade { LabourCode = "P5", Rate = 5 });
                labourGrades.Add(new LabourGrade { LabourCode = "P6", Rate = 6 });
                foreach (var lg in labourGrades)
                {
                    DbContext.LabourGrades!.Add(lg);
                }
                DbContext.SaveChanges();
            }
            else
            {
                adminGrade = DbContext.LabourGrades!.FirstOrDefault() ?? new LabourGrade { LabourCode = "JS", Rate = 1 };
            }

            //create a default admin
            ApplicationUser user = new ApplicationUser
            {
                Email = "admin@admin.com",
                UserName = "admin@admin.com",
                FirstName = "admin",
                LastName = "admin",
                JobTitle = "admin",
                EmailConfirmed = true,
                LabourGrade = adminGrade
            };
            var adminExist = await UserManager.FindByEmailAsync(user.Email);
            if (adminExist == null)
            {
                await UserManager.CreateAsync(user, "Password123!");
                var newAdmin = await UserManager.FindByEmailAsync("admin@admin.com");
                if (newAdmin != null)
                {
                    await UserManager.AddToRoleAsync(newAdmin, "Admin");
                }
            }

        }
        app.Run();
    }
}