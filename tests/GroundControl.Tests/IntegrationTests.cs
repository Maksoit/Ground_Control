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

public class IntegrationTests : IDisposable
{
    private readonly GroundControlDbContext _context;
    private readonly IRouteService _routeService;
    private readonly IPathfinder _pathfinder;

    public IntegrationTests()
    {
        var options = new DbContextOptionsBuilder<GroundControlDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GroundControlDbContext(options);
        _pathfinder = new Core.Services.DijkstraPathfinder();
        var logger = new Mock<ILogger<RouteService>>();
        _routeService = new RouteService(_context, _pathfinder, logger.Object);

        // Seed test data
        SeedTestData();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    private void SeedTestData()
    {
        // Терминалы
        _context.Nodes.AddRange(
            new Core.Models.Node { NodeId = "T-1", X = 100, Y = 100, NodeType = "terminal" },
            new Core.Models.Node { NodeId = "T-2", X = 100, Y = 300, NodeType = "terminal" }
        );

        // Стоянки
        _context.Nodes.AddRange(
            new Core.Models.Node { NodeId = "P-1", X = 300, Y = 100, NodeType = "parking" },
            new Core.Models.Node { NodeId = "P-2", X = 300, Y = 200, NodeType = "parking" },
            new Core.Models.Node { NodeId = "P-3", X = 300, Y = 300, NodeType = "parking" }
        );

        // Взлётная полоса
        _context.Nodes.Add(
            new Core.Models.Node { NodeId = "RW-1", X = 700, Y = 200, NodeType = "runway" }
        );

        // Точки пересечения
        _context.Nodes.AddRange(
            new Core.Models.Node { NodeId = "J-1", X = 200, Y = 100, NodeType = "junction" },
            new Core.Models.Node { NodeId = "J-2", X = 200, Y = 200, NodeType = "junction" },
            new Core.Models.Node { NodeId = "J-3", X = 200, Y = 300, NodeType = "junction" },
            new Core.Models.Node { NodeId = "J-4", X = 400, Y = 200, NodeType = "junction" }
        );

        // Рёбра (двусторонние дороги)
        _context.Edges.AddRange(
            // Терминал 1 <-> Junction 1
            new Core.Models.Edge { EdgeId = "E-T1-J1", FromNode = "T-1", ToNode = "J-1", Length = 100 },
            new Core.Models.Edge { EdgeId = "E-J1-T1", FromNode = "J-1", ToNode = "T-1", Length = 100 },

            // Junction 1 <-> Parking 1
            new Core.Models.Edge { EdgeId = "E-J1-P1", FromNode = "J-1", ToNode = "P-1", Length = 100 },
            new Core.Models.Edge { EdgeId = "E-P1-J1", FromNode = "P-1", ToNode = "J-1", Length = 100 },

            // Parking 1 <-> Junction 4
            new Core.Models.Edge { EdgeId = "E-P1-J4", FromNode = "P-1", ToNode = "J-4", Length = 100 },
            new Core.Models.Edge { EdgeId = "E-J4-P1", FromNode = "J-4", ToNode = "P-1", Length = 100 },

            // Junction 4 <-> Runway 1
            new Core.Models.Edge { EdgeId = "E-J4-RW1", FromNode = "J-4", ToNode = "RW-1", Length = 300 },
            new Core.Models.Edge { EdgeId = "E-RW1-J4", FromNode = "RW-1", ToNode = "J-4", Length = 300 },

            // Терминал 2 <-> Junction 3
            new Core.Models.Edge { EdgeId = "E-T2-J3", FromNode = "T-2", ToNode = "J-3", Length = 100 },
            new Core.Models.Edge { EdgeId = "E-J3-T2", FromNode = "J-3", ToNode = "T-2", Length = 100 },

            // Junction 3 <-> Parking 3
            new Core.Models.Edge { EdgeId = "E-J3-P3", FromNode = "J-3", ToNode = "P-3", Length = 100 },
            new Core.Models.Edge { EdgeId = "E-P3-J3", FromNode = "P-3", ToNode = "J-3", Length = 100 },

            // Parking 3 <-> Junction 4
            new Core.Models.Edge { EdgeId = "E-P3-J4", FromNode = "P-3", ToNode = "J-4", Length = 100 },
            new Core.Models.Edge { EdgeId = "E-J4-P3", FromNode = "J-4", ToNode = "P-3", Length = 100 }
        );

        _context.SaveChanges();
    }

    [Fact]
    public async Task Scenario_PlaneFromTerminalToRunway_ShouldReserveAndRelease()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var request = new ReserveRouteRequest
        {
            ReservationId = reservationId,
            VehicleId = "PL-1",
            VehicleType = "plane",
            FromNode = "T-1",
            ToNode = "RW-1",
            TtlMinutes = 10
        };

        // Act - Reserve route
        var route = await _routeService.ReserveRouteAsync(request);

        // Assert - Route created
        route.Should().NotBeNull();
        route.RouteId.Should().Be(reservationId);
        route.VehicleId.Should().Be("PL-1");
        route.Status.Should().Be("allocated");
        route.EdgesPath.Should().NotBeEmpty();

        // Assert - Edges are occupied
        var occupancy = await _context.EdgeOccupancy.ToListAsync();
        occupancy.Should().NotBeEmpty();
        occupancy.Should().AllSatisfy(o => o.OccupiedBy.Should().Be("PL-1"));

        // Act - Release route
        var releaseResult = await _routeService.ReleaseRouteAsync(reservationId);

        // Assert - Route released
        releaseResult.Released.Should().BeTrue();

        var occupancyAfterRelease = await _context.EdgeOccupancy.ToListAsync();
        occupancyAfterRelease.Should().BeEmpty();
    }

