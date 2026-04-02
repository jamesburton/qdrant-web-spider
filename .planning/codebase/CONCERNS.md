# Codebase Concerns

**Analysis Date:** 2026-04-02

## Tech Debt

**Crawler configuration drift:**
- Issue: `requestDelayMs` and `frequencyHours` are exposed in config but are not applied by the crawler loop. `RobotsTxt` parses `crawl-delay`, but `CrawlService` never waits between requests.
- Files: `shared/Config.cs`, `shared/RobotsTxt.cs`, `shared/CrawlService.cs`, `spider.json`
- Impact: Operators can believe crawl rate limiting and recrawl cadence exist when they do not, which increases the chance of over-crawling sites and creates misleading configuration surface area.
- Fix approach: Apply `requestDelayMs` and `RobotsTxt.CrawlDelayMs` inside `CrawlService`, and either implement `frequencyHours` scheduling semantics or remove it from `SpiderConfig` and example configs.

**Stored payload schema mismatch:**
- Issue: page chunks are written without `chunkIndex`, `summary`, or `contentSelector`, but downstream code and docs assume richer payloads. `GetPageAsync` sorts by `chunkIndex`, and `README.md` claims those fields are stored.
- Files: `shared/CrawlService.cs`, `shared/QdrantHelper.cs`, `README.md`
- Impact: reconstructed pages can be returned in unstable order, docs drift from reality, and later features that depend on those fields will fail silently or degrade.
- Fix approach: Persist `chunkIndex`, `summary`, and `contentSelector` during upsert, backfill or migrate existing collections, and align docs with the actual payload contract.

**Public API parameters are accepted but ignored:**
- Issue: `SpiderTools.SearchWebPages` exposes a `scoreThreshold` argument, but `SearchService` hardcodes `0.3f` and provides no way to pass the caller-supplied value through.
- Files: `shared/SpiderTools.cs`, `shared/SearchService.cs`
- Impact: MCP clients receive a misleading tool contract and cannot tune recall/precision as documented.
- Fix approach: Thread `scoreThreshold` into `SearchService.SearchAsync` and into `QdrantHelper.SearchAsync`, or remove the parameter from the tool surface.

## Known Bugs

**Page reconstruction order is unreliable:**
- Symptoms: `get_page` can return chunks in arbitrary order because `GetPageAsync` orders by `chunkIndex`, but the crawl path never writes `chunkIndex`.
- Files: `shared/CrawlService.cs`, `shared/QdrantHelper.cs`, `shared/SpiderTools.cs`
- Trigger: Crawl any page, then request it through the MCP `get_page` tool.
- Workaround: None in code. The only safe workaround is to add `chunkIndex` to stored payloads and reindex data.

**Recrawl does not remove stale chunks when page structure shrinks:**
- Symptoms: if a page previously produced many chunks and later produces fewer, old points can remain because recrawls only upsert current point IDs and never delete superseded chunks for the URL.
- Files: `shared/CrawlService.cs`, `shared/QdrantHelper.cs`
- Trigger: Crawl a page, then crawl a shorter version whose chunk count is lower than before.
- Workaround: Manually delete existing points for the URL before recrawling, or implement URL-scoped replace semantics.

## Security Considerations

**Remote model download has no integrity verification:**
- Risk: ONNX assets are downloaded from Hugging Face and written directly to the local model directory without checksum or signature validation.
- Files: `shared/ModelDownloader.cs`
- Current mitigation: downloads use HTTPS only.
- Recommendations: pin expected hashes per model, verify content before activation, and fail closed on mismatch.

**MCP tool surface can expose internal crawl inventory:**
- Risk: `crawl_status` and `list_urls` return collection names, Qdrant URL, configured sites, and crawled URL inventory to any connected MCP client.
- Files: `shared/SpiderTools.cs`
- Current mitigation: none in code beyond relying on MCP host trust.
- Recommendations: document the trust boundary clearly, consider an allowlist or reduced-output mode, and avoid returning infrastructure endpoints unless needed.

## Performance Bottlenecks

**Crawl concurrency does not scale within a single site:**
- Problem: the BFS loop is sequential per site; `SemaphoreSlim` limits work across site tasks, but one large site still crawls one page at a time.
- Files: `shared/CrawlService.cs`
- Cause: URLs are dequeued and processed inline in a single loop instead of being fanned out into worker tasks.
- Improvement path: adopt a worker pool over a concurrent queue/channel, keep visited-set synchronization explicit, and apply politeness delays per host.

**URL listing loads the entire collection into memory:**
- Problem: `list_urls` calls `ScrollAllAsync`, which materializes every point before deduplicating by URL.
- Files: `shared/SpiderTools.cs`, `shared/QdrantHelper.cs`
- Cause: `ScrollAllAsync` accumulates all results in a `List<ScoredPoint>` and only then filters.
- Improvement path: stream pages from Qdrant, project only needed payload fields, and aggregate unique URLs incrementally.

