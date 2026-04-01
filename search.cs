#:project ./shared/Shared.csproj
#:property PublishAot=false

using System.Text.Json;
using QdrantWebSpider;

var autoDownload = args.Contains("--auto-download");
var config = await SpiderConfig.LoadAsync(null, args);

var query = SpiderConfig.GetArgValue(args, "--query");
if (string.IsNullOrWhiteSpace(query))
{
    Console.Error.WriteLine("Usage: dotnet search.cs --query \"your search query\" [--config spider.json] [--limit 5] [--json] [--stale-days 30]");
    return;
}

var limit = int.TryParse(SpiderConfig.GetArgValue(args, "--limit"), out var l) ? l : 5;
var jsonOutput = args.Contains("--json");
var staleDays = int.TryParse(SpiderConfig.GetArgValue(args, "--stale-days"), out var sd) ? sd : (int?)null;

using var qdrant = new QdrantHelper(config.Qdrant);
using var embedder = await EmbeddingProviderFactory.CreateAsync(config.Embedding, autoDownload);

var queryVector = await embedder.EmbedAsync(query);

var cutoffDate = staleDays.HasValue ? DateTime.UtcNow.AddDays(-staleDays.Value) : (DateTime?)null;

var fetchLimit = cutoffDate.HasValue ? limit * 3 : limit;
var allResults = await qdrant.SearchAsync(queryVector, fetchLimit, scoreThreshold: 0.3f);

var results = cutoffDate.HasValue
    ? allResults.Where(r =>
    {
        var cd = r.Payload.GetString("captureDate");
        return DateTime.TryParse(cd, out var dt) && dt >= cutoffDate.Value;
    }).Take(limit).ToList()
    : allResults.Take(limit).ToList();

if (results.Count == 0)
{
    if (jsonOutput)
        Console.WriteLine("[]");
    else
        Console.WriteLine("No results found.");
    return;
}

if (jsonOutput)
{
    var jsonResults = results.Select((r, i) => new
    {
        rank = i + 1,
        score = r.Score,
        url = r.Payload.GetString("url"),
        title = r.Payload.GetString("title"),
        heading = r.Payload.GetString("heading"),
        snippet = Truncate(r.Payload.GetString("chunkText"), 200),
        captureDate = r.Payload.GetString("captureDate"),
    });
    Console.WriteLine(JsonSerializer.Serialize(jsonResults, new JsonSerializerOptions { WriteIndented = true }));
}
else
{
    Console.WriteLine($"Search results for: \"{query}\"\n");
    for (int i = 0; i < results.Count; i++)
    {
        var r = results[i];
        Console.WriteLine($"  {i + 1}. [{r.Score:F3}] {r.Payload.GetString("title")}");
        var heading = r.Payload.GetString("heading");
        if (!string.IsNullOrEmpty(heading))
            Console.WriteLine($"     Section: {heading}");
        Console.WriteLine($"     URL: {r.Payload.GetString("url")}");
        Console.WriteLine($"     Captured: {r.Payload.GetString("captureDate")}");
        Console.WriteLine($"     {Truncate(r.Payload.GetString("chunkText"), 200)}");
        Console.WriteLine();
    }
}

static string Truncate(string text, int maxLen)
{
    if (string.IsNullOrEmpty(text)) return "";
    text = text.Replace("\n", " ").Replace("\r", "");
    return text.Length <= maxLen ? text : text[..maxLen] + "...";
}
