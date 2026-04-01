using Xunit;
using QdrantWebSpider;

namespace QdrantWebSpider.Tests;

public class ChunkerTests
{
    [Fact]
    public void ChunkPage_ShouldCreateChunksForSections()
    {
        // Arrange
        var sections = new List<ExtractedSection>
        {
            new ExtractedSection("H1", "This is a test section with enough text to exceed the threshold. It needs more words to be safe."),
            new ExtractedSection("H2", "Another section here. This one also needs to be long enough to pass the threshold of ten tokens.")
        };
        var page = new ExtractedPage(
            Url: "url", Title: "Title", Headings: ["H1", "H2"], 
            BodyText: "...", Summary: "...", ContentSelector: "...", 
            Sections: sections, Links: []);

        // Act
        var result = Chunker.ChunkPage(page);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("H1", result[0].Heading);
        Assert.Contains("This is a test section", result[0].Text);
    }

    [Fact]
    public void ChunkPage_ShouldSplitLargeSection()
    {
        // Arrange
        // Approx 4 chars per token. 512 budget = ~2048 chars.
        // We need punctuation followed by space to trigger SplitIntoSentences.
        var sentence = "This is a sentence. ";
        var longText = string.Concat(Enumerable.Repeat(sentence, 200)); 
        var sections = new List<ExtractedSection>
        {
            new ExtractedSection("Big", longText)
        };
        var page = new ExtractedPage(
            Url: "url", Title: "Title", Headings: ["Big"], 
            BodyText: longText, Summary: "...", ContentSelector: "...", 
            Sections: sections, Links: []);

        // Act
        var result = Chunker.ChunkPage(page);

        // Assert
        Assert.True(result.Count > 1, $"Should have more than 1 chunk, got {result.Count}");
    }
}
