using System;
using System.Collections.Generic;
using FluentAssertions;
using Wakeel.Infrastructure.Services;
using Xunit;

namespace Wakeel.Tests.Unit.Services;

public class MarkdownLiteParserTests
{
    [Fact]
    public void Parse_Heading_ReturnsHeadingBlock()
    {
        var raw = "### Title";
        var blocks = MarkdownLiteParser.Parse(raw);

        blocks.Should().ContainSingle();
        var block = blocks[0];
        block.Type.Should().Be(MarkdownBlockType.Heading);
        block.Text.Should().Be("Title");
        block.HeadingLevel.Should().Be(3);
    }

    [Fact]
    public void Parse_HorizontalRule_ReturnsRuleBlock()
    {
        var raw = "---";
        var blocks = MarkdownLiteParser.Parse(raw);

        blocks.Should().ContainSingle();
        blocks[0].Type.Should().Be(MarkdownBlockType.HorizontalRule);
    }

    [Fact]
    public void Parse_Bullet_ReturnsBulletBlock()
    {
        var raw = "- Item 1\n* Item 2";
        var blocks = MarkdownLiteParser.Parse(raw);

        blocks.Should().HaveCount(2);
        blocks[0].Type.Should().Be(MarkdownBlockType.Bullet);
        blocks[0].Text.Should().Be("Item 1");
        blocks[1].Type.Should().Be(MarkdownBlockType.Bullet);
        blocks[1].Text.Should().Be("Item 2");
    }

    [Fact]
    public void Parse_HtmlBrAndP_SplitsBlocks()
    {
        var raw = "<p>First paragraph</p><br>Second paragraph";
        var blocks = MarkdownLiteParser.Parse(raw);

        blocks.Should().HaveCount(2);
        blocks[0].Type.Should().Be(MarkdownBlockType.Paragraph);
        blocks[0].Text.Should().Be("First paragraph");
        blocks[1].Type.Should().Be(MarkdownBlockType.Paragraph);
        blocks[1].Text.Should().Be("Second paragraph");
    }

    [Fact]
    public void Parse_HtmlHeading_ConvertsToMarkdownHeading()
    {
        var raw = "<h2>Html Heading</h2>";
        var blocks = MarkdownLiteParser.Parse(raw);

        blocks.Should().ContainSingle();
        var block = blocks[0];
        block.Type.Should().Be(MarkdownBlockType.Heading);
        block.Text.Should().Be("Html Heading");
        block.HeadingLevel.Should().Be(2);
    }

    [Fact]
    public void Parse_Bold_LeavesAsterisksForInlineRenderer()
    {
        // MarkdownLiteParser itself doesn't remove **; it leaves them for QuestPdfGeneratorService's inline renderer
        // But the HTML strong/b tags are converted to **
        var raw = "<strong>bold</strong>";
        var blocks = MarkdownLiteParser.Parse(raw);

        blocks.Should().ContainSingle();
        blocks[0].Text.Should().Be("**bold**");
    }
}
