# External Integrations

**Analysis Date:** 2026-04-02

## APIs & External Services

**Vector Database:**
- Qdrant - stores crawled chunks, payload metadata, and query results.
  - SDK/Client: `Qdrant.Client` in `shared/Shared.csproj`
  - Implementation: `shared/QdrantHelper.cs`
  - Auth: `QDRANT_API_KEY` env var or `qdrant.apiKey` in config loaded by `shared/Config.cs`
  - Endpoint config: `qdrant.url` in `spider.json`, `spider.local.json`, or `test.local.json`

**Embedding Providers:**
- OpenAI embeddings - used when `embedding.provider` is `openai`.
  - SDK/Client: `OpenAI` in `shared/Shared.csproj`
  - Implementation: `shared/OpenAiEmbeddingProvider.cs`
  - Auth: `OPENAI_API_KEY` env var or `embedding.apiKey` in config
- Azure OpenAI embeddings - used when `embedding.provider` is `azure-openai`.
  - SDK/Client: `OpenAI` in `shared/Shared.csproj`
  - Implementation: `shared/OpenAiEmbeddingProvider.cs`
  - Auth: `AZURE_OPENAI_API_KEY` env var or `embedding.apiKey` in config
  - Endpoint config: `embedding.baseUrl` in config loaded by `shared/Config.cs`
- Ollama embeddings - used through an OpenAI-compatible endpoint when `embedding.provider` is `ollama`.
  - SDK/Client: `OpenAI` in `shared/Shared.csproj`
  - Implementation: `shared/EmbeddingProvider.cs` defaults to `http://localhost:11434/v1`
  - Auth: none required by default
- LM Studio embeddings - used through an OpenAI-compatible endpoint when `embedding.provider` is `lmstudio`.
  - SDK/Client: `OpenAI` in `shared/Shared.csproj`
  - Implementation: `shared/EmbeddingProvider.cs` defaults to `http://localhost:1234/v1`
  - Auth: none required by default
- Local ONNX embeddings - default offline embedding provider when `embedding.provider` is `onnx`.
  - SDK/Client: `Microsoft.SemanticKernel.Connectors.Onnx` and `Microsoft.ML.OnnxRuntime` in `shared/Shared.csproj`
  - Implementation: `shared/OnnxEmbeddingProvider.cs`
  - Model source: Hugging Face URLs hardcoded in `shared/ModelDownloader.cs`

**Web Crawling Targets:**
- Arbitrary websites configured under `crawl.sites` in `spider.json`, `spider.local.json`, and `test.local.json`.
  - HTTP client: `HttpClient` instantiated in `Program.cs` and `spider.cs`
  - Robots.txt handling: `shared/RobotsTxt.cs`
  - Sitemap discovery: `shared/SitemapParser.cs`
  - Retry wrapper: `shared/HttpHelper.cs`

## Data Storage

**Databases:**
- Qdrant vector database
  - Connection: `qdrant.url` and optional `QDRANT_API_KEY`
  - Client: `Qdrant.Client` via `shared/QdrantHelper.cs`
  - Stored payload fields are written in `shared/CrawlService.cs`: `url`, `title`, `heading`, `chunkText`, `captureDate`, and `contentHash`

**File Storage:**
- Local filesystem only
  - ONNX model artifacts are stored under the resolved user profile path from `EmbeddingConfig.ResolveModelPath` in `shared/Config.cs`
  - Downloads are performed by `shared/ModelDownloader.cs`

**Caching:**
- None detected
  - Re-crawl deduplication relies on the `contentHash` payload in Qdrant through `shared/CrawlService.cs`, not a separate cache service

## Authentication & Identity

**Auth Provider:**
- No end-user authentication layer is present
  - The CLI and MCP server trust local execution context in `Program.cs` and `mcp-server.cs`

**Service Authentication:**
- Qdrant API key support is optional in `shared/QdrantHelper.cs`
- OpenAI and Azure OpenAI API key support is optional/required depending on provider in `shared/OpenAiEmbeddingProvider.cs`
- Ollama and LM Studio run without API keys by default in `shared/EmbeddingProvider.cs`

## Monitoring & Observability

**Error Tracking:**
- None detected

**Logs:**
- Console logging only
  - Crawl progress and retry output are written via `Console.WriteLine` or callback logging in `shared/CrawlService.cs`, `shared/EmbeddingProvider.cs`, `shared/HttpHelper.cs`, `shared/ModelDownloader.cs`, and `shared/SitemapParser.cs`
  - MCP host console logging is added in `mcp-server.cs`

## CI/CD & Deployment

**Hosting:**
- NuGet package distribution for the global tool
  - Package metadata and tool settings live in `qdrant-web-spider.csproj`
  - Publish target is `https://api.nuget.org/v3/index.json` in `.github/workflows/publish.yml`

**CI Pipeline:**
- GitHub Actions
  - Workflow: `.github/workflows/publish.yml`
  - Secret used: `NUGET_API_KEY`

## Environment Configuration

**Required env vars:**
- `QDRANT_API_KEY` for authenticated Qdrant deployments, read in `shared/Config.cs`
- `OPENAI_API_KEY` for the `openai` provider, read in `shared/Config.cs`
- `AZURE_OPENAI_API_KEY` for the `azure-openai` provider, read in `shared/Config.cs`

**Secrets location:**
- Local runtime secrets can be supplied through process environment variables read by `shared/Config.cs`
- Config files may also carry credentials through `qdrant.apiKey` and `embedding.apiKey` fields in `spider.local.json`
- CI publish credentials are stored as GitHub Actions secrets in `.github/workflows/publish.yml`

## Webhooks & Callbacks

**Incoming:**
- None

**Outgoing:**
- HTTPS/HTTP calls to configured crawl targets through `HttpClient` in `shared/CrawlService.cs`
- HTTP calls to `robots.txt` and sitemap endpoints through `shared/RobotsTxt.cs` and `shared/SitemapParser.cs`
- HTTP or HTTPS calls to Qdrant configured in `shared/QdrantHelper.cs`
- HTTP or HTTPS calls to embedding endpoints configured in `shared/OpenAiEmbeddingProvider.cs` and `shared/EmbeddingProvider.cs`
- HTTPS downloads from Hugging Face in `shared/ModelDownloader.cs`

## AI Agent Integration

**Model Context Protocol:**
- Local MCP server is exposed over stdio, not HTTP
  - Package: `ModelContextProtocol` in `qdrant-web-spider.csproj` and `shared/Shared.csproj`
  - Host setup: `Program.cs` and `mcp-server.cs`
  - Tool definitions: `shared/SpiderTools.cs`
  - Exposed tools: `search_web_pages`, `get_page`, `list_urls`, and `crawl_status`

---

*Integration audit: 2026-04-02*
