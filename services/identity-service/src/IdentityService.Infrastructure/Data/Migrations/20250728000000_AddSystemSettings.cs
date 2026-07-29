using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_Key",
                table: "SystemSettings",
                column: "Key",
                unique: true);

            migrationBuilder.InsertData(
                table: "SystemSettings",
                columns: new[] { "Id", "Key", "Value", "Description" },
                values: new object[,]
                {
                    { 1, "ticket_auto_close_days", "7", "Days after Pending Resolution before auto-close" },
                    { 2, "default_ticket_priority", "Medium", "Default priority for new tickets" },
                    { 3, "max_agent_active_tickets", "20", "Max open tickets an agent can hold" },
                    { 4, "sla_high_hours", "4", "SLA breach hours for High priority" },
                    { 5, "sla_critical_hours", "1", "SLA breach hours for Critical priority" },
                    { 6, "allow_employee_ticket_create", "true", "Whether employees can create tickets" },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SystemSettings");
        }
    }
}
