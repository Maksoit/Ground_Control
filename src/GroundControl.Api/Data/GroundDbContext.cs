using GroundControl.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GroundControl.Api.Data;

public class GroundDbContext : DbContext
{
    public GroundDbContext(DbContextOptions<GroundDbContext> options) : base(options) { }

    public DbSet<NodeEntity> Nodes => Set<NodeEntity>();
    public DbSet<EdgeEntity> Edges => Set<EdgeEntity>();
    public DbSet<RouteEntity> Routes => Set<RouteEntity>();
    public DbSet<EdgeOccupancy> EdgeOccupancies => Set<EdgeOccupancy>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<NodeEntity>(e =>
        {
            e.ToTable("nodes");
            e.HasKey(n => n.NodeId);
            e.Property(n => n.NodeId).HasColumnName("node_id");
            e.Property(n => n.X).HasColumnName("x");
            e.Property(n => n.Y).HasColumnName("y");
            e.Property(n => n.NodeType).HasColumnName("node_type");
        });

        modelBuilder.Entity<EdgeEntity>(e =>
        {
            e.ToTable("edges");
            e.HasKey(x => x.EdgeId);
            e.Property(x => x.EdgeId).HasColumnName("edge_id");
            e.Property(x => x.FromNode).HasColumnName("from_node");
            e.Property(x => x.ToNode).HasColumnName("to_node");
            e.Property(x => x.Length).HasColumnName("length");
        });

        modelBuilder.Entity<RouteEntity>(e =>
        {
            e.ToTable("routes");
            e.HasKey(r => r.RouteId);
            e.Property(r => r.RouteId).HasColumnName("route_id");
            e.Property(r => r.VehicleId).HasColumnName("vehicle_id");
            e.Property(r => r.VehicleType).HasColumnName("vehicle_type").HasConversion<string>();
            e.Property(r => r.FromNode).HasColumnName("from_node");
            e.Property(r => r.ToNode).HasColumnName("to_node");
            e.Property(r => r.EdgesPathJson).HasColumnName("edges_path_json").HasColumnType("text");
            e.Property(r => r.Status).HasColumnName("status").HasConversion<string>();
            e.Property(r => r.ExpiresAt).HasColumnName("expires_at");
            e.Property(r => r.TtlRemainingMinutes).HasColumnName("ttl_remaining_minutes");
            e.Property(r => r.CreatedAt).HasColumnName("created_at");
            e.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<EdgeOccupancy>(e =>
        {
            e.ToTable("edge_occupancy");
            e.HasKey(x => x.EdgeId);
            e.Property(x => x.EdgeId).HasColumnName("edge_id");
            e.Property(x => x.OccupiedBy).HasColumnName("occupied_by");
            e.Property(x => x.RouteId).HasColumnName("route_id");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<ProcessedEvent>(e =>
        {
            e.ToTable("processed_events");
            e.HasKey(x => x.EventId);
            e.Property(x => x.EventId).HasColumnName("event_id");
            e.Property(x => x.ProcessedAt).HasColumnName("processed_at");
        });
    }
}

/// <summary>Tracks processed Kafka event IDs for deduplication.</summary>
public class ProcessedEvent
{
    public string EventId { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
}
