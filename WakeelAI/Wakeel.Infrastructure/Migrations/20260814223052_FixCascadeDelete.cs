using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wakeel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixCascadeDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_USERS_UserId",
                table: "AuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_GeneratedDocuments_DocumentTemplates_TemplateId",
                table: "GeneratedDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_GeneratedDocuments_USERS_GeneratedByUserId",
                table: "GeneratedDocuments");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_USERS_UserId",
                table: "AuditLogs",
                column: "UserId",
                principalTable: "USERS",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GeneratedDocuments_DocumentTemplates_TemplateId",
                table: "GeneratedDocuments",
                column: "TemplateId",
                principalTable: "DocumentTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GeneratedDocuments_USERS_GeneratedByUserId",
                table: "GeneratedDocuments",
                column: "GeneratedByUserId",
                principalTable: "USERS",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_USERS_UserId",
                table: "AuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_GeneratedDocuments_DocumentTemplates_TemplateId",
                table: "GeneratedDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_GeneratedDocuments_USERS_GeneratedByUserId",
                table: "GeneratedDocuments");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_USERS_UserId",
                table: "AuditLogs",
                column: "UserId",
                principalTable: "USERS",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GeneratedDocuments_DocumentTemplates_TemplateId",
                table: "GeneratedDocuments",
                column: "TemplateId",
                principalTable: "DocumentTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GeneratedDocuments_USERS_GeneratedByUserId",
                table: "GeneratedDocuments",
                column: "GeneratedByUserId",
                principalTable: "USERS",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
