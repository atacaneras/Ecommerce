using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Messaging;
using Shared.Infrastructure.Repository;
using VerificationService.Consumers;
using VerificationService.Data;
using VerificationService.Models;
using VerificationService.Services;

var builder = WebApplication.CreateBuilder(args);

// CORS Policy
var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
builder.Services.AddDbContext<VerificationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// RabbitMQ
var rabbitMQSettings = new RabbitMQSettings();
builder.Configuration.GetSection("RabbitMQ").Bind(rabbitMQSettings);
builder.Services.AddSingleton(rabbitMQSettings);
builder.Services.AddSingleton<IMessagePublisher, RabbitMQPublisher>();

// Repository
builder.Services.AddScoped<DbContext>(provider => provider.GetRequiredService<VerificationDbContext>());
builder.Services.AddScoped<IRepository<VerificationRequest>, Repository<VerificationRequest>>();

// Services
builder.Services.AddScoped<IVerificationService, VerificationServiceImpl>();
builder.Services.AddHttpClient();

// Background Consumer
builder.Services.AddHostedService<VerificationConsumer>();

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

        var dbContext = scope.ServiceProvider.GetRequiredService<VerificationDbContext>();
        dbContext.Database.Migrate();

        logger.LogInformation("Veritabaný migrasyonlarý baþarýyla tamamlandý");
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
app.MapControllers();
app.Run();