using System.IO; // for Directory.CreateDirectory in SmtpEmailService
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

using RMPortal.Data;      // AppDbContext
using RMPortal.Services;  // IEmailService, SmtpEmailService, IFakeAdService

var builder = WebApplication.CreateBuilder(args);

// 1) Services
builder.Services.AddControllersWithViews();

// Email options + service
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddSingleton<IEmailService, SmtpEmailService>();

// DbContext
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Auth (fake login via cookies for dev)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opts =>
    {
        opts.LoginPath = "/Account/Login";
        opts.AccessDeniedPath = "/Account/AccessDenied";
    });

// Authorization + policies (one call only)
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("IsManager",  p => p.RequireClaim("groups", "RM_LineManagers"));
    options.AddPolicy("IsSecurity", p => p.RequireClaim("groups", "RM_Security"));
    options.AddPolicy("IsIT",       p => p.RequireClaim("groups", "RM_ITAdmins"));
});

// SignalR
builder.Services.AddSignalR();

// Fake AD for dev
builder.Services.AddSingleton<IFakeAdService, FakeAdService>();

// 2) Build
var app = builder.Build();

// 3) Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// 4) Endpoints
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<NotificationHub>("/hubs/notify");

// (Optional) auto-apply migrations so SQLite has all tables
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();
