using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Wakeel.Infrastructure.Services;

/// <summary>
/// One seeded demo account: which company it belongs to, its role, and the credentials
/// needed to log in (the password is the same fixed demo password for every account).
/// </summary>
public sealed record DemoLoginInfo(string CompanyName, string Role, string Email, string Password);

public sealed record DemoDataResetResult(IReadOnlyList<DemoLoginInfo> Logins);

/// <summary>
/// Wipes every tenant-owned row in the database and replaces it with a small set of
/// realistic demo companies/employees/leave data. Deliberately a manual, explicit
/// operation (invoked via a CLI flag) rather than something that could run automatically -
/// see <see cref="DemoDataResetService"/>.
/// </summary>
public interface IDemoDataResetService
{
    Task<DemoDataResetResult> ResetAsync(CancellationToken cancellationToken = default);
}
