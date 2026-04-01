#:project ./shared/Shared.csproj
#:property PublishAot=false

using System.Security.Cryptography;
using System.Text;
using Qdrant.Client.Grpc;
using QdrantWebSpider;

var autoDownload = args.Contains("--auto-download");
var config = await SpiderConfig.LoadAsync(null, args);

Console.WriteLine("Qdrant Web Spider");
Console.WriteLine($"  Qdrant:     {config.Qdrant.Url}");
Console.WriteLine($"  Collection: {config.Qdrant.CollectionName}");
Console.WriteLine($"  Embedding:  {config.Embedding.Provider} ({config.Embedding.Model})");
Console.WriteLine($"  Sites:      {config.Crawl.Sites.Count}");
Console.WriteLine();

using var qdrant = new QdrantHelper(config.Qdrant);
using var embedder = await EmbeddingProviderFactory.CreateAsync(config.Embedding, autoDownload);

await qdrant.EnsureCollectionAsync(embedder.Dimensions);

if (config.Crawl.Sites.Count == 0)
{
    Console.WriteLine("No sites configured. Add sites to your config file.");
    return;
}

using var http = new HttpClient();
http.DefaultRequestHeaders.UserAgent.ParseAdd(config.Crawl.UserAgent);
http.Timeout = TimeSpan.FromSeconds(30);

using var semaphore = new SemaphoreSlim(config.Crawl.MaxConcurrency);
var totalPages = 0;
var totalChunks = 0;

var crawlTasks = config.Crawl.Sites.Select(async site =>
{
    Console.WriteLine($"\n[QUEUED] {site.Url} (depth: {site.MaxDepth})");
    
    var baseUri = new Uri(site.Url);
    
    RobotsTxt? robots = null;
    if (config.Crawl.RespectRobotsTxt)
    {
        robots = await RobotsTxt.FetchAsync(http, baseUri);
    }

    var effectiveDelay = robots?.CrawlDelayMs ?? config.Crawl.RequestDelayMs;
    var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var queue = new Queue<(string Url, int Depth)>();

    if (robots != null && robots.Sitemaps.Any())
    {
        foreach (var sitemapUrl in robots.Sitemaps)
        {
            var sitemapUrls = await SitemapParser.FetchAndParseAsync(http, sitemapUrl);
            foreach (var url in sitemapUrls)
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Host.Equals(baseUri.Host, StringComparison.OrdinalIgnoreCase))
                {
                    queue.Enqueue((url.TrimEnd('/'), 0));
                }
            }
        }
    }

    if (queue.Count == 0)
    {
        queue.Enqueue((site.Url.TrimEnd('/'), 0));
    }

    while (queue.Count > 0)
    {
        var (currentUrl, depth) = queue.Dequeue();

        if (!visited.Add(currentUrl))
            continue;

        if (robots != null && !robots.IsAllowed(new Uri(currentUrl).AbsolutePath))
            continue;

        await semaphore.WaitAsync();
        try
        {
            string? html;
            try
            {
                html = await HttpHelper.GetStringWithRetryAsync(http, currentUrl);
                if (html == null) continue;
            }
            catch { continue; }

            var mode = site.Mode ?? config.Crawl.Mode;
            var page = PageExtractor.Extract(html, currentUrl, site.Selectors, mode);
            if (string.IsNullOrWhiteSpace(page.BodyText))
            {
                EnqueueLinks(page.Links, depth, visited, queue, site.MaxDepth);
                continue;
            }

            var chunks = Chunker.ChunkPage(page);
            if (chunks.Count == 0)
            {
                EnqueueLinks(page.Links, depth, visited, queue, site.MaxDepth);
                continue;
            }

            var contentHash = ComputeHash(page.BodyText);
            var existingPoints = await qdrant.GetByUrlAsync(currentUrl);
            if (existingPoints.Count > 0)
            {
                var existingHash = existingPoints[0].Payload.GetString("contentHash");
                if (existingHash == contentHash)
                {
                    EnqueueLinks(page.Links, depth, visited, queue, site.MaxDepth);
                    continue;
                }
            }

            var texts = chunks.Select(c => c.Text).ToArray();
            var embeddings = await embedder.EmbedBatchAsync(texts);

            var captureDate = DateTime.UtcNow.ToString("o");
            var points = new List<PointStruct>();

            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                var pointId = GeneratePointId(currentUrl, chunk.Index);

                var point = new PointStruct
                {
                    Id = new PointId { Uuid = pointId },
                    Vectors = embeddings[i],
                };
                point.Payload["url"] = currentUrl;
                point.Payload["title"] = page.Title;
                point.Payload["heading"] = chunk.Heading;
                point.Payload["chunkIndex"] = chunk.Index;
                point.Payload["chunkText"] = chunk.Text;
                point.Payload["summary"] = page.Summary;
                point.Payload["contentSelector"] = page.ContentSelector;
                point.Payload["captureDate"] = captureDate;
                point.Payload["contentHash"] = contentHash;

                points.Add(point);
            }

            await qdrant.UpsertAsync(points);
            Interlocked.Increment(ref totalPages);
            Interlocked.Add(ref totalChunks, chunks.Count);
            
            Console.WriteLine($"  [{depth}] {currentUrl} -> {chunks.Count} chunks stored.");

            EnqueueLinks(page.Links, depth, visited, queue, site.MaxDepth);

            if (effectiveDelay > 0)
                await Task.Delay(effectiveDelay);
        }
        finally
        {
            semaphore.Release();
        }
    }
    
    Console.WriteLine($"\n[DONE] {site.Url}");
});

await Task.WhenAll(crawlTasks);

var finalCount = await qdrant.GetPointCountAsync();
Console.WriteLine($"\nAll crawls finished. Processed {totalPages} pages, stored {totalChunks} chunks. Collection total: {finalCount} points.");

static void EnqueueLinks(List<string> links, int fromDepth, HashSet<string> visited, Queue<(string Url, int Depth)> queue, int maxDepth)
{
    if (fromDepth >= maxDepth) return;
    foreach (var link in links)
        if (!visited.Contains(link))
            queue.Enqueue((link, fromDepth + 1));
}

static string ComputeHash(string text)
{
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
    return Convert.ToHexStringLower(bytes);
}

static string GeneratePointId(string url, int chunkIndex)
{
    var bytes = MD5.HashData(Encoding.UTF8.GetBytes($"{url}::{chunkIndex}"));
    return new Guid(bytes).ToString();
}
