#pragma warning disable SKEXP0070

using Microsoft.SemanticKernel.Connectors.Onnx;

namespace QdrantWebSpider;

public class OnnxEmbeddingProvider : IEmbeddingProvider
{
    private readonly BertOnnxTextEmbeddingGenerationService _service;
    private readonly int _dimensions;

    private OnnxEmbeddingProvider(BertOnnxTextEmbeddingGenerationService service, int dimensions)
    {
        _service = service;
        _dimensions = dimensions;
    }

    public int Dimensions => _dimensions;

    public static async Task<OnnxEmbeddingProvider> CreateAsync(EmbeddingConfig config, bool autoDownload = false)
    {
        var (modelPath, vocabPath) = await ModelDownloader.EnsureModelAsync(config, autoDownload);

        var service = await BertOnnxTextEmbeddingGenerationService.CreateAsync(
            onnxModelPath: modelPath,
            vocabPath: vocabPath);

        return new OnnxEmbeddingProvider(service, config.Dimensions);
    }

    public async Task<float[]> EmbedAsync(string text)
    {
        var results = await _service.GenerateEmbeddingsAsync([text]);
        return results[0].ToArray();
    }

    public async Task<float[][]> EmbedBatchAsync(string[] texts)
    {
        var results = await _service.GenerateEmbeddingsAsync(texts);
        return results.Select(r => r.ToArray()).ToArray();
    }

    public void Dispose()
    {
        (_service as IDisposable)?.Dispose();
    }
}
