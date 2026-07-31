using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TicketService.Infrastructure.Data;

#nullable disable

namespace TicketService.Infrastructure.Data.Migrations
{
    [DbContext(typeof(TicketDbContext))]
    [Migration("20260101000000_AddCommentThreadingAndTargeting")]
    /// <inheritdoc />
    public partial class AddCommentThreadingAndTargeting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentCommentId",
                table: "TicketComments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TicketCommentRecipients",
                columns: table => new
                {
                    CommentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketCommentRecipients", x => new { x.CommentId, x.RecipientUserId });
                    table.ForeignKey(
                        name: "FK_TicketCommentRecipients_TicketComments_CommentId",
                        column: x => x.CommentId,
                        principalTable: "TicketComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketComments_ParentCommentId",
                table: "TicketComments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketCommentRecipients_CommentId",
                table: "TicketCommentRecipients",
                column: "CommentId");

            migrationBuilder.AddForeignKey(
                name: "FK_TicketComments_TicketComments_ParentCommentId",
                table: "TicketComments",
                column: "ParentCommentId",
                principalTable: "TicketComments",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TicketComments_TicketComments_ParentCommentId",
                table: "TicketComments");

            migrationBuilder.DropIndex(
                name: "IX_TicketComments_ParentCommentId",
                table: "TicketComments");

            migrationBuilder.DropIndex(
                name: "IX_TicketCommentRecipients_CommentId",
                table: "TicketCommentRecipients");

            migrationBuilder.DropTable(
                name: "TicketCommentRecipients");

            migrationBuilder.DropColumn(
                name: "ParentCommentId",
                table: "TicketComments");
        }
    }
}
