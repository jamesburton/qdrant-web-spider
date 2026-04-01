namespace QdrantWebSpider;

public class RobotsTxt
{
    private readonly List<string> _disallowed = [];
    private readonly int? _crawlDelayMs;

    private RobotsTxt(List<string> disallowed, int? crawlDelayMs)
    {
        _disallowed = disallowed;
        _crawlDelayMs = crawlDelayMs;
    }

    public int? CrawlDelayMs => _crawlDelayMs;

    public bool IsAllowed(string path)
    {
        return !_disallowed.Any(d => path.StartsWith(d, StringComparison.OrdinalIgnoreCase));
    }

    public static async Task<RobotsTxt> FetchAsync(HttpClient http, Uri baseUri)
    {
        try
        {
            var robotsUrl = new Uri(baseUri, "/robots.txt");
            var response = await http.GetAsync(robotsUrl);

            if (!response.IsSuccessStatusCode)
                return new RobotsTxt([], null);

            var content = await response.Content.ReadAsStringAsync();
            return Parse(content);
        }
        catch
        {
            return new RobotsTxt([], null);
        }
    }

    public static RobotsTxt Parse(string content)
    {
        var disallowed = new List<string>();
        int? crawlDelayMs = null;
        bool inRelevantBlock = false;

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Split('#')[0].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var colonIndex = line.IndexOf(':');
            if (colonIndex < 0) continue;

            var key = line[..colonIndex].Trim().ToLowerInvariant();
            var value = line[(colonIndex + 1)..].Trim();

            if (key == "user-agent")
            {
                inRelevantBlock = value == "*" ||
                    value.Contains("QdrantWebSpider", StringComparison.OrdinalIgnoreCase);
            }
            else if (inRelevantBlock)
            {
                if (key == "disallow" && !string.IsNullOrEmpty(value))
                    disallowed.Add(value);
                else if (key == "crawl-delay" && int.TryParse(value, out var delay))
                    crawlDelayMs = delay * 1000;
            }
        }

        return new RobotsTxt(disallowed, crawlDelayMs);
    }
}
