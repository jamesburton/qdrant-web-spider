using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using QdrantWebSpider;

namespace QdrantWebSpider;

[McpServerToolType]
public sealed class SpiderTools
{
    [McpServerTool(Name = "search_web_pages"), Description("Semantic search over crawled web pages stored in Qdrant")]
    public static async Task<string> SearchWebPages(
        QdrantHelper qdrant,
        IEmbeddingProvider embedder,
        [Description("The search query")] string query,
        [Description("Maximum number of results (default 5)")] int limit = 5,
        [Description("Minimum similarity score 0-1 (default 0.3)")] float scoreThreshold = 0.3f,
        [Description("Only return results captured within this many days")] int? staleDays = null)
    {
        var service = new SearchService(qdrant, embedder);
        List<SearchResult> results;
        try
        {
            results = await service.SearchAsync(query, limit, staleDays);
        }
        catch (EmbeddingProviderException ex)
        {
            return $"Embedding error: {ex.Message}";
        }

        if (results.Count == 0)
            return "No results found.";

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            sb.AppendLine($"## Result {i + 1} (score: {r.Score:F3})");
            if (!string.IsNullOrEmpty(r.Title)) sb.AppendLine($"**Title:** {r.Title}");
            if (!string.IsNullOrEmpty(r.Heading)) sb.AppendLine($"**Section:** {r.Heading}");
            sb.AppendLine($"**URL:** {r.Url}");
            sb.AppendLine($"**Captured:** {r.CaptureDate}");
            sb.AppendLine();
            sb.AppendLine(r.Text);
            sb.AppendLine();
            sb.AppendLine("---");
        }

        return sb.ToString();
    }

    [McpServerTool(Name = "get_page"), Description("Retrieve all stored chunks for a specific URL")]
    public static async Task<string> GetPage(
        QdrantHelper qdrant,
        [Description("The URL of the page to retrieve")] string url)
    {
        var results = await qdrant.GetPageAsync(url);

        if (results.Count == 0)
            return $"No stored content found for URL: {url}";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# {results[0].Title}");
        sb.AppendLine($"**URL:** {url}");
        sb.AppendLine($"**Captured:** {results[0].CaptureDate}");
        sb.AppendLine($"**Chunks:** {results.Count}");
        sb.AppendLine();

        foreach (var r in results)
        {
            if (!string.IsNullOrEmpty(r.Heading))
                sb.AppendLine($"## {r.Heading}");
            sb.AppendLine(r.Text);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    [McpServerTool(Name = "list_urls"), Description("List all crawled URLs with their capture dates")]
    public static async Task<string> ListUrls(
        QdrantHelper qdrant,
        [Description("Only show URLs older than this many days")] int? staleDays = null)
    {
        var allPoints = await qdrant.ScrollAllAsync();

        var urlMap = new Dictionary<string, (string title, string captured)>();
        foreach (var p in allPoints)
        {
            var url = p.Payload.GetString("url");
            if (string.IsNullOrEmpty(url) || urlMap.ContainsKey(url)) continue;
            urlMap[url] = (p.Payload.GetString("title"), p.Payload.GetString("captureDate"));
        }

        IEnumerable<KeyValuePair<string, (string title, string captured)>> entries = urlMap;

        if (staleDays.HasValue)
        {
            var cutoff = DateTime.UtcNow.AddDays(-staleDays.Value);
            entries = entries.Where(kv =>
                DateTime.TryParse(kv.Value.captured, out var dt) && dt < cutoff);
        }

        var sorted = entries.OrderBy(kv => kv.Value.captured).ToList();

        if (sorted.Count == 0)
            return staleDays.HasValue ? "No stale URLs found." : "No URLs found in collection.";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Found {sorted.Count} URLs:\n");
        foreach (var (url, (title, captured)) in sorted)
        {
            sb.AppendLine($"- **{title}** — {url}");
            sb.AppendLine($"  Captured: {captured}");
        }

        return sb.ToString();
    }

    [McpServerTool(Name = "crawl_status"), Description("Get collection statistics and status")]
    public static async Task<string> CrawlStatus(
        QdrantHelper qdrant,
        SpiderConfig config)
    {
        var pointCount = await qdrant.GetPointCountAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("## Qdrant Web Spider Status");
        sb.AppendLine();
        sb.AppendLine($"- **Collection:** {config.Qdrant.CollectionName}");
        sb.AppendLine($"- **Qdrant URL:** {config.Qdrant.Url}");
        sb.AppendLine($"- **Total points:** {pointCount}");
        sb.AppendLine($"- **Embedding provider:** {config.Embedding.Provider} ({config.Embedding.Model})");
        sb.AppendLine($"- **Configured sites:** {config.Crawl.Sites.Count}");

        foreach (var site in config.Crawl.Sites)
            sb.AppendLine($"  - {site.Url} (depth: {site.MaxDepth})");

        return sb.ToString();
    }
}
