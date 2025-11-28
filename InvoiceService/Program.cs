using Microsoft.EntityFrameworkCore;
using InvoiceService.Consumers;
using InvoiceService.Data;
using InvoiceService.Models;
using InvoiceService.Services;
using Shared.Infrastructure.Messaging;
using Shared.Infrastructure.Repository;

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
builder.Services.AddDbContext<InvoiceDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// RabbitMQ
var rabbitMQSettings = new RabbitMQSettings();
builder.Configuration.GetSection("RabbitMQ").Bind(rabbitMQSettings);
builder.Services.AddSingleton(rabbitMQSettings);
builder.Services.AddSingleton<IMessagePublisher, RabbitMQPublisher>();

// Repository
builder.Services.AddScoped<DbContext>(provider => provider.GetRequiredService<InvoiceDbContext>());
builder.Services.AddScoped<IRepository<Invoice>, Repository<Invoice>>();

// Services
builder.Services.AddScoped<IInvoiceService, InvoiceServiceImpl>();
builder.Services.AddHttpClient();

// Background Consumer
builder.Services.AddHostedService<InvoiceConsumer>();

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

        var dbContext = scope.ServiceProvider.GetRequiredService<InvoiceDbContext>();
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