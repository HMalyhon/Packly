using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Packly.Orchestrator.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSagaOrderLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Lines",
                table: "OrderState",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Lines",
                table: "OrderState");
        }
    }
}
