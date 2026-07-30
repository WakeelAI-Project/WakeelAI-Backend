using System.Threading;
using System.Threading.Tasks;

namespace Wakeel.Application.Interfaces;

public interface IEmailSender
{
    Task SendEmailAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
