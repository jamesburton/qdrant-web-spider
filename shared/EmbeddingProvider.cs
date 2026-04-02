namespace QdrantWebSpider;

public interface IEmbeddingProvider : IDisposable
{
    string ProviderName { get; }
    int Dimensions { get; }
    Task<float[]> EmbedAsync(string text);
    Task<float[][]> EmbedBatchAsync(string[] texts);
}

public sealed class EmbeddingProviderException : Exception
{
    public string ProviderName { get; }
    public string Operation { get; }
    public int Attempts { get; }

    public EmbeddingProviderException(string providerName, string operation, int attempts, Exception innerException)
        : base(
            $"Embedding provider '{providerName}' failed during {operation} after {attempts} attempt(s): {innerException.Message}",
            innerException)
    {
        ProviderName = providerName;
        Operation = operation;
        Attempts = attempts;
    }
}

public class RetryingEmbeddingProvider(
    IEmbeddingProvider inner,
    int maxRetries = 3,
    Func<int, TimeSpan>? retryDelay = null) : IEmbeddingProvider
{
    public string ProviderName => inner.ProviderName;
    public int Dimensions => inner.Dimensions;

    public async Task<float[]> EmbedAsync(string text)
    {
        int retry = 0;
        while (true)
        {
            try
            {
                return await inner.EmbedAsync(text);
            }
            catch (Exception ex) when (retry < maxRetries)
            {
                retry++;
                Console.WriteLine($"  [RETRY {retry}/{maxRetries}] Embedding failed for '{inner.ProviderName}': {ex.Message}");
                await Task.Delay(GetRetryDelay(retry));
            }
            catch (Exception ex)
            {
                throw new EmbeddingProviderException(inner.ProviderName, "single text embedding", retry + 1, ex);
            }
        }
    }

    public async Task<float[][]> EmbedBatchAsync(string[] texts)
    {
        int retry = 0;
        while (true)
        {
            try
            {
                return await inner.EmbedBatchAsync(texts);
            }
            catch (Exception ex) when (retry < maxRetries)
            {
                retry++;
                Console.WriteLine($"  [RETRY {retry}/{maxRetries}] Batch embedding failed for '{inner.ProviderName}': {ex.Message}");
                await Task.Delay(GetRetryDelay(retry));
            }
            catch (Exception ex)
            {
                throw new EmbeddingProviderException(inner.ProviderName, "batch embedding", retry + 1, ex);
            }
        }
    }

    public void Dispose() => inner.Dispose();

    private TimeSpan GetRetryDelay(int attempt) =>
        retryDelay?.Invoke(attempt) ?? TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
}

public static class EmbeddingProviderFactory
{
    public static async Task<IEmbeddingProvider> CreateAsync(EmbeddingConfig config, bool autoDownload = false)
    {
        IEmbeddingProvider provider = config.Provider.ToLowerInvariant() switch
        {
            "onnx" => await OnnxEmbeddingProvider.CreateAsync(config, autoDownload),
            "openai" => new OpenAiEmbeddingProvider(config),
            "azure-openai" => new OpenAiEmbeddingProvider(config, isAzure: true),
            "ollama" => new OpenAiEmbeddingProvider(config, baseUrl: config.BaseUrl ?? "http://localhost:11434/v1", requireApiKey: false),
            "lmstudio" => new OpenAiEmbeddingProvider(config, baseUrl: config.BaseUrl ?? "http://localhost:1234/v1", requireApiKey: false),
            _ => throw new ArgumentException($"Unknown embedding provider: '{config.Provider}'. Supported: onnx, openai, azure-openai, ollama, lmstudio")
        };

        return new RetryingEmbeddingProvider(provider);
    }
}
