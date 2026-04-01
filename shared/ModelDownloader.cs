namespace QdrantWebSpider;

public static class ModelDownloader
{
    private static readonly Dictionary<string, ModelInfo> KnownModels = new()
    {
        ["sentence-transformers/all-MiniLM-L6-v2"] = new(
            "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model.onnx",
            "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/vocab.txt",
            "~80 MB"),
    };

    public static async Task<(string modelPath, string vocabPath)> EnsureModelAsync(
        EmbeddingConfig config, bool autoDownload = false)
    {
        var modelDir = config.ResolveModelPath();
        var modelPath = Path.Combine(modelDir, "model.onnx");
        var vocabPath = Path.Combine(modelDir, "vocab.txt");

        if (File.Exists(modelPath) && File.Exists(vocabPath))
            return (modelPath, vocabPath);

        if (!KnownModels.TryGetValue(config.Model, out var info))
        {
            throw new FileNotFoundException(
                $"ONNX model not found at '{modelDir}'. " +
                $"Model '{config.Model}' is not in the known models list. " +
                $"Please download it manually or set 'modelPath' in config.");
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"ONNX model '{config.Model}' not found at: {modelDir}");
        Console.WriteLine($"Download size: {info.Size}");
        Console.ResetColor();

        if (!autoDownload)
        {
            Console.Write("Download now? [Y/n] ");
            var response = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (response is "n" or "no")
            {
                throw new OperationCanceledException(
                    "Model download cancelled. Use --auto-download to skip this prompt, " +
                    "or download manually and set 'modelPath' in config.");
            }
        }

        Directory.CreateDirectory(modelDir);

        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("QdrantWebSpider/1.0");

        Console.Write("Downloading model.onnx... ");
        await DownloadFileAsync(http, info.ModelUrl, modelPath);
        Console.WriteLine("done.");

        Console.Write("Downloading vocab.txt... ");
        await DownloadFileAsync(http, info.VocabUrl, vocabPath);
        Console.WriteLine("done.");

        Console.WriteLine($"Model saved to: {modelDir}");
        return (modelPath, vocabPath);
    }

    private static async Task DownloadFileAsync(HttpClient http, string url, string destination)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync();
        await using var dest = File.Create(destination);

        var buffer = new byte[81920];
        long downloaded = 0;
        int bytesRead;
        int lastPct = -1;

        while ((bytesRead = await source.ReadAsync(buffer)) > 0)
        {
            await dest.WriteAsync(buffer.AsMemory(0, bytesRead));
            downloaded += bytesRead;

            if (totalBytes > 0)
            {
                var pct = (int)(downloaded * 100 / totalBytes);
                if (pct != lastPct)
                {
                    lastPct = pct;
                    Console.Write($"\rDownloading... {pct}% ({downloaded / 1024 / 1024}MB / {totalBytes / 1024 / 1024}MB)  ");
                }
            }
        }

        Console.Write("\r" + new string(' ', 60) + "\r");
    }

    private record ModelInfo(string ModelUrl, string VocabUrl, string Size);
}
