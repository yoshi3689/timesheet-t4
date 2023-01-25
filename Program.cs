using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Timesheet.Data;
using Timesheet.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    string connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
}
);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews();

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


var scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();

using (var scope = scopeFactory.CreateScope())
{
    var DbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    var UserManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
    var RoleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    List<IdentityRole> roles = new List<IdentityRole>();
    roles.Add(new IdentityRole { Name = "ResponsibleEngineer", NormalizedName = "RESPONSIBLEENGINEER" });
    roles.Add(new IdentityRole { Name = "Admin", NormalizedName = "ADMIN" });
    roles.Add(new IdentityRole { Name = "EmployeeManager", NormalizedName = "EMPLOYEEMANAGER" });
    roles.Add(new IdentityRole { Name = "HRManager", NormalizedName = "HRMANAGER" });
    roles.Add(new IdentityRole { Name = "ProjectManager", NormalizedName = "PROJECTMANAGER" });
    roles.Add(new IdentityRole { Name = "AssistantProjectManager", NormalizedName = "ASSISTANTPROJECTMANAGER" });
    roles.Add(new IdentityRole { Name = "ProjectSupervisor", NormalizedName = "PROJECTSUPERVISOR" });
    roles.Add(new IdentityRole { Name = "LineManager", NormalizedName = "LINEMANAGER" });
    roles.Add(new IdentityRole { Name = "Employee", NormalizedName = "EMPLOYEE" });

    foreach (var role in roles)
    {
        var roleExist = await RoleManager.RoleExistsAsync(role.Name);
        if (!roleExist)
        {
            DbContext.Roles.Add(role);
            DbContext.SaveChanges();
        }
    }

    IdentityUser user = new IdentityUser{
        Email = "admin@admin.com",
        UserName = "admin@admin.com",
        EmailConfirmed = true
    };
    var adminExist = await UserManager.FindByEmailAsync(user.Email);
    if(adminExist == null){
        await UserManager.CreateAsync(user, "Password123!");
        var newAdmin = await UserManager.FindByEmailAsync("admin@admin.com");
        await UserManager.AddToRoleAsync(newAdmin, "Admin");
    }
}


app.Run();

