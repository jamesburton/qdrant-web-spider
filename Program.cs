using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Qdrant.Client.Grpc;
using QdrantWebSpider;

var rootCommand = new RootCommand("Qdrant Web Spider - Semantic web crawler and search CLI");

// Global Options
var configOption = new Option<string?>("--config", "Path to config file");
var autoDownloadOption = new Option<bool>("--auto-download", "Auto-download ONNX model");
rootCommand.AddGlobalOption(configOption);
rootCommand.AddGlobalOption(autoDownloadOption);

// Crawl Command
var crawlCommand = new Command("crawl", "Crawl configured websites");
crawlCommand.Handler = CommandHandler.Create<string?, bool, string[]>(async (config, autoDownload, args) =>
{
    var spiderConfig = await SpiderConfig.LoadAsync(config, args);
    await RunCrawl(spiderConfig, autoDownload);
});
rootCommand.AddCommand(crawlCommand);

// Search Command
var searchCommand = new Command("search", "Perform semantic search");
var queryOption = new Option<string>("--query", "Search query") { IsRequired = true };
var limitOption = new Option<int>("--limit", () => 5, "Max results");
var jsonOption = new Option<bool>("--json", "JSON output");
var staleDaysOption = new Option<int?>("--stale-days", "Filter by capture date (days)");
searchCommand.AddOption(queryOption);
searchCommand.AddOption(limitOption);
searchCommand.AddOption(jsonOption);
searchCommand.AddOption(staleDaysOption);
searchCommand.Handler = CommandHandler.Create<string?, bool, string, int, bool, int?, string[]>(
    async (config, autoDownload, query, limit, json, staleDays, args) =>
{
    var spiderConfig = await SpiderConfig.LoadAsync(config, args);
    await RunSearch(spiderConfig, autoDownload, query, limit, json, staleDays);
});
rootCommand.AddCommand(searchCommand);

// MCP Command
var mcpCommand = new Command("mcp", "Start MCP server");
mcpCommand.Handler = CommandHandler.Create<string?, bool, string[]>(async (config, autoDownload, args) =>
{
    var spiderConfig = await SpiderConfig.LoadAsync(config, args);
    await RunMcp(spiderConfig, autoDownload, args);
});
rootCommand.AddCommand(mcpCommand);

return await rootCommand.InvokeAsync(args);

static async Task RunCrawl(SpiderConfig config, bool autoDownload)
{
    Console.WriteLine("Qdrant Web Spider - Crawl");
    using var qdrant = new QdrantHelper(config.Qdrant);
    using var embedder = await EmbeddingProviderFactory.CreateAsync(config.Embedding, autoDownload);
    await qdrant.EnsureCollectionAsync(embedder.Dimensions);

    using var http = new HttpClient();
    http.DefaultRequestHeaders.UserAgent.ParseAdd(config.Crawl.UserAgent);
    
    using var semaphore = new SemaphoreSlim(config.Crawl.MaxConcurrency);
    var totalPages = 0;
    var totalChunks = 0;

    var tasks = config.Crawl.Sites.Select(async site =>
    {
        var baseUri = new Uri(site.Url);
        var robots = config.Crawl.RespectRobotsTxt ? await RobotsTxt.FetchAsync(http, baseUri) : null;
        var queue = new Queue<(string Url, int Depth)>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (robots != null && robots.Sitemaps.Any())
        {
            foreach (var s in robots.Sitemaps)
            {
                var urls = await SitemapParser.FetchAndParseAsync(http, s);
                foreach (var u in urls)
                    if (Uri.TryCreate(u, UriKind.Absolute, out var uri) && uri.Host == baseUri.Host)
                        queue.Enqueue((u.TrimEnd('/'), 0));
            }
        }
        if (queue.Count == 0) queue.Enqueue((site.Url.TrimEnd('/'), 0));

        while (queue.Count > 0)
        {
            var (url, depth) = queue.Dequeue();
            if (!visited.Add(url) || (robots != null && !robots.IsAllowed(new Uri(url).AbsolutePath))) continue;

            await semaphore.WaitAsync();
            try
            {
                var html = await HttpHelper.GetStringWithRetryAsync(http, url);
                if (html == null) continue;

                var mode = site.Mode ?? config.Crawl.Mode;
                var page = PageExtractor.Extract(html, url, site.Selectors, mode);
                var chunks = Chunker.ChunkPage(page);
                if (chunks.Count == 0) { EnqueueLinks(page.Links, depth, site.MaxDepth, visited, queue); continue; }

                var hash = ComputeHash(page.BodyText);
                var existing = await qdrant.GetByUrlAsync(url);
                if (existing.Count > 0 && existing[0].Payload.GetString("contentHash") == hash) 
                { EnqueueLinks(page.Links, depth, site.MaxDepth, visited, queue); continue; }

                var embeddings = await embedder.EmbedBatchAsync(chunks.Select(c => c.Text).ToArray());
                var points = chunks.Select((c, i) => new PointStruct
                {
                    Id = new PointId { Uuid = GeneratePointId(url, c.Index) },
                    Vectors = embeddings[i],
                    Payload = { 
                        ["url"] = url, ["title"] = page.Title, ["heading"] = c.Heading, 
                        ["chunkText"] = c.Text, ["captureDate"] = DateTime.UtcNow.ToString("o"), ["contentHash"] = hash 
                    }
                }).ToList();

                await qdrant.UpsertAsync(points);
                Interlocked.Increment(ref totalPages);
                Interlocked.Add(ref totalChunks, chunks.Count);
                Console.WriteLine($"  [{depth}] {url} -> {chunks.Count} chunks");
                EnqueueLinks(page.Links, depth, site.MaxDepth, visited, queue);
            }
            finally { semaphore.Release(); }
        }
    });

    await Task.WhenAll(tasks);
    Console.WriteLine($"\nDone. Pages: {totalPages}, Chunks: {totalChunks}");
}

