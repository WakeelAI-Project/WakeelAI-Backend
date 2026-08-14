using System;
using System.IO;
using System.Threading.Tasks;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Wakeel.Application.Interfaces;

namespace Wakeel.Infrastructure.Services;

public class QuestPdfGeneratorService : IPdfGeneratorService
{
    public QuestPdfGeneratorService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<string> GeneratePdfFromHtmlAsync(string htmlContent, string documentTitle)
    {
        // For the sake of this MVP, we create a basic PDF rendering.
        // A real HTML-to-PDF with QuestPDF requires a specific HTML rendering library or parsing, 
        // but here we just render the content as text or simple structure for demonstration.
        // Note: A true production app would use an HTML-to-PDF tool like Puppeteer, wkhtmltopdf, or an API.
        
        var fileName = $"{Guid.NewGuid():N}.pdf";
        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "documents");
        
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        var filePath = Path.Combine(uploadsFolder, fileName);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(12));

                page.Header()
                    .Text(documentTitle)
                    .SemiBold().FontSize(20).FontColor(Colors.Blue.Darken2);

                page.Content()
                    .PaddingVertical(1, Unit.Centimetre)
                    .Column(x =>
                    {
                        // Fallback: render raw HTML content as plain text since QuestPDF doesn't natively parse HTML.
                        x.Item().Text(htmlContent);
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                    });
            });
        })
        .GeneratePdf(filePath);

        // Return a relative URL path (assuming serving from wwwroot)
        var fileUrl = $"/uploads/documents/{fileName}";
        return Task.FromResult(fileUrl);
    }
}
