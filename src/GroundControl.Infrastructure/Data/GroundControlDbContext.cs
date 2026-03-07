using GroundControl.Core.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GroundControl.Infrastructure.Data;

public class GroundControlDbContext : DbContext
{
    public GroundControlDbContext(DbContextOptions<GroundControlDbContext> options)
        : base(options)
    {
    }

    public DbSet<Node> Nodes { get; set; }
    public DbSet<Edge> Edges { get; set; }
    public DbSet<Route> Routes { get; set; }
    public DbSet<EdgeOccupancy> EdgeOccupancy { get; set; }
    public DbSet<ProcessedEvent> ProcessedEvents { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Node configuration
        modelBuilder.Entity<Node>(entity =>
        {
            entity.ToTable("nodes");
            entity.HasKey(e => e.NodeId);
            entity.Property(e => e.NodeId).HasColumnName("node_id");
            entity.Property(e => e.X).HasColumnName("x").HasColumnType("numeric");
            entity.Property(e => e.Y).HasColumnName("y").HasColumnType("numeric");
            entity.Property(e => e.NodeType).HasColumnName("node_type");
        });

        // Edge configuration
        modelBuilder.Entity<Edge>(entity =>
        {
            entity.ToTable("edges");
            entity.HasKey(e => e.EdgeId);
            entity.Property(e => e.EdgeId).HasColumnName("edge_id");
            entity.Property(e => e.FromNode).HasColumnName("from_node");
            entity.Property(e => e.ToNode).HasColumnName("to_node");
            entity.Property(e => e.Length).HasColumnName("length").HasColumnType("numeric").HasDefaultValue(1);

            entity.HasIndex(e => e.FromNode).HasDatabaseName("edges_from_idx");
            entity.HasIndex(e => e.ToNode).HasDatabaseName("edges_to_idx");
        });

        // Route configuration
        modelBuilder.Entity<Route>(entity =>
        {
            entity.ToTable("routes");
            entity.HasKey(e => e.RouteId);
            entity.Property(e => e.RouteId).HasColumnName("route_id");
            entity.Property(e => e.VehicleId).HasColumnName("vehicle_id");
            entity.Property(e => e.VehicleType)
                .HasColumnName("vehicle_type")
                .HasConversion(
                    v => v.ToString().ToLower(),
                    v => Enum.Parse<VehicleType>(v, true));
            entity.Property(e => e.FromNode).HasColumnName("from_node");
            entity.Property(e => e.ToNode).HasColumnName("to_node");
            entity.Property(e => e.EdgesPath)
                .HasColumnName("edges_path")
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<EdgePathItem>>(v, (JsonSerializerOptions?)null) ?? new List<EdgePathItem>());
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasConversion(
                    v => v.ToString().ToLower(),
                    v => Enum.Parse<RouteStatus>(v, true));
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.Property(e => e.TtlRemainingMinutes).HasColumnName("ttl_remaining_minutes");

            entity.Ignore(e => e.ExpiresAt);

            entity.HasIndex(e => e.VehicleId).HasDatabaseName("routes_vehicle_idx");
        });

        // EdgeOccupancy configuration
        modelBuilder.Entity<EdgeOccupancy>(entity =>
        {
            entity.ToTable("edge_occupancy");
            entity.HasKey(e => e.EdgeId);
            entity.Property(e => e.EdgeId).HasColumnName("edge_id");
            entity.Property(e => e.OccupiedBy).HasColumnName("occupied_by");
            entity.Property(e => e.RouteId).HasColumnName("route_id");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        });

        // ProcessedEvent configuration
        modelBuilder.Entity<ProcessedEvent>(entity =>
        {
            entity.ToTable("processed_events");
            entity.HasKey(e => e.EventId);
            entity.Property(e => e.EventId).HasColumnName("event_id");
            entity.Property(e => e.ProcessedAt).HasColumnName("processed_at").HasDefaultValueSql("now()");
        });
    }
}