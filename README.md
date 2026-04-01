# Qdrant Web Spider

A set of .NET 10 file-based apps that crawl websites and store the results as vector embeddings in a Qdrant database. Includes a search CLI and an MCP server for integration with Claude Code and other AI agents.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Qdrant](https://qdrant.tech/) (local instance or cloud)

## Quick Start

```bash
# 1. Start a local Qdrant instance (Docker)
docker run -p 6333:6333 -p 6334:6334 qdrant/qdrant

# 2. Create your local config (copy and edit)
cp spider.json spider.local.json
# Edit spider.local.json with your target sites

# 3. Crawl
dotnet spider.cs --config spider.local.json

# 4. Search
dotnet search.cs --config spider.local.json --query "your search query"
```

On first run with the default ONNX embedding provider, you'll be prompted to download the `all-MiniLM-L6-v2` model (~80 MB). Use `--auto-download` to skip the prompt.

## File-Based Apps

| File | Purpose |
|------|---------|
| `spider.cs` | Web crawler — fetches pages, chunks content, generates embeddings, stores in Qdrant |
| `search.cs` | Search CLI — semantic search over crawled content |
| `mcp-server.cs` | MCP server — exposes search tools for Claude Code / AI agent integration |
| `shared/` | Shared class library — config, Qdrant helpers, embedding providers, HTML extraction, chunking |

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
