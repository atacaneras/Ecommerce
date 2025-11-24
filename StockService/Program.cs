using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Messaging;
using Shared.Infrastructure.Repository;
using StockService.Consumers;
using StockService.Data;
using StockService.Models;
using StockService.Services;

var builder = WebApplication.CreateBuilder(args);

// CORS Politikası Adı
var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS Tanımlaması: Frontend adresinden gelen isteklere izin veriyoruz
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

// Veritabanı
builder.Services.AddDbContext<StockDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// RabbitMQ
var rabbitMQSettings = new RabbitMQSettings();
builder.Configuration.GetSection("RabbitMQ").Bind(rabbitMQSettings);
builder.Services.AddSingleton(rabbitMQSettings);
builder.Services.AddSingleton<IMessagePublisher, RabbitMQPublisher>();

// Repository
builder.Services.AddScoped<DbContext>(provider => provider.GetRequiredService<StockDbContext>());
builder.Services.AddScoped<IRepository<Product>, Repository<Product>>();

// Servisler
builder.Services.AddScoped<IStockService, StockServiceImpl>();
builder.Services.AddHttpClient();
builder.Services.AddHostedService<StockOrderApprovedConsumer>();

// Arka plan tüketici
builder.Services.AddHostedService<StockUpdateConsumer>();
builder.Services.AddHostedService<StockOrderApprovedConsumer>();

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Veritabanı Migrasyonu
using (var scope = app.Services.CreateScope())
{
    try
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Veritabanı migrasyonları çalıştırılıyor...");

        var dbContext = scope.ServiceProvider.GetRequiredService<StockDbContext>();

        dbContext.Database.Migrate();

        logger.LogInformation("Veritabanı migrasyonları başarıyla tamamlandı");

        var productCount = dbContext.Products.Count();
        logger.LogInformation("Mevcut ürün sayısı: {Count}", productCount);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Veritabanı migrasyonu başarısız oldu: {Message}", ex.Message);
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// CORS Middleware'ini etkinleştirin
app.UseCors(MyAllowSpecificOrigins);

app.MapControllers();
app.Run();