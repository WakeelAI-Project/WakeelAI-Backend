using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wakeel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyHandbook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "COMPANY_HANDBOOK",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_COMPANY_HANDBOOK", x => x.Id);
                    table.ForeignKey(
                        name: "FK_COMPANY_HANDBOOK_COMPANY_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "COMPANY",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_COMPANY_HANDBOOK_USERS_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "USERS",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_COMPANY_HANDBOOK_CompanyId",
                table: "COMPANY_HANDBOOK",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_COMPANY_HANDBOOK_UploadedByUserId",
                table: "COMPANY_HANDBOOK",
                column: "UploadedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "COMPANY_HANDBOOK");
        }
    }
}
