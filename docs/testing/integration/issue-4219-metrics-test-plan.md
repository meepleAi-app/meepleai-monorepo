# Integration Test Plan: Issue #4219 - PDF Metrics & ETA

**Date**: 2026-02-13
**Issue**: #4219 - Duration Metrics & ETA Calculation
**Status**: Ready for manual testing

---

## Prerequisites

- [x] Backend running (http://localhost:8080)
- [x] Frontend running (http://localhost:3000)
- [x] Database migration applied (Issue4219_PdfMetricsTiming)
- [x] Valid user session with PDF upload permissions
- [ ] Test PDF file available (recommend: 5-10 pages for quick testing)

---

## Manual Test Procedure

### Test 1: Upload PDF and Track Metrics

**Steps**:
1. Navigate to http://localhost:3000
2. Login with valid credentials
3. Navigate to a game detail page
4. Click "Upload PDF" or access Knowledge Base
5. Upload a test PDF (5-10 pages recommended)
6. Note the document ID from response

**Expected Behavior**:
- Upload succeeds
- Processing starts automatically
- Document ID returned

---

### Test 2: Verify Metrics Endpoint (Backend)

**API Call**:
```bash
# Replace {documentId} with actual ID from Test 1
curl -X GET http://localhost:8080/api/v1/documents/{documentId}/metrics \
  -H "Cookie: session_token={your_session}" \
  -H "Content-Type: application/json"
```

**Expected Response** (200 OK):
```json
{
  "documentId": "guid",
  "currentState": "Extracting",
  "progressPercentage": 30,
  "totalDuration": null,
  "estimatedTimeRemaining": "00:01:30",
  "stateDurations": {
    "Uploading": "00:00:15"
  },
  "retryCount": 0,
  "pageCount": 5
}
```

**Validation Checklist**:
- [ ] documentId matches uploaded PDF
- [ ] currentState reflects actual processing state
- [ ] progressPercentage is 0-100 range
- [ ] estimatedTimeRemaining is not null (unless Ready/Failed)
- [ ] stateDurations contains at least current state
- [ ] retryCount is 0 (or correct if retried)
- [ ] pageCount matches PDF (null if not extracted yet)

---

### Test 3: Verify Frontend Display (UI)

**Steps**:
1. Navigate to page showing PdfMetricsDisplay component
2. Observe progress bar and ETA display
3. Wait for state transitions
4. Verify ETA updates as processing progresses

**Expected UI Elements**:
- [ ] Progress bar shows percentage (0-100%)
- [ ] Current state label visible (e.g., "Extracting", "Embedding")
- [ ] ETA displays as "~Xm Ys remaining" (e.g., "~3m 45s remaining")
- [ ] ETA decreases as processing progresses
- [ ] ETA shows "-" or "0s" when completed/failed
- [ ] Retry count indicator appears if retries occurred
- [ ] Total duration visible (if showTotalDuration=true)

**Accessibility**:
- [ ] Progress bar has aria-label
- [ ] ETA has aria-live="polite"
- [ ] Keyboard navigable
- [ ] Screen reader announces progress updates

---

### Test 4: State Transition Timing

**Procedure**:
1. Upload PDF
2. Poll metrics endpoint every 2 seconds
3. Record timing for each state transition
4. Compare with expected timing

**Expected State Progression**:
```
Pending (0%) → Uploading (10%) → Extracting (30%) →
Chunking (50%) → Embedding (70%) → Indexing (90%) → Ready (100%)
```

**Timing Validation**:
- [ ] Each state has non-null StartedAt timestamp
- [ ] StateDurations accurately reflects time spent per state
- [ ] TotalDuration = ProcessedAt - UploadedAt (when complete)
- [ ] ETA decreases as states progress
- [ ] ETA formula: ~2 seconds/page × remaining states (MVP)

---

### Test 5: Error Scenarios

**Test 5a: Failed Processing**
1. Upload corrupted/invalid PDF
2. Wait for failure
3. Verify metrics endpoint

**Expected**:
- [ ] currentState = "Failed"
- [ ] progressPercentage = 0
- [ ] estimatedTimeRemaining = "00:00:00" (zero)
- [ ] errorMessage populated (if available)
- [ ] failedAtState indicates where failure occurred

**Test 5b: Retry After Failure**
1. From failed state, trigger retry (POST /documents/{id}/retry)
2. Poll metrics endpoint
3. Verify retry count increments

**Expected**:
- [ ] retryCount increments (1, 2, or 3)
- [ ] State resumes from failedAtState
- [ ] Timing resets for retried states
- [ ] ETA recalculates

**Test 5c: Non-Existent Document**
```bash
curl -X GET http://localhost:8080/api/v1/documents/00000000-0000-0000-0000-000000000001/metrics
```

**Expected**:
- [ ] 404 Not Found
- [ ] Error message: "PDF document {id} not found"

---

### Test 6: Performance Validation

**Metrics Endpoint Response Time**:
```bash
# Measure response time
time curl http://localhost:8080/api/v1/documents/{documentId}/metrics
```

**Expected**:
- [ ] Response time < 200ms (performance target)
- [ ] No N+1 query issues
- [ ] StateDurations calculated efficiently

---

## Automated E2E Test (Future)

**Playwright Test Outline**:
```typescript
test('PDF Metrics and ETA - Full Pipeline', async ({ page }) => {
  // 1. Login
  await page.goto('http://localhost:3000/login');
  await page.fill('[name="email"]', 'test@example.com');
  await page.fill('[name="password"]', 'password');
  await page.click('button[type="submit"]');

  // 2. Navigate to game
  await page.goto('http://localhost:3000/games/{gameId}');

  // 3. Upload PDF
  const fileInput = await page.locator('input[type="file"]');
  await fileInput.setInputFiles('test-files/sample-5pages.pdf');

  // 4. Wait for processing to start
  await page.waitForSelector('[data-testid="pdf-metrics-display"]');

  // 5. Verify progress bar visible
  const progressBar = page.locator('[data-testid="pdf-progress-bar"]');
  await expect(progressBar).toBeVisible();

  // 6. Verify ETA displays
  const eta = page.locator('[data-testid="eta-display"]');
  await expect(eta).toBeVisible();
  await expect(eta).toContainText('remaining');

  // 7. Poll until completed (max 2 minutes)
  await page.waitForSelector('[data-testid="progress-percentage"]:has-text("100%")', {
    timeout: 120000,
  });

  // 8. Verify final state
  const finalProgress = await page.locator('[data-testid="progress-percentage"]').textContent();
  expect(finalProgress).toBe('100%');

  const finalETA = await page.locator('[data-testid="eta-display"]').textContent();
  expect(finalETA).toBe('-'); // No remaining time
});
```

---

## Success Criteria

**Integration Test Passes When**:
- [ ] PDF uploads successfully
- [ ] Metrics endpoint returns valid data
- [ ] Frontend displays progress bar with ETA
- [ ] ETA updates as processing progresses
- [ ] ETA reaches 0 when completed
- [ ] StateDurations accurately reflect timing
- [ ] Retry count tracked correctly (if retries occur)
- [ ] Performance < 200ms for metrics endpoint

---

## Known Limitations (MVP)

1. **Static ETA**: Formula uses fixed 2s/page, actual may vary
2. **No Historical Data**: ML predictor requires data collection (Phase 2)
3. **State Granularity**: Timing per-state, not per-page
4. **Integration Test**: Manual for now, E2E automation scheduled

---

**Test Status**: ⏳ Ready for manual execution
**Next**: Upload PDF and validate metrics + ETA accuracy
