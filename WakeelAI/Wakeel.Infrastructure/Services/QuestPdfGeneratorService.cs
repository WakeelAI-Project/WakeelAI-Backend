using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Wakeel.Application.Interfaces;

namespace Wakeel.Infrastructure.Services;

public class QuestPdfGeneratorService : IPdfGeneratorService
{
    private const string ArabicFontFamily = "Amiri";
    private static readonly object FontLock = new();
    private static bool _fontsRegistered;

    public QuestPdfGeneratorService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        RegisterArabicFonts();
    }

    private static void RegisterArabicFonts()
    {
        if (_fontsRegistered) return;
        lock (FontLock)
        {
            if (_fontsRegistered) return;
            var assembly = Assembly.GetExecutingAssembly();
            var fontResources = assembly.GetManifestResourceNames()
                .Where(n => n.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase));
            foreach (var resourceName in fontResources)
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                    FontManager.RegisterFont(stream);
            }
            _fontsRegistered = true;
        }
    }

    public Task<string> GeneratePdfFromHtmlAsync(string htmlContent, string documentTitle)
    {
        var fileName = $"{Guid.NewGuid():N}.pdf";
        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "documents");
        if (!Directory.Exists(uploadsFolder))
            Directory.CreateDirectory(uploadsFolder);
        var filePath = Path.Combine(uploadsFolder, fileName);

        var blocks = MarkdownLiteParser.Parse(htmlContent ?? string.Empty);
        var isRtl = ContainsArabic(htmlContent) || ContainsArabic(documentTitle);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x
                    .FontSize(12)
                    .FontFamily(ArabicFontFamily, Fonts.Arial, Fonts.SegoeUI)
                    .LineHeight(1.5f));

                if (isRtl)
                    page.ContentFromRightToLeft();

                page.Header()
                    .Text(documentTitle)
                    .SemiBold().FontSize(20).FontColor(Colors.Blue.Darken2);

                page.Content()
                    .PaddingVertical(1, Unit.Centimetre)
                    .Column(column =>
                    {
                        column.Spacing(6);
                        foreach (var block in blocks)
                            RenderBlock(column, block);
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

        var fileUrl = $"/uploads/documents/{fileName}";
        return Task.FromResult(fileUrl);
    }

    private static bool ContainsArabic(string? text) =>
        !string.IsNullOrEmpty(text) && Regex.IsMatch(text, @"\p{IsArabic}");

    private static void RenderBlock(ColumnDescriptor column, MarkdownBlock block)
    {
        switch (block.Type)
        {
            case MarkdownBlockType.Heading:
                var fontSize = block.HeadingLevel switch
                {
                    1 => 18f,
                    2 => 16f,
                    _ => 14f
                };
                column.Item().PaddingTop(6).Text(text =>
                {
                    RenderInline(text, block.Text);
                    text.DefaultTextStyle(s => s.SemiBold().FontSize(fontSize));
                });
                break;

            case MarkdownBlockType.Bullet:
                column.Item().Row(row =>
                {
                    row.AutoItem().PaddingHorizontal(6).Text("\u2022");
                    row.RelativeItem().Text(text => RenderInline(text, block.Text));
                });
                break;

            case MarkdownBlockType.HorizontalRule:
                column.Item().PaddingVertical(4).LineHorizontal(0.75f).LineColor(Colors.Grey.Lighten1);
                break;

            default:
                column.Item().Text(text =>
                {
                    text.Justify();
                    RenderInline(text, block.Text);
                });
                break;
        }
    }

    // Renders **bold** and *italic* inline markers as styled spans.
    private static void RenderInline(TextDescriptor text, string content)
    {
        var pattern = new Regex(@"(\*\*(?<b>[^*]+)\*\*)|(\*(?<i>[^*]+)\*)|(__(?<b2>[^_]+)__)");
        var lastIndex = 0;
        foreach (Match match in pattern.Matches(content))
        {
            if (match.Index > lastIndex)
                text.Span(content[lastIndex..match.Index]);

            if (match.Groups["b"].Success)
                text.Span(match.Groups["b"].Value).SemiBold();
            else if (match.Groups["b2"].Success)
                text.Span(match.Groups["b2"].Value).SemiBold();
            else if (match.Groups["i"].Success)
                text.Span(match.Groups["i"].Value).Italic();

            lastIndex = match.Index + match.Length;
        }
        if (lastIndex < content.Length)
            text.Span(content[lastIndex..]);
    }
}

public enum MarkdownBlockType { Paragraph, Heading, Bullet, HorizontalRule }

public sealed record MarkdownBlock(MarkdownBlockType Type, string Text, int HeadingLevel = 0);

public static class MarkdownLiteParser
{
    public static IReadOnlyList<MarkdownBlock> Parse(string raw)
    {
        // 1) Normalize HTML-ish input into plain lines.
        var normalized = raw
            .Replace("\r\n", "\n");
        normalized = Regex.Replace(normalized, @"<\s*br\s*/?\s*>", "\n", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"<\s*/\s*(p|div|h[1-6]|li)\s*>", "\n", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"<\s*(h(?<lvl>[1-6]))[^>]*>", m => "\n" + new string('#', int.Parse(m.Groups["lvl"].Value)) + " ", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"<\s*li[^>]*>", "\n- ", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"<\s*(strong|b)\s*>", "**", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"<\s*/\s*(strong|b)\s*>", "**", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"<\s*(em|i)\s*>", "*", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, @"<\s*/\s*(em|i)\s*>", "*", RegexOptions.IgnoreCase);
        normalized = Regex.Replace(normalized, "<[^>]+>", string.Empty); // strip any remaining tags
        normalized = WebUtility.HtmlDecode(normalized);

        // 2) Parse line-by-line.
        var blocks = new List<MarkdownBlock>();
        foreach (var rawLine in normalized.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;

            var headingMatch = Regex.Match(line, @"^(#{1,6})\s+(.*)$");
            if (headingMatch.Success)
            {
                blocks.Add(new MarkdownBlock(MarkdownBlockType.Heading,
                    headingMatch.Groups[2].Value.Trim(),
                    headingMatch.Groups[1].Value.Length));
                continue;
            }

            if (Regex.IsMatch(line, @"^(-{3,}|\*{3,}|_{3,})$"))
            {
                blocks.Add(new MarkdownBlock(MarkdownBlockType.HorizontalRule, string.Empty));
                continue;
            }

            var bulletMatch = Regex.Match(line, @"^[-*\u2022]\s+(.*)$");
            if (bulletMatch.Success)
            {
                blocks.Add(new MarkdownBlock(MarkdownBlockType.Bullet, bulletMatch.Groups[1].Value.Trim()));
                continue;
            }

            blocks.Add(new MarkdownBlock(MarkdownBlockType.Paragraph, line));
        }

        if (blocks.Count == 0)
            blocks.Add(new MarkdownBlock(MarkdownBlockType.Paragraph, string.Empty));

        return blocks;
    }
}
