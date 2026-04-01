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

foreach (var site in config.Crawl.Sites)
{
    Console.WriteLine($"\nCrawling: {site.Url} (depth: {site.MaxDepth})");

    var baseUri = new Uri(site.Url);

    RobotsTxt? robots = null;
    if (config.Crawl.RespectRobotsTxt)
    {
        robots = await RobotsTxt.FetchAsync(http, baseUri);
        Console.WriteLine("  robots.txt loaded.");
    }

    var effectiveDelay = robots?.CrawlDelayMs ?? config.Crawl.RequestDelayMs;

    var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var queue = new Queue<(string Url, int Depth)>();
    queue.Enqueue((site.Url.TrimEnd('/'), 0));

    void EnqueueLinks(List<string> links, int fromDepth)
    {
        if (fromDepth >= site.MaxDepth) return;
        foreach (var link in links)
            if (!visited.Contains(link))
                queue.Enqueue((link, fromDepth + 1));
    }

    while (queue.Count > 0)
    {
        var (currentUrl, depth) = queue.Dequeue();

        if (!visited.Add(currentUrl))
            continue;

        if (robots != null && !robots.IsAllowed(new Uri(currentUrl).AbsolutePath))
        {
            Console.WriteLine($"  [SKIP] {currentUrl} (robots.txt)");
            continue;
        }

        await semaphore.WaitAsync();
        try
        {
            Console.Write($"  [{depth}] {currentUrl} ... ");

            string html;
            try
            {
                html = await http.GetStringAsync(currentUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAILED ({ex.Message})");
                continue;
            }

            var page = PageExtractor.Extract(html, currentUrl, site.Selectors);

            if (string.IsNullOrWhiteSpace(page.BodyText))
            {
                Console.WriteLine("no content.");
                EnqueueLinks(page.Links, depth);
                continue;
            }

            var chunks = Chunker.ChunkPage(page);
            if (chunks.Count == 0)
            {
                Console.WriteLine("no chunks.");
                EnqueueLinks(page.Links, depth);
                continue;
            }

            var contentHash = ComputeHash(page.BodyText);
            var existingPoints = await qdrant.GetByUrlAsync(currentUrl);
            if (existingPoints.Count > 0)
            {
                var existingHash = existingPoints[0].Payload.GetString("contentHash");
                if (existingHash == contentHash)
                {
                    Console.WriteLine($"{chunks.Count} chunks (unchanged, skipped).");
                    EnqueueLinks(page.Links, depth);
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
            totalPages++;
            totalChunks += chunks.Count;
            Console.WriteLine($"{chunks.Count} chunks stored.");

            EnqueueLinks(page.Links, depth);

            if (effectiveDelay > 0)
                await Task.Delay(effectiveDelay);
        }
        finally
        {
            semaphore.Release();
        }
    }
}

var finalCount = await qdrant.GetPointCountAsync();
Console.WriteLine($"\nDone. Crawled {totalPages} pages, stored {totalChunks} chunks. Collection total: {finalCount} points.");

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