    [Fact]
    public async Task Scenario_TwoPlanes_ShouldConflictOnSamePath()
    {
        // Arrange
        var plane1Request = new ReserveRouteRequest
        {
            ReservationId = Guid.NewGuid(),
            VehicleId = "PL-1",
            VehicleType = "plane",
            FromNode = "T-1",
            ToNode = "RW-1",
            TtlMinutes = 10
        };

        var plane2Request = new ReserveRouteRequest
        {
            ReservationId = Guid.NewGuid(),
            VehicleId = "PL-2",
            VehicleType = "plane",
            FromNode = "T-1",
            ToNode = "RW-1",
            TtlMinutes = 10
        };

        // Act - First plane reserves
        var route1 = await _routeService.ReserveRouteAsync(plane1Request);
        route1.Should().NotBeNull();

        // Act & Assert - Second plane should fail
        await Assert.ThrowsAsync<RouteConflictException>(
            () => _routeService.ReserveRouteAsync(plane2Request));
    }

    [Fact]
    public async Task Scenario_TwoPlanes_DifferentPaths_ShouldBothSucceed()
    {
        // Arrange
        var plane1Request = new ReserveRouteRequest
        {
            ReservationId = Guid.NewGuid(),
            VehicleId = "PL-1",
            VehicleType = "plane",
            FromNode = "T-1",
            ToNode = "P-1",
            TtlMinutes = 10
        };

        var plane2Request = new ReserveRouteRequest
        {
            ReservationId = Guid.NewGuid(),
            VehicleId = "PL-2",
            VehicleType = "plane",
            FromNode = "T-2",
            ToNode = "P-3",
            TtlMinutes = 10
        };

        // Act
        var route1 = await _routeService.ReserveRouteAsync(plane1Request);
        var route2 = await _routeService.ReserveRouteAsync(plane2Request);

        // Assert
        route1.Should().NotBeNull();
        route2.Should().NotBeNull();
        route1.VehicleId.Should().Be("PL-1");
        route2.VehicleId.Should().Be("PL-2");

        var occupancy = await _context.EdgeOccupancy.ToListAsync();
        occupancy.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task Scenario_ReleaseAndReserveAgain_ShouldSucceed()
    {
        // Arrange
        var plane1Id = Guid.NewGuid();
        var plane1Request = new ReserveRouteRequest
        {
            ReservationId = plane1Id,
            VehicleId = "PL-1",
            VehicleType = "plane",
            FromNode = "T-1",
            ToNode = "RW-1",
            TtlMinutes = 10
        };

        var plane2Request = new ReserveRouteRequest
        {
            ReservationId = Guid.NewGuid(),
            VehicleId = "PL-2",
            VehicleType = "plane",
            FromNode = "T-1",
            ToNode = "RW-1",
            TtlMinutes = 10
        };

        // Act - First plane reserves
        await _routeService.ReserveRouteAsync(plane1Request);

        // Act - First plane releases
        await _routeService.ReleaseRouteAsync(plane1Id);

        // Act - Second plane reserves same path
        var route2 = await _routeService.ReserveRouteAsync(plane2Request);

        // Assert
        route2.Should().NotBeNull();
        route2.VehicleId.Should().Be("PL-2");
        route2.Status.Should().Be("allocated");
    }

    [Fact]
    public async Task Scenario_PathfindingFindsShortestPath()
    {
        // Arrange
        var request = new ReserveRouteRequest
        {
            ReservationId = Guid.NewGuid(),
            VehicleId = "PL-1",
            VehicleType = "plane",
            FromNode = "T-1",
            ToNode = "RW-1",
            TtlMinutes = 10
        };

        // Act
        var route = await _routeService.ReserveRouteAsync(request);

        // Assert - Path should be: T-1 -> J-1 -> P-1 -> J-4 -> RW-1
        route.EdgesPath.Should().HaveCount(4);
        route.EdgesPath[0].FromNode.Should().Be("T-1");
        route.EdgesPath[^1].ToNode.Should().Be("RW-1");
    }

    [Fact]
    public async Task Scenario_IdempotentReserve_ShouldReturnSameRoute()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var request = new ReserveRouteRequest
        {
            ReservationId = reservationId,
            VehicleId = "PL-1",
            VehicleType = "plane",
            FromNode = "T-1",
            ToNode = "P-1",
            TtlMinutes = 10
        };

        // Act - Reserve twice with same ID
        var route1 = await _routeService.ReserveRouteAsync(request);
        var route2 = await _routeService.ReserveRouteAsync(request);

        // Assert - Should return same route
        route1.RouteId.Should().Be(route2.RouteId);
        route1.VehicleId.Should().Be(route2.VehicleId);

        // Should not create duplicate occupancy
        var occupancy = await _context.EdgeOccupancy.ToListAsync();
        var uniqueEdges = occupancy.Select(o => o.EdgeId).Distinct().Count();
        occupancy.Count.Should().Be(uniqueEdges);
    }
}