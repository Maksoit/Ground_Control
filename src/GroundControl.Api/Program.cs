using GroundControl.Core.Interfaces;
using GroundControl.Core.Services;
using GroundControl.Infrastructure.Data;
using GroundControl.Infrastructure.Kafka;
using GroundControl.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=ground_db;Port=5432;Database=ground;Username=postgres;Password=postgres";

builder.Services.AddDbContext<GroundControlDbContext>(options =>
    options.UseNpgsql(connectionString));

// Services
builder.Services.AddScoped<IPathfinder, DijkstraPathfinder>();
builder.Services.AddScoped<IRouteService, RouteService>();

// Kafka Consumer (optional - can be disabled via config)
var enableKafka = builder.Configuration.GetValue<bool>("Kafka:Enabled", true);
if (enableKafka)
{
    var bootstrapServers = builder.Configuration.GetValue<string>("Kafka:BootstrapServers") ?? "kafka:9092";
    var groupId = builder.Configuration.GetValue<string>("Kafka:GroupId") ?? "ground-service";

    builder.Services.AddSingleton<SimulationTickConsumer>(sp =>
        new SimulationTickConsumer(sp, sp.GetRequiredService<ILogger<SimulationTickConsumer>>(), bootstrapServers, groupId));
    builder.Services.AddHostedService(sp => sp.GetRequiredService<SimulationTickConsumer>());
}

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

// Ensure database is created and seeded
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GroundControlDbContext>();
    try
    {
        db.Database.Migrate();
        Log.Information("Database migration completed successfully");

        // Seed initial data
        var seeder = new GroundControl.Infrastructure.Data.DbSeeder(
            db,
            scope.ServiceProvider.GetRequiredService<ILogger<GroundControl.Infrastructure.Data.DbSeeder>>());
        await seeder.SeedAsync();
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error during database migration or seeding");
    }
}

Log.Information("Ground Control service starting on port 8000");

app.Run();