using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Wakeel.Infrastructure.Security;

namespace Wakeel.Infrastructure.Services;

/// <inheritdoc cref="IEmployeeProfileEncryptionBackfillService" />
public class EmployeeProfileEncryptionBackfillService : IEmployeeProfileEncryptionBackfillService
{
    private readonly string _connectionString;
    private readonly IFieldEncryptionService _fieldEncryptionService;
    private readonly ILogger<EmployeeProfileEncryptionBackfillService> _logger;

    public EmployeeProfileEncryptionBackfillService(
        IConfiguration configuration,
        IFieldEncryptionService fieldEncryptionService,
        ILogger<EmployeeProfileEncryptionBackfillService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");
        _fieldEncryptionService = fieldEncryptionService ?? throw new ArgumentNullException(nameof(fieldEncryptionService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<int> BackfillAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Deliberately bypasses ApplicationDbContext/EF entirely: the EF model already maps
        // NationalId/Salary through the encryption converters (the post-cutover shape), so
        // reading "plaintext" NationalId/Salary here has to go around it via a raw
        // connection reading the *still-plaintext* pre-cutover columns directly.
        var rows = new List<(Guid UserId, decimal Salary, string? NationalId)>();
        await using (var selectCommand = new SqlCommand(
            "SELECT UserId, Salary, NationalId FROM EMPLOYEE_PROFILE WHERE SalaryEnc IS NULL",
            connection))
        await using (var reader = await selectCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add((
                    reader.GetGuid(0),
                    reader.GetDecimal(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2)));
            }
        }

        var backfilledCount = 0;
        foreach (var row in rows)
        {
            var salaryEnc = _fieldEncryptionService.Encrypt(row.Salary.ToString("G", CultureInfo.InvariantCulture));
            var nationalIdEnc = row.NationalId is null ? null : _fieldEncryptionService.Encrypt(row.NationalId);

            await using var updateCommand = new SqlCommand(
                "UPDATE EMPLOYEE_PROFILE SET SalaryEnc = @salaryEnc, NationalIdEnc = @nationalIdEnc WHERE UserId = @userId",
                connection);
            updateCommand.Parameters.AddWithValue("@salaryEnc", salaryEnc);
            updateCommand.Parameters.AddWithValue("@nationalIdEnc", (object?)nationalIdEnc ?? DBNull.Value);
            updateCommand.Parameters.AddWithValue("@userId", row.UserId);

            await updateCommand.ExecuteNonQueryAsync(cancellationToken);
            backfilledCount++;
        }

        // Count only - never the encrypted or plaintext values themselves.
        _logger.LogInformation("Employee profile encryption backfill: encrypted {Count} row(s).", backfilledCount);

        return backfilledCount;
    }
}
