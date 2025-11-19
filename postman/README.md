# Postman Testing Guide - KnowledgeBase DDD Phase 3

## Overview

Postman collection for testing KnowledgeBase bounded context DDD implementation.

**Collection**: `KnowledgeBase-DDD-Tests.postman_collection.json`
**Environment**: `Local-Development.postman_environment.json`

---

## Quick Start

### 1. Import Collection and Environment

**In Postman**:
1. Click **Import** button (top left)
2. Drag and drop both JSON files:
   - `KnowledgeBase-DDD-Tests.postman_collection.json`
   - `Local-Development.postman_environment.json`
3. Click **Import**

### 2. Select Environment

1. Click environment dropdown (top right)
2. Select **Local Development**
3. Verify `baseUrl` is `http://localhost:5080`

### 3. Start API Server

```bash
cd apps/api/src/Api
dotnet run
```

Wait for:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5080
```

### 4. Run Setup Requests (IMPORTANT)

**Execute in order**:
1. ✅ **Setup → 1. Login - Get Session Cookie**
   - Uses demo user: `user@meepleai.dev` / `Demo123!`
   - Saves session cookie automatically
   - Sets `userId` variable

2. ✅ **Setup → 2. Get Games - Find Test Game ID**
   - Retrieves game list
   - Saves first game ID to `testGameId` variable
   - Required for all other tests

**Verify Setup**:
- Check **Cookies** tab - should see `.AspNetCore.Session` cookie
- Check **Environment** variables - `testGameId` should have UUID value

### 5. Run Test Collections

**Run All Tests**:
1. Click collection **KnowledgeBase DDD - Phase 3 Tests**
2. Click **Run** button
3. Select **Local Development** environment
4. Click **Run KnowledgeBase DDD - Phase 3 Tests**
5. Review test results

**Run Individual Folders**:
- **KnowledgeBase - Search**: 4 test cases for search endpoint
- **KnowledgeBase - Q&A**: 4 test cases for Q&A endpoint
- **Authentication Tests**: 1 test for auth validation

---

## Test Cases

### Setup (2 requests)

| # | Request | Purpose | Expected |
|---|---------|---------|----------|
| 1 | Login - Get Session Cookie | Authenticate user | 200 OK, session cookie set |
| 2 | Get Games - Find Test Game ID | Get valid game ID | 200 OK, saves gameId |

### KnowledgeBase - Search (4 requests)

| # | Request | Purpose | Expected |
|---|---------|---------|----------|
| 1 | Search - Hybrid Mode | Test hybrid search (vector + keyword RRF) | 200 OK, results with hybrid scores |
| 2 | Search - Vector Only Mode | Test pure vector search | 200 OK, semantic results only |
| 3 | Search - High Min Score Filter | Test filtering by min score (0.85) | 200 OK, all results ≥ 0.85 |
| 4 | Search - Invalid GameId | Test validation | 400 Bad Request, error message |

### KnowledgeBase - Q&A (4 requests)

| # | Request | Purpose | Expected |
|---|---------|---------|----------|
| 1 | Ask - Simple Question | Test full RAG pipeline | 200 OK, answer + confidence scores |
| 2 | Ask - Complex Multi-Part Question | Test multi-source answers | 200 OK, comprehensive answer |
| 3 | Ask - With Cache Bypass | Test cache bypass | 200 OK, fresh response |
| 4 | Ask - Empty Query | Test validation | 400 Bad Request, error message |

### Authentication Tests (1 request)

| # | Request | Purpose | Expected |
|---|---------|---------|----------|
| 1 | Search - Unauthenticated | Test auth requirement | 401 Unauthorized |

**Total**: 11 test requests, ~50 assertions

---

## Automated Test Assertions

### Search Endpoint Tests

**Successful Response**:
- ✅ Status code: 200 OK
- ✅ `success` flag: true
- ✅ `results` array present
- ✅ `count` number present
- ✅ `searchMode` matches request

**Result Validation**:
- ✅ Each result has: `vectorDocumentId`, `textContent`, `pageNumber`, `relevanceScore`, `rank`, `searchMethod`
- ✅ Relevance scores ≥ minScore threshold
- ✅ Results ranked correctly (rank 1, 2, 3...)

### Q&A Endpoint Tests

**Successful Response**:
- ✅ Status code: 200 OK
- ✅ `success` flag: true
- ✅ `answer` string non-empty
- ✅ `sources` array present
- ✅ Confidence scores present: `searchConfidence`, `llmConfidence`, `overallConfidence`
- ✅ All confidence scores in range 0-1
- ✅ `isLowQuality` boolean present
- ✅ `citations` array present

**Quality Tracking**:
- ✅ Overall confidence = (search * 0.7) + (llm * 0.3)
- ✅ Low quality flag: true if overall < 0.5, false otherwise

**Error Cases**:
- ✅ Invalid gameId → 400 Bad Request
- ✅ Empty query → 400 Bad Request
- ✅ No authentication → 401 Unauthorized

---

## Test Data Requirements

### Prerequisites

**Database Must Have**:
1. ✅ Demo users seeded (Migration: `20251009140700_SeedDemoData`)
   - `user@meepleai.dev` (password: `Demo123!`)
   - `editor@meepleai.dev` (password: `Demo123!`)
   - `admin@meepleai.dev` (password: `Demo123!`)

2. ✅ At least one game in database
   - Check via: `GET /api/v1/games`
   - Seed data includes demo games

3. ✅ Vector documents indexed for test game
   - If no vectors: Upload a PDF first via `/api/v1/pdfs/upload`
   - Or use existing indexed game from seed data

**Verify Prerequisites**:
```bash
# Check if demo users exist
curl http://localhost:5080/health

