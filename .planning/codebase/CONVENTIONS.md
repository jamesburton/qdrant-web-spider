# Coding Conventions

**Analysis Date:** 2026-04-02

## Naming Patterns

**Files:**
- Use PascalCase for shared C# source files in `shared\*.cs`, for example `shared\PageExtractor.cs`, `shared\SearchService.cs`, and `shared\OpenAiEmbeddingProvider.cs`.
- Use descriptive top-level file names for file-based entrypoints at the repo root, for example `spider.cs`, `search.cs`, and `mcp-server.cs`.
- Test files follow `<Subject>Tests.cs` in `tests\`, for example `tests\PageExtractorTests.cs`, `tests\ChunkerTests.cs`, and `tests\EmbeddingProviderTests.cs`.

**Functions:**
- Use PascalCase for methods and local functions, for example `shared\Config.cs` uses `LoadAsync`, `GetArgValue`, and `ResolveModelPath`, while `Program.cs` defines `ReportEmbeddingFailure`.
- Use the `Async` suffix on asynchronous methods, including `shared\CrawlService.cs`, `shared\SearchService.cs`, `shared\QdrantHelper.cs`, and `shared\HttpHelper.cs`.
- Keep private helper names action-oriented and concise, such as `ExtractAll`, `SelectNodes`, `CssToXPath`, `ComputeHash`, and `SplitBySentences` in `shared\PageExtractor.cs` and `shared\Chunker.cs`.

**Variables:**
- Use camelCase for locals, parameters, and fields. Constructor-injected dependencies stay camelCase in primary constructors, as shown in `shared\CrawlService.cs` and `shared\SearchService.cs`.
- Prefix private fields with `_` in conventional class bodies, as shown in `shared\OpenAiEmbeddingProvider.cs`, `shared\QdrantHelper.cs`, and `shared\OnnxEmbeddingProvider.cs`.
- Use descriptive tuple element names when tuples are needed, such as `(string Url, int Depth)` in `shared\CrawlService.cs`.

**Types:**
- Use PascalCase for classes, records, interfaces, and enums. Data-shape types are commonly records, such as `SpiderConfig`, `SearchResult`, `ExtractedPage`, `ExtractedSection`, and `Chunk` in `shared\Config.cs`, `shared\SearchService.cs`, `shared\PageExtractor.cs`, and `shared\Chunker.cs`.
- Prefix interfaces with `I`, as shown by `IEmbeddingProvider` in `shared\EmbeddingProvider.cs`.
- Use singular type names for value objects and services, for example `QdrantHelper`, `SearchService`, `ModelDownloader`, and `SelectorConfig`.

## Code Style

**Formatting:**
- No repository-level formatter config is detected. There is no `.editorconfig`, `.prettierrc`, `eslint.config.*`, or equivalent tool config in the project root.
- Follow the formatting already present in `shared\*.cs` and `tests\*.cs`: 4-space indentation, braces on new lines, file-scoped namespaces, and blank lines between logical sections.
- Prefer collection expressions and modern C# syntax where already used, such as `Sites { get; init; } = [];` in `shared\Config.cs` and `Headings: ["H1", "H2"]` in `tests\ChunkerTests.cs`.

**Linting:**
- No Roslyn analyzer configuration or custom rule set is checked in. Quality gates come from compiler settings in `qdrant-web-spider.csproj`, `shared\Shared.csproj`, and `tests\tests.csproj`.
- Keep `Nullable` and `ImplicitUsings` enabled assumptions intact because they are explicitly set in `qdrant-web-spider.csproj`, `shared\Shared.csproj`, and `tests\tests.csproj`.
- Preserve explicit null handling patterns instead of relying on suppressed warnings. Representative examples are `string?` config values in `shared\Config.cs` and nullable return types like `Task<string?>` in `shared\HttpHelper.cs`.

## Import Organization

**Order:**
1. Framework or BCL namespaces first, such as `System.Text.Json` or `System.CommandLine` in `Program.cs`.
2. Third-party package namespaces next, such as `HtmlAgilityPack`, `Qdrant.Client.Grpc`, `OpenAI`, and `ModelContextProtocol.Server` in `shared\PageExtractor.cs`, `shared\CrawlService.cs`, and `shared\SpiderTools.cs`.
3. Project namespaces last, typically `using QdrantWebSpider;` in root entrypoints and tests like `Program.cs` and `tests\PageExtractorTests.cs`.

**Path Aliases:**
- Not applicable. The codebase uses standard .NET namespaces and project references rather than alias-based imports.

## Error Handling

**Patterns:**
- Throw specific exceptions at provider and configuration boundaries. Examples include `EmbeddingProviderException` in `shared\EmbeddingProvider.cs`, `InvalidOperationException` in `shared\OpenAiEmbeddingProvider.cs`, and `FileNotFoundException` or `OperationCanceledException` in `shared\ModelDownloader.cs`.
- Catch `EmbeddingProviderException` at CLI and MCP boundaries, convert it into user-facing output, and avoid leaking stack traces. This pattern appears in `Program.cs`, `mcp-server.cs`, and `shared\SpiderTools.cs`.
- Prefer returning `null` or empty collections for expected non-success paths instead of throwing, as shown by `shared\HttpHelper.cs` returning `null` on non-retryable HTTP failures and `shared\QdrantHelper.cs` returning `[]` when no page data exists.
- Retry transient operations inside infrastructure wrappers rather than at call sites. `shared\HttpHelper.cs` retries HTTP requests, and `shared\EmbeddingProvider.cs` retries embedding generation and wraps the terminal failure with context.

## Logging

**Framework:** Console-first logging with selective `Microsoft.Extensions.Logging` setup for the MCP host.

**Patterns:**
- Use `Console.WriteLine` and `Console.Error.WriteLine` for CLI output and recoverable status reporting, as seen in `Program.cs`, `search.cs`, `mcp-server.cs`, `shared\QdrantHelper.cs`, and `shared\ModelDownloader.cs`.
- Pass an optional `Action<string>` logger into long-running services instead of binding them directly to a logging framework. `shared\CrawlService.cs` accepts `Action<string>? logger = null` and the CLI passes `Console.WriteLine`.
- When logging retries or failures, include operation context inline. Examples include retry counts in `shared\HttpHelper.cs` and provider names plus attempt numbers in `shared\EmbeddingProvider.cs`.

## Comments

**When to Comment:**
- Add short intent comments only where the code is doing non-obvious translation or control-flow work. Examples include `// Overlay environment variables for secrets` in `shared\Config.cs`, `// Convert simple CSS to XPath` in `shared\PageExtractor.cs`, and `// Split at paragraph boundaries` in `shared\Chunker.cs`.
- Avoid redundant comments for straightforward assignments or assertions. Most methods in `shared\SearchService.cs` and `shared\OpenAiEmbeddingProvider.cs` remain uncommented because the code is direct.

