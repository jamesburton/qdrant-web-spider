namespace QdrantWebSpider;

public interface IEmbeddingProvider : IDisposable
{
    int Dimensions { get; }
    Task<float[]> EmbedAsync(string text);
    Task<float[][]> EmbedBatchAsync(string[] texts);
}

public static class EmbeddingProviderFactory
{
    public static async Task<IEmbeddingProvider> CreateAsync(EmbeddingConfig config, bool autoDownload = false)
    {
        return config.Provider.ToLowerInvariant() switch
        {
            "onnx" => await OnnxEmbeddingProvider.CreateAsync(config, autoDownload),
            "openai" => new OpenAiEmbeddingProvider(config),
            "azure-openai" => new OpenAiEmbeddingProvider(config, isAzure: true),
            "ollama" => new OpenAiEmbeddingProvider(config, baseUrl: config.BaseUrl ?? "http://localhost:11434/v1", requireApiKey: false),
            "lmstudio" => new OpenAiEmbeddingProvider(config, baseUrl: config.BaseUrl ?? "http://localhost:1234/v1", requireApiKey: false),
            _ => throw new ArgumentException($"Unknown embedding provider: '{config.Provider}'. Supported: onnx, openai, azure-openai, ollama, lmstudio")
        };
    }
}
