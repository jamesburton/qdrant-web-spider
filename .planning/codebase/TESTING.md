# Testing Patterns

**Analysis Date:** 2026-04-02

## Test Framework

**Runner:**
- xUnit `2.9.3`
- Config: `tests\tests.csproj` with `Microsoft.NET.Test.Sdk` `17.14.1`, `xunit.runner.visualstudio` `3.1.4`, and `coverlet.collector` `6.0.4`

**Assertion Library:**
- xUnit assertions via `Assert.*`, used directly in `tests\PageExtractorTests.cs`, `tests\ChunkerTests.cs`, and `tests\EmbeddingProviderTests.cs`

**Run Commands:**
```bash
dotnet test tests/tests.csproj
dotnet test tests/tests.csproj --filter FullyQualifiedName~PageExtractorTests
dotnet test tests/tests.csproj --collect:"XPlat Code Coverage"
```

## Test File Organization

**Location:**
- All current tests live under the dedicated `tests\` project rather than co-located with production files.
- The test project references `shared\Shared.csproj` in `tests\tests.csproj`, so new tests for reusable logic should continue to go in `tests\`.

**Naming:**
- Use `<SubjectUnderTest>Tests.cs` for file names and `<SubjectUnderTest>Tests` for classes, as seen in `tests\PageExtractorTests.cs`, `tests\ChunkerTests.cs`, and `tests\EmbeddingProviderTests.cs`.
- Use descriptive method names in the `Method_ShouldExpectedBehavior` form, such as `Extract_ShouldExtractBasicContent` and `RetryingProvider_WrapsBatchFailureWithContext`.

**Structure:**
```text
tests/
├── tests.csproj
├── PageExtractorTests.cs
├── ChunkerTests.cs
├── EmbeddingProviderTests.cs
└── UnitTest1.cs
```

## Test Structure

**Suite Organization:**
```csharp
public class PageExtractorTests
{
    [Fact]
    public void Extract_ShouldExtractBasicContent()
    {
        // Arrange
        var html = "...";
        var selectors = new SelectorConfig { Content = "main" };

        // Act
        var result = PageExtractor.Extract(html, url, selectors);

        // Assert
        Assert.Equal("Test Title", result.Title);
    }
}
```

**Patterns:**
- Use `[Fact]` for concrete examples. No `[Theory]` usage is present in the current suite.
- Follow explicit `Arrange` / `Act` / `Assert` comments in tests, especially in `tests\PageExtractorTests.cs` and `tests\ChunkerTests.cs`.
- Keep each test focused on one behavior branch, then use several assertions to validate the returned object graph.
- Use async tests only when the production API is async, as shown in `tests\EmbeddingProviderTests.cs`.

## Mocking

**Framework:** Moq is referenced in `tests\tests.csproj`, but the current suite does not use it.

**Patterns:**
```csharp
private sealed class FakeEmbeddingProvider : IEmbeddingProvider
{
    public int FailuresBeforeSuccess { get; set; }
    public Exception? BatchException { get; set; }

    public Task<float[]> EmbedAsync(string text)
    {
        if (FailuresBeforeSuccess > 0)
        {
            FailuresBeforeSuccess--;
            throw new InvalidOperationException("temporary failure");
        }

        return Task.FromResult(new[] { 1f, 2f });
    }
}
```

**What to Mock:**
- Prefer hand-written fakes for narrow interfaces with simple behavior control. `tests\EmbeddingProviderTests.cs` uses a nested `FakeEmbeddingProvider` instead of a mocking framework.
- Mock or fake external boundaries only: embedding providers, HTTP calls, Qdrant client wrappers, filesystem download prompts, and similar integration seams.

**What NOT to Mock:**
- Do not mock pure transformation logic. `tests\PageExtractorTests.cs` and `tests\ChunkerTests.cs` exercise the real parsing and chunking code directly.
- Avoid mocking records or simple DTOs like `ExtractedPage`, `ExtractedSection`, `SpiderConfig`, or `SearchResult`.

## Fixtures and Factories

**Test Data:**
```csharp
var sections = new List<ExtractedSection>
{
    new ExtractedSection("H1", "This is a test section with enough text to exceed the threshold."),
    new ExtractedSection("H2", "Another section here.")
};

var page = new ExtractedPage(
    Url: "url",
    Title: "Title",
    Headings: ["H1", "H2"],
    BodyText: "...",
    Summary: "...",
    ContentSelector: "...",
    Sections: sections,
    Links: []);
```

**Location:**
- There is no shared fixture or factory directory yet. Test data is currently created inline inside each test method in `tests\PageExtractorTests.cs`, `tests\ChunkerTests.cs`, and `tests\EmbeddingProviderTests.cs`.
- If repeated object setup grows, add test-only builders under `tests\` instead of pushing them into production code.

## Coverage

**Requirements:** No coverage threshold or CI enforcement is detected.

**View Coverage:**
```bash
dotnet test tests/tests.csproj --collect:"XPlat Code Coverage"
```

## Test Types

**Unit Tests:**
- Current coverage is almost entirely unit-level. `tests\PageExtractorTests.cs` validates HTML parsing and markdown conversion, `tests\ChunkerTests.cs` validates chunk splitting, and `tests\EmbeddingProviderTests.cs` validates retry and exception-wrapping behavior.

**Integration Tests:**
- No true integration test project is present. There are no tests exercising `shared\QdrantHelper.cs`, `shared\CrawlService.cs`, `shared\SearchService.cs`, or the root entrypoints against live dependencies.

**E2E Tests:**
- Not used. No browser, CLI snapshot, or full crawl-to-search flow tests are present.

## Common Patterns

**Async Testing:**
```csharp
[Fact]
public async Task RetryingProvider_WrapsBatchFailureWithContext()
{
    using var provider = new RetryingEmbeddingProvider(inner, maxRetries: 0, retryDelay: _ => TimeSpan.Zero);

    var ex = await Assert.ThrowsAsync<EmbeddingProviderException>(() =>
        provider.EmbedBatchAsync(["one", "two"]));

    Assert.Equal("batch embedding", ex.Operation);
}
```

**Error Testing:**
```csharp
var ex = await Assert.ThrowsAsync<EmbeddingProviderException>(() =>
    provider.EmbedBatchAsync(["one", "two"]));

Assert.Contains("upstream timeout", ex.Message);
Assert.IsType<InvalidOperationException>(ex.InnerException);
```

## Current State

- `dotnet test tests\tests.csproj` passes with 9 tests on `2026-04-02`.
- The passing suite covers `shared\PageExtractor.cs`, `shared\Chunker.cs`, and `shared\EmbeddingProvider.cs`.
- `tests\UnitTest1.cs` is a placeholder with an empty `[Fact]` and should not be copied as a pattern for new tests.
- Test restore emits `NU1904` for `Microsoft.SemanticKernel.Core 1.39.0` through `shared\Shared.csproj`. That warning does not fail the suite, but dependency-sensitive test work should account for it.

## Gaps To Respect When Adding Tests

- New tests for crawling should isolate network and Qdrant boundaries because `shared\CrawlService.cs` currently composes `HttpClient`, `QdrantHelper`, and `IEmbeddingProvider` directly.
- New tests for CLI behavior should target extractable logic first. The root files `Program.cs`, `spider.cs`, `search.cs`, and `mcp-server.cs` are thin entrypoints and may require refactoring before they are easy to verify.
- Continue using deterministic local inputs. Existing tests avoid clocks, environment variables, console prompts, and network access.

---

*Testing analysis: 2026-04-02*
