using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMPortal.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailActionTokenToRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmailActionToken",
                table: "Requests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TokenExpiresAt",
                table: "Requests",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailActionToken",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "TokenExpiresAt",
                table: "Requests");
        }
    }
}
