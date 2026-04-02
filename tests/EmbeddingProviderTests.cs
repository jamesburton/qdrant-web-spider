using QdrantWebSpider;

namespace QdrantWebSpider.Tests;

public class EmbeddingProviderTests
{
    [Fact]
    public async Task RetryingProvider_RetriesTransientSingleTextFailure()
    {
        var inner = new FakeEmbeddingProvider
        {
            FailuresBeforeSuccess = 1
        };
        using var provider = new RetryingEmbeddingProvider(inner, maxRetries: 1, retryDelay: _ => TimeSpan.Zero);

        var vector = await provider.EmbedAsync("hello");

        Assert.Equal([1f, 2f], vector);
        Assert.Equal(2, inner.SingleCalls);
    }

    [Fact]
    public async Task RetryingProvider_WrapsBatchFailureWithContext()
    {
        var inner = new FakeEmbeddingProvider
        {
            BatchException = new InvalidOperationException("upstream timeout")
        };
        using var provider = new RetryingEmbeddingProvider(inner, maxRetries: 0, retryDelay: _ => TimeSpan.Zero);

        var ex = await Assert.ThrowsAsync<EmbeddingProviderException>(() =>
            provider.EmbedBatchAsync(["one", "two"]));

        Assert.Equal("fake", ex.ProviderName);
        Assert.Equal("batch embedding", ex.Operation);
        Assert.Equal(1, ex.Attempts);
        Assert.Contains("upstream timeout", ex.Message);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    private sealed class FakeEmbeddingProvider : IEmbeddingProvider
    {
        public int FailuresBeforeSuccess { get; set; }
        public Exception? BatchException { get; set; }
        public int SingleCalls { get; private set; }

        public string ProviderName => "fake";
        public int Dimensions => 2;

        public Task<float[]> EmbedAsync(string text)
        {
            SingleCalls++;
            if (FailuresBeforeSuccess > 0)
            {
                FailuresBeforeSuccess--;
                throw new InvalidOperationException("temporary failure");
            }

            return Task.FromResult(new[] { 1f, 2f });
        }

        public Task<float[][]> EmbedBatchAsync(string[] texts)
        {
            if (BatchException is not null)
                throw BatchException;

            return Task.FromResult(texts.Select(_ => new[] { 1f, 2f }).ToArray());
        }

        public void Dispose() { }
    }
}
