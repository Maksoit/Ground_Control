using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GroundControl.Infrastructure.Data;

public class DbSeeder
{
    private readonly GroundControlDbContext _context;
    private readonly ILogger<DbSeeder> _logger;

    public DbSeeder(GroundControlDbContext context, ILogger<DbSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            // Проверяем, есть ли уже данные
            var nodesCount = await _context.Nodes.CountAsync();
            if (nodesCount > 0)
            {
                _logger.LogInformation("Database already seeded, skipping seed");
                return;
            }

            _logger.LogInformation("Starting database seeding...");

            // Загружаем SQL скрипт
            var seedSqlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "seed_data.sql");

            // Если файл не найден в базовой директории, пробуем относительный путь
            if (!File.Exists(seedSqlPath))
            {
                seedSqlPath = Path.Combine(Directory.GetCurrentDirectory(), "Docs", "seed_data.sql");
            }

            if (File.Exists(seedSqlPath))
            {
                var sql = await File.ReadAllTextAsync(seedSqlPath);
                await _context.Database.ExecuteSqlRawAsync(sql);
                _logger.LogInformation("Database seeded successfully from SQL file");
            }
            else
            {
                _logger.LogWarning("Seed SQL file not found at {Path}, seeding programmatically", seedSqlPath);
                await SeedProgrammatically();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding database");
            throw;
        }
    }

    private async Task SeedProgrammatically()
    {
        // Seed данные программно (на случай если SQL файл недоступен)
        var nodes = new[]
        {
            new Core.Models.Node { NodeId = "T-1", X = 100, Y = 100, NodeType = "terminal" },
            new Core.Models.Node { NodeId = "T-2", X = 100, Y = 300, NodeType = "terminal" },
            new Core.Models.Node { NodeId = "P-1", X = 300, Y = 100, NodeType = "parking" },
            new Core.Models.Node { NodeId = "P-2", X = 300, Y = 200, NodeType = "parking" },
            new Core.Models.Node { NodeId = "P-3", X = 300, Y = 300, NodeType = "parking" },
            new Core.Models.Node { NodeId = "RW-1", X = 700, Y = 200, NodeType = "runway" },
            new Core.Models.Node { NodeId = "J-1", X = 200, Y = 100, NodeType = "junction" },
            new Core.Models.Node { NodeId = "J-2", X = 200, Y = 200, NodeType = "junction" },
            new Core.Models.Node { NodeId = "J-3", X = 200, Y = 300, NodeType = "junction" },
            new Core.Models.Node { NodeId = "J-4", X = 400, Y = 200, NodeType = "junction" },
        };

        var edges = new[]
        {
            new Core.Models.Edge { EdgeId = "E-T1-J1", FromNode = "T-1", ToNode = "J-1", Length = 100 },
            new Core.Models.Edge { EdgeId = "E-J1-T1", FromNode = "J-1", ToNode = "T-1", Length = 100 },
            new Core.Models.Edge { EdgeId = "E-J1-P1", FromNode = "J-1", ToNode = "P-1", Length = 100 },
            new Core.Models.Edge { EdgeId = "E-P1-J1", FromNode = "P-1", ToNode = "J-1", Length = 100 },
            new Core.Models.Edge { EdgeId = "E-P1-J4", FromNode = "P-1", ToNode = "J-4", Length = 100 },
            new Core.Models.Edge { EdgeId = "E-J4-P1", FromNode = "J-4", ToNode = "P-1", Length = 100 },
            new Core.Models.Edge { EdgeId = "E-J4-RW1", FromNode = "J-4", ToNode = "RW-1", Length = 300 },
            new Core.Models.Edge { EdgeId = "E-RW1-J4", FromNode = "RW-1", ToNode = "J-4", Length = 300 },
        };

        _context.Nodes.AddRange(nodes);
        _context.Edges.AddRange(edges);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Database seeded programmatically");
    }
}