using FYP.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<DB>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ==============================================================================
// 1. ENABLE SESSION STATE
// ==============================================================================
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Session lasts 30 minutes
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true; // Required for GDPR compliance
});

// ==============================================================================
// 2. AUTHENTICATION (Cookies)
// ==============================================================================
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "StudentCookie";
    options.DefaultSignInScheme = "StudentCookie";
    options.DefaultChallengeScheme = "StudentCookie";

    options.DefaultAuthenticateScheme = "StaffAuth";
    options.DefaultSignInScheme = "StaffAuth";
    options.DefaultChallengeScheme = "StaffAuth";
})
.AddCookie("StudentCookie", options =>
{
    options.LoginPath = "/Student/Login";
    options.AccessDeniedPath = "/Student/Login";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(15);
    options.SlidingExpiration = true;
})
.AddCookie("StaffAuth", options => // FIXED: Added "StaffAuth" scheme name
{
    options.LoginPath = "/Staff/Login";
    options.AccessDeniedPath = "/Staff/Login";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(15);
    options.SlidingExpiration = true;
});

// ==============================================================================
// 3. AUTHORIZATION (Folder Rules)
// ==============================================================================
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Admin", "AdminOnly");
    options.Conventions.AuthorizeFolder("/Staff", "StaffOnly");
    options.Conventions.AuthorizeFolder("/Student", "StudentOnly");

    options.Conventions.AllowAnonymousToPage("/Staff/Login");
    options.Conventions.AllowAnonymousToPage("/Student/Login");
    options.Conventions.AllowAnonymousToPage("/Staff/MfaVerify");
    options.Conventions.AllowAnonymousToPage("/Staff/ForceChangePassword");
    options.Conventions.AllowAnonymousToPage("/Staff/ForgotPassword");
    options.Conventions.AllowAnonymousToPage("/Staff/PasswordReset");
    options.Conventions.AllowAnonymousToPage("/Student/ForgotPassword");
    options.Conventions.AllowAnonymousToPage("/Student/ResetPassword");
    options.Conventions.AllowAnonymousToPage("/Student/ForceChangePassword");
    options.Conventions.AllowAnonymousToPage("/Student/MFA");
});

// ==============================================================================
// 4. POLICIES (The Rules)
// ==============================================================================
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("StudentOnly", policy =>
    {
        policy.RequireClaim("RoleId", "3");
        policy.AddAuthenticationSchemes("StudentCookie");
    });

    options.AddPolicy("StaffOnly", policy =>
    {
        policy.RequireClaim("RoleId", "2");
        policy.AddAuthenticationSchemes("StaffAuth");
    });

    options.AddPolicy("AdminOnly", policy =>
    {
        policy.RequireClaim("RoleId", "1");
        policy.AddAuthenticationSchemes("StaffAuth");
    });
});

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

// ==============================================================================
// 5. PIPELINE ORDER (CRITICAL)
// ==============================================================================
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
// ==============================================================================

// ==============================================================================
// 6. PREVENT BROWSER CACHING (FIX FOR THE BACK BUTTON)
// ==============================================================================
app.Use(async (context, next) =>
{
    context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
    context.Response.Headers["Pragma"] = "no-cache";
    context.Response.Headers["Expires"] = "-1";
    await next();
});

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

app.Run();