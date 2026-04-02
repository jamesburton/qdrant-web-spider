# Architecture

**Analysis Date:** 2026-04-02

## Pattern Overview

**Overall:** Thin entry-point composition over a shared application library.

**Key Characteristics:**
- Runtime entry points in `Program.cs`, `spider.cs`, `search.cs`, and `mcp-server.cs` only wire configuration, infrastructure clients, and top-level commands.
- Core behavior lives in the shared project `shared/Shared.csproj`, with services and helpers all under the single `QdrantWebSpider` namespace.
- The same domain layer is reused by three surfaces: packaged CLI commands, file-based .NET scripts, and MCP tools.

## Layers

**Entry Points:**
- Purpose: Start the process, parse arguments, load configuration, and compose services.
- Location: `Program.cs`, `spider.cs`, `search.cs`, `mcp-server.cs`
- Contains: `System.CommandLine` setup, host bootstrap code, and minimal orchestration.
- Depends on: `shared/Config.cs`, `shared/CrawlService.cs`, `shared/SearchService.cs`, `shared/QdrantHelper.cs`, `shared/EmbeddingProvider.cs`, `shared/SpiderTools.cs`
- Used by: CLI users running `qdrant-web-spider`, `dotnet spider.cs`, `dotnet search.cs`, or `dotnet mcp-server.cs`

**Configuration Layer:**
- Purpose: Normalize config from JSON, environment variables, and CLI overrides into immutable records.
- Location: `shared/Config.cs`
- Contains: `SpiderConfig`, `QdrantConfig`, `EmbeddingConfig`, `CrawlConfig`, `SiteConfig`, `SelectorConfig`, and `ExtractionMode`
- Depends on: `System.Text.Json`
- Used by: All entry points and MCP tool execution

**Application Services:**
- Purpose: Implement crawl and semantic search workflows.
- Location: `shared/CrawlService.cs`, `shared/SearchService.cs`
- Contains: crawl orchestration, page deduplication, embedding generation, Qdrant writes, and semantic search filtering
- Depends on: `shared/PageExtractor.cs`, `shared/Chunker.cs`, `shared/QdrantHelper.cs`, `shared/EmbeddingProvider.cs`, `shared/RobotsTxt.cs`, `shared/SitemapParser.cs`, `shared/HttpHelper.cs`
- Used by: `Program.cs`, `spider.cs`, `search.cs`, and `shared/SpiderTools.cs`

**Content Processing Layer:**
- Purpose: Convert raw HTML into structured sections and chunked text suitable for embedding.
- Location: `shared/PageExtractor.cs`, `shared/Chunker.cs`
- Contains: selector-based extraction, markdown/text/html conversion, same-host link extraction, section splitting, and chunk sizing
- Depends on: `HtmlAgilityPack` and config selector records from `shared/Config.cs`
- Used by: `shared/CrawlService.cs`

**Infrastructure Adapters:**
- Purpose: Wrap external systems behind project-specific APIs.
- Location: `shared/QdrantHelper.cs`, `shared/EmbeddingProvider.cs`, `shared/OnnxEmbeddingProvider.cs`, `shared/OpenAiEmbeddingProvider.cs`, `shared/ModelDownloader.cs`, `shared/HttpHelper.cs`
- Contains: Qdrant collection management, search/upsert helpers, embedding provider factory and retry wrapper, HTTP retry behavior, and ONNX model download support
- Depends on: `Qdrant.Client`, `OpenAI`, `Microsoft.SemanticKernel.Connectors.Onnx`, `Microsoft.ML.OnnxRuntime`, and `HttpClient`
- Used by: application services and entry points

**Protocol Surface:**
- Purpose: Expose the search index to AI clients over MCP stdio transport.
- Location: `shared/SpiderTools.cs`, `mcp-server.cs`, `Program.cs`
- Contains: MCP tool methods for `search_web_pages`, `get_page`, `list_urls`, and `crawl_status`
- Depends on: `ModelContextProtocol`, `shared/SearchService.cs`, `shared/QdrantHelper.cs`, `shared/Config.cs`
- Used by: MCP clients connected to the `mcp` command or `dotnet mcp-server.cs`

## Data Flow

**Crawl Flow:**

1. `Program.cs` or `spider.cs` loads `SpiderConfig` from `shared/Config.cs`, creates `QdrantHelper`, creates an `IEmbeddingProvider`, and constructs `CrawlService`.
2. `shared/CrawlService.cs` loops configured sites, fetches `robots.txt` via `shared/RobotsTxt.cs`, optionally expands sitemap seeds through `shared/SitemapParser.cs`, and downloads page HTML through `shared/HttpHelper.cs`.
3. `shared/PageExtractor.cs` converts HTML into `ExtractedPage` sections and links, then `shared/Chunker.cs` converts sections into bounded chunks.
4. `shared/CrawlService.cs` hashes the page body, checks existing chunks in Qdrant through `shared/QdrantHelper.cs`, embeds new chunks through the configured embedding provider, and upserts points into Qdrant.

