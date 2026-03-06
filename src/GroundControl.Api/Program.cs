using GroundControl.Api.Data;
using GroundControl.Api.Kafka;
using GroundControl.Api.Services;
using GroundControl.Api.Workers;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=ground_db;Port=5432;Database=ground;Username=ground;Password=ground";

builder.Services.AddDbContext<GroundDbContext>(opt =>
    opt.UseNpgsql(connectionString));

// ── Kafka ─────────────────────────────────────────────────────────────────────
var kafkaBootstrap = builder.Configuration["Kafka:BootstrapServers"];
if (!string.IsNullOrWhiteSpace(kafkaBootstrap))
{
    builder.Services.AddSingleton<IKafkaProducer, KafkaProducer>();
    builder.Services.AddHostedService<SimTimeTickWorker>();
}
else
{
    builder.Services.AddSingleton<IKafkaProducer, NullKafkaProducer>();
}

// ── Application Services ──────────────────────────────────────────────────────
builder.Services.AddScoped<IPathfinderService, PathfinderService>();
builder.Services.AddScoped<IRouteService, RouteService>();

// ── ASP.NET ────────────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opt =>
    {
        opt.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        opt.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

var app = builder.Build();

// ── Migrate & Seed ─────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GroundDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db);
}

app.MapControllers();

app.Run();
