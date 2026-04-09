using QdrantWebSpider;

namespace QdrantWebSpider.Tests;

public class ConfigTests
{
    [Fact]
    public void GetArgValue_ShouldReturnMatchingValue()
    {
        var args = new[] { "--config", "spider.json", "--limit", "10" };

        Assert.Equal("spider.json", SpiderConfig.GetArgValue(args, "--config"));
        Assert.Equal("10", SpiderConfig.GetArgValue(args, "--limit"));
    }

    [Fact]
    public void GetArgValue_ShouldReturnNullForMissing()
    {
        var args = new[] { "--config", "spider.json" };

        Assert.Null(SpiderConfig.GetArgValue(args, "--missing"));
    }

    [Fact]
    public void GetArgValue_ShouldBeCaseInsensitive()
    {
        var args = new[] { "--CONFIG", "spider.json" };

        Assert.Equal("spider.json", SpiderConfig.GetArgValue(args, "--config"));
    }

    [Fact]
    public void GetArgValue_ShouldNotReturnValueWhenArgIsLast()
    {
        var args = new[] { "--config" };

        Assert.Null(SpiderConfig.GetArgValue(args, "--config"));
    }

    [Fact]
    public async Task LoadAsync_ShouldReturnDefaultsWithNoFile()
    {
        var config = await SpiderConfig.LoadAsync(null, []);

        Assert.Equal("http://localhost:6334", config.Qdrant.Url);
        Assert.Equal("qdrant-web-spider", config.Qdrant.CollectionName);
        Assert.Equal("onnx", config.Embedding.Provider);
        Assert.Equal(384, config.Embedding.Dimensions);
    }

    [Fact]
    public async Task LoadAsync_ShouldOverlayCliArgs()
    {
        var args = new[] { "--qdrant-url", "http://remote:6334", "--collection", "custom", "--provider", "openai" };

        var config = await SpiderConfig.LoadAsync(null, args);

        Assert.Equal("http://remote:6334", config.Qdrant.Url);
        Assert.Equal("custom", config.Qdrant.CollectionName);
        Assert.Equal("openai", config.Embedding.Provider);
    }

    [Fact]
    public async Task LoadAsync_ShouldLoadFromJsonFile()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                {
                    "qdrant": { "collectionName": "from-json" },
                    "embedding": { "provider": "ollama", "dimensions": 768 }
                }
                """);

            var config = await SpiderConfig.LoadAsync(tempFile, []);

            Assert.Equal("from-json", config.Qdrant.CollectionName);
            Assert.Equal("ollama", config.Embedding.Provider);
            Assert.Equal(768, config.Embedding.Dimensions);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task LoadAsync_CliArgsShouldOverrideJson()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, """
                { "qdrant": { "collectionName": "from-json" } }
                """);

            var config = await SpiderConfig.LoadAsync(tempFile, ["--collection", "from-cli"]);

            Assert.Equal("from-cli", config.Qdrant.CollectionName);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void EmbeddingConfig_ResolveModelPath_ShouldUseDefault()
    {
        var config = new EmbeddingConfig();
        var path = config.ResolveModelPath();

        Assert.Contains(".qdrant-web-spider", path);
        Assert.Contains("all-MiniLM-L6-v2", path);
    }

    [Fact]
    public void EmbeddingConfig_ResolveModelPath_ShouldUseExplicitPath()
    {
        var config = new EmbeddingConfig { ModelPath = "/custom/path" };

        Assert.Equal("/custom/path", config.ResolveModelPath());
    }
}
