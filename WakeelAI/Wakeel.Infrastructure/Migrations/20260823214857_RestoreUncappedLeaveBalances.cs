using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wakeel.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RestoreUncappedLeaveBalances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // FIX-01: reverses BackfillUnpaidLeaveBalanceDefaultCap. Unpaid leave has no
            // statutory quota and Sick leave has no fixed day quota under Egyptian Labour
            // Law No. 14/2025 (it is staged paid-through-insurance instead) - both are
            // uncapped, which this codebase represents as a NULL TotalDays. Only rows still
            // sitting at their old hardcoded default are touched, so a balance HR has since
            // genuinely capped (a non-default value, or one with days already used) is left
            // exactly as HR set it.
            migrationBuilder.Sql(
                "UPDATE [LEAVE_BALANCE] SET [TotalDays] = NULL WHERE [LeaveType] = 'Unpaid' AND [TotalDays] = 0;");
            migrationBuilder.Sql(
                "UPDATE [LEAVE_BALANCE] SET [TotalDays] = NULL WHERE [LeaveType] = 'Sick' AND [TotalDays] = 10 AND [UsedDays] = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [LEAVE_BALANCE] SET [TotalDays] = 0 WHERE [LeaveType] = 'Unpaid' AND [TotalDays] IS NULL;");
            migrationBuilder.Sql(
                "UPDATE [LEAVE_BALANCE] SET [TotalDays] = 10 WHERE [LeaveType] = 'Sick' AND [TotalDays] IS NULL AND [UsedDays] = 0;");
        }
    }
}
