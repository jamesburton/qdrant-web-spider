# Qdrant Web Spider

A high-performance semantic web crawler and search CLI that stores data in Qdrant. It features site-level parallelism, sitemap discovery, resilient retries, and intelligent Markdown extraction.

## Features

- **Parallel Crawling:** Multi-site concurrent crawling with polite per-site concurrency.
- **Intelligent Extraction:** Extracts content as clean **Markdown** (default), HTML, or Text.
- **Sitemap Support:** Discovers URLs from `sitemap.xml` and sitemap indices.
- **Resilient:** Automatic exponential backoff retries for HTTP and Embedding requests.
- **Semantic Search:** Built-in CLI for performing vector search over crawled content.
- **MCP Integration:** Full Model Context Protocol (MCP) support for use with Claude Code and other AI agents.
- **Hybrid Embeddings:** Supports local ONNX (CPU) or OpenAI/Azure/Ollama providers.

## Installation

Install as a global .NET tool:

```bash
dotnet tool install -g qdrant-web-spider
```

## Quick Start

```bash
# 1. Start Qdrant (Docker)
docker run -p 6333:6333 -p 6334:6334 qdrant/qdrant

# 2. Configure sites in spider.json
# 3. Crawl
qdrant-web-spider crawl --config spider.json

# 4. Search
qdrant-web-spider search --query "how does vector search work?"
```

## Commands

| Command | Description |
|---------|-------------|
| `crawl` | Web crawler — fetches pages, chunks, generates embeddings, stores in Qdrant |
| `search` | Search CLI — semantic search over crawled content |
| `mcp` | MCP server — exposes search tools for AI agent integration |

### Global Options

- `--config <path>`: Path to JSON config file
- `--auto-download`: Auto-download ONNX model without prompting
- `--api-key <key>`: OpenAI/Azure API key (overrides env var)
- `--qdrant-url <url>`: Qdrant endpoint (default: http://localhost:6334)
- `--collection <name>`: Qdrant collection name

## Extraction Modes

The spider supports three extraction modes, configurable in `spider.json`:

- `Markdown` (Default): Converts HTML to clean Markdown tables, lists, and code blocks.
- `Html`: Persists raw HTML fragments.
- `Text`: Persists plain text only.

## Development

```bash
# Run directly from source
dotnet spider.cs --config spider.local.json
dotnet search.cs --query "test"
dotnet mcp-server.cs
```

## Configuration

Create a `spider.local.json` (gitignored) based on `spider.json`:

```json
{
  "qdrant": {
    "url": "http://localhost:6334",
    "collectionName": "qdrant-web-spider"
  },
  "embedding": {
    "provider": "onnx",
    "model": "sentence-transformers/all-MiniLM-L6-v2",
    "dimensions": 384
  },
  "crawl": {
    "sites": [
      {
        "url": "https://docs.example.com",
        "maxDepth": 3,
        "selectors": {
          "content": "main, article, .content",
          "title": "h1, title",
          "heading": "h1, h2, h3",
          "summary": "meta[name=description]"
        }
      }
    ]
  }
}
```

### Embedding Providers

| Provider | `provider` value | API key required | Default model |
|----------|-----------------|------------------|---------------|
| Local ONNX (default) | `onnx` | No | `all-MiniLM-L6-v2` (384 dims) |
| OpenAI | `openai` | Yes (`OPENAI_API_KEY`) | `text-embedding-3-small` (1536 dims) |
| Azure OpenAI | `azure-openai` | Yes (`AZURE_OPENAI_API_KEY`) | configurable |
| Ollama | `ollama` | No | `nomic-embed-text` (768 dims) |
| LM Studio | `lmstudio` | No | configurable |

Set API keys via environment variables or in the config JSON.

### CLI Arguments

Arguments override config file values:

| Argument | Description |
|----------|-------------|
| `--config <path>` | Config file path |
| `--provider <name>` | Embedding provider |
| `--model <name>` | Embedding model |
| `--qdrant-url <url>` | Qdrant server URL |
| `--collection <name>` | Qdrant collection name |
| `--auto-download` | Auto-download ONNX model without prompting |

## Spider (Crawler)

```bash
dotnet spider.cs --config spider.local.json
```

The crawler performs BFS traversal per configured site:
- Follows same-domain links up to `maxDepth`
- Respects `robots.txt` (Disallow rules and Crawl-delay)
- Extracts content using configured CSS/XPath selectors
- Chunks text by heading boundaries (~512 token budget)
- Generates embeddings and stores chunks in Qdrant
- Skips unchanged pages on re-crawl (SHA-256 content hash)

Each stored chunk includes: `url`, `title`, `heading`, `chunkText`, `summary`, `contentSelector`, `captureDate`, `contentHash`.

## Search CLI

```bash
dotnet search.cs --config spider.local.json --query "how to configure authentication"
dotnet search.cs --config spider.local.json --query "API rate limits" --limit 10 --json
dotnet search.cs --config spider.local.json --query "setup guide" --stale-days 30
```

| Flag | Description |
|------|-------------|
| `--query <text>` | Search query (required) |
| `--limit <n>` | Max results (default 5) |
| `--json` | JSON output format |
| `--stale-days <n>` | Only show results captured within N days |

## MCP Server

The MCP server exposes these tools via stdio transport:

| Tool | Description |
|------|-------------|
| `search_web_pages` | Semantic search with query, limit, score threshold, stale days |
| `get_page` | Retrieve all stored chunks for a URL |
| `list_urls` | List all crawled URLs with capture dates |
| `crawl_status` | Collection stats and configuration |

### Claude Code Integration

Add to your `.claude/settings.json`:

```json
{
  "mcpServers": {
    "qdrant-web-spider": {
      "command": "dotnet",
      "args": ["run", "--file", "mcp-server.cs", "--", "--config", "spider.local.json"]
    }
  }
}
```

## References

- [File-based apps (.NET 10)](https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps)
- [Running tools via dnx](https://andrewlock.net/exploring-dotnet-10-preview-features-5-running-one-off-dotnet-tools-with-dnx/)
- [Qdrant .NET Client](https://github.com/qdrant/qdrant-dotnet)
- [Model Context Protocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
- [all-MiniLM-L6-v2](https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2)