**JSDoc/TSDoc:**
- XML documentation is rare. The only notable use is the summary on `ResolveModelPath` in `shared\Config.cs`. Follow that pattern only when a public API’s behavior is not obvious from its name and signature.

## Function Design

**Size:** Keep orchestration methods moderately sized and push parsing or transformation details into private helpers. `shared\PageExtractor.cs` and `shared\Chunker.cs` are the clearest examples.

**Parameters:** Pass concrete dependencies explicitly through constructors or method parameters. Service classes in `shared\CrawlService.cs`, `shared\SearchService.cs`, and `shared\QdrantHelper.cs` do not use service locators or static globals.

**Return Values:** Prefer typed records and collections over anonymous objects. Representative returns are `List<SearchResult>` from `shared\SearchService.cs`, `ExtractedPage` from `shared\PageExtractor.cs`, and `List<Chunk>` from `shared\Chunker.cs`.

## Module Design

**Exports:** One main public type per file is the dominant pattern, with colocated supporting records or extensions where it improves cohesion. Examples include `shared\PageExtractor.cs` containing `ExtractedPage` and `ExtractedSection`, and `shared\QdrantHelper.cs` containing `PayloadExtensions`.

**Barrel Files:** Not used. Composition is handled through `ProjectReference` entries in `qdrant-web-spider.csproj` and `tests\tests.csproj`, plus `using QdrantWebSpider;` where needed.

## Quality Practices

- Keep entrypoint behavior thin and delegate domain logic to `shared\` modules. `Program.cs`, `spider.cs`, `search.cs`, and `mcp-server.cs` mainly parse arguments, construct services, and format output.
- Preserve the repository’s modern C# style: file-scoped namespaces, records for immutable config or result models, primary constructors for service classes, and collection expressions where available.
- Treat restore warnings as part of quality review. `dotnet test tests\tests.csproj` currently passes, but restore emits `NU1904` for `Microsoft.SemanticKernel.Core 1.39.0` via `shared\Shared.csproj`, so dependency changes should be reviewed with that warning in mind.

---

*Convention analysis: 2026-04-02*
