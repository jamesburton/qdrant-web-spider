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
    public void Extract_ShouldHandleNoContent()
    {
        // Arrange
        var html = "<html><body></body></html>";
        var selectors = new SelectorConfig { Content = "main", Title = "title", Heading = "h1", Summary = "meta" };

        // Act
        var result = PageExtractor.Extract(html, "https://example.com", selectors);

        // Assert
        Assert.Empty(result.Sections);
        Assert.Empty(result.BodyText);
    }
}
