using Wakeel.Application.Interfaces;

namespace Wakeel.Tests.Integration;

public class FakeEmailSender : IEmailSender
{
    public List<SentEmail> SentEmails { get; } = new();

    public Task SendEmailAsync(
        string to,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        SentEmails.Add(new SentEmail(to, subject, body));

        return Task.CompletedTask;
    }
}

public record SentEmail(
    string To,
    string Subject,
    string Body);