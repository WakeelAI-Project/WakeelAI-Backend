using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wakeel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillUnpaidLeaveBalanceDefaultCap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Pre-existing employees still carry the old "uncapped" NULL TotalDays for
            // Unpaid leave; new employees now start at 0 (see EmployeeService.CreateEmployeeAsync).
            // Backfill so every employee's Unpaid balance is consistently a real, capped number.
            migrationBuilder.Sql(
                "UPDATE [LEAVE_BALANCE] SET [TotalDays] = 0 WHERE [LeaveType] = 'Unpaid' AND [TotalDays] IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [LEAVE_BALANCE] SET [TotalDays] = NULL WHERE [LeaveType] = 'Unpaid' AND [TotalDays] = 0 AND [UsedDays] = 0;");
        }
    }
}
