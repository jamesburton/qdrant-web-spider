# Codebase Structure

**Analysis Date:** 2026-04-02

## Directory Layout

```text
qdrant-web-spider/
├── .github/                 # CI workflow for build, pack, and NuGet publish
├── .planning/codebase/      # Generated codebase mapping documents
├── bin/                     # Root build output from the packaged CLI project
├── obj/                     # Root intermediate build artifacts
├── shared/                  # Reusable application library used by all entry points
├── tests/                   # xUnit test project for shared-library behavior
├── Program.cs               # Packaged CLI entry point with crawl/search/mcp commands
├── spider.cs                # File-based crawler entry script
├── search.cs                # File-based search entry script
├── mcp-server.cs            # File-based MCP server entry script
├── qdrant-web-spider.csproj # Packaged tool project
├── spider.json              # Sample crawler configuration
├── spider.local.json        # Local runtime config override (present in repo root)
├── test.local.json          # Local test config example
├── README.md                # User-facing usage and setup guide
└── STATE.md                 # Lightweight project status notes
```

## Directory Purposes

**`.github/workflows`:**
- Purpose: Automation for build and package publishing.
- Contains: GitHub Actions workflow YAML.
- Key files: `.github/workflows/publish.yml`

**`.planning/codebase`:**
- Purpose: Generated architecture and codebase reference docs.
- Contains: Mapper output documents such as `ARCHITECTURE.md` and `STRUCTURE.md`.
- Key files: `.planning/codebase/ARCHITECTURE.md`, `.planning/codebase/STRUCTURE.md`

**`shared`:**
- Purpose: Main implementation project and the only place for reusable application logic.
- Contains: config records, crawler/search services, content extraction utilities, Qdrant access, embedding providers, and MCP tools.
- Key files: `shared/Shared.csproj`, `shared/Config.cs`, `shared/CrawlService.cs`, `shared/SearchService.cs`, `shared/PageExtractor.cs`, `shared/Chunker.cs`, `shared/QdrantHelper.cs`, `shared/SpiderTools.cs`

**`tests`:**
- Purpose: Unit test project for behavior in `shared/`.
- Contains: xUnit test classes and `tests/tests.csproj`.
- Key files: `tests/ChunkerTests.cs`, `tests/PageExtractorTests.cs`, `tests/EmbeddingProviderTests.cs`, `tests/tests.csproj`

**`bin` and `obj`:**
- Purpose: Standard .NET build outputs and intermediates.
- Contains: generated artifacts.
- Key files: Not source-owned; do not place implementation code here.

## Key File Locations

**Entry Points:**
- `Program.cs`: Main command-based executable for the packaged .NET tool
- `spider.cs`: Standalone crawl script for file-based app execution
- `search.cs`: Standalone semantic search script for file-based app execution
- `mcp-server.cs`: Standalone MCP stdio host for file-based app execution

**Configuration:**
- `qdrant-web-spider.csproj`: Root packaging, command name, and shared-project reference
- `shared/Shared.csproj`: Dependency container for the shared library
- `shared/Config.cs`: Runtime configuration model and config loading logic
- `spider.json`: Example crawl/search configuration

**Core Logic:**
- `shared/CrawlService.cs`: Crawl orchestration and persistence flow
- `shared/SearchService.cs`: Query embedding and search result shaping
- `shared/PageExtractor.cs`: HTML parsing and section extraction
- `shared/Chunker.cs`: Token-budget chunk creation
- `shared/QdrantHelper.cs`: Qdrant access wrapper
- `shared/EmbeddingProvider.cs`: Embedding abstraction and provider factory
- `shared/SpiderTools.cs`: MCP tool surface

**Testing:**
- `tests/tests.csproj`: xUnit test project referencing `shared/Shared.csproj`
- `tests/ChunkerTests.cs`: Chunking behavior tests
- `tests/PageExtractorTests.cs`: Extraction and selector tests
- `tests/EmbeddingProviderTests.cs`: Embedding provider behavior tests

## Naming Conventions

**Files:**
- PascalCase `.cs` files for reusable library code in `shared/`: `CrawlService.cs`, `PageExtractor.cs`, `QdrantHelper.cs`
- kebab-case for the root project file: `qdrant-web-spider.csproj`
- descriptive lowercase script-style names for file-based entry points: `spider.cs`, `search.cs`, `mcp-server.cs`
- lowercase JSON config filenames keyed to runtime context: `spider.json`, `spider.local.json`, `test.local.json`

**Directories:**
- lowercase singular-purpose directories: `shared`, `tests`, `.github`, `.planning`

## Where to Add New Code

**New Feature:**
- Primary code: add reusable implementation under `shared/`
- Tests: add xUnit coverage under `tests/`

**New CLI Command:**
- Command registration: `Program.cs`
- Shared behavior: new service/helper under `shared/`
- Optional file-based equivalent: add another top-level `*.cs` script in the repository root only if the feature needs standalone execution

**New MCP Tool:**
- Tool method: `shared/SpiderTools.cs`
- Supporting query or business logic: `shared/SearchService.cs` or a new focused file in `shared/`

**New Crawler Processing Step:**
- Extraction/chunking: extend `shared/PageExtractor.cs` or `shared/Chunker.cs`
- Crawl orchestration and persistence decisions: extend `shared/CrawlService.cs`

**Utilities:**
- Shared helpers: place in `shared/` beside the subsystem they support, not in the repository root

## Special Directories

**`shared/bin` and `shared/obj`:**
- Purpose: Build output for the shared project
- Generated: Yes
- Committed: No

**`tests/bin` and `tests/obj`:**
- Purpose: Build output for the test project
- Generated: Yes
- Committed: No

**`.planning/codebase`:**
- Purpose: Generated analysis artifacts consumed by planning workflows
- Generated: Yes
- Committed: Project-dependent, but the directory is intended for maintained planning artifacts

## Placement Rules

- Keep executable composition in `Program.cs`, `spider.cs`, `search.cs`, and `mcp-server.cs` thin; put nontrivial logic in `shared/`.
- Treat `shared/Shared.csproj` as the module boundary for anything that should be reused by both CLI and MCP surfaces.
- Add tests in `tests/` against the shared library rather than duplicating assertions in entry scripts.
- Do not add source files under `bin/` or `obj/`; those directories are generated.
- Keep repository-root files for orchestration, packaging, configs, and standalone scripts only.

---

*Structure analysis: 2026-04-02*
