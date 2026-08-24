using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wakeel.Infrastructure.Migrations
{
    /// <summary>
    /// FIX-26, step 1 of 2. Purely additive - adds the two columns the encryption backfill
    /// writes to, alongside the existing plaintext NationalId/Salary columns (untouched).
    /// Safe to deploy and run standalone. See EmployeeProfileEncryptionBackfillService for
    /// the backfill step, and EncryptEmployeeProfileColumns (the next migration) for the
    /// cutover that drops the plaintext columns - that one must only run after the backfill
    /// has been verified to have populated every row.
    /// </summary>
    public partial class AddEncryptedEmployeeProfileColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NationalIdEnc",
                table: "EMPLOYEE_PROFILE",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalaryEnc",
                table: "EMPLOYEE_PROFILE",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NationalIdEnc",
                table: "EMPLOYEE_PROFILE");

            migrationBuilder.DropColumn(
                name: "SalaryEnc",
                table: "EMPLOYEE_PROFILE");
        }
    }
}
