using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wakeel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaveEntitlementTiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BaseDays",
                table: "LEAVE_ENTITLEMENT",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumServiceMonths",
                table: "LEAVE_ENTITLEMENT",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeniorDays",
                table: "LEAVE_ENTITLEMENT",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeniorityYears",
                table: "LEAVE_ENTITLEMENT",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StandardDays",
                table: "LEAVE_ENTITLEMENT",
                type: "int",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "LEAVE_ENTITLEMENT",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "BaseDays", "DefaultDays", "MinimumServiceMonths", "SeniorDays", "SeniorityYears", "StandardDays" },
                values: new object[] { 15, null, 6, 30, 10, 21 });

            migrationBuilder.UpdateData(
                table: "LEAVE_ENTITLEMENT",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                columns: new[] { "BaseDays", "DefaultDays", "MinimumServiceMonths", "SeniorDays", "SeniorityYears", "StandardDays" },
                values: new object[] { null, null, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "LEAVE_ENTITLEMENT",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                columns: new[] { "BaseDays", "MinimumServiceMonths", "SeniorDays", "SeniorityYears", "StandardDays" },
                values: new object[] { null, null, null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaseDays",
                table: "LEAVE_ENTITLEMENT");

            migrationBuilder.DropColumn(
                name: "MinimumServiceMonths",
                table: "LEAVE_ENTITLEMENT");

            migrationBuilder.DropColumn(
                name: "SeniorDays",
                table: "LEAVE_ENTITLEMENT");

            migrationBuilder.DropColumn(
                name: "SeniorityYears",
                table: "LEAVE_ENTITLEMENT");

            migrationBuilder.DropColumn(
                name: "StandardDays",
                table: "LEAVE_ENTITLEMENT");

            migrationBuilder.UpdateData(
                table: "LEAVE_ENTITLEMENT",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "DefaultDays",
                value: 15);

            migrationBuilder.UpdateData(
                table: "LEAVE_ENTITLEMENT",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "DefaultDays",
                value: 10);
        }
    }
}
