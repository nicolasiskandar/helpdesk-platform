using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TicketService.Infrastructure.Data;

#nullable disable

namespace TicketService.Infrastructure.Data.Migrations
{
    [DbContext(typeof(TicketDbContext))]
    [Migration("20260804000000_AddAttachmentSize")]
    /// <inheritdoc />
    public partial class AddAttachmentSize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Size",
                table: "TicketAttachments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Size",
                table: "TicketAttachments");
        }
    }
}
