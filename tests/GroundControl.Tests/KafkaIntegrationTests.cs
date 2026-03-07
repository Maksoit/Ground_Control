using GroundControl.Core.DTOs;
using GroundControl.Core.Interfaces;
using GroundControl.Infrastructure.Data;
using GroundControl.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using Xunit;

namespace GroundControl.Tests;

public class KafkaIntegrationTests : IDisposable
{
    private readonly GroundControlDbContext _context;
    private readonly IRouteService _routeService;
    private readonly IPathfinder _pathfinder;

    public KafkaIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<GroundControlDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GroundControlDbContext(options);
        _pathfinder = new Core.Services.DijkstraPathfinder();
        var logger = new Mock<ILogger<RouteService>>();
        _routeService = new RouteService(_context, _pathfinder, logger.Object);

        SeedTestData();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private void SeedTestData()
    {
        _context.Nodes.AddRange(
            new Core.Models.Node { NodeId = "T-1", X = 100, Y = 100, NodeType = "terminal" },
            new Core.Models.Node { NodeId = "P-1", X = 300, Y = 100, NodeType = "parking" },
            new Core.Models.Node { NodeId = "RW-1", X = 700, Y = 200, NodeType = "runway" },
            new Core.Models.Node { NodeId = "J-1", X = 200, Y = 100, NodeType = "junction" },
            new Core.Models.Node { NodeId = "J-4", X = 400, Y = 200, NodeType = "junction" }
        );

        _context.Edges.AddRange(
            new Core.Models.Edge { EdgeId = "E-T1-J1", FromNode = "T-1", ToNode = "J-1", Length = 100 },
            new Core.Models.Edge { EdgeId = "E-J1-P1", FromNode = "J-1", ToNode = "P-1", Length = 100 },
            new Core.Models.Edge { EdgeId = "E-P1-J4", FromNode = "P-1", ToNode = "J-4", Length = 100 },
            new Core.Models.Edge { EdgeId = "E-J4-RW1", FromNode = "J-4", ToNode = "RW-1", Length = 300 }
        );

        _context.SaveChanges();
    }

