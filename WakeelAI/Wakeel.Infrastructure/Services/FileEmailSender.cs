using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Wakeel.Application.Interfaces;

namespace Wakeel.Infrastructure.Services;

public class FileEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<FileEmailSender> _logger;
    private readonly string _outputDir;

    public FileEmailSender(IConfiguration configuration, ILogger<FileEmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _outputDir = Path.Combine(AppContext.BaseDirectory, "sent_emails");
        Directory.CreateDirectory(_outputDir);
    }

    public Task SendEmailAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var fileName = Path.Combine(_outputDir, $"email_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid()}.html");
        var sb = new StringBuilder();
        sb.AppendLine($"To: {to}");
        sb.AppendLine($"Subject: {subject}");
        sb.AppendLine("---");
        sb.AppendLine(htmlBody ?? string.Empty);

        File.WriteAllText(fileName, sb.ToString());
        _logger.LogInformation("Email written to file {Path}", fileName);
        return Task.CompletedTask;
    }
}
