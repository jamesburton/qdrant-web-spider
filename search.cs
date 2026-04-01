#:project ./shared/Shared.csproj
#:property PublishAot=false

using System.Text.Json;
using QdrantWebSpider;

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
using var embedder = await EmbeddingProviderFactory.CreateAsync(config.Embedding, args.Contains("--auto-download"));

var service = new SearchService(qdrant, embedder);
var results = await service.SearchAsync(query, limit, staleDays);

if (jsonOutput)
{
    Console.WriteLine(JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
}
else
{
    Console.WriteLine($"Search results for: \"{query}\"\n");
    foreach (var r in results)
    {
        Console.WriteLine($"[{r.Score:F3}] {r.Url}");
        if (!string.IsNullOrEmpty(r.Title)) Console.WriteLine($"  Title: {r.Title}");
        if (!string.IsNullOrEmpty(r.Heading)) Console.WriteLine($"  Section: {r.Heading}");
        Console.WriteLine($"  {r.Text[..Math.Min(200, r.Text.Length)].Replace("\n", " ")}...\n");
    }
}
