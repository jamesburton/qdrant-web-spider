---
name: web-spider
description: Search and retrieve crawled web content from Qdrant vector database. Use when you need to find documentation, reference material, or any previously crawled web pages.
triggers:
  - search crawled pages
  - find in crawled docs
  - web spider search
  - look up documentation
  - what does the documentation say about
---

# Web Spider - Semantic Web Search

Search over web pages crawled and stored in Qdrant using the `qdrant-web-spider` MCP server.

## When to use

- User asks about content from a crawled website
- User needs documentation references from indexed sites
- User wants to check if crawled content is stale and needs re-crawling
- User asks "what does [site] say about X"

## Available MCP Tools

Use these tools directly (they are provided by the `qdrant-web-spider` MCP server):

| Tool | Use when |
|------|----------|
| `search_web_pages` | Finding content by semantic query. Params: `query`, `limit`, `scoreThreshold`, `staleDays` |
| `get_page` | Retrieving full page content by URL. Params: `url` |
| `list_urls` | Listing all crawled URLs or finding stale ones. Params: `staleDays` |
| `crawl_status` | Checking collection stats, configured sites, point counts |

## Staleness Detection

Content has a `captureDate` field. Use `staleDays` parameter to filter:
- `search_web_pages(query: "auth setup", staleDays: 30)` - only recent results
- `list_urls(staleDays: 7)` - find pages not refreshed in a week

If content is stale, suggest the user re-crawl:
```bash
qdrant-web-spider crawl --config spider.json
```

## Re-crawl When Not Matched

If a search returns no results or low-quality results for a topic the user expects to be covered:
1. Check `crawl_status` to see which sites are configured
2. Check `list_urls` to see what's been crawled
3. Suggest adding the relevant site to `spider.json` and re-crawling
4. Or suggest increasing `maxDepth` if the site is configured but pages are missing

## Example Workflow

```
User: "How do I configure authentication in the Qdrant docs?"

1. search_web_pages(query: "configure authentication", limit: 5)
2. If results found → present them
3. If no results → check crawl_status, suggest crawling qdrant docs
```
