using System.Threading.Tasks;

namespace Wakeel.Application.Interfaces;

public interface IPdfGeneratorService
{
    Task<string> GeneratePdfFromHtmlAsync(string htmlContent, string documentTitle);
}
