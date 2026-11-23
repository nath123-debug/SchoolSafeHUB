using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentIncidentSystem.Data;
using StudentIncidentSystem.Models;
using StudentIncidentSystem.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Database (connection string comes from appsettings.json)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// 2. Identity (core only: we use our own AccountController)
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;

        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false; // fictional dev data only
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>()
    .AddDefaultTokenProviders();

// 3. Secure cookies
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Home/AccessDenied";
});

// Deactivated accounts and role changes take effect within a minute
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));

// 4. Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanViewReports", policy =>
        policy.RequireRole(AppRoles.Staff, AppRoles.Administrator));
});

// 5. Login rate limiting: at most 10 login or registration attempts per minute per IP address
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var isAuthPost = HttpMethods.IsPost(context.Request.Method) &&
            (context.Request.Path.StartsWithSegments("/Account/Login") ||
             context.Request.Path.StartsWithSegments("/Account/Register"));

        if (!isAuthPost)
            return RateLimitPartition.GetNoLimiter("other");

        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter("auth:" + ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });
});

builder.Services.AddMemoryCache();

// 6. MVC with automatic anti-forgery checks and audit logging
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    options.Filters.Add<AuditActionFilter>();
});

// 7. Our services
builder.Services.AddSingleton<AuditService>();
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<IncidentService>();
builder.Services.AddScoped<StaffCaseService>();
builder.Services.AddScoped<UserAdminService>();
builder.Services.AddScoped<CategoryAdminService>();
builder.Services.AddScoped<CaseAssignmentService>();
builder.Services.AddScoped<AttachmentService>();
builder.Services.AddScoped<ReportService>();

var app = builder.Build();

// Seed roles, categories, and (in Development only) fictional test accounts
using (var scope = app.Services.CreateScope())
{
    await DbInitializer.InitializeAsync(scope.ServiceProvider, app.Environment.IsDevelopment());
    var adminEmail = builder.Configuration["InitialAdmin:Email"];
    var adminPassword = builder.Configuration["InitialAdmin:Password"];

    if (!app.Environment.IsDevelopment() &&
        !string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
    {
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (await users.FindByEmailAsync(adminEmail) == null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = "System Administrator",
                IsActive = true
            };
            var created = await users.CreateAsync(admin, adminPassword);
            if (created.Succeeded) await users.AddToRoleAsync(admin, AppRoles.Administrator);
        }
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Basic security headers on every response
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
