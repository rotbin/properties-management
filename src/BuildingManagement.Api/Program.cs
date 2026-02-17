using System.Text;
using BuildingManagement.Core.Entities;
using BuildingManagement.Core.Interfaces;
using BuildingManagement.Infrastructure.Data;
using BuildingManagement.Infrastructure.Services.Sms;
using BuildingManagement.Infrastructure.Jobs;
using BuildingManagement.Infrastructure.Services;
using BuildingManagement.Infrastructure.Services.Gateways;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ─── Database ───────────────────────────────────────────
// Supported providers: InMemory (default for demo), Sqlite, SqlServer
var dbProvider = builder.Configuration["Database:Provider"] ?? "InMemory";

if (dbProvider.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseInMemoryDatabase("BuildingManagementDb"));
}
else if (dbProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required for SqlServer provider.");
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(connectionString));
}
else // Sqlite (default persistent option)
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=buildingmgmt.db";
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(connectionString));
}

// ─── Identity ───────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// ─── JWT Authentication ─────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"] ?? "SuperSecretKeyForDevelopmentOnly123456!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "BuildingManagement";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "BuildingManagement";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});

builder.Services.AddAuthorization();

// ─── Services ───────────────────────────────────────────
builder.Services.AddScoped<JwtTokenService>();

// File Storage
var fileProvider = builder.Configuration["FileStorage:Provider"] ?? "Local";
if (fileProvider.Equals("Azure", StringComparison.OrdinalIgnoreCase))
{
    var blobConn = builder.Configuration["AzureBlob:ConnectionString"]!;
    var container = builder.Configuration["AzureBlob:ContainerName"] ?? "building-files";
    builder.Services.AddSingleton<IFileStorageService>(sp =>
        new AzureBlobStorageService(blobConn, container, sp.GetRequiredService<ILogger<AzureBlobStorageService>>()));
}
else
{
    var wwwrootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
    builder.Services.AddSingleton<IFileStorageService>(sp =>
        new LocalFileStorageService(wwwrootPath, sp.GetRequiredService<ILogger<LocalFileStorageService>>()));
}

// Email
builder.Services.AddSingleton<IEmailService, LoggingEmailService>();

// Payment Gateways (HttpClientFactory + typed clients)
builder.Services.AddHttpClient<MeshulamGateway>();
builder.Services.AddHttpClient<PelecardGateway>();
builder.Services.AddHttpClient<TranzilaGateway>();
builder.Services.AddHttpClient<PayPalGateway>();
builder.Services.AddSingleton<FakePaymentGateway>();
builder.Services.AddSingleton<MeshulamGateway>();
builder.Services.AddSingleton<PelecardGateway>();
builder.Services.AddSingleton<TranzilaGateway>();
builder.Services.AddSingleton<PayPalGateway>();
builder.Services.AddSingleton<IPaymentGatewayFactory, PaymentGatewayFactory>();

// HOA Fee Service
builder.Services.AddScoped<IHOAFeeService, HOAFeeService>();

// SMS
var smsProvider = builder.Configuration["Sms:Provider"] ?? "Fake";
if (smsProvider.Equals("AzureAcs", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<ISmsSender, BuildingManagement.Infrastructure.Services.Sms.AzureAcsSmsSender>();
else
    builder.Services.AddSingleton<ISmsSender, BuildingManagement.Infrastructure.Services.Sms.FakeSmsSender>();
builder.Services.AddSingleton(new BuildingManagement.Infrastructure.Services.Sms.SmsRateLimiter(30));

// Email sender
builder.Services.AddSingleton<IEmailSender, BuildingManagement.Infrastructure.Services.Email.FakeEmailSender>();

// Background Jobs
builder.Services.AddSingleton<MaintenanceJobService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MaintenanceJobService>());
builder.Services.AddSingleton<RecurringPaymentJob>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RecurringPaymentJob>());

// Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

// ─── CORS ───────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                  "http://localhost:5173", "http://localhost:3000", "http://localhost:4173",
                  "https://properties-management-d8g3gfd8gfcwajft.israelcentral-01.azurewebsites.net")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ─── Controllers & Swagger ──────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Building Management API",
        Version = "v1",
        Description = "API for Building Maintenance Management System"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ─── Database Initialization & Seed Data ────────────────
{
    var isInMemory = dbProvider.Equals("InMemory", StringComparison.OrdinalIgnoreCase);
    if (isInMemory)
    {
        // InMemory doesn't support migrations — just ensure schema is created and seed
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    if (isInMemory)
    {
        // InMemory: always seed (data is lost on restart anyway)
        await DataSeeder.SeedAsync(app.Services, useInMemory: true);
    }
    else if (app.Environment.IsDevelopment())
    {
        await DataSeeder.SeedAsync(app.Services, useInMemory: false);
    }
    else
    {
        // Production with real DB: just run migrations
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }
}

// ─── Middleware Pipeline ────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Building Management API v1"));
}

app.UseCors("AllowFrontend");

// Serve React SPA static files from wwwroot
app.UseDefaultFiles(); // Serves index.html for root "/"
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

// SPA fallback: for any non-API, non-file request, serve index.html so React Router works
app.MapFallbackToFile("index.html");

app.Run();