# Check if games exist (requires login first)
# Use Postman Setup requests
```

---

## Expected Response Times

### Search Endpoint
- **Hybrid search**: ~100-200ms
  - Embedding generation: ~50ms
  - Vector search (Qdrant): ~30-50ms
  - RRF fusion: ~5ms
  - Mapping: <1ms

- **Vector-only search**: ~80-150ms
  - Faster (no keyword search)

### Q&A Endpoint
- **Full RAG pipeline**: ~600-2500ms
  - Search phase: ~100-200ms (via SearchQueryHandler)
  - Prompt building: ~5ms
  - LLM generation: ~500-2000ms (depends on model)
  - Quality tracking: ~5ms
  - Mapping: <1ms

**Cache Impact**:
- First request: Full time
- Cached request: ~10-50ms (HybridCache L1+L2)
- `bypassCache: true`: Always full time

---

## Test Results Interpretation

### Success Indicators ✅

**Search Endpoint**:
- 200 OK status
- Results array with 1-5 items (depends on topK)
- Relevance scores between minScore and 1.0
- Correct searchMode ("hybrid" or "vector")

**Q&A Endpoint**:
- 200 OK status
- Non-empty answer (50+ characters)
- 3-5 sources cited
- Search confidence: 0.6-0.95 (typical range)
- LLM confidence: 0.5-0.8 (typical range)
- Overall confidence: 0.55-0.88 (typical range)
- isLowQuality: false (for good responses)
- Citations present with page numbers

### Failure Scenarios ❌

**400 Bad Request**:
- Invalid gameId format (not UUID)
- Empty or missing query
- Invalid searchMode (not "hybrid" or "vector")

**401 Unauthorized**:
- No session cookie
- Expired session
- No API key header

**500 Internal Server Error**:
- Qdrant service down
- LLM service timeout
- Database connection failed
- Check logs: `docker compose logs meepleai-api -f`

---

## Troubleshooting

### Issue: "No games found"

**Cause**: Database not seeded or empty

**Fix**:
```bash
# Check database
cd apps/api/src/Api
dotnet ef database update

# Verify seed data migration applied
# Should see: 20251009140700_SeedDemoData
```

### Issue: "401 Unauthorized"

**Cause**: Session cookie not set or expired

**Fix**:
1. Re-run **Setup → 1. Login** request
2. Verify **Cookies** tab shows `.AspNetCore.Session`
3. Check cookie hasn't expired (30 days default)

### Issue: "No results found"

**Cause**: No vector documents indexed for game

**Fix**:
1. Upload a PDF for the game first
2. Or use a game that already has indexed PDFs
3. Check via: `GET /api/v1/games` for games with PDFs

### Issue: "500 Internal Server Error"

**Cause**: Infrastructure service down (Qdrant, LLM, DB)

**Fix**:
```bash
# Check services
docker compose ps

