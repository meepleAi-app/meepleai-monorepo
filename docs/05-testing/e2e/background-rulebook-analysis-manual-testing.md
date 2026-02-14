# Manual Testing Guide - Background Rulebook Analysis

**Issue**: #2454
**PR**: #2524
**Last Updated**: 2026-01-16

## Prerequisites

### Environment Setup
```bash
# 1. Start infrastructure
cd infra
docker compose up -d postgres qdrant redis

# 2. Verify Redis connection
docker compose exec redis redis-cli PING
# Expected: PONG

# 3. Start API
cd ../apps/api/src/Api
dotnet run
# API: http://localhost:8080
# Swagger: http://localhost:8080/scalar/v1
```

### Test Data Requirements

**Small Rulebook** (< 30k chars):
- Use existing short rulebook PDFs in test data
- Example: Tic-Tac-Toe, simple card games
- Expected behavior: Synchronous analysis (200 OK)

**Large Rulebook** (> 30k chars):
- Complex board games: Gloomhaven, Twilight Imperium, Arkham Horror
- 50+ pages recommended for realistic testing
- Expected behavior: Background processing (202 Accepted)

## Test Scenarios

### Scenario 1: Small Rulebook (Synchronous Flow)

**Objective**: Verify <30k chars uses sync analysis

**Steps**:
1. Upload small rulebook PDF (<30k chars)
   ```bash
   POST /api/v1/shared-games/{gameId}/documents
   Content-Type: multipart/form-data
   Body: rulebook.pdf (< 50 pages)
   ```

2. Trigger analysis
   ```bash
   POST /api/v1/documents/{documentId}/analyze?sharedGameId={gameId}
   Authorization: Bearer {token}
   ```

3. **Expected Response** (200 OK):
   ```json
   {
     "analysis": {
       "gameTitle": "Catan",
       "summary": "...",
       "keyMechanics": [...],
       "confidenceScore": 0.85
     },
     "analyzedAt": "2026-01-16T...",
     "isBackgroundTask": false,
     "taskId": null
   }
   ```

**Validation**:
- ✅ Response time: < 30s
- ✅ `isBackgroundTask = false`
- ✅ `taskId = null`
- ✅ Analysis object populated immediately
- ✅ No Redis background task created

---

### Scenario 2: Large Rulebook (Background Flow)

**Objective**: Verify >30k chars triggers background processing

**Test Rulebook**: Gloomhaven (80+ pages, ~60k chars)

**Steps**:
1. Upload large rulebook PDF
   ```bash
   POST /api/v1/shared-games/{gameId}/documents
   ```

2. Trigger analysis
   ```bash
   POST /api/v1/documents/{documentId}/analyze?sharedGameId={gameId}
   ```

3. **Expected Response** (202 Accepted):
   ```json
   {
     "analysis": null,
     "analyzedAt": "2026-01-16T...",
     "isBackgroundTask": true,
     "taskId": "a1b2c3d4-..."
   }
   ```

4. **Verify Response Time**: < 500ms

5. **Poll Status Endpoint** (every 2-3 seconds):
   ```bash
   GET /api/v1/documents/{documentId}/analysis/status/{taskId}?sharedGameId={gameId}
   ```

6. **Expected Progress Sequence**:
   ```json
   // t=0s: Scheduled
   { "status": "Scheduled", "progressPercentage": 0 }

   // t=10s: Phase 1
   { "status": "Running", "progressPercentage": 10, "currentPhase": "OverviewExtraction" }

   // t=20s: Phase 2
   { "status": "Running", "progressPercentage": 20, "currentPhase": "SemanticChunking" }

   // t=30-120s: Phase 3 (incremental)
   { "status": "Running", "progressPercentage": 35, "currentPhase": "ChunkAnalysis",
     "statusMessage": "Analyzing chunks (3/8)", "estimatedTimeRemaining": "00:02:30" }

   // t=180s: Phase 4
   { "status": "Running", "progressPercentage": 90, "currentPhase": "MergeAndValidation" }

   // t=200s: Completed
   { "status": "Completed", "progressPercentage": 100,
     "result": { "gameTitle": "Gloomhaven", ... } }
   ```

**Validation**:
- ✅ Initial response < 500ms
- ✅ Progress increases: 0% → 10% → 20% → ... → 100%
- ✅ Phases transition correctly (4 phases visible)
- ✅ Estimated time remaining shown during Phase 3
- ✅ Total analysis time: 2-5 minutes
- ✅ Final result available when status=Completed
- ✅ Full content analyzed (no 15k truncation)

---

### Scenario 3: Progress Tracking Accuracy

**Objective**: Verify progress updates reflect actual work

**Steps**:
1. Start large rulebook analysis (>30k chars)
2. Poll status endpoint every 1 second
3. Log progress percentage with timestamps

