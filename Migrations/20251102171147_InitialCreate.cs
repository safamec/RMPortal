using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMPortal.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Requests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RequestNumber = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    EmploymentStatus = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    EmployeeNumberOrEmployer = table.Column<string>(type: "TEXT", nullable: true),
                    Title = table.Column<string>(type: "TEXT", nullable: true),
                    OfficeExtension = table.Column<string>(type: "TEXT", nullable: true),
                    Department = table.Column<string>(type: "TEXT", nullable: true),
                    Directorate = table.Column<string>(type: "TEXT", nullable: true),
                    LoginName = table.Column<string>(type: "TEXT", nullable: false),
                    UserMachineId = table.Column<string>(type: "TEXT", nullable: true),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Classification = table.Column<string>(type: "TEXT", nullable: false),
                    Justification = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBySam = table.Column<string>(type: "TEXT", nullable: false),
                    RequesterSignAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ManagerSignAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SecuritySignAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ITSignAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RequestDecisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MediaAccessRequestId = table.Column<int>(type: "INTEGER", nullable: false),
                    Stage = table.Column<string>(type: "TEXT", nullable: false),
                    Decision = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    DecidedBySam = table.Column<string>(type: "TEXT", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestDecisions_Requests_MediaAccessRequestId",
                        column: x => x.MediaAccessRequestId,
                        principalTable: "Requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RequestDecisions_MediaAccessRequestId",
                table: "RequestDecisions",
                column: "MediaAccessRequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RequestDecisions");

            migrationBuilder.DropTable(
                name: "Requests");
        }
    }
}