static async Task RunSearch(SpiderConfig config, bool autoDownload, string query, int limit, bool json, int? staleDays)
{
    using var qdrant = new QdrantHelper(config.Qdrant);
    using var embedder = await EmbeddingProviderFactory.CreateAsync(config.Embedding, autoDownload);
    var vector = await embedder.EmbedAsync(query);
    var results = await qdrant.SearchAsync(vector, limit: staleDays.HasValue ? limit * 3 : limit, scoreThreshold: 0.3f);
    
    var filtered = results.ToList();
    if (staleDays.HasValue)
    {
        var cutoff = DateTime.UtcNow.AddDays(-staleDays.Value);
        filtered = results.Where(r => DateTime.TryParse(r.Payload.GetString("captureDate"), out var dt) && dt >= cutoff).Take(limit).ToList();
    }

    if (json)
    {
        Console.WriteLine(JsonSerializer.Serialize(filtered.Select(r => new { r.Score, url = r.Payload.GetString("url"), text = r.Payload.GetString("chunkText") }), new JsonSerializerOptions { WriteIndented = true }));
    }
    else
    {
        foreach (var r in filtered) Console.WriteLine($"[{r.Score:F3}] {r.Payload.GetString("url")}\n{r.Payload.GetString("chunkText")[..Math.Min(200, r.Payload.GetString("chunkText").Length)]}...\n");
    }
}

static async Task RunMcp(SpiderConfig config, bool autoDownload, string[] args)
{
    var qdrant = new QdrantHelper(config.Qdrant);
    var embedder = await EmbeddingProviderFactory.CreateAsync(config.Embedding, autoDownload);
    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddSingleton(config);
    builder.Services.AddSingleton(qdrant);
    builder.Services.AddSingleton(embedder);
    builder.Services.AddMcpServer().WithStdioServerTransport().WithTools<SpiderTools>();
    await builder.Build().RunAsync();
}

static void EnqueueLinks(List<string> links, int fromDepth, int maxDepth, HashSet<string> visited, Queue<(string Url, int Depth)> queue)
{
    if (fromDepth >= maxDepth) return;
    foreach (var l in links) if (!visited.Contains(l)) queue.Enqueue((l, fromDepth + 1));
}

static string ComputeHash(string t) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(t)));
static string GeneratePointId(string u, int i) => new Guid(MD5.HashData(Encoding.UTF8.GetBytes($"{u}::{i}"))).ToString();
