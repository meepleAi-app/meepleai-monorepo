# Phase 3 Manual Testing Guide

**Date**: 2025-11-11
**Status**: Ready for testing
**Build**: ✅ SUCCESS (0 errors, 3 warnings)

---

## Prerequisites

### 1. Ensure Docker Services Running

```bash
cd infra
docker compose up -d postgres qdrant redis
```

Verify:
```bash
docker compose ps
```

Should show: postgres, qdrant, redis as **healthy**

### 2. Database Seeded with Demo Data

Demo users should exist (Migration: `20251009140700_SeedDemoData`):
- `user@meepleai.dev` / `Demo123!`
- `editor@meepleai.dev` / `Demo123!`
- `admin@meepleai.dev` / `Demo123!`

---

## Testing Method 1: Postman (Recommended)

### Quick Start (5 minutes)

**1. Import Collection**:
- Open Postman
- Import → Drag files:
  - `postman/KnowledgeBase-DDD-Tests.postman_collection.json`
  - `postman/Local-Development.postman_environment.json`

**2. Start Server**:
```bash
cd apps/api/src/Api
dotnet run
```

Wait for console message:
```
Now listening on: http://localhost:8080
```

**3. Run Tests**:
- Select "Local Development" environment (top right)
- Run "Setup" folder first:
  - ✅ "1. Login - Get Session Cookie"
  - ✅ "2. Get Games - Find Test Game ID"
- Run entire collection or individual folders
- **Target**: 11/11 tests passing ✅

### Test Cases

**Search Tests** (4):
1. Hybrid search (vector + keyword RRF)
2. Vector-only search (semantic)
3. High min score filter
4. Invalid gameId (400 error)

**Q&A Tests** (4):
1. Simple question (full RAG)
2. Complex question
3. Cache bypass
4. Empty query (400 error)

**Auth Test** (1):
5. Unauthenticated (401 error)

---

## Testing Method 2: cURL Commands

### 1. Login and Get Cookie

```bash
# Login
curl -i -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "user@meepleai.dev",
    "password": "Demo123!"
  }' \
  -c cookies.txt

# Extract session cookie from cookies.txt
```

### 2. Get Test Game ID

```bash
# Get games
curl -X GET http://localhost:8080/api/v1/games \
  -b cookies.txt

# Copy first game ID from response
```

### 3. Test Search Endpoint

```bash
# Replace <GAME_ID> with actual UUID
curl -X POST http://localhost:8080/api/v1/knowledge-base/search \
  -H "Content-Type: application/json" \
  -b cookies.txt \
  -d '{
    "gameId": "<GAME_ID>",
    "query": "How do you win?",
    "topK": 5,
    "minScore": 0.7,
    "searchMode": "hybrid",
    "language": "en"
  }'
```

**Expected Response**:
```json
{
  "success": true,
  "results": [
    {
      "vectorDocumentId": "uuid",
      "textContent": "...",
      "pageNumber": 12,
      "relevanceScore": 0.92,
      "rank": 1,
      "searchMethod": "hybrid"
    }
  ],
  "count": 5,
  "searchMode": "hybrid"
}
```

### 4. Test Q&A Endpoint

```bash
curl -X POST http://localhost:8080/api/v1/knowledge-base/ask \
  -H "Content-Type: application/json" \
  -b cookies.txt \
  -d '{
    "gameId": "<GAME_ID>",
    "query": "How many points do you need to win?",
    "language": "en",
    "bypassCache": false
  }'
```

**Expected Response**:
```json
{
  "success": true,
  "answer": "To win the game, you need...",
  "sources": [...],
  "searchConfidence": 0.85,
  "llmConfidence": 0.75,
  "overallConfidence": 0.82,
  "isLowQuality": false,
  "citations": [...]
}
```

---

## Testing Method 3: Swagger UI

### 1. Start Server and Open Swagger

```bash
cd apps/api/src/Api
dotnet run
```

Open browser: `http://localhost:8080/api/docs`

### 2. Authenticate

1. Find `POST /api/v1/auth/login` in "Auth" section
2. Click "Try it out"
3. Enter body:
```json
{
  "email": "user@meepleai.dev",
  "password": "Demo123!"
}
```
4. Click "Execute"
5. Verify 200 OK response
6. Session cookie automatically saved in browser

### 3. Get Test Game ID

1. Find `GET /api/v1/games`
2. Click "Try it out" → "Execute"
3. Copy first game ID from response

### 4. Test KnowledgeBase Endpoints

**Find "KnowledgeBase" section in Swagger**

**Test Search**:
1. `POST /api/v1/knowledge-base/search`
2. Try it out
3. Body:
```json
{
  "gameId": "<paste-game-id>",
  "query": "How do you win?",
  "topK": 5,
  "minScore": 0.7,
  "searchMode": "hybrid"
}
```
4. Execute
5. Verify 200 OK with results

**Test Q&A**:
1. `POST /api/v1/knowledge-base/ask`
2. Try it out
3. Body:
```json
{
  "gameId": "<paste-game-id>",
  "query": "What are the rules?"
}
```
4. Execute
5. Verify 200 OK with answer and confidence scores

---

## What Each Test Validates