    [Fact]
    public async Task SimulationTick_ShouldDecreaseTTL_ButNotExpireRoute()
    {
        // Arrange - Reserve route with 10 minutes TTL
        var routeId = Guid.NewGuid();
        var request = new ReserveRouteRequest
        {
            ReservationId = routeId,
            VehicleId = "PL-1",
            VehicleType = "plane",
            FromNode = "T-1",
            ToNode = "RW-1",
            TtlMinutes = 10
        };

        await _routeService.ReserveRouteAsync(request);

        // Act - Simulate tick of 3 minutes
        var tickEventId = Guid.NewGuid();
        await _routeService.ProcessSimulationTickAsync(tickEventId, 3);

        // Assert - Route should still be allocated
        var route = await _context.Routes.FindAsync(routeId);
        route.Should().NotBeNull();
        route!.Status.Should().Be(Core.Models.RouteStatus.Allocated);
        route.TtlRemainingMinutes.Should().Be(7); // 10 - 3 = 7

        // Edges should still be occupied
        var occupancy = await _context.EdgeOccupancy.ToListAsync();
        occupancy.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SimulationTick_ShouldExpireRoute_WhenTTLExhausted()
    {
        // Arrange - Reserve route with 5 minutes TTL
        var routeId = Guid.NewGuid();
        var request = new ReserveRouteRequest
        {
            ReservationId = routeId,
            VehicleId = "PL-1",
            VehicleType = "plane",
            FromNode = "T-1",
            ToNode = "RW-1",
            TtlMinutes = 5
        };

        await _routeService.ReserveRouteAsync(request);

        // Act - Simulate tick of 6 minutes (more than TTL)
        var tickEventId = Guid.NewGuid();
        await _routeService.ProcessSimulationTickAsync(tickEventId, 6);

        // Assert - Route should be finished (expired)
        var route = await _context.Routes.FindAsync(routeId);
        route.Should().NotBeNull();
        route!.Status.Should().Be(Core.Models.RouteStatus.Finished);

        // Edges should be freed
        var occupancy = await _context.EdgeOccupancy.ToListAsync();
        occupancy.Should().BeEmpty();
    }

    [Fact]
    public async Task SimulationTick_ShouldBeIdempotent_WhenSameEventProcessedTwice()
    {
        // Arrange - Reserve route
        var routeId = Guid.NewGuid();
        var request = new ReserveRouteRequest
        {
            ReservationId = routeId,
            VehicleId = "PL-1",
            VehicleType = "plane",
            FromNode = "T-1",
            ToNode = "RW-1",
            TtlMinutes = 10
        };

        await _routeService.ReserveRouteAsync(request);

        var tickEventId = Guid.NewGuid();

        // Act - Process same tick event twice
        await _routeService.ProcessSimulationTickAsync(tickEventId, 3);
        await _routeService.ProcessSimulationTickAsync(tickEventId, 3);

        // Assert - TTL should decrease only once
        var route = await _context.Routes.FindAsync(routeId);
        route.Should().NotBeNull();
        route!.TtlRemainingMinutes.Should().Be(7); // 10 - 3 = 7 (not 10 - 3 - 3 = 4)

        // Event should be recorded only once
        var processedEvents = await _context.ProcessedEvents
            .Where(e => e.EventId == tickEventId)
            .ToListAsync();
        processedEvents.Should().HaveCount(1);
    }

    [Fact]
    public async Task SimulationTick_ShouldHandleMultipleRoutes_ExpiringSomeButNotAll()
    {
        // Arrange - Reserve two routes with different TTLs
        var route1Id = Guid.NewGuid();
        var route2Id = Guid.NewGuid();

        await _routeService.ReserveRouteAsync(new ReserveRouteRequest
        {
            ReservationId = route1Id,
            VehicleId = "PL-1",
            VehicleType = "plane",
            FromNode = "T-1",
            ToNode = "P-1",
            TtlMinutes = 3
        });

        // Need different path to avoid conflict
        _context.EdgeOccupancy.RemoveRange(_context.EdgeOccupancy);
        await _context.SaveChangesAsync();

        await _routeService.ReserveRouteAsync(new ReserveRouteRequest
        {
            ReservationId = route2Id,
            VehicleId = "PL-2",
            VehicleType = "plane",
            FromNode = "T-1",
            ToNode = "P-1",
            TtlMinutes = 10
        });

        // Act - Simulate tick of 5 minutes
        var tickEventId = Guid.NewGuid();
        await _routeService.ProcessSimulationTickAsync(tickEventId, 5);

        // Assert - First route should be expired
        var route1 = await _context.Routes.FindAsync(route1Id);
        route1!.Status.Should().Be(Core.Models.RouteStatus.Finished);

        // Second route should still be allocated
        var route2 = await _context.Routes.FindAsync(route2Id);
        route2!.Status.Should().Be(Core.Models.RouteStatus.Allocated);
        route2.TtlRemainingMinutes.Should().Be(5); // 10 - 5 = 5
    }

    [Fact]
    public async Task SimulationTick_ShouldNotAffectFinishedRoutes()
    {
        // Arrange - Reserve and then release route
        var routeId = Guid.NewGuid();
        var request = new ReserveRouteRequest
        {
            ReservationId = routeId,
            VehicleId = "PL-1",
            VehicleType = "plane",
            FromNode = "T-1",
            ToNode = "RW-1",
            TtlMinutes = 10
        };

        await _routeService.ReserveRouteAsync(request);
        await _routeService.ReleaseRouteAsync(routeId);

        var initialRoute = await _context.Routes.FindAsync(routeId);
        var initialUpdatedAt = initialRoute!.UpdatedAt;

        // Act - Simulate tick
        var tickEventId = Guid.NewGuid();
        await _routeService.ProcessSimulationTickAsync(tickEventId, 3);

        // Assert - Finished route should not be affected
        var route = await _context.Routes.FindAsync(routeId);
        route!.Status.Should().Be(Core.Models.RouteStatus.Finished);
        route.UpdatedAt.Should().Be(initialUpdatedAt); // Should not change
    }

    [Fact]
    public async Task SimulationTick_ShouldHandleMultipleTicksSequentially()
    {
        // Arrange - Reserve route with 10 minutes TTL
        var routeId = Guid.NewGuid();
        var request = new ReserveRouteRequest
        {
            ReservationId = routeId,
            VehicleId = "PL-1",
            VehicleType = "plane",
            FromNode = "T-1",
            ToNode = "RW-1",
            TtlMinutes = 10
        };

        await _routeService.ReserveRouteAsync(request);

        // Act - Simulate multiple ticks
        await _routeService.ProcessSimulationTickAsync(Guid.NewGuid(), 2); // 10 - 2 = 8
        await _routeService.ProcessSimulationTickAsync(Guid.NewGuid(), 3); // 8 - 3 = 5
        await _routeService.ProcessSimulationTickAsync(Guid.NewGuid(), 2); // 5 - 2 = 3

        // Assert - TTL should decrease correctly
        var route = await _context.Routes.FindAsync(routeId);
        route!.TtlRemainingMinutes.Should().Be(3);
        route.Status.Should().Be(Core.Models.RouteStatus.Allocated);

        // Act - One more tick to expire
        await _routeService.ProcessSimulationTickAsync(Guid.NewGuid(), 5); // 3 - 5 = -2 (expired)

        // Assert - Route should be expired
        route = await _context.Routes.FindAsync(routeId);
        route!.Status.Should().Be(Core.Models.RouteStatus.Finished);

        var occupancy = await _context.EdgeOccupancy.ToListAsync();
        occupancy.Should().BeEmpty();
    }
}