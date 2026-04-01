using System.ClientModel;
using OpenAI;
using OpenAI.Embeddings;

namespace QdrantWebSpider;

public class OpenAiEmbeddingProvider : IEmbeddingProvider
{
    private readonly EmbeddingClient _client;
    private readonly int _dimensions;
    private readonly EmbeddingGenerationOptions? _options;
    private readonly string _providerName;

    public string ProviderName => _providerName;

    public OpenAiEmbeddingProvider(EmbeddingConfig config, bool isAzure = false, string? baseUrl = null, bool requireApiKey = true)
    {
        _providerName = config.Provider;
        var apiKey = config.ApiKey;
        if (requireApiKey && apiKey == null)
            throw new InvalidOperationException(
                $"API key required for '{config.Provider}' provider. " +
                "Set via config, --api-key arg, or OPENAI_API_KEY env var.");

        var credential = new ApiKeyCredential(apiKey ?? "no-key");
        var options = new OpenAIClientOptions();

        if (baseUrl != null)
            options.Endpoint = new Uri(baseUrl);
        else if (isAzure && config.BaseUrl != null)
            options.Endpoint = new Uri(config.BaseUrl);

        var model = config.Model;
        if (model.Contains('/'))
            model = model.Split('/').Last();

        _client = new EmbeddingClient(model, credential, options);
        _dimensions = config.Dimensions;

        // Only set dimensions option for providers that support it (OpenAI/Azure)
        if (requireApiKey)
            _options = new EmbeddingGenerationOptions { Dimensions = _dimensions };
    }

    public int Dimensions => _dimensions;

    public async Task<float[]> EmbedAsync(string text)
    {
        var result = await _client.GenerateEmbeddingAsync(text, _options);
        return result.Value.ToFloats().ToArray();
    }

    public async Task<float[][]> EmbedBatchAsync(string[] texts)
    {
        var result = await _client.GenerateEmbeddingsAsync(texts, _options);
        return result.Value.Select(e => e.ToFloats().ToArray()).ToArray();
    }

    public void Dispose() { }
}
