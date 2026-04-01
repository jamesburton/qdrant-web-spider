using System.Xml.Linq;

namespace QdrantWebSpider;

public static class SitemapParser
{
    public static async Task<List<string>> FetchAndParseAsync(HttpClient http, string sitemapUrl, List<string>? visited = null)
    {
        visited ??= [];
        if (visited.Contains(sitemapUrl)) return [];
        visited.Add(sitemapUrl);

        var urls = new List<string>();
        try
        {
            var response = await http.GetAsync(sitemapUrl);
            if (!response.IsSuccessStatusCode) return urls;

            var content = await response.Content.ReadAsStringAsync();
            var doc = XDocument.Parse(content);
            XNamespace ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;

            // Handle Sitemap Index
            if (doc.Root?.Name.LocalName == "sitemapindex")
            {
                var sitemapNodes = doc.Root.Elements(ns + "sitemap");
                foreach (var s in sitemapNodes)
                {
                    var loc = s.Element(ns + "loc")?.Value;
                    if (!string.IsNullOrEmpty(loc))
                    {
                        var childUrls = await FetchAndParseAsync(http, loc, visited);
                        urls.AddRange(childUrls);
                    }
                }
            }
            // Handle standard Sitemap
            else if (doc.Root?.Name.LocalName == "urlset")
            {
                var urlNodes = doc.Root.Elements(ns + "url");
                foreach (var u in urlNodes)
                {
                    var loc = u.Element(ns + "loc")?.Value;
                    if (!string.IsNullOrEmpty(loc))
                        urls.Add(loc);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [WARN] Failed to parse sitemap {sitemapUrl}: {ex.Message}");
        }

        return urls.Distinct().ToList();
    }
}
