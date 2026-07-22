using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TicketService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxRetryCountAndDLQ : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "OutboxMessages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxRetries",
                table: "OutboxMessages",
                type: "int",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedAt_RetryCount",
                table: "OutboxMessages",
                columns: new[] { "ProcessedAt", "RetryCount" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_ProcessedAt_RetryCount",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "MaxRetries",
                table: "OutboxMessages");
        }
    }
}
