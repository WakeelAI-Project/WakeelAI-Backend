using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wakeel.Infrastructure.Migrations
{
    /// <summary>
    /// FIX-26, step 2 of 2 - the cutover to encrypted columns. Requires
    /// AddEncryptedEmployeeProfileColumns to have already run AND the encryption backfill
    /// (EmployeeProfileEncryptionBackfillService) to have populated every row's
    /// NationalIdEnc/SalaryEnc first - verify that before running this migration. It drops
    /// the old plaintext NationalId/Salary columns and renames the *Enc columns into their
    /// place, rather than an in-place AlterColumn, which would corrupt or destroy the
    /// existing data (Salary's type change alone, decimal -> nvarchar, is exactly the "single
    /// AlterColumn silently destroys every salary value" failure mode this two-step approach
    /// exists to avoid).
    /// </summary>
    public partial class EncryptEmployeeProfileColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NationalId",
                table: "EMPLOYEE_PROFILE");

            migrationBuilder.DropColumn(
                name: "Salary",
                table: "EMPLOYEE_PROFILE");

            migrationBuilder.RenameColumn(
                name: "NationalIdEnc",
                table: "EMPLOYEE_PROFILE",
                newName: "NationalId");

            migrationBuilder.RenameColumn(
                name: "SalaryEnc",
                table: "EMPLOYEE_PROFILE",
                newName: "Salary");

            // Every row's Salary must already be backfilled by this point (Salary, unlike
            // NationalId, is non-nullable) - this ALTER intentionally fails if any row was
            // missed, rather than silently leaving a NULL in a column the model declares
            // required.
            migrationBuilder.AlterColumn<string>(
                name: "Salary",
                table: "EMPLOYEE_PROFILE",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }

        /// <summary>
        /// Cannot restore plaintext NationalId/Salary - the encrypted values are all this
        /// migration has to work with, and decrypting them back into plaintext columns would
        /// require running application code (FieldEncryptionService) from inside a migration,
        /// which this Down() deliberately does not attempt. Restores the encrypted values
        /// under their original *Enc column names (the pre-cutover, still-decryptable state)
        /// rather than fabricating empty plaintext columns.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Salary",
                table: "EMPLOYEE_PROFILE",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.RenameColumn(
                name: "NationalId",
                table: "EMPLOYEE_PROFILE",
                newName: "NationalIdEnc");

            migrationBuilder.RenameColumn(
                name: "Salary",
                table: "EMPLOYEE_PROFILE",
                newName: "SalaryEnc");

            migrationBuilder.AddColumn<string>(
                name: "NationalId",
                table: "EMPLOYEE_PROFILE",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Salary",
                table: "EMPLOYEE_PROFILE",
                type: "decimal(18,2)",
                nullable: true);
        }
    }
}
