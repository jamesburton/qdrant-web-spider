using System.Text;
using HtmlAgilityPack;

namespace QdrantWebSpider;

public record ExtractedPage(
    string Url,
    string Title,
    List<string> Headings,
    string BodyText,
    string Summary,
    string ContentSelector,
    List<ExtractedSection> Sections,
    List<string> Links);

public record ExtractedSection(string Heading, string Text);

public static class PageExtractor
{
    public static ExtractedPage Extract(string html, string url, SelectorConfig selectors, ExtractionMode mode = ExtractionMode.Markdown)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var title = ExtractFirst(doc, selectors.Title)?.Trim() ?? "";
        var summary = ExtractMetaDescription(doc, selectors.Summary) ?? "";
        var headings = ExtractAll(doc, selectors.Heading);
        var contentNodes = SelectNodes(doc, selectors.Content);
        var sections = ExtractSections(contentNodes, selectors.Heading, mode);
        var bodyText = string.Join("\n\n", sections.Select(s => s.Text));
        var links = ExtractLinksFromDoc(doc, url);

        return new ExtractedPage(
            Url: url,
            Title: title,
            Headings: headings,
            BodyText: bodyText,
            Summary: summary,
            ContentSelector: selectors.Content,
            Sections: sections,
            Links: links);
    }

    private static List<string> ExtractLinksFromDoc(HtmlDocument doc, string baseUrl)
    {
        var baseUri = new Uri(baseUrl);
        var links = new List<string>();

        var anchors = doc.DocumentNode.SelectNodes("//a[@href]");
        if (anchors == null) return links;

        foreach (var anchor in anchors)
        {
            var href = anchor.GetAttributeValue("href", "");
            if (string.IsNullOrWhiteSpace(href)) continue;

            if (href.StartsWith('#') || href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
                || href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                continue;

            if (Uri.TryCreate(baseUri, href, out var absoluteUri))
            {
                if (absoluteUri.Host.Equals(baseUri.Host, StringComparison.OrdinalIgnoreCase))
                {
                    var clean = new UriBuilder(absoluteUri) { Fragment = "" }.Uri.ToString().TrimEnd('/');
                    links.Add(clean);
                }
            }
        }

        return links.Distinct().ToList();
    }

    private static List<ExtractedSection> ExtractSections(List<HtmlNode> contentNodes, string headingSelector, ExtractionMode mode)
    {
        var sections = new List<ExtractedSection>();

        if (contentNodes.Count == 0)
            return sections;

        var headingTags = ParseHeadingTags(headingSelector);
        var currentHeading = "";
        var currentText = new List<string>();

        foreach (var node in contentNodes)
        {
            foreach (var child in node.ChildNodes)
            {
                if (child.NodeType == HtmlNodeType.Element && headingTags.Contains(child.Name.ToLowerInvariant()))
                {
                    // Flush current section
                    if (currentText.Count > 0)
                    {
                        sections.Add(new ExtractedSection(currentHeading, string.Join("\n", currentText).Trim()));
                        currentText.Clear();
                    }
                    currentHeading = CleanText(child.InnerText);
                }
                else
                {
                    var content = mode switch
                    {
                        ExtractionMode.Html => child.OuterHtml,
                        ExtractionMode.Markdown => HtmlToMarkdown(child),
                        ExtractionMode.Text => CleanText(child.InnerText),
                        _ => CleanText(child.InnerText)
                    };

                    if (!string.IsNullOrWhiteSpace(content))
                        currentText.Add(content);
                }
            }
        }

        // Flush last section
        if (currentText.Count > 0)
            sections.Add(new ExtractedSection(currentHeading, string.Join("\n", currentText).Trim()));

        return sections;
    }

    private static string HtmlToMarkdown(HtmlNode node)
    {
        if (node.NodeType == HtmlNodeType.Text)
            return CleanText(node.InnerText);

        if (node.NodeType != HtmlNodeType.Element)
            return "";

        var tag = node.Name.ToLowerInvariant();
        var inner = string.Join("", node.ChildNodes.Select(HtmlToMarkdown)).Trim();

        if (string.IsNullOrWhiteSpace(inner) && tag != "hr" && tag != "br" && tag != "table")
            return "";

        return tag switch
        {
            "p" => $"\n{inner}\n",
            "strong" or "b" => $"**{inner}**",
            "em" or "i" => $"*{inner}*",
            "li" => $"\n- {inner}",
            "ul" or "ol" => $"\n{inner}\n",
            "a" => $"[{inner}]({node.GetAttributeValue("href", "")})",
            "code" => $"`{inner}`",
            "pre" => $"\n```\n{node.InnerText}\n```\n",
            "br" => "\n",
            "hr" => "\n---\n",
            "h1" => $"\n# {inner}\n",
            "h2" => $"\n## {inner}\n",
            "h3" => $"\n### {inner}\n",
            "h4" => $"\n#### {inner}\n",
            "h5" => $"\n##### {inner}\n",
            "h6" => $"\n###### {inner}\n",
            "blockquote" => $"\n> {inner}\n",
            "table" => $"\n{ConvertTable(node)}\n",
            "th" or "td" => $"| {inner} ",
            "tr" => inner + "|\n",
            _ => inner
        };
    }

    private static string ConvertTable(HtmlNode table)
    {
        var sb = new StringBuilder();
        var rows = table.SelectNodes(".//tr");
        if (rows == null || rows.Count == 0) return "";

        bool headerProcessed = false;
        foreach (var row in rows)
        {
            var cells = row.SelectNodes("./th | ./td");
            if (cells == null) continue;

            sb.Append("| ");
            foreach (var cell in cells)
            {
                sb.Append(CleanText(cell.InnerText).Replace("|", "\\|")).Append(" | ");
            }
            sb.AppendLine();

            if (!headerProcessed)
            {
                sb.Append("| ");
                foreach (var _ in cells) sb.Append("--- | ");
                sb.AppendLine();
                headerProcessed = true;
            }
        }
        return sb.ToString();
    }

    private static HashSet<string> ParseHeadingTags(string headingSelector)
    {
        // Parse CSS selector like "h1, h2, h3" into tag names
        return headingSelector
            .Split(',')
            .Select(s => s.Trim().ToLowerInvariant())
            .Where(s => s.StartsWith('h') && s.Length == 2 && char.IsDigit(s[1]))
            .ToHashSet();
    }

    private static string? ExtractFirst(HtmlDocument doc, string selector)
    {
        var nodes = SelectNodes(doc, selector);
        return nodes.FirstOrDefault()?.InnerText;
    }

    private static string? ExtractMetaDescription(HtmlDocument doc, string selector)
    {
        // Handle meta[name=description] specially
        if (selector.Contains("meta[name=description]", StringComparison.OrdinalIgnoreCase))
        {
            var meta = doc.DocumentNode.SelectSingleNode("//meta[@name='description']");
            return meta?.GetAttributeValue("content", null);
        }
        return ExtractFirst(doc, selector);
    }

    private static List<string> ExtractAll(HtmlDocument doc, string selector)
    {
        var nodes = SelectNodes(doc, selector);
        return nodes.Select(n => CleanText(n.InnerText)).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
    }

    private static List<HtmlNode> SelectNodes(HtmlDocument doc, string selector)
    {
        // Try CSS selectors first (comma-separated tag/class selectors)
        var parts = selector.Split(',').Select(s => s.Trim()).ToList();
        var nodes = new List<HtmlNode>();

        foreach (var part in parts)
        {
            string xpath;
            if (part.StartsWith("//") || part.StartsWith("./"))
            {
                // Already XPath
                xpath = part;
            }
            else
            {
                // Convert simple CSS to XPath
                xpath = CssToXPath(part);
            }

            var found = doc.DocumentNode.SelectNodes(xpath);
            if (found != null)
                nodes.AddRange(found);
        }

        return nodes;
    }

    private static string CssToXPath(string css)
    {
        css = css.Trim();

        // Tag name only (e.g., "h1", "main", "article")
        if (css.All(c => char.IsLetterOrDigit(c) || c == '-'))
            return $"//{css}";

        // Class selector (e.g., ".content")
        if (css.StartsWith('.'))
            return $"//*[contains(concat(' ', normalize-space(@class), ' '), ' {css[1..]} ')]";

        // ID selector (e.g., "#main")
        if (css.StartsWith('#'))
            return $"//*[@id='{css[1..]}']";

        // Attribute selector (e.g., "meta[name=description]")
        var attrMatch = System.Text.RegularExpressions.Regex.Match(css, @"^(\w+)\[(\w+)=(\w+)\]$");
        if (attrMatch.Success)
            return $"//{attrMatch.Groups[1].Value}[@{attrMatch.Groups[2].Value}='{attrMatch.Groups[3].Value}']";

        // Fallback: try as-is XPath
        return $"//{css}";
    }

    private static string CleanText(string text)
    {
        return System.Net.WebUtility.HtmlDecode(text)
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Trim();
    }
}