**Expected Timeline** (for 8-chunk rulebook):
```
t=0s:   0% - Starting
t=5s:   10% - Overview complete
t=10s:  20% - Chunking complete (8 chunks created)
t=15s:  27% - Chunk 1/8 analyzed (20% + 60%*1/8)
t=20s:  35% - Chunk 2/8 analyzed
t=25s:  42% - Chunk 3/8 analyzed (parallel batching visible)
...
t=120s: 80% - All chunks analyzed
t=130s: 90% - Merging
t=140s: 100% - Completed
```

**Validation**:
- ✅ Progress never decreases
- ✅ Phase 3 shows chunk count: "Analyzing chunks (N/total)"
- ✅ Estimated time decreases as chunks complete
- ✅ No gaps in progress (smooth increments)

---

### Scenario 4: Semantic Chunking Strategies

**Objective**: Verify 3-level chunking fallback

**Test Cases**:

#### 4a: Embedding-Based (Happy Path)
- **Rulebook**: Well-structured with clear sections
- **Expected**: Strategy=EmbeddingBased, chunks follow section boundaries
- **Validation**: Check logs for "Successfully created X chunks using embedding-based strategy"

#### 4b: Header-Based Fallback
- **Simulate**: Embedding service unavailable (stop embedding container)
- **Expected**: Strategy=HeaderBased, chunks split by headers
- **Validation**: Logs show "Embedding-based chunking failed, falling back to header-based"

#### 4c: Fixed-Size Fallback
- **Simulate**: No headers detected (plain text rulebook)
- **Expected**: Strategy=FixedSize, 10k chunks with 500 overlap
- **Validation**: Logs show "Header-based chunking insufficient, using fixed-size fallback"

---

### Scenario 5: Partial Chunk Failure

**Objective**: Verify 70% success threshold

**Simulate Failures**:
```bash
# Temporarily break LLM service to cause chunk failures
# Or modify code to fail specific chunks

# Expected behavior:
# - 8 chunks total
# - 2 chunks fail (timeout/LLM error)
# - Success rate: 75% (6/8) > 70% threshold
# - Analysis proceeds to merge
# - Confidence score reduced proportionally
```

**Validation**:
- ✅ Logs show "Chunk 3/8 analysis FAILED"
- ✅ Progress continues despite failures
- ✅ Final merge excludes failed chunks
- ✅ Confidence score: < 0.9 (penalty for failures)
- ✅ Analysis completes successfully

**Below Threshold Test**:
- Simulate 5/8 chunks failing (62.5% success)
- Expected: Orchestration fails at Phase 3 with error message
- Status: "FAILED: Chunk analysis success rate too low: 0.62 < 0.70 threshold"

---

### Scenario 6: Concurrent Requests (Load Test)

**Objective**: Verify distributed locking prevents duplicate analysis

**Steps**:
1. Trigger 3 analyses for same game+PDF simultaneously
   ```bash
   # Terminal 1
   curl -X POST .../analyze?sharedGameId={gameId} &

   # Terminal 2
   curl -X POST .../analyze?sharedGameId={gameId} &

   # Terminal 3
   curl -X POST .../analyze?sharedGameId={gameId} &
   ```

2. **Expected**: Only 1 task executes, others wait or deduplicate

3. Check Redis locks:
   ```bash
   redis-cli KEYS "meepleai:tasks:lock:rulebook:analysis:*"
   ```

**Validation**:
- ✅ Only 1 background task active per game+PDF
- ✅ Lock acquired by first request
- ✅ Subsequent requests either wait or return existing taskId
- ✅ Lock released after completion

---

### Scenario 7: Configuration Override

**Objective**: Verify appsettings.json values applied

**Test**:
1. Modify `appsettings.Development.json`:
   ```json
   "Features:RulebookAnalysis:BackgroundProcessing": {
     "LargeRulebookThreshold": 5000,  // Lower threshold for testing
     "MaxChunkSize": 2000,
     "MaxParallelChunks": 2
   }
   ```

2. Restart API
3. Upload 6k char rulebook → Should trigger background processing

**Validation**:
- ✅ 6k rulebook → 202 Accepted (threshold=5000)
- ✅ Chunks ~2k size (max=2000)
- ✅ Max 2 chunks analyzed concurrently

---

## Redis Inspection Commands

### Check Task Status
```bash
# List all analysis tasks
redis-cli KEYS "meepleai:tasks:analysis:*"

# Get specific task status
redis-cli GET "meepleai:tasks:analysis:{taskId}"
```

### Check Progress
```bash
# Get progress data
redis-cli GET "meepleai:analysis:progress:{taskId}"

# Decode JSON
redis-cli GET "meepleai:analysis:progress:{taskId}" | jq .
```

### Monitor Locks
```bash
# List active locks
redis-cli KEYS "meepleai:tasks:lock:*"

# Check lock TTL
redis-cli TTL "meepleai:tasks:lock:rulebook:analysis:{gameId}:{pdfId}"
```

---

## Logging Validation

### Expected Log Patterns

**Phase 1** (Overview):
```
[INFO] Extracting overview for Gloomhaven, content length: 58942 chars
[INFO] Successfully extracted overview for Gloomhaven
```

**Phase 2** (Chunking):
```
[INFO] Chunking rulebook content: 58942 chars, 12 section headers
[INFO] Successfully created 8 chunks using embedding-based strategy
```

