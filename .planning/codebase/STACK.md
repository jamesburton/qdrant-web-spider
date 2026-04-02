# Technology Stack

**Analysis Date:** 2026-04-02

## Languages

**Primary:**
- C# / .NET 10 - application code lives in `Program.cs`, the shared library in `shared/`, and tests in `tests/`.

**Secondary:**
- JSON - runtime configuration is defined in `spider.json`, `spider.local.json`, and `test.local.json`.
- YAML - CI publishing workflow is defined in `.github/workflows/publish.yml`.

## Runtime

**Environment:**
- .NET SDK 10 / `net10.0` - declared in `qdrant-web-spider.csproj`, `shared/Shared.csproj`, `tests/tests.csproj`, and the GitHub Actions setup in `.github/workflows/publish.yml`.

**Package Manager:**
- NuGet via SDK-style projects - dependencies are declared in `qdrant-web-spider.csproj`, `shared/Shared.csproj`, and `tests/tests.csproj`.
- Lockfile: missing; package restore is handled by `dotnet restore` in `.github/workflows/publish.yml`.

## Frameworks

**Core:**
- .NET console application / global tool - the executable entrypoint is `Program.cs`, with packaging enabled in `qdrant-web-spider.csproj` through `PackAsTool` and `ToolCommandName`.
- Shared class library - reusable crawl, embedding, Qdrant, and MCP logic lives under `shared/` and is built by `shared/Shared.csproj`.
- File-based apps (.NET 10) - alternate single-file entrypoints are `spider.cs`, `search.cs`, and `mcp-server.cs`, referenced in `README.md`.

**Testing:**
- xUnit `2.9.3` - test framework in `tests/tests.csproj`.
- Microsoft.NET.Test.Sdk `17.14.1` - test runner integration in `tests/tests.csproj`.
- coverlet.collector `6.0.4` - coverage collection in `tests/tests.csproj`.
- Moq `4.20.72` - mocking library in `tests/tests.csproj`.

**Build/Dev:**
- System.CommandLine `2.0.0-beta4.22272.1` - CLI command parsing in `Program.cs`.
- Microsoft.Extensions.Hosting `10.*` - host bootstrapping for the MCP server in `Program.cs` and `mcp-server.cs`.
- MinVer `6.0.0` - versioning during packaging in `qdrant-web-spider.csproj`.
- GitHub Actions - build, pack, and publish pipeline in `.github/workflows/publish.yml`.

## Key Dependencies

**Critical:**
- `Qdrant.Client` `1.17.*` - vector database client used by `shared/QdrantHelper.cs` for collection management, upserts, search, scrolling, and page retrieval.
- `OpenAI` `2.*` - embedding client used by `shared/OpenAiEmbeddingProvider.cs` for OpenAI-compatible APIs, Azure OpenAI, Ollama, and LM Studio.
- `Microsoft.SemanticKernel.Connectors.Onnx` `1.39.*-*` - ONNX text embedding service used by `shared/OnnxEmbeddingProvider.cs`.
- `Microsoft.ML.OnnxRuntime` `1.24.*` - local runtime backing the ONNX embedding path in `shared/OnnxEmbeddingProvider.cs`.
- `ModelContextProtocol` `1.2.0` - MCP server transport and tool registration in `Program.cs`, `mcp-server.cs`, and `shared/SpiderTools.cs`.

**Infrastructure:**
- `HtmlAgilityPack` `1.11.*` in `shared/Shared.csproj` and `1.12.4` in `tests/tests.csproj` - HTML parsing for page extraction tests and crawler logic.
- `Microsoft.Extensions.Hosting` `10.*` - DI container and app hosting for the MCP server in `Program.cs` and `mcp-server.cs`.
- `System.CommandLine.NamingConventionBinder` `2.0.0-beta4.22272.1` - binds CLI handler parameters in `Program.cs`.

## Configuration

**Environment:**
- Runtime config is JSON-driven through `SpiderConfig.LoadAsync` in `shared/Config.cs`.
- Secrets and overrides come from environment variables in `shared/Config.cs`: `QDRANT_API_KEY`, `OPENAI_API_KEY`, and `AZURE_OPENAI_API_KEY`.
- CLI overrides are also applied in `shared/Config.cs`: `--config`, `--qdrant-url`, `--collection`, `--provider`, `--model`, and `--embedding-url`.
- Local config files are present at `spider.local.json` and `test.local.json`; they are treated as environment-specific overlays.

**Build:**
- Main package metadata and tool packaging live in `qdrant-web-spider.csproj`.
- Shared library dependencies live in `shared/Shared.csproj`.
- Test dependencies live in `tests/tests.csproj`.
- CI build and publish steps are defined in `.github/workflows/publish.yml`.

## Platform Requirements

**Development:**
- Requires the .NET 10 SDK to build and run `Program.cs`, `spider.cs`, `search.cs`, and `mcp-server.cs`.
- Requires a reachable Qdrant instance; the default config points to `http://localhost:6334` in `spider.json`.
- Local ONNX embeddings require model files under the resolved user profile path from `EmbeddingConfig.ResolveModelPath` in `shared/Config.cs`, or permission to auto-download them via `shared/ModelDownloader.cs`.

**Production:**
- Primary deployment target is a packaged .NET global tool published to NuGet from `.github/workflows/publish.yml`.
- Runtime execution modes are local CLI commands (`crawl`, `search`, `mcp`) in `Program.cs` or file-based app execution shown in `README.md`.

---

*Stack analysis: 2026-04-02*
