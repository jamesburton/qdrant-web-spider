using QdrantWebSpider;

namespace QdrantWebSpider.Tests;

public class RobotsTxtTests
{
    [Fact]
    public void Parse_ShouldExtractDisallowRulesForWildcard()
    {
        var content = """
            User-agent: *
            Disallow: /admin
            Disallow: /private/
            """;

        var robots = RobotsTxt.Parse(content);

        Assert.False(robots.IsAllowed("/admin"));
        Assert.False(robots.IsAllowed("/admin/page"));
        Assert.False(robots.IsAllowed("/private/data"));
        Assert.True(robots.IsAllowed("/public"));
        Assert.True(robots.IsAllowed("/"));
    }

    [Fact]
    public void Parse_ShouldExtractCrawlDelay()
    {
        var content = """
            User-agent: *
            Crawl-delay: 2
            Disallow: /slow
            """;

        var robots = RobotsTxt.Parse(content);

        Assert.Equal(2000, robots.CrawlDelayMs);
    }

    [Fact]
    public void Parse_ShouldExtractSitemaps()
    {
        var content = """
            Sitemap: https://example.com/sitemap.xml
            Sitemap: https://example.com/sitemap2.xml
            User-agent: *
            Disallow:
            """;

        var robots = RobotsTxt.Parse(content);

        Assert.Equal(2, robots.Sitemaps.Count);
        Assert.Equal("https://example.com/sitemap.xml", robots.Sitemaps[0]);
    }

    [Fact]
    public void Parse_ShouldIgnoreIrrelevantUserAgentBlocks()
    {
        var content = """
            User-agent: Googlebot
            Disallow: /google-only

            User-agent: *
            Disallow: /blocked
            """;

        var robots = RobotsTxt.Parse(content);

        Assert.True(robots.IsAllowed("/google-only"));
        Assert.False(robots.IsAllowed("/blocked"));
    }

    [Fact]
    public void Parse_ShouldMatchQdrantWebSpiderAgent()
    {
        var content = """
            User-agent: QdrantWebSpider
            Disallow: /spider-blocked

            User-agent: *
            Disallow: /general-blocked
            """;

        var robots = RobotsTxt.Parse(content);

        Assert.False(robots.IsAllowed("/spider-blocked"));
    }

    [Fact]
    public void Parse_EmptyContent_ShouldAllowAll()
    {
        var robots = RobotsTxt.Parse("");

        Assert.True(robots.IsAllowed("/anything"));
        Assert.Null(robots.CrawlDelayMs);
        Assert.Empty(robots.Sitemaps);
    }

    [Fact]
    public void Parse_ShouldIgnoreComments()
    {
        var content = """
            # This is a comment
            User-agent: * # all bots
            Disallow: /secret # hidden
            """;

        var robots = RobotsTxt.Parse(content);

        Assert.False(robots.IsAllowed("/secret"));
    }
}
