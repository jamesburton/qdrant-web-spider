using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using QdrantWebSpider;

var rootCommand = new RootCommand("Qdrant Web Spider - Semantic web crawler and search CLI");

static void ReportEmbeddingFailure(EmbeddingProviderException ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}

// Global Options
var configOption = new Option<string?>("--config", "Path to config file");
var autoDownloadOption = new Option<bool>("--auto-download", "Auto-download ONNX model");
rootCommand.AddGlobalOption(configOption);
rootCommand.AddGlobalOption(autoDownloadOption);

// Crawl Command
var crawlCommand = new Command("crawl", "Crawl configured websites");
crawlCommand.Handler = CommandHandler.Create<string?, bool, string[]>(async (config, autoDownload, args) =>
{
    try
    {
        var spiderConfig = await SpiderConfig.LoadAsync(config, args);
        using var qdrant = new QdrantHelper(spiderConfig.Qdrant);
        using var embedder = await EmbeddingProviderFactory.CreateAsync(spiderConfig.Embedding, autoDownload);
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd(spiderConfig.Crawl.UserAgent);

        var service = new CrawlService(qdrant, embedder, http, spiderConfig);
        await service.CrawlAsync(Console.WriteLine);
    }
    catch (EmbeddingProviderException ex)
    {
        ReportEmbeddingFailure(ex);
    }
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
    try
    {
        var spiderConfig = await SpiderConfig.LoadAsync(config, args);
        using var qdrant = new QdrantHelper(spiderConfig.Qdrant);
        using var embedder = await EmbeddingProviderFactory.CreateAsync(spiderConfig.Embedding, autoDownload);

        var service = new SearchService(qdrant, embedder);
        var results = await service.SearchAsync(query, limit, staleDays);

        if (json)
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
    }
    catch (EmbeddingProviderException ex)
    {
        ReportEmbeddingFailure(ex);
    }
});
rootCommand.AddCommand(searchCommand);

// MCP Command
var mcpCommand = new Command("mcp", "Start MCP server");
mcpCommand.Handler = CommandHandler.Create<string?, bool, string[]>(async (config, autoDownload, args) =>
{
    try
    {
        var spiderConfig = await SpiderConfig.LoadAsync(config, args);
        using var qdrant = new QdrantHelper(spiderConfig.Qdrant);
        using var embedder = await EmbeddingProviderFactory.CreateAsync(spiderConfig.Embedding, autoDownload);

        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddSingleton(spiderConfig);
        builder.Services.AddSingleton(qdrant);
        builder.Services.AddSingleton(embedder);
        builder.Services.AddMcpServer().WithStdioServerTransport().WithTools<SpiderTools>();
        await builder.Build().RunAsync();
    }
    catch (EmbeddingProviderException ex)
    {
        ReportEmbeddingFailure(ex);
    }
});
rootCommand.AddCommand(mcpCommand);

return await rootCommand.InvokeAsync(args);
