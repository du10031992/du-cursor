using System.Text;
using MepPanel.LicenseServer.Data;
using MepPanel.LicenseServer.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MepPanel.LicenseServer",
        Version = "v1",
        Description =
            "Admin khóa/mở tài khoản, thiết bị & chức năng plugin theo SĐT. " +
            "UI quản trị: /admin · Mỗi SĐT mặc định 1 máy."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT. Ví dụ: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityDefinition("AdminApiKey", new OpenApiSecurityScheme
    {
        Description = "Admin API key trong header X-Admin-ApiKey",
        Name = "X-Admin-ApiKey",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
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
        },
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "AdminApiKey"
                }
            },
            Array.Empty<string>()
        }
    });
});

var databasePath = ResolveDatabasePath(builder.Configuration);
Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath}"));

builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<OtpService>();
builder.Services.AddScoped<ISmsGateway, WebhookSmsGateway>();
builder.Services.AddHttpClient("SmsWebhook");

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Chưa cấu hình Jwt:Key.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("Chưa cấu hình Jwt:Issuer.");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("Chưa cấu hình Jwt:Audience.");

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbSeeder.SeedAsync(db, app.Configuration);
}

static string? ResolveAdminIndexPath(IWebHostEnvironment env)
{
    var candidates = new[]
    {
        Path.Combine(env.WebRootPath ?? "", "admin", "index.html"),
        Path.Combine(env.ContentRootPath, "wwwroot", "admin", "index.html")
    };

    foreach (var path in candidates)
    {
        if (File.Exists(path))
        {
            return path;
        }
    }

    return null;
}

static string ResolveDatabasePath(IConfiguration configuration)
{
    // A shared/server deployment can override this without changing source:
    // set MEP_PANEL_LICENSE_DB_PATH to its absolute database file path.
    var configuredPath =
        Environment.GetEnvironmentVariable("MEP_PANEL_LICENSE_DB_PATH") ??
        configuration["LicenseStorage:DatabasePath"];

    if (string.IsNullOrWhiteSpace(configuredPath))
    {
        configuredPath = OperatingSystem.IsWindows()
            ? @"C:\MepPanel\du-cursor\MepPanel.LicenseServer\mep-panel-license.db"
            : Path.Combine(AppContext.BaseDirectory, "mep-panel-license.db");
    }

    return Path.GetFullPath(configuredPath);
}

var adminIndex = ResolveAdminIndexPath(app.Environment);

// Admin UI — xu ly som, truoc static files / auth
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";

    if (path.Equals("/admin", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.Redirect("/admin/", permanent: false);
        return;
    }

    if (path.Equals("/admin/", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/admin/index.html", StringComparison.OrdinalIgnoreCase))
    {
        var indexPath = ResolveAdminIndexPath(app.Environment);
        if (indexPath is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync(
                "Khong tim thay wwwroot/admin/index.html. Hay git pull repo du-cursor roi Rebuild Solution.");
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        await context.Response.SendFileAsync(indexPath);
        return;
    }

    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new
{
    ok = true,
    adminUi = adminIndex is not null,
    databasePath,
    version = "2026-08-05-admin-v2"
}));

// Trang mac dinh
app.MapGet("/", () => Results.Redirect("/admin/"));

app.Run();

public partial class Program;
