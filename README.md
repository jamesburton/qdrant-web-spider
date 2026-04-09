# Qdrant Web Spider

A semantic web crawler and search tool that indexes content into [Qdrant](https://qdrant.tech) for vector search. Crawl documentation sites, search them from the CLI, or expose them as MCP tools for AI agents.

## Features

- **Parallel crawling** with polite per-site concurrency and configurable delay
- **Intelligent extraction** to clean Markdown (default), HTML, or plain text
- **Sitemap discovery** from `robots.txt` and sitemap indices
- **Resilient** HTTP and embedding requests with exponential backoff retries
- **Semantic search** via CLI or MCP tools
- **Hybrid embeddings** — local ONNX (zero config), OpenAI, Azure OpenAI, Ollama, or LM Studio
- **Staleness detection** — SHA-256 content hashing skips unchanged pages; `captureDate` enables freshness queries
- **Single-page mode** — set `maxDepth: 0` to crawl just one URL without following links

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A running [Qdrant](https://qdrant.tech/documentation/quick-start/) instance
- No API keys needed with the default ONNX embedding provider

## Quick Start

```bash
# 1. Start Qdrant
docker run -p 6333:6333 -p 6334:6334 qdrant/qdrant

# 2. Configure target sites in spider.json

# 3. Crawl
dnx qdrant-web-spider crawl --config spider.json

# 4. Search
dnx qdrant-web-spider search --query "how does vector search work?"
```

## Installation

### Via dnx (no install required)

[`dnx`](https://andrewlock.net/exploring-dotnet-10-preview-features-5-running-one-off-dotnet-tools-with-dnx/) downloads the tool on first use and caches it locally:

```bash
dnx qdrant-web-spider crawl --config spider.json
dnx qdrant-web-spider search --query "authentication setup"
dnx qdrant-web-spider mcp --config spider.json
```

For pre-release versions:

```bash
# Latest pre-release
dnx --prerelease qdrant-web-spider crawl --config spider.json

# Pinned version
dnx qdrant-web-spider@1.0.0 crawl --config spider.json

# From a local nupkg
dnx --add-source ./nupkgs qdrant-web-spider crawl --config spider.json
```

### Global .NET tool

```bash
dotnet tool install -g qdrant-web-spider
qdrant-web-spider crawl --config spider.json
```

### From source

The project uses .NET 10 [file-based apps](https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps) — each `.cs` file in the root is independently runnable:

```bash
dotnet spider.cs --config spider.local.json
dotnet search.cs --query "test"
dotnet mcp-server.cs --config spider.local.json
```

## Commands

| Command | Description |
|---------|-------------|
| `crawl` | Fetch pages, chunk text, generate embeddings, store in Qdrant |
| `search` | Semantic search over crawled content |
| `mcp` | Start MCP server exposing search tools for AI agents |

### Global Options

| Flag | Description |
|------|-------------|
| `--config <path>` | Path to JSON config file |
| `--auto-download` | Auto-download ONNX model without prompting |
| `--api-key <key>` | OpenAI/Azure API key (overrides env var) |
| `--provider <name>` | Embedding provider |
| `--model <name>` | Embedding model |
| `--qdrant-url <url>` | Qdrant endpoint (default: `http://localhost:6334`) |
| `--collection <name>` | Qdrant collection name |

## Crawling

```bash
qdrant-web-spider crawl --config spider.json
```

The crawler performs BFS traversal per configured site:

- Follows same-domain links up to `maxDepth` (set `0` for single-page crawl)
- Respects `robots.txt` (Disallow rules and Crawl-delay)
- Discovers URLs from `sitemap.xml` automatically (skipped when `maxDepth` is 0)
- Extracts content using configured CSS/XPath selectors
- Chunks text by heading boundaries (~512 token budget)
- Generates embeddings and stores chunks in Qdrant
- Skips unchanged pages on re-crawl (SHA-256 content hash)

Each chunk stores: `url`, `title`, `heading`, `chunkIndex`, `chunkText`, `summary`, `contentSelector`, `captureDate`, `contentHash`.

### Extraction Modes

Set per-site or globally via the `mode` field:

| Mode | Description |
|------|-------------|
| `Markdown` (default) | Clean Markdown with tables, lists, and code blocks |
| `Html` | Raw HTML fragments |
| `Text` | Plain text only |

## Searching

```bash
qdrant-web-spider search --query "how to configure authentication"
qdrant-web-spider search --query "API rate limits" --limit 10 --json
qdrant-web-spider search --query "setup guide" --stale-days 30
```

| Flag | Description |
|------|-------------|
| `--query <text>` | Search query (required) |
| `--limit <n>` | Max results (default 5) |
| `--json` | Output as JSON |
| `--stale-days <n>` | Only results captured within N days |

## MCP Server

Exposes search tools via stdio transport for Claude Code, Cursor, and other MCP-compatible agents.

```bash
qdrant-web-spider mcp --config spider.json
```

### Tools

| Tool | Description | Parameters |
|------|-------------|------------|
| `search_web_pages` | Semantic search over crawled pages | `query`, `limit?`, `scoreThreshold?`, `staleDays?` |
| `get_page` | Retrieve all chunks for a URL | `url` |
| `list_urls` | List all crawled URLs with capture dates | `staleDays?` |
| `crawl_status` | Collection stats and configuration | — |

### Integration

**Claude Code** — add to `~/.claude.json`:

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

**Via dnx** (no global install):

```json
{
  "mcpServers": {
    "qdrant-web-spider": {
      "command": "dnx",
      "args": ["--prerelease", "qdrant-web-spider", "mcp", "--config", "/path/to/spider.json"]
    }
  }
}
```

**From source:**

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

This repo also includes `.claude/settings.json` (project-level MCP config), a skill definition at `.claude/skills/web-spider/SKILL.md`, and a `crawl-and-index` agent at `.claude/agents/crawl-and-index.md`.

## Configuration

Copy `spider.json` to `spider.local.json` (gitignored) and edit:

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
    ],
    "respectRobotsTxt": true,
    "requestDelayMs": 500,
    "maxConcurrency": 4,
    "userAgent": "QdrantWebSpider/1.0",
    "mode": "Markdown"
  }
}
```

### Embedding Providers

| Provider | Config value | API key | Default model |
|----------|-------------|---------|---------------|
| ONNX (default) | `onnx` | No | `all-MiniLM-L6-v2` (384 dims) |
| OpenAI | `openai` | `OPENAI_API_KEY` | `text-embedding-3-small` (1536 dims) |
| Azure OpenAI | `azure-openai` | `AZURE_OPENAI_API_KEY` | configurable |
| Ollama | `ollama` | No | `nomic-embed-text` (768 dims) |
| LM Studio | `lmstudio` | No | configurable |

Priority: CLI args > environment variables > config JSON values.

### Staleness Detection

Each chunk stores `captureDate` (ISO 8601) and `contentHash` (SHA-256):

- **Re-crawl:** Unchanged pages (same hash) are skipped automatically
- **Search:** `--stale-days 30` filters to results captured within 30 days
- **MCP:** `staleDays` parameter on `search_web_pages` and `list_urls`

## Project Structure

```
qdrant-web-spider/
  Program.cs                   # CLI entry point (crawl, search, mcp commands)
  spider.cs                    # File-based app: crawl
  search.cs                    # File-based app: search
  mcp-server.cs                # File-based app: MCP server
  spider.json                  # Config template
  shared/
    Config.cs                  # Configuration model + three-tier loader
    CrawlService.cs            # BFS crawl orchestration
    SearchService.cs           # Search query execution
    SpiderTools.cs             # MCP tool definitions
    QdrantHelper.cs            # Qdrant client wrapper
    EmbeddingProvider.cs       # IEmbeddingProvider interface + factory + retry
    OnnxEmbeddingProvider.cs   # Local ONNX via Semantic Kernel
    OpenAiEmbeddingProvider.cs # OpenAI / Azure / Ollama / LM Studio
    Chunker.cs                 # Heading-boundary + token-budget chunking
    PageExtractor.cs           # HTML extraction + link discovery
    ModelDownloader.cs         # ONNX model auto-download from Hugging Face
    RobotsTxt.cs               # robots.txt parser
    SitemapParser.cs           # sitemap.xml discovery
    HttpHelper.cs              # Resilient HTTP with retries
  tests/
    ChunkerTests.cs
    PageExtractorTests.cs
    EmbeddingProviderTests.cs
    RobotsTxtTests.cs
    ConfigTests.cs
  .claude/
    settings.json              # Project-level MCP config
    skills/web-spider/SKILL.md # Claude Code skill
    agents/crawl-and-index.md  # Claude Code agent
```

## References

- [File-based apps (.NET 10)](https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps)
- [Running tools via dnx](https://andrewlock.net/exploring-dotnet-10-preview-features-5-running-one-off-dotnet-tools-with-dnx/)
- [Qdrant .NET Client](https://github.com/qdrant/qdrant-dotnet)
- [Model Context Protocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
- [all-MiniLM-L6-v2](https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2)

## License

MIT