**Fresh-result filtering is heuristic and can drop valid matches:**
- Problem: stale filtering searches only `limit * 3` results before filtering by `captureDate`.
- Files: `shared/SearchService.cs`
- Cause: the search path overfetches by a fixed multiplier instead of using a proper Qdrant filter.
- Improvement path: push date filtering into Qdrant when possible, or keep paginating until enough fresh results are collected.

## Fragile Areas

**HTML selector conversion is intentionally narrow:**
- Files: `shared/PageExtractor.cs`, `tests/PageExtractorTests.cs`
- Why fragile: `CssToXPath` only handles a small subset of selectors. Real-world docs often rely on descendant selectors, attribute values with quotes or hyphens, and more complex combinations.
- Safe modification: add tests before widening selector support and keep the supported selector subset explicit in docs.
- Test coverage: only happy-path extraction, markdown, table, and HTML mode are covered; complex selector handling is not.

**Network and retry behavior is hard to observe and hard to cancel:**
- Files: `shared/HttpHelper.cs`, `shared/EmbeddingProvider.cs`, `Program.cs`
- Why fragile: retries log directly to console, no cancellation tokens are threaded through the crawl or embedding paths, and HTTP behavior is difficult to unit test because `HttpClient` is created inline.
- Safe modification: introduce injectable HTTP abstractions or handlers, propagate cancellation tokens, and route logs through a single abstraction instead of `Console.Write`.
- Test coverage: retry coverage exists only for `RetryingEmbeddingProvider`; HTTP retry and crawl retry behavior are untested.

## Scaling Limits

**Collection growth increases duplicate-scan cost:**
- Current capacity: `GetByUrlAsync` scrolls up to 1000 matching points for a URL and `list_urls` scans the whole collection.
- Limit: crawl and MCP operations become progressively slower as more chunks accumulate, especially with highly chunked pages or broad crawls.
- Scaling path: use deterministic URL replacement or tombstoning, add URL-level metadata documents, and move more filtering into Qdrant instead of client-side scans.

## Dependencies at Risk

**Experimental ONNX embedding API:**
- Risk: `shared/OnnxEmbeddingProvider.cs` suppresses `SKEXP0070`, indicating dependence on an experimental Semantic Kernel ONNX API.
- Impact: framework or package updates can break the local embedding path without much warning.
- Migration plan: isolate the adapter behind `IEmbeddingProvider`, pin package versions carefully, and be prepared to swap to a stable ONNX runtime integration if the API changes.

**Beta command-line stack:**
- Risk: the CLI depends on `System.CommandLine` beta packages in `qdrant-web-spider.csproj`.
- Impact: command binding behavior and package compatibility may shift across updates.
- Migration plan: move to a stable `System.CommandLine` release when available, or keep command parsing intentionally shallow to limit upgrade cost.

## Missing Critical Features

**No deletion or refresh lifecycle for crawled pages:**
- Problem: the code can insert and update points, but it has no first-class mechanism to remove URLs that disappear, change host policy, or should be purged.
- Blocks: safe long-term operation on evolving documentation sites and predictable recrawl hygiene.

**No end-to-end crawler verification path:**
- Problem: there is no integration harness covering crawl -> embed -> upsert -> search or MCP tool behavior.
- Blocks: confident refactoring of `shared/CrawlService.cs`, `shared/QdrantHelper.cs`, and `shared/SpiderTools.cs`.

## Test Coverage Gaps

**Core crawl and storage workflow is untested:**
- What's not tested: `shared/CrawlService.cs`, `shared/QdrantHelper.cs`, `shared/SearchService.cs`, and `shared/SpiderTools.cs`
- Files: `shared/CrawlService.cs`, `shared/QdrantHelper.cs`, `shared/SearchService.cs`, `shared/SpiderTools.cs`
- Risk: regressions in payload shape, chunk lifecycle, scoring, and MCP output can land unnoticed.
- Priority: High

**Robots, downloader, and HTTP behaviors are largely untested:**
- What's not tested: `shared/RobotsTxt.cs`, `shared/ModelDownloader.cs`, `shared/HttpHelper.cs`
- Files: `shared/RobotsTxt.cs`, `shared/ModelDownloader.cs`, `shared/HttpHelper.cs`
- Risk: politeness bugs, download failures, and retry regressions will show up only in live usage.
- Priority: High

**Test suite still contains an empty placeholder test:**
- What's not tested: `tests/UnitTest1.cs` contains an empty `Test1` and contributes no behavioral coverage.
- Files: `tests/UnitTest1.cs`
- Risk: it adds noise to the suite and obscures actual coverage quality.
- Priority: Low

---

*Concerns audit: 2026-04-02*