**Search Flow:**

1. `Program.cs` or `search.cs` resolves the query and constructs `SearchService`.
2. `shared/SearchService.cs` embeds the query through `IEmbeddingProvider`.
3. `shared/QdrantHelper.cs` executes vector search against the configured collection.
4. `shared/SearchService.cs` maps raw payloads into `SearchResult` records and optionally filters by `captureDate`.

**MCP Flow:**

1. `mcp-server.cs` or the `mcp` subcommand in `Program.cs` builds a `Host` and registers config, `QdrantHelper`, `IEmbeddingProvider`, and `SpiderTools`.
2. MCP clients call tool methods defined in `shared/SpiderTools.cs`.
3. Tool methods either delegate to `SearchService` or query Qdrant directly through `QdrantHelper`.
4. Results are formatted as markdown/plain text responses for the MCP client.

**State Management:**
- Configuration state is immutable record data from `shared/Config.cs`.
- Crawl traversal state is held in-memory per run inside `shared/CrawlService.cs` using queues, visited sets, counters, and a `SemaphoreSlim`.
- Persistent state is stored in Qdrant only; there is no local database or background job state store.

## Key Abstractions

**`SpiderConfig`:**
- Purpose: Single normalized runtime configuration object.
- Examples: `shared/Config.cs`, `Program.cs`, `mcp-server.cs`
- Pattern: Load once at startup, then inject or pass through to services.

**`IEmbeddingProvider`:**
- Purpose: Provider-agnostic interface for turning text into vectors.
- Examples: `shared/EmbeddingProvider.cs`, `shared/OnnxEmbeddingProvider.cs`, `shared/OpenAiEmbeddingProvider.cs`
- Pattern: Factory-selected adapter wrapped by `RetryingEmbeddingProvider`.

**`QdrantHelper`:**
- Purpose: Repository-style adapter over the Qdrant client.
- Examples: `shared/QdrantHelper.cs`
- Pattern: Encapsulate collection lifecycle, point writes, search, and URL-based lookups behind task-returning methods.

**`ExtractedPage` and `Chunk`:**
- Purpose: Intermediate data contracts between extraction, chunking, and embedding.
- Examples: `shared/PageExtractor.cs`, `shared/Chunker.cs`
- Pattern: Pure records passed between processing stages.

## Entry Points

**Packaged CLI Host:**
- Location: `Program.cs`
- Triggers: `qdrant-web-spider crawl`, `qdrant-web-spider search`, `qdrant-web-spider mcp`
- Responsibilities: Define command tree, parse options, instantiate shared services, and handle embedding startup failures consistently

**File-Based Crawl Script:**
- Location: `spider.cs`
- Triggers: `dotnet spider.cs --config ...`
- Responsibilities: Minimal crawl bootstrap for local development without packaging

**File-Based Search Script:**
- Location: `search.cs`
- Triggers: `dotnet search.cs --query ...`
- Responsibilities: Minimal search bootstrap and console formatting

**File-Based MCP Script:**
- Location: `mcp-server.cs`
- Triggers: `dotnet mcp-server.cs`
- Responsibilities: Start an MCP stdio host backed by the shared library

## Error Handling

**Strategy:** Catch provider startup and embedding failures at process boundaries, and skip failed pages during long crawl runs.

**Patterns:**
- `Program.cs` and `mcp-server.cs` catch `EmbeddingProviderException` during startup and return a user-visible error without crashing deeper in the call stack.
- `shared/CrawlService.cs` catches embedding failures per page, logs a crawl-local error, and continues processing the queue.
- `shared/RobotsTxt.cs` and `shared/SitemapParser.cs` degrade to permissive defaults or warnings instead of failing the entire crawl.
- `shared/EmbeddingProvider.cs` centralizes retries and wraps terminal failures in `EmbeddingProviderException`.

## Cross-Cutting Concerns

**Logging:** Console-oriented logging only. `Program.cs`, `shared/CrawlService.cs`, `shared/EmbeddingProvider.cs`, and `shared/SitemapParser.cs` write directly to console or via a passed `Action<string>`.

**Validation:** Startup validation is implicit. `shared/Config.cs` supplies defaults and reads overrides, while command-required values such as `--query` are enforced in `Program.cs` or manually checked in `search.cs`.

**Authentication:** External credentials are configuration-driven. `shared/Config.cs` overlays `QDRANT_API_KEY`, `OPENAI_API_KEY`, and `AZURE_OPENAI_API_KEY` into the config records; infrastructure adapters consume those values when constructing clients.

---

*Architecture analysis: 2026-04-02*
