using System.Text.Json;
using Qdrant.Client.Grpc;

namespace QdrantWebSpider;

public record SearchResult(float Score, string Url, string Title, string Heading, string Text, string CaptureDate);

public class SearchService(QdrantHelper qdrant, IEmbeddingProvider embedder)
{
    public async Task<List<SearchResult>> SearchAsync(string query, int limit = 5, int? staleDays = null)
    {
        var vector = await embedder.EmbedAsync(query);
        var results = await qdrant.SearchAsync(vector, limit: staleDays.HasValue ? limit * 3 : limit, scoreThreshold: 0.3f);

        var filtered = results.AsEnumerable();
        if (staleDays.HasValue)
        {
            var cutoff = DateTime.UtcNow.AddDays(-staleDays.Value);
            filtered = results.Where(r => DateTime.TryParse(r.Payload.GetString("captureDate"), out var dt) && dt >= cutoff);
        }

        return filtered.Take(limit).Select(r => new SearchResult(
            Score: r.Score,
            Url: r.Payload.GetString("url"),
            Title: r.Payload.GetString("title"),
            Heading: r.Payload.GetString("heading"),
            Text: r.Payload.GetString("chunkText"),
            CaptureDate: r.Payload.GetString("captureDate")
        )).ToList();
    }
}
