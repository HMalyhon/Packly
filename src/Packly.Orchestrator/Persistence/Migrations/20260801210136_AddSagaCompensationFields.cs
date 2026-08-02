using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Packly.Orchestrator.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSagaCompensationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "OrderState",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PaymentReference",
                table: "OrderState",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "OrderState");

            migrationBuilder.DropColumn(
                name: "PaymentReference",
                table: "OrderState");
        }
    }
}
