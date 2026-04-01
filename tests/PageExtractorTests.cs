using Xunit;
using QdrantWebSpider;

namespace QdrantWebSpider.Tests;

public class PageExtractorTests
{
    [Fact]
    public void Extract_ShouldExtractBasicContent()
    {
        // Arrange
        var html = @"
            <html>
                <head><title>Test Title</title><meta name='description' content='Test Summary'></head>
                <body>
                    <main>
                        <h1>Main Heading</h1>
                        <p>Some text in the main section.</p>
                        <h2>Sub Heading</h2>
                        <p>More text here.</p>
                    </main>
                    <a href='/link1'>Link 1</a>
                    <a href='https://external.com'>External</a>
                </body>
            </html>";
        var url = "https://example.com/page";
        var selectors = new SelectorConfig
        {
            Content = "main",
            Title = "title",
            Heading = "h1, h2",
            Summary = "meta[name=description]"
        };

        // Act
        var result = PageExtractor.Extract(html, url, selectors);

        // Assert
        Assert.Equal("Test Title", result.Title);
        Assert.Equal("Test Summary", result.Summary);
        Assert.Equal(2, result.Headings.Count);
        Assert.Contains("Main Heading", result.Headings);
        Assert.Contains("Sub Heading", result.Headings);
        Assert.Equal(2, result.Sections.Count);
        Assert.Equal("Main Heading", result.Sections[0].Heading);
        Assert.Contains("Some text in the main section.", result.Sections[0].Text);
        Assert.Single(result.Links);
        Assert.Equal("https://example.com/link1", result.Links[0]);
    }

    [Fact]
    public void Extract_ShouldExtractMarkdown()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <main>
                        <h1>Heading</h1>
                        <p>This is <strong>bold</strong> and [link](https://test.com).</p>
                        <ul><li>Item 1</li></ul>
                    </main>
                </body>
            </html>";
        var url = "https://example.com";
        var selectors = new SelectorConfig { Content = "main", Heading = "h1" };

        // Act
        var result = PageExtractor.Extract(html, url, selectors, ExtractionMode.Markdown);

        // Assert
        Assert.Contains("**bold**", result.BodyText);
        Assert.Contains("- Item 1", result.BodyText);
    }

    [Fact]
    public void Extract_ShouldConvertTableToMarkdown()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <main>
                        <table>
                            <tr><th>Header 1</th><th>Header 2</th></tr>
                            <tr><td>Cell 1</td><td>Cell 2</td></tr>
                        </table>
                    </main>
                </body>
            </html>";
        var url = "https://example.com";
        var selectors = new SelectorConfig { Content = "main" };

        // Act
        var result = PageExtractor.Extract(html, url, selectors, ExtractionMode.Markdown);

        // Assert
        Assert.Contains("| Header 1 | Header 2 |", result.BodyText);
        Assert.Contains("| --- | --- |", result.BodyText);
        Assert.Contains("| Cell 1 | Cell 2 |", result.BodyText);
    }

    [Fact]
    public void Extract_ShouldExtractHtml()
    {
        // Arrange
        var html = "<html><body><main><p>Hello</p></main></body></html>";
        var selectors = new SelectorConfig { Content = "main" };

        // Act
        var result = PageExtractor.Extract(html, "https://example.com", selectors, ExtractionMode.Html);

        // Assert
        Assert.Contains("<p>Hello</p>", result.BodyText);
    }
}
