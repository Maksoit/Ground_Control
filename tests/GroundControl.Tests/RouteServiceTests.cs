using GroundControl.Core.DTOs;
using GroundControl.Core.Interfaces;
using GroundControl.Core.Models;
using GroundControl.Infrastructure.Data;
using GroundControl.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using Xunit;

namespace GroundControl.Tests;

public class RouteServiceTests : IDisposable
{
    private readonly GroundControlDbContext _context;
    private readonly Mock<IPathfinder> _pathfinderMock;
    private readonly Mock<ILogger<RouteService>> _loggerMock;
    private readonly RouteService _service;

    public RouteServiceTests()
    {
        var options = new DbContextOptionsBuilder<GroundControlDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GroundControlDbContext(options);
        _pathfinderMock = new Mock<IPathfinder>();
        _loggerMock = new Mock<ILogger<RouteService>>();
        _service = new RouteService(_context, _pathfinderMock.Object, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task ReserveRoute_ShouldCreateNewRoute_WhenPathIsAvailable()
    {
        // Arrange
        var edges = new List<Edge>
        {
            new Edge { EdgeId = "E1", FromNode = "A", ToNode = "B", Length = 1 }
        };
        _context.Edges.AddRange(edges);
        await _context.SaveChangesAsync();

        var path = new List<EdgePathItem>
        {
            new EdgePathItem { EdgeId = "E1", FromNode = "A", ToNode = "B" }
        };
        _pathfinderMock.Setup(p => p.FindPath("A", "B", It.IsAny<List<Edge>>()))
            .Returns(path);

        var request = new ReserveRouteRequest
        {
            ReservationId = Guid.NewGuid(),
            VehicleId = "V1",
            VehicleType = "plane",
            FromNode = "A",
            ToNode = "B",
            TtlMinutes = 10
        };

        // Act
        var result = await _service.ReserveRouteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.RouteId.Should().Be(request.ReservationId);
        result.VehicleId.Should().Be("V1");
        result.Status.Should().Be("allocated");

        var occupancy = await _context.EdgeOccupancy.ToListAsync();
        occupancy.Should().HaveCount(1);
        occupancy[0].EdgeId.Should().Be("E1");
    }

    [Fact]
    public async Task ReserveRoute_ShouldReturnExistingRoute_WhenReservationIdExists()
    {
        // Arrange
        var routeId = Guid.NewGuid();
        var existingRoute = new Route
        {
            RouteId = routeId,
            VehicleId = "V1",
            VehicleType = VehicleType.Plane,
            FromNode = "A",
            ToNode = "B",
            EdgesPath = new List<EdgePathItem>
            {
                new EdgePathItem { EdgeId = "E1", FromNode = "A", ToNode = "B" }
            },
            Status = RouteStatus.Allocated,
            TtlRemainingMinutes = 10,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        _context.Routes.Add(existingRoute);
        await _context.SaveChangesAsync();

        var request = new ReserveRouteRequest
        {
            ReservationId = routeId,
            VehicleId = "V1",
            VehicleType = "plane",
            FromNode = "A",
            ToNode = "B",
            TtlMinutes = 10
        };

        // Act
        var result = await _service.ReserveRouteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.RouteId.Should().Be(routeId);
        _pathfinderMock.Verify(p => p.FindPath(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<Edge>>()), Times.Never);
    }

    [Fact]
    public async Task ReserveRoute_ShouldThrowRouteConflictException_WhenEdgeIsOccupied()
    {
        // Arrange
        var edges = new List<Edge>
        {
            new Edge { EdgeId = "E1", FromNode = "A", ToNode = "B", Length = 1 }
        };
        _context.Edges.AddRange(edges);

        var occupancy = new EdgeOccupancy
        {
            EdgeId = "E1",
            OccupiedBy = "V2",
            RouteId = Guid.NewGuid(),
            UpdatedAt = DateTime.UtcNow
        };
        _context.EdgeOccupancy.Add(occupancy);
        await _context.SaveChangesAsync();

        var path = new List<EdgePathItem>
        {
            new EdgePathItem { EdgeId = "E1", FromNode = "A", ToNode = "B" }
        };
        _pathfinderMock.Setup(p => p.FindPath("A", "B", It.IsAny<List<Edge>>()))
            .Returns(path);

        var request = new ReserveRouteRequest
        {
            ReservationId = Guid.NewGuid(),
            VehicleId = "V1",
            VehicleType = "plane",
            FromNode = "A",
            ToNode = "B",
            TtlMinutes = 10
        };

        // Act & Assert
        await Assert.ThrowsAsync<RouteConflictException>(() => _service.ReserveRouteAsync(request));
    }

    [Fact]
    public async Task ReleaseRoute_ShouldFreeEdges_WhenRouteExists()
    {
        // Arrange
        var routeId = Guid.NewGuid();
        var route = new Route
        {
            RouteId = routeId,
            VehicleId = "V1",
            VehicleType = VehicleType.Plane,
            FromNode = "A",
            ToNode = "B",
            EdgesPath = new List<EdgePathItem>
            {
                new EdgePathItem { EdgeId = "E1", FromNode = "A", ToNode = "B" }
            },
            Status = RouteStatus.Allocated,
            TtlRemainingMinutes = 10,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Routes.Add(route);

        var occupancy = new EdgeOccupancy
        {
            EdgeId = "E1",
            OccupiedBy = "V1",
            RouteId = routeId,
            UpdatedAt = DateTime.UtcNow
        };
        _context.EdgeOccupancy.Add(occupancy);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ReleaseRouteAsync(routeId);

        // Assert
        result.Released.Should().BeTrue();
        
        var updatedRoute = await _context.Routes.FindAsync(routeId);
        updatedRoute!.Status.Should().Be(RouteStatus.Finished);

        var remainingOccupancy = await _context.EdgeOccupancy.ToListAsync();
        remainingOccupancy.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessSimulationTick_ShouldExpireRoutes_WhenTtlExhausted()
    {
        // Arrange
        var routeId = Guid.NewGuid();
        var route = new Route
        {
            RouteId = routeId,
            VehicleId = "V1",
            VehicleType = VehicleType.Plane,
            FromNode = "A",
            ToNode = "B",
            EdgesPath = new List<EdgePathItem>
            {
                new EdgePathItem { EdgeId = "E1", FromNode = "A", ToNode = "B" }
            },
            Status = RouteStatus.Allocated,
            TtlRemainingMinutes = 2,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Routes.Add(route);

        var occupancy = new EdgeOccupancy
        {
            EdgeId = "E1",
            OccupiedBy = "V1",
            RouteId = routeId,
            UpdatedAt = DateTime.UtcNow
        };
        _context.EdgeOccupancy.Add(occupancy);
        await _context.SaveChangesAsync();

        var eventId = Guid.NewGuid();

        // Act
        await _service.ProcessSimulationTickAsync(eventId, 3);

        // Assert
        var updatedRoute = await _context.Routes.FindAsync(routeId);
        updatedRoute!.Status.Should().Be(RouteStatus.Finished);

        var remainingOccupancy = await _context.EdgeOccupancy.ToListAsync();
        remainingOccupancy.Should().BeEmpty();

        var processedEvent = await _context.ProcessedEvents.FindAsync(eventId);
        processedEvent.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessSimulationTick_ShouldBeIdempotent()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var processedEvent = new ProcessedEvent
        {
            EventId = eventId,
            ProcessedAt = DateTime.UtcNow
        };
        _context.ProcessedEvents.Add(processedEvent);
        await _context.SaveChangesAsync();

        // Act
        await _service.ProcessSimulationTickAsync(eventId, 1);

        // Assert - should not throw and should not process again
        var events = await _context.ProcessedEvents.Where(e => e.EventId == eventId).ToListAsync();
        events.Should().HaveCount(1);
    }
}