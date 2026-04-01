# Project State: Qdrant Web Spider

**Goal:** A high-performance .NET 10 web crawler that generates vector embeddings and stores them in Qdrant for semantic search and AI agent integration (MCP).

## Current Status
- **Phase:** Initial Setup & Discovery
- **Health:** 🟡 Qdrant service unreachable; environment not fully configured.
- **Last Updated:** 2026-04-01

## 📋 Task List

### 🏗️ Infrastructure & Environment
- [ ] Start/Verify local Qdrant instance (Docker: `docker run -p 6333:6333 -p 6334:6334 qdrant/qdrant`) [TODO]
- [ ] Configure `spider.local.json` for testing [TODO]
- [ ] Verify ONNX model download (`all-MiniLM-L6-v2`) [TODO]

### 📦 Source Control
- [ ] Track existing project files in Git [TODO]
- [ ] Commit initial baseline [TODO]

### 🕷️ Crawler & Search
- [ ] Test crawl on a small target (e.g., example.com) [TODO]
- [ ] Verify semantic search via `search.cs` [TODO]
- [ ] Verify MCP server tools via `mcp-server.cs` [TODO]

### 🛠️ Enhancements (Backlog)
- [ ] Add unit tests for `Chunker` and `PageExtractor` [BACKLOG]
- [ ] Implement retry logic for failed HTTP requests [BACKLOG]
- [ ] Add support for XML sitemaps [BACKLOG]
- [ ] Improve error handling for embedding provider failures [BACKLOG]

## 📝 Notes
- Project uses .NET 10 "file-based apps" (running via `dotnet file.cs`).
- Shared logic is in `shared/Shared.csproj`.
- Default embedding provider is local ONNX (CPU).
