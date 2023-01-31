using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TimesheetApp.Data;
using TimesheetApp.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    string connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
}
);

builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
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

    var UserManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var RoleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    List<IdentityRole> roles = new List<IdentityRole>();
    roles.Add(new IdentityRole { Name = "Responsible Engineer", NormalizedName = "RESPONSIBLE ENGINEER" });
    roles.Add(new IdentityRole { Name = "Admin", NormalizedName = "ADMIN" });
    roles.Add(new IdentityRole { Name = "Employee Manager", NormalizedName = "EMPLOYEE MANAGER" });
    roles.Add(new IdentityRole { Name = "HR Manager", NormalizedName = "HR MANAGER" });
    roles.Add(new IdentityRole { Name = "Project Manager", NormalizedName = "PROJECT MANAGER" });
    roles.Add(new IdentityRole { Name = "Assistant Project Manager", NormalizedName = "ASSISTANT PROJECTMANAGER" });
    roles.Add(new IdentityRole { Name = "Project Supervisor", NormalizedName = "PROJECT SUPERVISOR" });
    roles.Add(new IdentityRole { Name = "Line Manager", NormalizedName = "LINE MANAGER" });
    roles.Add(new IdentityRole { Name = "Employee", NormalizedName = "EMPLOYEE" });

    foreach (var role in roles)
    {
        var roleExist = await RoleManager.RoleExistsAsync(role.NormalizedName);
        if (!roleExist)
        {
            DbContext.Roles.Add(role);
            DbContext.SaveChanges();
        }
    }

    ApplicationUser user = new ApplicationUser{
        Email = "admin@admin.com",
        UserName = "admin@admin.com",
        FirstName = "admin",
        LastName = "admin",
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

