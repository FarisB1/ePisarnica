using ePisarnica.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using ePisarnica.Helpers;
using System.Text.Json.Serialization;
using ePisarnica.Services;
using OfficeOpenXml;
using System.ComponentModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,                       // broj ponovljenih pokušaja
            maxRetryDelay: TimeSpan.FromSeconds(10), // maksimalno vrijeme čekanja između pokušaja
            errorNumbersToAdd: null                 // dodatni SQL error kodovi, opcionalno
        )
    )
);
// Add authentication services
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.Name = "episarnica.Auth";
    });
// Add this to your services configuration
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin"));
    
});

builder.Services.AddControllersWithViews(options =>
{
    // Require authenticated user globally
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.Filters.Add(new AuthorizeFilter(policy));
});

// Add to your services configuration
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<EmailService>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddScoped<IWordToPdfService, WordToPdfService>();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(7); // Keep session for 7 days
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = "episarnica.Session";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});
// Add this before builder.Build()
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104857600; // 100MB limit
});
builder.Services.AddScoped<QRHelper>();
builder.Services.AddScoped<IPDFSigningService, PDFSigningService>();
builder.Services.AddScoped<IPDFSignatureDetectionService, PDFSignatureDetectionService>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Add these two lines - authentication must come before authorization
app.UseAuthentication();
app.UseAuthorization();

app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();