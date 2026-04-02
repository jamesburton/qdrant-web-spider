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
- **Staleness Detection:** SHA-256 content hashing skips unchanged pages on re-crawl; `captureDate` enables stale content queries.

## Installation

### Install as a global .NET tool

```bash
dotnet tool install -g qdrant-web-spider
```

### Run without installing (dnx)

.NET 10+ supports running tools directly without permanent installation:

```bash
# Run any command via dnx — no install required
dnx qdrant-web-spider crawl --config spider.json
dnx qdrant-web-spider search --query "how does vector search work?"
dnx qdrant-web-spider mcp --config spider.json
```

`dnx` downloads the tool on first use and caches it locally. Subsequent runs use the cache.

### Run from source (.NET 10 file-based apps)

```bash
dotnet spider.cs --config spider.local.json
dotnet search.cs --query "test"
dotnet mcp-server.cs --config spider.local.json
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

| Argument | Description |
|----------|-------------|
| `--config <path>` | Path to JSON config file |
| `--auto-download` | Auto-download ONNX model without prompting |
| `--api-key <key>` | OpenAI/Azure API key (overrides env var) |
| `--provider <name>` | Embedding provider |
| `--model <name>` | Embedding model |
| `--qdrant-url <url>` | Qdrant endpoint (default: `http://localhost:6334`) |
| `--collection <name>` | Qdrant collection name |

## Spider (Crawler)

```bash
qdrant-web-spider crawl --config spider.json
```

The crawler performs BFS traversal per configured site:
- Follows same-domain links up to `maxDepth`
- Respects `robots.txt` (Disallow rules and Crawl-delay)
- Discovers URLs from `sitemap.xml` automatically
- Extracts content using configured CSS/XPath selectors
- Chunks text by heading boundaries (~512 token budget)
- Generates embeddings and stores chunks in Qdrant
- Skips unchanged pages on re-crawl (SHA-256 content hash)

Each stored chunk includes: `url`, `title`, `heading`, `chunkIndex`, `chunkText`, `summary`, `contentSelector`, `captureDate`, `contentHash`.

### Extraction Modes

Configurable per-site or globally in `spider.json`:

- `Markdown` (Default): Converts HTML to clean Markdown tables, lists, and code blocks.
- `Html`: Persists raw HTML fragments.
- `Text`: Persists plain text only.

## Search CLI

```bash
qdrant-web-spider search --query "how to configure authentication"
qdrant-web-spider search --query "API rate limits" --limit 10 --json
qdrant-web-spider search --query "setup guide" --stale-days 30
```

| Flag | Description |
|------|-------------|
| `--query <text>` | Search query (required) |
| `--limit <n>` | Max results (default 5) |
| `--json` | JSON output format |
| `--stale-days <n>` | Only show results captured within N days |

## MCP Server

The MCP server exposes search tools via stdio transport for use with Claude Code, Cursor, and other MCP-compatible AI agents.

```bash
qdrant-web-spider mcp --config spider.json
```

### Available Tools

| Tool | Description | Parameters |
|------|-------------|------------|
| `search_web_pages` | Semantic search over crawled pages | `query`, `limit?`, `scoreThreshold?`, `staleDays?` |
| `get_page` | Retrieve all stored chunks for a URL | `url` |
| `list_urls` | List all crawled URLs with capture dates | `staleDays?` |
| `crawl_status` | Collection stats and configuration | — |

### Claude Code Integration

#### Option 1: Project-level (recommended)

This repo includes `.claude/settings.json` pre-configured. Clone the repo and the MCP server is available automatically when working in this directory.

#### Option 2: Global settings

Add to `~/.claude/settings.json` to make the MCP server available in all projects:

```json
{
  "mcpServers": {
    "qdrant-web-spider": {
      "command": "qdrant-web-spider",
      "args": ["mcp", "--config", "/path/to/spider.json"]
    }
  }
}
```

#### Option 3: Via dnx (no install)

```json
{
  "mcpServers": {
    "qdrant-web-spider": {
      "command": "dnx",
      "args": ["qdrant-web-spider", "mcp", "--config", "/path/to/spider.json"]
    }
  }
}
```

#### Option 4: From source

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

### Claude Code Skill

This repo includes a Claude Code skill at `.claude/skills/web-spider/SKILL.md` that teaches Claude when and how to use the MCP tools. When working in this repo, Claude will automatically use `search_web_pages`, `get_page`, `list_urls`, and `crawl_status` to answer questions about crawled content.

### Claude Code Agent

A `crawl-and-index` agent is defined at `.claude/agents/crawl-and-index.md` for autonomous crawling workflows. It can read and modify `spider.json`, run the crawler, and report results.

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

Set API keys via environment variables or in the config JSON. Priority: CLI args > env vars > JSON values.

### Staleness Detection

Content is tracked with `captureDate` (ISO 8601) and `contentHash` (SHA-256) fields:

- **On re-crawl:** Pages with unchanged content hash are skipped automatically.
- **Search filtering:** Use `--stale-days 30` to only return results captured within the last 30 days.
- **MCP queries:** The `staleDays` parameter on `search_web_pages` and `list_urls` enables stale content discovery.

To find and refresh stale content:
```bash
# Find URLs not refreshed in 7 days (via MCP: list_urls with staleDays=7)
# Then re-crawl to refresh
qdrant-web-spider crawl --config spider.json
```

## Architecture

```
qdrant-web-spider/
  shared/                      # Class library (Shared.csproj)
    Config.cs                  # SpiderConfig model + loader
    QdrantHelper.cs            # Collection management, upsert, search, scroll
    EmbeddingProvider.cs       # IEmbeddingProvider interface + factory + retry wrapper
    OnnxEmbeddingProvider.cs   # Local ONNX via SemanticKernel.Connectors.Onnx
    OpenAiEmbeddingProvider.cs # OpenAI / Azure / Ollama / LM Studio
    Chunker.cs                 # Heading-boundary + token-budget splitting
    PageExtractor.cs           # HtmlAgilityPack extraction + link discovery
    ModelDownloader.cs         # Auto-download ONNX models from Hugging Face
    RobotsTxt.cs               # robots.txt parsing
    SitemapParser.cs           # sitemap.xml discovery
    CrawlService.cs            # BFS crawl orchestration
    SearchService.cs           # Search query execution
    SpiderTools.cs             # MCP tool definitions
    HttpHelper.cs              # Resilient HTTP with retries
  Program.cs                   # Unified CLI entry point (System.CommandLine)
  spider.cs                    # File-based app: crawl
  search.cs                    # File-based app: search
  mcp-server.cs                # File-based app: MCP server
  spider.json                  # Config template
  .claude/
    settings.json              # Project-level MCP server config
    skills/web-spider/SKILL.md # Claude Code skill for search/retrieval
    agents/crawl-and-index.md  # Claude Code agent for autonomous crawling
```

## Requirements

- .NET 10 SDK (for file-based apps and dnx)
- Qdrant instance (local Docker or cloud)
- No API keys needed with default ONNX provider

## References

- [File-based apps (.NET 10)](https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps)
- [Running tools via dnx](https://andrewlock.net/exploring-dotnet-10-preview-features-5-running-one-off-dotnet-tools-with-dnx/)
- [Qdrant .NET Client](https://github.com/qdrant/qdrant-dotnet)
- [Model Context Protocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
- [all-MiniLM-L6-v2](https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2)
