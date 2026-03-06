using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroundControl.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "edge_occupancy",
                columns: table => new
                {
                    edge_id = table.Column<string>(type: "text", nullable: false),
                    occupied_by = table.Column<string>(type: "text", nullable: false),
                    route_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_edge_occupancy", x => x.edge_id);
                });

            migrationBuilder.CreateTable(
                name: "edges",
                columns: table => new
                {
                    edge_id = table.Column<string>(type: "text", nullable: false),
                    from_node = table.Column<string>(type: "text", nullable: false),
                    to_node = table.Column<string>(type: "text", nullable: false),
                    length = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_edges", x => x.edge_id);
                });

            migrationBuilder.CreateTable(
                name: "nodes",
                columns: table => new
                {
                    node_id = table.Column<string>(type: "text", nullable: false),
                    x = table.Column<double>(type: "double precision", nullable: false),
                    y = table.Column<double>(type: "double precision", nullable: false),
                    node_type = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nodes", x => x.node_id);
                });

            migrationBuilder.CreateTable(
                name: "processed_events",
                columns: table => new
                {
                    event_id = table.Column<string>(type: "text", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processed_events", x => x.event_id);
                });

            migrationBuilder.CreateTable(
                name: "routes",
                columns: table => new
                {
                    route_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<string>(type: "text", nullable: false),
                    vehicle_type = table.Column<string>(type: "text", nullable: false),
                    from_node = table.Column<string>(type: "text", nullable: false),
                    to_node = table.Column<string>(type: "text", nullable: false),
                    edges_path_json = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ttl_remaining_minutes = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_routes", x => x.route_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "edge_occupancy");

            migrationBuilder.DropTable(
                name: "edges");

            migrationBuilder.DropTable(
                name: "nodes");

            migrationBuilder.DropTable(
                name: "processed_events");

            migrationBuilder.DropTable(
                name: "routes");
        }
    }
}
