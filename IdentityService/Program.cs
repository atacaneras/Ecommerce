using IdentityService.Data;
using IdentityService.Security;
using IdentityService.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// CORS Policy
var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger with JWT support
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Identity Service API",
        Version = "v1",
        Description = "Secure Authentication & Authorization Service"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
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
        }
    });
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
        policy =>
        {
            policy.WithOrigins("http://localhost:3001")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

// Database
builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");

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
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero // Remove default 5 minute tolerance
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
            {
                context.Response.Headers.Append("Token-Expired", "true");
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Database Migration
using (var scope = app.Services.CreateScope())
{
    try
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Veritabaný migrasyonlarý çalýþtýrýlýyor...");

        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        dbContext.Database.Migrate();

        logger.LogInformation("Veritabaný migrasyonlarý baþarýyla tamamlandý");

        // Create default admin user if not exists
        if (!dbContext.Users.Any(u => u.Role == IdentityService.Models.UserRole.Admin))
        {
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var (hash, salt) = passwordHasher.HashPassword("Admin@123");

            var adminUser = new IdentityService.Models.User
            {
                Username = "admin",
                Email = "admin@beymen.com",
                PasswordHash = hash,
                Salt = salt,
                Role = IdentityService.Models.UserRole.Admin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                FirstName = "System",
                LastName = "Administrator"
            };

            dbContext.Users.Add(adminUser);
            dbContext.SaveChanges();

            logger.LogInformation("Default admin user created: admin / Admin@123");
        }
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Veritabaný migrasyonu baþarýsýz oldu: {Message}", ex.Message);
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(MyAllowSpecificOrigins);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();