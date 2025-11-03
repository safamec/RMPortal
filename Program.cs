using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using RMPortal.Services;

var builder = WebApplication.CreateBuilder(args);

// 1) Services (ALL registrations happen BEFORE Build)
builder.Services.AddControllersWithViews();

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

builder.Services.AddAuthorization(options =>
{
    // You can add policies if needed, roles are coming from your fake AD login
});

// SignalR
builder.Services.AddSignalR();

// Email
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddScoped<IEmailService, SmtpEmailService>();

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
app.UseAuthentication();  // must be BEFORE UseAuthorization
app.UseAuthorization();

// 4) Endpoints (Areas + default)
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// SignalR hub
app.MapHub<NotificationHub>("/hubs/notify");

app.Run();
