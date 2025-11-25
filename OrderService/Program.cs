using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.Models;
using OrderService.Services;
using Shared.Infrastructure.Messaging;
using Shared.Infrastructure.Repository;
using RabbitMQ.Client;
using OrderService.Consumers;

var builder = WebApplication.CreateBuilder(args);

// CORS Politikas� Ad�
var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

// Servisleri ekle
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS Tan�mlamas�: Frontend adresinden gelen isteklere izin veriyoruz
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

// Veritaban�
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// RabbitMQ - Lazy initialization ile haz�r olmas�n� bekle
var rabbitMQSettings = new RabbitMQSettings();
builder.Configuration.GetSection("RabbitMQ").Bind(rabbitMQSettings);
builder.Services.AddSingleton(rabbitMQSettings);

// Singleton olarak ama lazy initialization ile ekle
builder.Services.AddSingleton<IMessagePublisher>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<RabbitMQPublisher>>();
    var settings = sp.GetRequiredService<RabbitMQSettings>();

    logger.LogInformation("RabbitMQ Yay�mc�s� ba�lat�l�yor...");

    var maxWaitSeconds = 60;
    var waitedSeconds = 0;

    while (waitedSeconds < maxWaitSeconds)
    {
        try
        {
            logger.LogInformation("RabbitMQ'ya ba�lanmaya �al���l�yor {Host}:{Port} (Beklenen s�re: {Seconds}s)",
                settings.Host, settings.Port, waitedSeconds);

            var publisher = new RabbitMQPublisher(settings, logger);
            logger.LogInformation("RabbitMQ Yay�mc�s� ba�ar�yla ba�lat�ld�!");
            return publisher;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "RabbitMQ hen�z haz�r de�il, 2 saniye bekleniyor... ({Waited}/{Max} saniye)",
                waitedSeconds, maxWaitSeconds);

            if (waitedSeconds >= maxWaitSeconds)
            {
                logger.LogError("RabbitMQ'ya {Seconds} saniye sonunda ba�lan�lamad�", maxWaitSeconds);
                throw new InvalidOperationException(
                    $"RabbitMQ'ya {settings.Host}:{settings.Port} adresinden {maxWaitSeconds} saniye sonunda ba�lan�lamad�. " +
                    "L�tfen RabbitMQ'nun �al���r ve eri�ilebilir oldu�unu kontrol edin.", ex);
            }

            Thread.Sleep(2000);
            waitedSeconds += 2;
        }
    }

    throw new InvalidOperationException("RabbitMQ Yay�mc�s� ba�lat�lamad�");
});

// Repository
builder.Services.AddScoped<DbContext>(provider => provider.GetRequiredService<OrderDbContext>());
builder.Services.AddScoped<IRepository<Order>, Repository<Order>>();

// HttpClient
builder.Services.AddHttpClient();

// Servisler
builder.Services.AddScoped<IOrderService, OrderServiceImpl>();
builder.Services.AddHostedService<OrderApprovedConsumer>();

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// RabbitMQ exchange ve queue'lar� ba�lat
using (var scope = app.Services.CreateScope())
{
    try
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("RabbitMQ exchange'leri ba�lat�l�yor...");

        var factory = new ConnectionFactory
        {
            HostName = rabbitMQSettings.Host,
            UserName = rabbitMQSettings.Username,
            Password = rabbitMQSettings.Password,
            Port = rabbitMQSettings.Port
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        // Exchange'leri tan�mla
        channel.ExchangeDeclare("stock-exchange", ExchangeType.Direct, durable: true);
        channel.ExchangeDeclare("notification-exchange", ExchangeType.Direct, durable: true);
        channel.ExchangeDeclare("verification-exchange", ExchangeType.Direct, durable: true); 

        logger.LogInformation("RabbitMQ exchange'leri ba�ar�yla ba�lat�ld�");
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "RabbitMQ exchange'leri ba�lat�lamad�. Servis �al��maya devam edecek ancak mesajla�ma ba�ar�s�z olabilir.");
    }
}

// Veritaban� migrasyonu
using (var scope = app.Services.CreateScope())
{
    try
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Veritaban� migrasyonlar� �al��t�r�l�yor...");

        var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        dbContext.Database.Migrate();

        logger.LogInformation("Veritaban� migrasyonlar� ba�ar�yla tamamland�");
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Veritaban� migrasyonu ba�ar�s�z oldu: {Message}", ex.Message);

        // PostgreSQL yoksa InMemory kullan (geli�tirme i�in)
        if (builder.Environment.IsDevelopment())
        {
            logger.LogWarning("Yedek olarak InMemory veritaban� kullan�l�yor");
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// CORS Middleware'ini etkinle�tirin
app.UseCors(MyAllowSpecificOrigins);

app.MapControllers();
app.Run();