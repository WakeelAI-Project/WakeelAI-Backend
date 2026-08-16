using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Wakeel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaveEntitlementTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LEAVE_ENTITLEMENT",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaveType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DefaultDays = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LEAVE_ENTITLEMENT", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "LEAVE_ENTITLEMENT",
                columns: new[] { "Id", "DefaultDays", "LeaveType" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), 15, "Annual" },
                    { new Guid("22222222-2222-2222-2222-222222222222"), 10, "Sick" },
                    { new Guid("33333333-3333-3333-3333-333333333333"), null, "Unpaid" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LEAVE_ENTITLEMENT");
        }
    }
}
