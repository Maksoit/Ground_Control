using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GroundControl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTtlRemainingMinutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ttl_remaining_minutes",
                table: "routes",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ttl_remaining_minutes",
                table: "routes");
        }
    }
}
