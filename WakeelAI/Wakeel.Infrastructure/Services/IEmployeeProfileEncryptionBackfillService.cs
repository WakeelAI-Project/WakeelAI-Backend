using System.Threading;
using System.Threading.Tasks;

namespace Wakeel.Infrastructure.Services;

/// <summary>
/// FIX-26, data step (between the two migrations): encrypts every EMPLOYEE_PROFILE row's
/// plaintext NationalId/Salary into the NationalIdEnc/SalaryEnc columns added by
/// AddEncryptedEmployeeProfileColumns. Must run - and be verified to have covered every
/// row - after that migration and before EncryptEmployeeProfileColumns (which drops the
/// plaintext columns). Invoked manually (see Program.cs's `--backfill-employee-encryption`
/// flag), never automatically, so it never races an automatic CI deploy that could apply
/// both migrations back-to-back with no chance for this step to run in between.
/// </summary>
public interface IEmployeeProfileEncryptionBackfillService
{
    /// <summary>
    /// Encrypts and writes NationalIdEnc/SalaryEnc for every row where SalaryEnc is still
    /// null. Safe to re-run - already-backfilled rows are skipped. Returns the number of
    /// rows encrypted in this run; never logs the values themselves.
    /// </summary>
    Task<int> BackfillAsync(CancellationToken cancellationToken = default);
}