# Start missing services
docker compose up -d meepleai-postgres meepleai-qdrant meepleai-redis

# Check API logs
docker compose logs meepleai-api -f
```

### Issue: "Search returns empty results"

**Cause**: Query doesn't match indexed content

**Fix**:
- Try broader queries: "rules", "setup", "how to play"
- Lower minScore: try 0.5 instead of 0.7
- Check which games have indexed content

---

## Advanced Usage

### Using API Key Instead of Session

**Add header to requests**:
```
X-API-Key: mpl_dev_<your-api-key>
```

**Get API key**:
1. Login via session
2. POST `/api/v1/users/me/api-keys` with name
3. Copy returned API key
4. Add to environment variable `apiKey`
5. Add header: `X-API-Key: {{apiKey}}`

### Running Tests in CI/CD

**Using Newman (Postman CLI)**:
```bash
# Install Newman
npm install -g newman

# Run collection
newman run postman/KnowledgeBase-DDD-Tests.postman_collection.json \
  --environment postman/Local-Development.postman_environment.json \
  --reporters cli,json \
  --reporter-json-export results.json

# With retry on failure
newman run postman/KnowledgeBase-DDD-Tests.postman_collection.json \
  --environment postman/Local-Development.postman_environment.json \
  --bail \
  --delay-request 100
```

### Performance Testing

**Monitor Response Times**:
1. Run collection with **Save responses** enabled
2. Check **Test Results** tab
3. Review response times for each request
4. Target: Search <200ms, Q&A <2500ms

**Stress Testing**:
```bash
# Run 100 iterations with 10 concurrent requests
newman run postman/KnowledgeBase-DDD-Tests.postman_collection.json \
  --environment postman/Local-Development.postman_environment.json \
  --iteration-count 100 \
  --delay-request 50
```

---

## Test Coverage

### Functional Tests ✅
- [x] Hybrid search mode
- [x] Vector-only search mode
- [x] Min score filtering
- [x] Full RAG Q&A pipeline
- [x] Confidence score calculation (70/30 weighting)
- [x] Low quality detection
- [x] Cache bypass
- [x] Multi-language support (en)
- [x] Input validation (invalid UUID, empty query)
- [x] Authentication requirement

### Edge Cases ✅
- [x] Invalid gameId format
- [x] Empty query
- [x] Unauthenticated request
- [x] High min score threshold (few results)
- [x] Complex multi-part questions

### Not Covered ⏳
- [ ] Different languages (it, de, fr, es)
- [ ] TopK edge cases (0, negative, >100)
- [ ] MinScore edge cases (<0, >1)
- [ ] Concurrent requests
- [ ] Very long queries (>1000 chars)
- [ ] Special characters in queries
- [ ] Games with no indexed content
- [ ] Qdrant service failures

---

## Success Criteria

Run the collection and verify:

✅ **All Setup requests**: 2/2 passing (100%)
✅ **All Search tests**: 4/4 passing (100%)
✅ **All Q&A tests**: 4/4 passing (100%)
✅ **All Auth tests**: 1/1 passing (100%)

**Total**: 11/11 tests passing (100%) = **PRODUCTION READY** ✅

---

## Next Steps After Testing

### If All Tests Pass ✅
1. Document test results
2. Commit Postman collection
3. Consider Phase 4 (GameManagement) or add more tests

### If Tests Fail ❌
1. Review failed test assertion
2. Check meepleai-api logs: `docker compose logs meepleai-api -f`
3. Debug handler code
4. Fix issue
5. Re-run tests

### Add More Tests
- Integration tests with Testcontainers
- Unit tests for domain services
- Load tests for performance
- E2E tests with Playwright

---

## Files Created

```
postman/
├── KnowledgeBase-DDD-Tests.postman_collection.json  (11 requests, ~50 assertions)
├── Local-Development.postman_environment.json        (6 variables)
└── README.md                                         (this file)
```

**Ready to test!** 🚀

Import the collection and run the tests to verify Phase 3 implementation.
