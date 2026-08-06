using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wakeel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeProfileDepartmentForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_EMPLOYEE_PROFILE_DepartmentId",
                table: "EMPLOYEE_PROFILE",
                column: "DepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_EMPLOYEE_PROFILE_DEPARTMENT_DepartmentId",
                table: "EMPLOYEE_PROFILE",
                column: "DepartmentId",
                principalTable: "DEPARTMENT",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EMPLOYEE_PROFILE_DEPARTMENT_DepartmentId",
                table: "EMPLOYEE_PROFILE");

            migrationBuilder.DropIndex(
                name: "IX_EMPLOYEE_PROFILE_DepartmentId",
                table: "EMPLOYEE_PROFILE");
        }
    }
}
