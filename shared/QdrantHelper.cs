using Google.Protobuf.Collections;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace QdrantWebSpider;

public class QdrantHelper : IDisposable
{
    private readonly QdrantClient _client;
    private readonly string _collectionName;

    public QdrantHelper(QdrantConfig config)
    {
        var uri = new Uri(config.Url);

        if (!string.IsNullOrEmpty(config.ApiKey))
        {
            _client = new QdrantClient(
                host: uri.Host,
                port: uri.Port,
                https: uri.Scheme == "https",
                apiKey: config.ApiKey);
        }
        else
        {
            _client = new QdrantClient(
                host: uri.Host,
                port: uri.Port,
                https: uri.Scheme == "https");
        }

        _collectionName = config.CollectionName;
    }

    public async Task EnsureCollectionAsync(int vectorSize)
    {
        var collections = await _client.ListCollectionsAsync();
        if (collections.Contains(_collectionName))
        {
            Console.WriteLine($"Collection '{_collectionName}' already exists.");
            return;
        }

        await _client.CreateCollectionAsync(_collectionName, new VectorParams
        {
            Size = (ulong)vectorSize,
            Distance = Distance.Cosine,
        });

        // Create indexes for faster filtering
        await _client.CreatePayloadIndexAsync(_collectionName, "url", PayloadSchemaType.Keyword);
        await _client.CreatePayloadIndexAsync(_collectionName, "captureDate", PayloadSchemaType.Datetime);

        Console.WriteLine($"Created collection '{_collectionName}' with vector size {vectorSize} and payload indexes.");
    }

    public async Task UpsertAsync(IReadOnlyList<PointStruct> points)
    {
        if (points.Count == 0) return;

        await _client.UpsertAsync(_collectionName, points);
    }

    public async Task<IReadOnlyList<ScoredPoint>> SearchAsync(
        float[] queryVector,
        int limit = 5,
        float? scoreThreshold = null,
        Filter? filter = null)
    {
        var results = await _client.SearchAsync(
            _collectionName,
            queryVector,
            limit: (ulong)limit,
            scoreThreshold: scoreThreshold,
            filter: filter);

        return results;
    }

    public async Task<List<RetrievedPoint>> GetByUrlAsync(string url)
    {
        var filter = new Filter();
        filter.Must.Add(new Condition
        {
            Field = new FieldCondition
            {
                Key = "url",
                Match = new Match { Keyword = url }
            }
        });

        var scrollResult = await _client.ScrollAsync(
            _collectionName,
            filter: filter,
            limit: 1000,
            payloadSelector: true);
        return scrollResult.Result.ToList();
    }

    public async Task<List<SearchResult>> GetPageAsync(string url)
    {
        var points = await GetByUrlAsync(url);
        if (points.Count == 0) return [];

        return points
            .OrderBy(r => r.Payload.GetInt("chunkIndex"))
            .Select(r => new SearchResult(
                Score: 1.0f,
                Url: r.Payload.GetString("url"),
                Title: r.Payload.GetString("title"),
                Heading: r.Payload.GetString("heading"),
                Text: r.Payload.GetString("chunkText"),
                CaptureDate: r.Payload.GetString("captureDate")
            )).ToList();
    }

    public async Task<IReadOnlyList<ScoredPoint>> ScrollAllAsync(Filter? filter = null)
    {
        var all = new List<ScoredPoint>();
        PointId? nextOffset = null;

        do
        {
            var scrollResult = await _client.ScrollAsync(
                _collectionName,
                filter: filter,
                limit: 100,
                offset: nextOffset,
                payloadSelector: true);

            foreach (var p in scrollResult.Result)
            {
                var scored = new ScoredPoint { Id = p.Id, Score = 1.0f };
                foreach (var kv in p.Payload)
                    scored.Payload[kv.Key] = kv.Value;
                all.Add(scored);
            }

            nextOffset = scrollResult.NextPageOffset;
        } while (nextOffset != null);

        return all;
    }

    public async Task<ulong> GetPointCountAsync()
    {
        var info = await _client.GetCollectionInfoAsync(_collectionName);
        return info.PointsCount;
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}

public static class PayloadExtensions
{
    public static string GetString(this MapField<string, Value> payload, string key, string defaultValue = "")
        => payload.TryGetValue(key, out var v) && v.KindCase == Value.KindOneofCase.StringValue ? v.StringValue : defaultValue;

    public static long GetInt(this MapField<string, Value> payload, string key, long defaultValue = 0)
        => payload.TryGetValue(key, out var v) && v.KindCase == Value.KindOneofCase.IntegerValue ? v.IntegerValue : defaultValue;

    public static DateTime? GetDateTime(this MapField<string, Value> payload, string key)
        => payload.TryGetValue(key, out var v) && DateTime.TryParse(v.StringValue, out var dt) ? dt : null;
}
