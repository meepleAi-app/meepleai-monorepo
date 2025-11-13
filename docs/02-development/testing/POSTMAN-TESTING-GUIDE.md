# Postman Testing Guide - Quick Reference

## 🚀 Quick Start (5 minutes)

### Step 1: Import Collection (1 min)
1. Open Postman
2. Click **Import** (top left)
3. Drag files from `postman/` folder:
   - `KnowledgeBase-DDD-Tests.postman_collection.json`
   - `Local-Development.postman_environment.json`
4. Click **Import**

### Step 2: Select Environment (30 sec)
1. Click environment dropdown (top right)
2. Select **Local Development**
3. Verify baseUrl = `http://localhost:8080`

### Step 3: Start Server (1 min)
```bash
cd apps/api/src/Api
dotnet run
```
Wait for: `Now listening on: http://localhost:8080`

### Step 4: Run Setup (1 min)
1. Expand **Setup** folder
2. Run **1. Login - Get Session Cookie** ✅
3. Run **2. Get Games - Find Test Game ID** ✅
4. Verify environment variables set: `testGameId`

### Step 5: Run All Tests (2 min)
1. Click **KnowledgeBase DDD - Phase 3 Tests** collection
2. Click **Run** button
3. Click **Run KnowledgeBase DDD - Phase 3 Tests**
4. Wait for results: Target **11/11 passing** ✅

---

## 📊 Test Collection Overview

### 11 Requests, ~50 Assertions

**Setup** (2 requests):
- Login with demo user
- Get test game ID

**Search Tests** (4 requests):
- Hybrid search (vector + keyword RRF)
- Vector-only search
- High min score filter
- Invalid gameId (error case)

**Q&A Tests** (4 requests):
- Simple question (full RAG)
- Complex multi-part question
- Cache bypass
- Empty query (error case)

**Auth Tests** (1 request):
- Unauthenticated request (401)

---

## ✅ Success Criteria

**11/11 tests passing** = Phase 3 implementation verified ✅

### Expected Results

**Search Endpoint** (`/knowledge-base/search`):
- Status: 200 OK
- Results: 1-5 items (based on topK)
- Each result: vectorDocumentId, textContent, pageNumber, relevanceScore, rank
- SearchMode: "hybrid" or "vector"
- Response time: <200ms

**Q&A Endpoint** (`/knowledge-base/ask`):
- Status: 200 OK
- Answer: 50-500 characters
- Sources: 3-5 items
- SearchConfidence: 0.6-0.95
- LlmConfidence: 0.5-0.8
- OverallConfidence: 0.55-0.88 (70% search + 30% LLM)
- IsLowQuality: false (for good responses)
- Citations: Array with page numbers
- Response time: <2500ms

---

## 🔧 Common Issues

| Issue | Cause | Fix |
|-------|-------|-----|
| "No games found" | DB not seeded | Run migrations, verify seed data |
| 401 Unauthorized | No session cookie | Re-run Setup → Login request |
| 500 Error | Service down | Check `docker compose ps`, start services |
| Empty results | No indexed PDFs | Upload PDF or use game with content |
| Slow responses | Cold start | First request slower, subsequent faster |

---

## 📈 What Each Test Validates

### Domain Layer Tests

**VectorSearchDomainService**:
- ✅ Search - Vector Only Mode
- ✅ Search - High Min Score Filter
- Tests: Cosine similarity, score filtering, ranking

**RrfFusionDomainService**:
- ✅ Search - Hybrid Mode
- Tests: RRF formula, result fusion, re-ranking

**QualityTrackingDomainService**:
- ✅ Ask - Confidence Score Validation
- Tests: Weighted average (70/30), low quality threshold

### Application Layer Tests

**SearchQueryHandler**:
- ✅ All Search tests
- Tests: Query validation, embedding generation, mode selection

**AskQuestionQueryHandler**:
- ✅ All Q&A tests
- Tests: Full RAG pipeline, prompt building, LLM integration

### Infrastructure Layer Tests

**KnowledgeBaseMappers**:
- ✅ Implicit in all tests (DTOs mapped correctly)
- Tests: Domain ↔ DTO conversion

**QdrantVectorStoreAdapter**:
- ✅ Search - Vector Only Mode
- ✅ Search - Hybrid Mode
- Tests: Qdrant integration, result mapping

### API Layer Tests

**KnowledgeBaseEndpoints**:
- ✅ All requests
- Tests: Routing, auth, error handling, JSON serialization

---

## 📝 Test Results Template

After running tests, document:

```markdown
## Test Execution Report

**Date**: 2025-11-11
**Environment**: Local Development
**Server**: http://localhost:8080

### Results Summary
- Total Tests: 11
- Passed: X/11
- Failed: Y/11
- Status: [PASS/FAIL]

### Performance Metrics
- Search (Hybrid): Xms avg
- Search (Vector): Xms avg
- Q&A (Simple): Xms avg
- Q&A (Complex): Xms avg

### Issues Found
1. [List any failures]
2. [Expected behavior]
3. [Actual behavior]

### Next Steps
- [Fix issues or proceed to Phase 4]
```

---

## 🎯 After Testing

### If 11/11 Pass ✅
**Phase 3 is production-ready!**

Options:
- **A**: Proceed to Phase 4 (GameManagement)
- **B**: Add automated unit tests (xUnit)
- **C**: Merge to main branch

### If Any Fail ❌
1. Review failed test details
2. Check API logs: `docker compose logs api -f`
3. Debug handler/service code
4. Fix issue
5. Re-run tests
6. Update code and commit fixes

---

**Created**: 2025-11-11
**Phase**: DDD Phase 3 - KnowledgeBase Integration Testing
**Status**: Ready for manual verification via Postman
