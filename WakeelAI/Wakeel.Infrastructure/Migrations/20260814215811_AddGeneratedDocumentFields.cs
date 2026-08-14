using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wakeel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGeneratedDocumentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EmailSentAt",
                table: "GeneratedDocuments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailSentTo",
                table: "GeneratedDocuments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinalizedAt",
                table: "GeneratedDocuments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GeneratedByUserId",
                table: "GeneratedDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PdfUrl",
                table: "GeneratedDocuments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TemplateId",
                table: "GeneratedDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedDocuments_GeneratedByUserId",
                table: "GeneratedDocuments",
                column: "GeneratedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedDocuments_TemplateId",
                table: "GeneratedDocuments",
                column: "TemplateId");

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
                name: "FK_GeneratedDocuments_DocumentTemplates_TemplateId",
                table: "GeneratedDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_GeneratedDocuments_USERS_GeneratedByUserId",
                table: "GeneratedDocuments");

            migrationBuilder.DropIndex(
                name: "IX_GeneratedDocuments_GeneratedByUserId",
                table: "GeneratedDocuments");

            migrationBuilder.DropIndex(
                name: "IX_GeneratedDocuments_TemplateId",
                table: "GeneratedDocuments");

            migrationBuilder.DropColumn(
                name: "EmailSentAt",
                table: "GeneratedDocuments");

            migrationBuilder.DropColumn(
                name: "EmailSentTo",
                table: "GeneratedDocuments");

            migrationBuilder.DropColumn(
                name: "FinalizedAt",
                table: "GeneratedDocuments");

            migrationBuilder.DropColumn(
                name: "GeneratedByUserId",
                table: "GeneratedDocuments");

            migrationBuilder.DropColumn(
                name: "PdfUrl",
                table: "GeneratedDocuments");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "GeneratedDocuments");
        }
    }
}
