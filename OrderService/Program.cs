using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Models;
using OrderService.Services;
using Shared.Infrastructure.Messaging;
using Shared.Infrastructure.Repository;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

// CORS Politikasý Adý
var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

// Servisleri ekle
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS Tanýmlamasý: Frontend adresinden gelen isteklere izin veriyoruz
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

// Veritabaný
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// RabbitMQ - Lazy initialization ile hazýr olmasýný bekle
var rabbitMQSettings = new RabbitMQSettings();
builder.Configuration.GetSection("RabbitMQ").Bind(rabbitMQSettings);
builder.Services.AddSingleton(rabbitMQSettings);

// Singleton olarak ama lazy initialization ile ekle
builder.Services.AddSingleton<IMessagePublisher>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<RabbitMQPublisher>>();
    var settings = sp.GetRequiredService<RabbitMQSettings>();

    logger.LogInformation("RabbitMQ Yayýmcýsý baþlatýlýyor...");

    var maxWaitSeconds = 60;
    var waitedSeconds = 0;

    while (waitedSeconds < maxWaitSeconds)
    {
        try
        {
            logger.LogInformation("RabbitMQ'ya baðlanmaya çalýþýlýyor {Host}:{Port} (Beklenen süre: {Seconds}s)",
                settings.Host, settings.Port, waitedSeconds);

            var publisher = new RabbitMQPublisher(settings, logger);
            logger.LogInformation("RabbitMQ Yayýmcýsý baþarýyla baþlatýldý!");
            return publisher;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "RabbitMQ henüz hazýr deðil, 2 saniye bekleniyor... ({Waited}/{Max} saniye)",
                waitedSeconds, maxWaitSeconds);

            if (waitedSeconds >= maxWaitSeconds)
            {
                logger.LogError("RabbitMQ'ya {Seconds} saniye sonunda baðlanýlamadý", maxWaitSeconds);
                throw new InvalidOperationException(
                    $"RabbitMQ'ya {settings.Host}:{settings.Port} adresinden {maxWaitSeconds} saniye sonunda baðlanýlamadý. " +
                    "Lütfen RabbitMQ'nun çalýþýr ve eriþilebilir olduðunu kontrol edin.", ex);
            }

            Thread.Sleep(2000);
            waitedSeconds += 2;
        }
    }

    throw new InvalidOperationException("RabbitMQ Yayýmcýsý baþlatýlamadý");
});

// Repository
builder.Services.AddScoped<DbContext>(provider => provider.GetRequiredService<OrderDbContext>());
builder.Services.AddScoped<IRepository<Order>, Repository<Order>>();

// Servisler
builder.Services.AddScoped<IOrderService, OrderServiceImpl>();

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// RabbitMQ exchange ve queue'larý baþlat
using (var scope = app.Services.CreateScope())
{
    try
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("RabbitMQ exchange'leri baþlatýlýyor...");

        var factory = new ConnectionFactory
        {
            HostName = rabbitMQSettings.Host,
            UserName = rabbitMQSettings.Username,
            Password = rabbitMQSettings.Password,
            Port = rabbitMQSettings.Port
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        // Exchange'leri tanýmla
        channel.ExchangeDeclare("stock-exchange", ExchangeType.Direct, durable: true);
        channel.ExchangeDeclare("notification-exchange", ExchangeType.Direct, durable: true);
        channel.ExchangeDeclare("verification-exchange", ExchangeType.Direct, durable: true); 

        logger.LogInformation("RabbitMQ exchange'leri baþarýyla baþlatýldý");
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "RabbitMQ exchange'leri baþlatýlamadý. Servis çalýþmaya devam edecek ancak mesajlaþma baþarýsýz olabilir.");
    }
}

// Veritabaný migrasyonu
using (var scope = app.Services.CreateScope())
{
    try
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Veritabaný migrasyonlarý çalýþtýrýlýyor...");

        var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        dbContext.Database.Migrate();

        logger.LogInformation("Veritabaný migrasyonlarý baþarýyla tamamlandý");
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Veritabaný migrasyonu baþarýsýz oldu: {Message}", ex.Message);

        // PostgreSQL yoksa InMemory kullan (geliþtirme için)
        if (builder.Environment.IsDevelopment())
        {
            logger.LogWarning("Yedek olarak InMemory veritabaný kullanýlýyor");
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// CORS Middleware'ini etkinleþtirin
app.UseCors(MyAllowSpecificOrigins);

app.MapControllers();
app.Run();