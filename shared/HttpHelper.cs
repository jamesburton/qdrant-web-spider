namespace QdrantWebSpider;

public static class HttpHelper
{
    public static async Task<string?> GetStringWithRetryAsync(HttpClient http, string url, int maxRetries = 3, int initialDelayMs = 1000)
    {
        int retryCount = 0;
        while (retryCount <= maxRetries)
        {
            try
            {
                var response = await http.GetAsync(url);
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadAsStringAsync();
                
                if (response.StatusCode >= System.Net.HttpStatusCode.InternalServerError)
                {
                    // Server error, worth retrying
                    Console.Write($"[Retry {retryCount + 1}/{maxRetries}] ");
                }
                else
                {
                    // Client error (404, 403, etc), don't retry
                    return null;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                Console.Write($"[Retry {retryCount + 1}/{maxRetries} ({ex.Message})] ");
            }

            retryCount++;
            if (retryCount <= maxRetries)
            {
                await Task.Delay(initialDelayMs * (int)Math.Pow(2, retryCount - 1));
            }
        }

        return null;
    }
}