### Domain Layer

**VectorSearchDomainService**:
- Vector similarity search
- Score filtering
- Result ranking

**RrfFusionDomainService**:
- RRF formula: score = 1/(k + rank)
- Vector + keyword fusion
- Re-ranking by combined score

**QualityTrackingDomainService**:
- Search confidence (weighted average of top 5)
- LLM confidence (citations + source quality)
- Overall = (search * 0.7) + (llm * 0.3)
- Low quality threshold < 0.5

### Application Layer

**SearchQueryHandler**:
- Query validation
- Embedding generation
- Search mode routing (hybrid vs vector)
- Domain service orchestration
- DTO mapping

**AskQuestionQueryHandler**:
- Search delegation to SearchQueryHandler
- Prompt template retrieval
- LLM call via ILlmService
- Quality tracking
- Citation extraction

### Infrastructure Layer

**KnowledgeBaseMappers**:
- VectorDocument ↔ VectorDocumentEntity
- HybridSearchResult → SearchResult
- EmbeddingResult → float[]
- Guid handling

**QdrantVectorStoreAdapter**:
- Wraps IQdrantService
- Vector search execution
- Result mapping to domain Embeddings

**Repositories**:
- VectorDocumentRepository (EF Core + mappers)
- EmbeddingRepository (EF Core + Qdrant adapter coordination)

### API Layer

**KnowledgeBaseEndpoints**:
- Routing to correct handlers
- Authentication validation
- Request validation (gameId UUID, query required)
- Error handling (400, 401, 500)
- JSON serialization

---

## Expected Results

### Search Endpoint

**Success** (200 OK):
```json
{
  "success": true,
  "results": [
    {
      "vectorDocumentId": "uuid",
      "textContent": "relevant text content",
      "pageNumber": 5,
      "relevanceScore": 0.92,
      "rank": 1,
      "searchMethod": "hybrid"
    }
  ],
  "count": 5,
  "searchMode": "hybrid"
}
```

**Validation Error** (400):
```json
{
  "error": "Invalid gameId format"
}
```

**Unauthorized** (401):
- No JSON body, just 401 status

### Q&A Endpoint

**Success** (200 OK):
```json
{
  "success": true,
  "answer": "Detailed answer from LLM...",
  "sources": [
    {
      "vectorDocumentId": "uuid",
      "textContent": "source text",
      "pageNumber": 5,
      "relevanceScore": 0.88,
      "rank": 1,
      "searchMethod": "hybrid"
    }
  ],
  "searchConfidence": 0.85,
  "llmConfidence": 0.72,
  "overallConfidence": 0.81,
  "isLowQuality": false,
  "citations": [
    {
      "documentId": "uuid",
      "pageNumber": 5,
      "snippet": "excerpt from source...",
      "relevanceScore": 0.88
    }
  ]
}
```

---

## Troubleshooting

### Server Won't Start

**Check Docker services**:
```bash
docker compose ps
```

All should be "healthy": postgres, qdrant, redis

**Start missing services**:
```bash
docker compose up -d postgres qdrant redis
```

### 401 Unauthorized

**Cause**: No session cookie or expired

**Fix**:
- Re-run login request
- Verify cookie saved (Postman: Cookies tab)
- Check cookie expiration (30 days default)

### No Results Found

**Cause**: No indexed PDFs for game

**Check**:
```bash
# See which games have indexed content
curl http://localhost:8080/api/v1/games -b cookies.txt
```

**Fix**:
- Upload a PDF via `/api/v1/pdfs/upload`
- Or use game with existing indexed content
- Or use demo data game IDs

### 500 Internal Server Error

**Check logs**:
```bash
# API logs
tail -f apps/api/src/Api/api-server.log

# Docker logs
docker compose logs api -f
```

**Common causes**:
- Qdrant service down
- Database connection failed
- LLM service timeout (OpenRouter API)

---

## Success Criteria

✅ **All Postman tests pass** (11/11)
✅ **Search returns results** (1-5 items)
✅ **Q&A returns answer** with confidence scores
✅ **Response times acceptable**:
   - Search: <200ms
   - Q&A: <2500ms
✅ **Confidence scores in range** (0-1)
✅ **Quality weighting correct** (70/30)
✅ **Error handling works** (400, 401)

**If all criteria met**: Phase 3 is **PRODUCTION READY** ✅

---

## Test Execution Report Template

After testing, document results:

```markdown
## Phase 3 Test Execution Report

**Date**: 2025-11-11
**Tester**: [Your name]
**Environment**: Local Development

### Results
- Setup tests: ✅ 2/2 passing
- Search tests: ✅ 4/4 passing
- Q&A tests: ✅ 4/4 passing
- Auth tests: ✅ 1/1 passing
- **Total**: ✅ 11/11 passing (100%)

### Performance
- Search (Hybrid): 125ms avg
- Search (Vector): 95ms avg
- Q&A (Simple): 1850ms avg
- Q&A (Complex): 2100ms avg

### Issues Found
- [None or list issues]

### Verdict
✅ Phase 3 implementation verified and production-ready
```

---

**Ready to test!** 🚀

Use Postman (recommended) or cURL/Swagger for manual verification.
