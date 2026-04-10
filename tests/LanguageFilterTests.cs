using System.Reflection;
using QdrantWebSpider;

namespace QdrantWebSpider.Tests;

public class LanguageFilterTests
{
    private static bool IsLanguageAllowed(string url, Uri baseUri, string? language)
    {
        var method = typeof(CrawlService).GetMethod("IsLanguageAllowed", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (bool)method.Invoke(null, [url, baseUri, language])!;
    }

    private static bool IsHtmlLanguageAllowed(string html, string? language)
    {
        var method = typeof(CrawlService).GetMethod("IsHtmlLanguageAllowed", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (bool)method.Invoke(null, [html, language])!;
    }

    // --- URL path filtering ---

    [Fact]
    public void UrlFilter_AllowsEnglishPath()
    {
        var baseUri = new Uri("https://aspire.dev");
        Assert.True(IsLanguageAllowed("https://aspire.dev/architecture/overview", baseUri, "en"));
    }

    [Fact]
    public void UrlFilter_BlocksNonEnglishPath()
    {
        var baseUri = new Uri("https://aspire.dev");
        Assert.False(IsLanguageAllowed("https://aspire.dev/da/app-host/configuration", baseUri, "en"));
        Assert.False(IsLanguageAllowed("https://aspire.dev/fr/getting-started", baseUri, "en"));
        Assert.False(IsLanguageAllowed("https://aspire.dev/zh-cn/overview", baseUri, "en"));
    }

    [Fact]
    public void UrlFilter_AllowsMatchingLanguagePath()
    {
        var baseUri = new Uri("https://aspire.dev");
        Assert.True(IsLanguageAllowed("https://aspire.dev/da/app-host/configuration", baseUri, "da"));
    }

    [Fact]
    public void UrlFilter_AllowsAllWhenLanguageNull()
    {
        var baseUri = new Uri("https://aspire.dev");
        Assert.True(IsLanguageAllowed("https://aspire.dev/da/overview", baseUri, null));
        Assert.True(IsLanguageAllowed("https://aspire.dev/fr/overview", baseUri, null));
    }

    [Fact]
    public void UrlFilter_AllowsNoLanguagePrefixPaths()
    {
        var baseUri = new Uri("https://aspire.dev");
        Assert.True(IsLanguageAllowed("https://aspire.dev/community", baseUri, "en"));
        Assert.True(IsLanguageAllowed("https://aspire.dev/architecture/overview", baseUri, "en"));
    }

    [Fact]
    public void UrlFilter_DoesNotTreatLongSegmentsAsLanguage()
    {
        var baseUri = new Uri("https://example.com");
        // "docs" is 4 chars — not a language code
        Assert.True(IsLanguageAllowed("https://example.com/docs/setup", baseUri, "en"));
    }

    // --- HTML lang attribute filtering ---

    [Fact]
    public void HtmlFilter_AllowsEnglishPage()
    {
        var html = """<html lang="en"><head></head><body></body></html>""";
        Assert.True(IsHtmlLanguageAllowed(html, "en"));
    }

    [Fact]
    public void HtmlFilter_AllowsEnglishVariant()
    {
        var html = """<html lang="en-US"><head></head><body></body></html>""";
        Assert.True(IsHtmlLanguageAllowed(html, "en"));
    }

    [Fact]
    public void HtmlFilter_BlocksNonEnglishPage()
    {
        var html = """<html lang="da"><head></head><body></body></html>""";
        Assert.False(IsHtmlLanguageAllowed(html, "en"));
    }

    [Fact]
    public void HtmlFilter_AllowsWhenNoLangAttribute()
    {
        var html = """<html><head></head><body></body></html>""";
        Assert.True(IsHtmlLanguageAllowed(html, "en"));
    }

    [Fact]
    public void HtmlFilter_AllowsAllWhenLanguageNull()
    {
        var html = """<html lang="fr"><head></head><body></body></html>""";
        Assert.True(IsHtmlLanguageAllowed(html, null));
    }
}
