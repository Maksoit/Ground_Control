using GroundControl.Api.Data;
using GroundControl.Api.Kafka;
using GroundControl.Api.Models;
using GroundControl.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GroundControl.Tests;

public class RouteServiceTests
{
    private static GroundDbContext CreateInMemoryDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<GroundDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var db = new GroundDbContext(options);

        // Seed minimal graph: A -> B -> C (unidirectional for simplicity)
        db.Nodes.AddRange(
            new NodeEntity { NodeId = "A", X = 0, Y = 0 },
            new NodeEntity { NodeId = "B", X = 10, Y = 0 },
            new NodeEntity { NodeId = "C", X = 20, Y = 0 }
        );
        db.Edges.AddRange(
            new EdgeEntity { EdgeId = "E-AB", FromNode = "A", ToNode = "B", Length = 10 },
            new EdgeEntity { EdgeId = "E-BC", FromNode = "B", ToNode = "C", Length = 10 }
        );
        db.SaveChanges();
        return db;
    }

    private static RouteService CreateService(GroundDbContext db)
        => new(db, new PathfinderService(), new NullKafkaProducer(),
            NullLogger<RouteService>.Instance);

    [Fact]
    public async Task Reserve_NewRoute_ReturnsCreatedAndAllocated()
    {
        var db = CreateInMemoryDb(nameof(Reserve_NewRoute_ReturnsCreatedAndAllocated));
        var svc = CreateService(db);

        var req = new ReserveRouteRequest
        {
            ReservationId = Guid.NewGuid(),
            VehicleId = "PL-1",
            VehicleType = VehicleType.plane,
            FromNode = "A",
            ToNode = "C",
            TtlMinutes = 10,
        };

        var (route, created) = await svc.ReserveAsync(req);

        Assert.True(created);
        Assert.Equal(RouteStatus.allocated, route.Status);
        Assert.Equal(2, route.EdgesPath.Count);
    }

    [Fact]
    public async Task Reserve_Idempotent_ReturnsSameRoute()
    {
        var db = CreateInMemoryDb(nameof(Reserve_Idempotent_ReturnsSameRoute));
        var svc = CreateService(db);
        var reservationId = Guid.NewGuid();

        var req = new ReserveRouteRequest
        {
            ReservationId = reservationId,
            VehicleId = "PL-1",
            VehicleType = VehicleType.plane,
            FromNode = "A",
            ToNode = "C",
            TtlMinutes = 10,
        };

        var (route1, created1) = await svc.ReserveAsync(req);
        var (route2, created2) = await svc.ReserveAsync(req);

        Assert.True(created1);
        Assert.False(created2);
        Assert.Equal(route1.RouteId, route2.RouteId);
    }

    [Fact]
    public async Task Reserve_ConflictingEdges_ThrowsRouteConflictException()
    {
        var db = CreateInMemoryDb(nameof(Reserve_ConflictingEdges_ThrowsRouteConflictException));
        var svc = CreateService(db);

        var req1 = new ReserveRouteRequest
        {
            ReservationId = Guid.NewGuid(),
            VehicleId = "PL-1",
            VehicleType = VehicleType.plane,
            FromNode = "A",
            ToNode = "C",
            TtlMinutes = 10,
        };

        await svc.ReserveAsync(req1);

        var req2 = new ReserveRouteRequest
        {
            ReservationId = Guid.NewGuid(),
            VehicleId = "BUS-1",
            VehicleType = VehicleType.bus,
            FromNode = "A",
            ToNode = "B",
            TtlMinutes = 5,
        };

        await Assert.ThrowsAsync<RouteConflictException>(() => svc.ReserveAsync(req2));
    }

    [Fact]
    public async Task Release_ExistingRoute_FreesEdges()
    {
        var db = CreateInMemoryDb(nameof(Release_ExistingRoute_FreesEdges));
        var svc = CreateService(db);

        var req = new ReserveRouteRequest
        {
            ReservationId = Guid.NewGuid(),
            VehicleId = "PL-1",
            VehicleType = VehicleType.plane,
            FromNode = "A",
            ToNode = "C",
            TtlMinutes = 10,
        };

        var (route, _) = await svc.ReserveAsync(req);
        var release = await svc.ReleaseAsync(route.RouteId);

        Assert.True(release.Released);
        Assert.Empty(await db.EdgeOccupancies.ToListAsync());

        var dbRoute = await db.Routes.FindAsync(route.RouteId);
        Assert.Equal(RouteStatus.finished, dbRoute!.Status);
    }

    [Fact]
    public async Task Release_UnknownRoute_ReturnsFalse()
    {
        var db = CreateInMemoryDb(nameof(Release_UnknownRoute_ReturnsFalse));
        var svc = CreateService(db);

        var result = await svc.ReleaseAsync(Guid.NewGuid());

        Assert.False(result.Released);
    }

    [Fact]
    public async Task Release_AlreadyFinished_ReturnsTrue()
    {
        var db = CreateInMemoryDb(nameof(Release_AlreadyFinished_ReturnsTrue));
        var svc = CreateService(db);

        var req = new ReserveRouteRequest
        {
            ReservationId = Guid.NewGuid(),
            VehicleId = "PL-1",
            VehicleType = VehicleType.plane,
            FromNode = "A",
            ToNode = "C",
            TtlMinutes = 10,
        };

        var (route, _) = await svc.ReserveAsync(req);
        await svc.ReleaseAsync(route.RouteId);

        // Release again
        var result = await svc.ReleaseAsync(route.RouteId);
        Assert.True(result.Released);
    }

    [Fact]
    public async Task ExpireByTick_ReducesTtl()
    {
        var db = CreateInMemoryDb(nameof(ExpireByTick_ReducesTtl));
        var svc = CreateService(db);

        var req = new ReserveRouteRequest
        {
            ReservationId = Guid.NewGuid(),
            VehicleId = "PL-1",
            VehicleType = VehicleType.plane,
            FromNode = "A",
            ToNode = "C",
            TtlMinutes = 10,
        };

        await svc.ReserveAsync(req);
        await svc.ExpireByTickAsync(3, "evt-1");

        var dbRoute = await db.Routes.FindAsync(req.ReservationId);
        Assert.Equal(7, dbRoute!.TtlRemainingMinutes);
        Assert.Equal(RouteStatus.allocated, dbRoute.Status);
    }

    [Fact]
    public async Task ExpireByTick_TtlExhausted_ExpiresRoute()
    {
        var db = CreateInMemoryDb(nameof(ExpireByTick_TtlExhausted_ExpiresRoute));
        var svc = CreateService(db);

        var req = new ReserveRouteRequest
        {
            ReservationId = Guid.NewGuid(),
            VehicleId = "PL-1",
            VehicleType = VehicleType.plane,
            FromNode = "A",
            ToNode = "C",
            TtlMinutes = 5,
        };

        await svc.ReserveAsync(req);
        await svc.ExpireByTickAsync(10, "evt-2");

        var dbRoute = await db.Routes.FindAsync(req.ReservationId);
        Assert.Equal(RouteStatus.finished, dbRoute!.Status);
        Assert.Empty(await db.EdgeOccupancies.ToListAsync());
    }

    [Fact]
    public async Task ExpireByTick_Deduplication_ProcessedTwice_OnlyExpiresOnce()
    {
        var db = CreateInMemoryDb(nameof(ExpireByTick_Deduplication_ProcessedTwice_OnlyExpiresOnce));
        var svc = CreateService(db);

        var req = new ReserveRouteRequest
        {
            ReservationId = Guid.NewGuid(),
            VehicleId = "PL-1",
            VehicleType = VehicleType.plane,
            FromNode = "A",
            ToNode = "C",
            TtlMinutes = 10,
        };

        await svc.ReserveAsync(req);
        await svc.ExpireByTickAsync(3, "dup-evt");
        await svc.ExpireByTickAsync(3, "dup-evt"); // same event, should be ignored

        var dbRoute = await db.Routes.FindAsync(req.ReservationId);
        // TTL should only be reduced once
        Assert.Equal(7, dbRoute!.TtlRemainingMinutes);
    }

    [Fact]
    public async Task GetOccupancy_ReturnsAllOccupiedEdges()
    {
        var db = CreateInMemoryDb(nameof(GetOccupancy_ReturnsAllOccupiedEdges));
        var svc = CreateService(db);

        var req = new ReserveRouteRequest
        {
            ReservationId = Guid.NewGuid(),
            VehicleId = "PL-1",
            VehicleType = VehicleType.plane,
            FromNode = "A",
            ToNode = "C",
            TtlMinutes = 10,
        };

        await svc.ReserveAsync(req);
        var occupancy = await svc.GetOccupancyAsync();

        Assert.Equal(2, occupancy.Count);
        Assert.All(occupancy, o => Assert.Equal("PL-1", o.OccupiedBy));
    }
}
