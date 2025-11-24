using Microsoft.EntityFrameworkCore;
using Shared.Infrastructure.Messaging;
using Shared.Infrastructure.Repository;
using VerificationService.Consumers;
using VerificationService.Data;
using VerificationService.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("_myAllowSpecificOrigins",
        policy => policy.WithOrigins("http://localhost:3001").AllowAnyHeader().AllowAnyMethod());
});

// Database
builder.Services.AddDbContext<VerificationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Repository
builder.Services.AddScoped<DbContext>(provider => provider.GetRequiredService<VerificationDbContext>());
builder.Services.AddScoped<IRepository<Verification>, Repository<Verification>>();

// RabbitMQ
var rabbitMQSettings = new RabbitMQSettings();
builder.Configuration.GetSection("RabbitMQ").Bind(rabbitMQSettings);
builder.Services.AddSingleton(rabbitMQSettings);
builder.Services.AddSingleton<IMessagePublisher, RabbitMQPublisher>();

// Consumer
builder.Services.AddHostedService<VerificationRequestConsumer>();

var app = builder.Build();

// Migration
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<VerificationDbContext>();
    dbContext.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("_myAllowSpecificOrigins");
app.MapControllers();
app.Run();