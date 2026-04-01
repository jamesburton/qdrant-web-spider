using System.Text.Json;
using System.Text.Json.Serialization;

namespace QdrantWebSpider;

public record SpiderConfig
{
    [JsonPropertyName("qdrant")]
    public QdrantConfig Qdrant { get; init; } = new();

    [JsonPropertyName("embedding")]
    public EmbeddingConfig Embedding { get; init; } = new();

    [JsonPropertyName("crawl")]
    public CrawlConfig Crawl { get; init; } = new();

    public static async Task<SpiderConfig> LoadAsync(string? configPath, string[] args)
    {
        var config = new SpiderConfig();

        // Load from JSON file
        configPath ??= GetArgValue(args, "--config");
        if (configPath != null && File.Exists(configPath))
        {
            await using var stream = File.OpenRead(configPath);
            config = await JsonSerializer.DeserializeAsync<SpiderConfig>(stream, JsonOptions) ?? config;
        }

        // Overlay environment variables for secrets
        var qdrant = config.Qdrant with
        {
            ApiKey = Environment.GetEnvironmentVariable("QDRANT_API_KEY") ?? config.Qdrant.ApiKey
        };

        var embedding = config.Embedding with
        {
            ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")
                ?? config.Embedding.ApiKey
        };

        // Overlay CLI args
        qdrant = qdrant with
        {
            Url = GetArgValue(args, "--qdrant-url") ?? qdrant.Url,
            CollectionName = GetArgValue(args, "--collection") ?? qdrant.CollectionName,
        };

        embedding = embedding with
        {
            Provider = GetArgValue(args, "--provider") ?? embedding.Provider,
            Model = GetArgValue(args, "--model") ?? embedding.Model,
            BaseUrl = GetArgValue(args, "--embedding-url") ?? embedding.BaseUrl,
        };

        return config with { Qdrant = qdrant, Embedding = embedding };
    }

    public static string? GetArgValue(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}

public record QdrantConfig
{
    [JsonPropertyName("url")]
    public string Url { get; init; } = "http://localhost:6334";

    [JsonPropertyName("apiKey")]
    public string? ApiKey { get; init; }

    [JsonPropertyName("collectionName")]
    public string CollectionName { get; init; } = "qdrant-web-spider";
}

public record EmbeddingConfig
{
    [JsonPropertyName("provider")]
    public string Provider { get; init; } = "onnx";

    [JsonPropertyName("modelPath")]
    public string? ModelPath { get; init; }

    [JsonPropertyName("model")]
    public string Model { get; init; } = "sentence-transformers/all-MiniLM-L6-v2";

    [JsonPropertyName("apiKey")]
    public string? ApiKey { get; init; }

    [JsonPropertyName("baseUrl")]
    public string? BaseUrl { get; init; }

    [JsonPropertyName("dimensions")]
    public int Dimensions { get; init; } = 384;

    /// <summary>
    /// Resolves the local model storage path. Defaults to ~/.qdrant-web-spider/models/{modelName}/
    /// </summary>
    public string ResolveModelPath()
    {
        if (ModelPath != null)
            return ModelPath;

        var modelName = Model.Contains('/') ? Model.Split('/').Last() : Model;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".qdrant-web-spider", "models", modelName);
    }
}

public record CrawlConfig
{
    [JsonPropertyName("sites")]
    public List<SiteConfig> Sites { get; init; } = [];

    [JsonPropertyName("respectRobotsTxt")]
    public bool RespectRobotsTxt { get; init; } = true;

    [JsonPropertyName("requestDelayMs")]
    public int RequestDelayMs { get; init; } = 500;

    [JsonPropertyName("maxConcurrency")]
    public int MaxConcurrency { get; init; } = 4;

    [JsonPropertyName("userAgent")]
    public string UserAgent { get; init; } = "QdrantWebSpider/1.0";
}

public record SiteConfig
{
    [JsonPropertyName("url")]
    public string Url { get; init; } = "";

    [JsonPropertyName("maxDepth")]
    public int MaxDepth { get; init; } = 3;

    [JsonPropertyName("frequencyHours")]
    public int FrequencyHours { get; init; } = 24;

    [JsonPropertyName("selectors")]
    public SelectorConfig Selectors { get; init; } = new();
}

public record SelectorConfig
{
    [JsonPropertyName("content")]
    public string Content { get; init; } = "main, article, .content";

    [JsonPropertyName("title")]
    public string Title { get; init; } = "h1, title";

    [JsonPropertyName("heading")]
    public string Heading { get; init; } = "h1, h2, h3";

    [JsonPropertyName("summary")]
    public string Summary { get; init; } = "meta[name=description]";
}
