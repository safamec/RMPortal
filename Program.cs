using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

using RMPortal.Data;       // AppDbContext
using RMPortal.Services;   // EmailOptions, IEmailService, SmtpEmailService, IFakeAdService, FakeAdService, IWorkflowNotifier, WorkflowNotifier

var builder = WebApplication.CreateBuilder(args);

// ========== Services ==========
builder.Services.AddControllersWithViews();

// Email / Notifier
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Smtp")); // reads the "Smtp" section
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<IWorkflowNotifier, WorkflowNotifier>();
builder.Services.AddHttpContextAccessor();

// DbContext (SQLite)
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Auth: cookie
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opts =>
    {
        opts.LoginPath = "/Account/Login";
        opts.AccessDeniedPath = "/Account/AccessDenied";
    });

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("IsManager",  p => p.RequireClaim("groups", "RM_LineManagers"));
    options.AddPolicy("IsSecurity", p => p.RequireClaim("groups", "RM_Security"));
    options.AddPolicy("IsIT",       p => p.RequireClaim("groups", "RM_ITAdmins"));
});

// SignalR
builder.Services.AddSignalR();

// Fake AD for development
builder.Services.AddSingleton<IFakeAdService, FakeAdService>();

var app = builder.Build();

// ========== Middleware ==========
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

// ✅ Enable attribute-routed controllers like [HttpGet("/dev/test-email")]
app.MapControllers();

// Conventional MVC routes
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// SignalR hub (remove if you don't use it)
app.MapHub<NotificationHub>("/hubs/notify");

// Auto-migrate (optional)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();
