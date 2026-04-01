# Project State: Qdrant Web Spider

**Goal:** A high-performance .NET 10 web crawler that generates vector embeddings and stores them in Qdrant for semantic search and AI agent integration (MCP).

## Current Status
- **Phase:** Initial Setup & Discovery
- **Health:** 🟢 Qdrant service reachable; environment configured.
- **Last Updated:** 2026-04-01

## 📋 Task List

### 🏗️ Infrastructure & Environment
- [x] Start/Verify local Qdrant instance (Docker: `docker run -p 6333:6333 -p 6334:6334 qdrant/qdrant`) [DONE]
- [x] Configure `spider.local.json` for testing [DONE]
- [x] Verify ONNX model download (`all-MiniLM-L6-v2`) [DONE]

### 📦 Source Control
- [x] Track existing project files in Git [DONE]
- [x] Commit initial baseline [DONE]

### 🕷️ Crawler & Search
- [x] Test crawl on a small target (e.g., example.com) [DONE]
- [x] Verify semantic search via `search.cs` [DONE]
- [x] Verify MCP server tools via `mcp-server.cs` [DONE]

### 🛠️ Enhancements (Backlog)
- [x] Add unit tests for `Chunker` and `PageExtractor` [DONE]
- [x] Implement retry logic for failed HTTP requests [DONE]
- [x] Add support for XML sitemaps [DONE]
- [ ] Improve error handling for embedding provider failures [BACKLOG]

## 📝 Notes
- Project uses .NET 10 "file-based apps" (running via `dotnet file.cs`).
- Shared logic is in `shared/Shared.csproj`.
- Default embedding provider is local ONNX (CPU).