**Phase 3** (Parallel Analysis):
```
[INFO] Starting parallel analysis of 8 chunks with max parallelism 3
[INFO] Chunk 1/8 analyzed (12.5%): SUCCESS
[INFO] Chunk 2/8 analyzed (25.0%): SUCCESS
[INFO] Chunk 3/8 analyzed (37.5%): SUCCESS
...
[INFO] Parallel analysis complete: 8/8 successful (100.0% success rate)
```

**Phase 4** (Merge):
```
[INFO] Merging analyses: overview + 8 chunks (100.0% success rate)
[INFO] Pre-merge deduplication: 127 items → 89 items (38 duplicates removed)
[INFO] Merge complete: 12 mechanics, 24 resources, 8 phases, confidence: 0.91
[INFO] Background analysis complete: 187.4s total
```

---

## Performance Benchmarks

### Target Metrics (50-page rulebook)

| Phase | Duration | Notes |
|-------|----------|-------|
| Overview | 5-10s | LLM processes 17k sampled chars |
| Chunking | 3-8s | Embedding generation for sections |
| Analysis | 90-150s | Parallel (3 chunks at a time) |
| Merge | 10-20s | LLM synthesizes results |
| **Total** | **2-3min** | Well under 5min timeout |

### Redis Key Count

After 10 large analyses:
- Task status keys: 10 (with 24h TTL)
- Progress keys: 10 (with 24h TTL)
- Total: 20 keys (cleaned automatically after 24h)

---

## Troubleshooting

### Issue: 202 Accepted but progress stuck at 0%

**Diagnosis**:
```bash
# Check if task is running
redis-cli GET "meepleai:tasks:analysis:{taskId}"

# Check API logs
docker logs meepleai-api-1 | grep "taskId={taskId}"
```

**Possible Causes**:
- Background task failed to start
- Exception thrown before first progress update
- Redis connection issues

---

### Issue: Analysis completes but status=Running

**Diagnosis**:
```bash
# Verify task status in orchestrator
redis-cli GET "meepleai:tasks:status:{taskId}"

# Check if analysis persisted to DB
psql -c "SELECT id, game_title, is_active FROM rulebook_analyses WHERE pdf_document_id = '{pdfId}' ORDER BY analyzed_at DESC LIMIT 1;"
```

**Possible Causes**:
- Final progress update failed
- Database save succeeded but status update failed

---

### Issue: Chunks fail with timeout

**Diagnosis**:
```bash
# Check LLM service logs
docker logs meepleai-api-1 | grep "LlmService"

# Verify embedding service responsive
curl http://localhost:8080/health
```

**Possible Causes**:
- LLM API rate limiting
- Embedding service overloaded
- Network timeout to external LLM

---

## Success Criteria Checklist

### Functional
- [ ] Small rulebook (<30k) → 200 OK immediate
- [ ] Large rulebook (>30k) → 202 Accepted <500ms
- [ ] Progress visible and accurate (0% → 100%)
- [ ] All 4 phases execute in sequence
- [ ] Final result matches quality of sync analysis
- [ ] No 15k truncation (full content analyzed)

### Performance
- [ ] API response: <500ms for 202 Accepted
- [ ] Background analysis: 2-5 minutes for 50+ page rulebook
- [ ] Progress updates: Every 10% increment minimum
- [ ] Phase 3: 3 chunks analyzed concurrently (check logs)

### Reliability
- [ ] Partial failures handled (70% threshold)
- [ ] Retry logic: 3 attempts with exponential backoff
- [ ] Cancellation: Clean termination when user cancels
- [ ] Redis cleanup: Keys expire after 24h

### Configuration
- [ ] `appsettings.json` overrides work
- [ ] Lower threshold triggers background earlier
- [ ] Max chunk size respected
- [ ] Parallelism configurable

---

## Manual Test Execution Log

**Date**: _______
**Tester**: _______
**Environment**: Development / Staging

| Scenario | Result | Notes |
|----------|--------|-------|
| 1. Small rulebook sync | ☐ PASS ☐ FAIL | Response time: _____ |
| 2. Large rulebook async | ☐ PASS ☐ FAIL | Total time: _____ |
| 3. Progress tracking | ☐ PASS ☐ FAIL | Updates count: _____ |
| 4. Semantic chunking | ☐ PASS ☐ FAIL | Strategy used: _____ |
| 5. Partial failure | ☐ PASS ☐ FAIL | Success rate: _____ |
| 6. Concurrent requests | ☐ PASS ☐ FAIL | Locks acquired: _____ |
| 7. Config override | ☐ PASS ☐ FAIL | Threshold: _____ |

**Issues Found**: ___________________________________________

**Recommendations**: _______________________________________

---

## Automated Test Recommendations

After manual validation, create automated tests:
- **#2525**: Unit/integration tests for all services
- **E2E**: Playwright tests for full flow
- **Load**: k6 tests for concurrent requests
- **Chaos**: Simulate failures (Redis down, LLM timeout)
