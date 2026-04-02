using System.Security.Cryptography;
using System.Text;
using Qdrant.Client.Grpc;

namespace QdrantWebSpider;

public class CrawlService(
    QdrantHelper qdrant,
    IEmbeddingProvider embedder,
    HttpClient http,
    SpiderConfig config)
{
    public async Task CrawlAsync(Action<string>? logger = null)
    {
        await qdrant.EnsureCollectionAsync(embedder.Dimensions);

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
                    if (chunks.Count == 0)
                    {
                        EnqueueLinks(page.Links, depth, site.MaxDepth, visited, queue);
                        continue;
                    }

                    var hash = ComputeHash(page.BodyText);
                    var existing = await qdrant.GetByUrlAsync(url);
                    if (existing.Count > 0 && existing[0].Payload.GetString("contentHash") == hash)
                    {
                        EnqueueLinks(page.Links, depth, site.MaxDepth, visited, queue);
                        continue;
                    }

                    float[][] embeddings;
                    try
                    {
                        embeddings = await embedder.EmbedBatchAsync(chunks.Select(c => c.Text).ToArray());
                    }
                    catch (EmbeddingProviderException ex)
                    {
                        logger?.Invoke($"  [EMBED ERROR] {url} -> {ex.Message}");
                        EnqueueLinks(page.Links, depth, site.MaxDepth, visited, queue);
                        continue;
                    }

                    var points = chunks.Select((c, i) => new PointStruct
                    {
                        Id = new PointId { Uuid = GeneratePointId(url, c.Index) },
                        Vectors = embeddings[i],
                        Payload = {
                            ["url"] = url,
                            ["title"] = page.Title,
                            ["heading"] = c.Heading,
                            ["chunkText"] = c.Text,
                            ["captureDate"] = DateTime.UtcNow.ToString("o"),
                            ["contentHash"] = hash
                        }
                    }).ToList();

                    await qdrant.UpsertAsync(points);
                    Interlocked.Increment(ref totalPages);
                    Interlocked.Add(ref totalChunks, chunks.Count);
                    logger?.Invoke($"  [{depth}] {url} -> {chunks.Count} chunks");
                    EnqueueLinks(page.Links, depth, site.MaxDepth, visited, queue);
                }
                finally { semaphore.Release(); }
            }
        });

        await Task.WhenAll(tasks);
        logger?.Invoke($"\nDone. Pages: {totalPages}, Chunks: {totalChunks}");
    }

    private static void EnqueueLinks(List<string> links, int fromDepth, int maxDepth, HashSet<string> visited, Queue<(string Url, int Depth)> queue)
    {
        if (fromDepth >= maxDepth) return;
        foreach (var l in links) if (!visited.Contains(l)) queue.Enqueue((l, fromDepth + 1));
    }

    private static string ComputeHash(string t) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(t)));
    private static string GeneratePointId(string u, int i) => new Guid(MD5.HashData(Encoding.UTF8.GetBytes($"{u}::{i}"))).ToString();
}
