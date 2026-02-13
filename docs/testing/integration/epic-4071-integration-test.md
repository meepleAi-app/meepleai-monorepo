# Epic #4071 Integration Test - Complete PDF Status Tracking

**Date**: 2026-02-13
**Epic**: #4071 PDF Status Tracking
**Issues**: #4219 (Metrics), #4220 (Notifications)
**Estimated Time**: 15-20 minutes

---

## Prerequisites

### 1. Services Running
```bash
# Terminal 1: Backend
cd /d/Repositories/meepleai-monorepo-backend/apps/api/src/Api
dotnet run

# Terminal 2: Frontend
cd /d/Repositories/meepleai-monorepo-frontend/apps/web
pnpm dev

# Terminal 3: Infrastructure (if not running)
cd /d/Repositories/meepleai-monorepo-backend/infra
docker compose up -d postgres qdrant redis
```

### 2. Test Materials
- [ ] Test PDF file (5-10 pages recommended for quick test)
- [ ] Valid user account with login credentials
- [ ] Browser: Chrome/Firefox/Edge

### 3. Database Migration
```bash
cd /d/Repositories/meepleai-monorepo-backend/apps/api/src/Api
dotnet ef database update --context MeepleAiDbContext
```

**Expected**: Migrations applied successfully
- `Issue4219_PdfMetricsTiming` - 5 timing columns
- `Issue4220_NotificationPreferences` - notification preferences table

---

## Test Procedure

### Phase 1: Authentication & Setup (5 min)

**Step 1.1**: Navigate to Application
```
URL: http://localhost:3000
```

**Step 1.2**: Login
- Enter credentials
- Verify successful login
- Confirm redirected to dashboard/home

**Step 1.3**: Navigate to Game Detail
- Select any existing game OR create a new test game
- Navigate to game detail page
- Locate PDF upload section (Knowledge Base or Documents tab)

**Checkpoint**:
- [ ] Logged in successfully
- [ ] On game detail page
- [ ] Upload button visible

---

### Phase 2: PDF Upload & Processing (5-10 min)

**Step 2.1**: Upload PDF
1. Click "Upload PDF" button
2. Select test PDF file (5-10 pages)
3. Confirm upload
4. **Note Document ID** from response/URL

**Expected**:
- [ ] Upload succeeds (200 OK)
- [ ] Document ID returned (GUID format)
- [ ] Processing starts automatically
- [ ] Initial state: "Uploading" or "Extracting"

**Step 2.2**: Monitor Progress (Real-Time)
1. Observe progress bar (should be visible)
2. Watch state transitions
3. **Record state changes** and timing

**Expected State Progression**:
```
Pending (0%) → Uploading (10%) → Extracting (30%) →
Chunking (50%) → Embedding (70%) → Indexing (90%) → Ready (100%)
```

**Timing Target**: ~30-60 seconds for 5-page PDF (varies by system)

**Checkpoint**:
- [ ] Progress bar visible
- [ ] Progress percentage updates
- [ ] State label shows current state

---

### Phase 3: Metrics Verification (Issue #4219) (3 min)

**Step 3.1**: Verify ETA Display (Frontend)

**Expected UI Elements**:
- [ ] Progress bar shows 0-100%
- [ ] State label (e.g., "Extracting", "Embedding")
- [ ] **ETA display**: "~Xm Ys remaining" (e.g., "~3m 45s remaining")
- [ ] ETA decreases as processing progresses
- [ ] ETA shows "-" or "0s" when state = Ready

**Step 3.2**: API Endpoint Test (Backend)

**Replace {documentId} with actual ID from Step 2.1**:

```bash
# Get session cookie from browser DevTools:
# 1. Open DevTools (F12)
# 2. Application tab → Cookies → localhost:3000
# 3. Copy 'session_token' value

curl -X GET "http://localhost:8080/api/v1/documents/{documentId}/metrics" \
  -H "Cookie: session_token={YOUR_SESSION_TOKEN}" \
  -H "Content-Type: application/json" | jq
```

**Expected Response** (200 OK):
```json
{
  "documentId": "guid",
  "currentState": "Ready",
  "progressPercentage": 100,
  "totalDuration": "00:00:45.1234567",
  "estimatedTimeRemaining": "00:00:00",
  "stateDurations": {
    "Uploading": "00:00:05.2000000",
    "Extracting": "00:00:15.8000000",
    "Chunking": "00:00:08.5000000",
    "Embedding": "00:00:10.3000000",
    "Indexing": "00:00:05.3000000"
  },
  "retryCount": 0,
  "pageCount": 5
}
```

**Validation Checklist**:
- [ ] `documentId` matches uploaded PDF
- [ ] `currentState` = "Ready" (or current state if still processing)
- [ ] `progressPercentage` = 100 (or 0-99 if in progress)
- [ ] `totalDuration` not null (when completed)
- [ ] `estimatedTimeRemaining` = "00:00:00" (when Ready/Failed)
- [ ] `stateDurations` contains all completed states
- [ ] `retryCount` = 0 (no retries)
- [ ] `pageCount` matches PDF page count

**Step 3.3**: Timing Accuracy Validation

Calculate expected vs actual timing:
- **Formula**: ETA = 2 seconds/page × remaining states
- **Example**: 5-page PDF at "Extracting" (state 2/7)
  - Remaining states: 5 (Chunking, Embedding, Indexing, Ready)
  - Expected ETA: 2s × 5 pages × 5 states = 50 seconds
  - Actual: Check `estimatedTimeRemaining` value

**Validation**:
- [ ] ETA formula accuracy within ±30% (MVP static calculation)
- [ ] State durations sum ≈ total duration
- [ ] No negative durations
- [ ] Timing consistent across states

---

### Phase 4: Notification Verification (Issue #4220) (3 min)

**Step 4.1**: Check In-App Notifications

1. Navigate to Notification Center (bell icon or /notifications)
2. Look for PDF processing notification

**Expected Notification**:
- [ ] Notification appears when PDF reaches "Ready" state
- [ ] Title: "PDF Ready" or "PDF Processing Complete"
- [ ] Message: References document name or game
- [ ] Type: Success (green indicator)
- [ ] Timestamp: Recent (within last minute)
- [ ] Clickable link to Knowledge Base (optional)

**Step 4.2**: Notification Preferences API Test

```bash
# GET preferences
curl -X GET "http://localhost:8080/api/v1/notifications/preferences" \
  -H "Cookie: session_token={YOUR_SESSION_TOKEN}" | jq

# Expected response:
{
  "userId": "guid",
  "emailOnDocumentReady": true,
  "emailOnDocumentFailed": true,
  "emailOnRetryAvailable": false,
  "pushOnDocumentReady": true,
  "pushOnDocumentFailed": true,
  "pushOnRetryAvailable": false,
  "inAppOnDocumentReady": true,
  "inAppOnDocumentFailed": true,
  "inAppOnRetryAvailable": true
}
```

**Validation**:
- [ ] Endpoint responds (200 OK)
- [ ] All 9 boolean preferences present
- [ ] Default values correct (most true, retry false)

**Step 4.3**: Update Preferences Test

```bash
# PUT preferences (disable email notifications)
curl -X PUT "http://localhost:8080/api/v1/notifications/preferences" \
  -H "Cookie: session_token={YOUR_SESSION_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "emailOnDocumentReady": false,
    "emailOnDocumentFailed": false,
    "emailOnRetryAvailable": false,
    "pushOnDocumentReady": true,
    "pushOnDocumentFailed": true,
    "pushOnRetryAvailable": false,
    "inAppOnDocumentReady": true,
    "inAppOnDocumentFailed": true,
    "inAppOnRetryAvailable": true
  }'

# Verify update
curl -X GET "http://localhost:8080/api/v1/notifications/preferences" \
  -H "Cookie: session_token={YOUR_SESSION_TOKEN}" | jq '.emailOnDocumentReady'
```

**Expected**:
- [ ] PUT returns 204 No Content
- [ ] GET shows updated values (emailOnDocumentReady = false)
- [ ] Next upload should NOT create email notification

---

### Phase 5: Error Scenario Testing (5 min)

**Step 5.1**: Upload Invalid PDF (Trigger Failure)

1. Create corrupted PDF or use invalid file
2. Upload via same flow
3. Wait for processing to fail

**Expected**:
- [ ] State transitions to "Failed"
- [ ] Progress percentage = 0
- [ ] ETA = "00:00:00" (zero)
- [ ] Error message populated
- [ ] **Notification**: "PDF Failed" notification appears (if preference enabled)

**Step 5.2**: Test Retry Flow

1. From failed PDF, click "Retry" button (if UI exists)
2. OR call retry endpoint:
```bash
curl -X POST "http://localhost:8080/api/v1/documents/{documentId}/retry" \
  -H "Cookie: session_token={YOUR_SESSION_TOKEN}"
```

**Expected**:
- [ ] Retry count increments (retryCount = 1)
- [ ] State resumes from failedAtState
- [ ] **Notification**: "PDF Retry" notification appears (if preference enabled)
- [ ] Metrics reflect retry overhead

---

## Success Criteria

### Issue #4219 (Metrics & ETA)
- [ ] ✅ Metrics endpoint returns valid data
- [ ] ✅ Frontend displays progress bar (0-100%)
- [ ] ✅ ETA shows "~Xm Ys remaining" format
- [ ] ✅ ETA updates as processing progresses
- [ ] ✅ ETA reaches 0 when completed
- [ ] ✅ Total duration calculated correctly
- [ ] ✅ State durations accurate (±30% tolerance)
- [ ] ✅ Retry count tracked

### Issue #4220 (Notifications)
- [ ] ✅ In-app notification created on DocumentReady
- [ ] ✅ Notification appears in Notification Center
- [ ] ✅ Preferences API (GET/PUT) functional
- [ ] ✅ Preference changes affect notification dispatch
- [ ] ✅ Error notifications created on failure (if enabled)
- [ ] ✅ Retry notifications created (if enabled)

---

## Known Limitations (MVP Scope)

1. **Email Notifications**: Templates exist but dispatch not implemented (Issue #4220 Phase 2)
2. **Push Notifications**: Schema defined but service integration pending
3. **Settings UI**: Preferences API ready but UI not implemented
4. **ETA Accuracy**: Static formula (2s/page), actual may vary ±30%

---

## Quick Manual Test (5 minutes)

**Minimal Validation Flow**:

1. **Login**: http://localhost:3000 → Enter credentials
2. **Navigate**: Games → Select game → Knowledge Base/Documents
3. **Upload**: Click Upload → Select PDF (5 pages) → Confirm
4. **Observe**: Watch progress bar + "~Xm Ys remaining" display
5. **Verify**: After completion, check Notification Center for "PDF Ready"
6. **API Test**: Copy document ID → Test `/metrics` endpoint with curl

**Result**: ✅ Pass if all 6 steps complete without errors

---

## Automated E2E Test Script (Future)

**Playwright Test** (to be implemented):
```typescript
// tests/e2e/epic-4071-pdf-status-tracking.spec.ts
test('Epic #4071: Complete PDF Status Tracking Flow', async ({ page }) => {
  // 1. Login
  await page.goto('http://localhost:3000/login');
  await page.fill('[name="email"]', 'test@meepleai.dev');
  await page.fill('[name="password"]', 'Test123!');
  await page.click('button[type="submit"]');

  // 2. Navigate to game
  await page.goto('http://localhost:3000/games/{gameId}/knowledge-base');

  // 3. Upload PDF
  const fileInput = page.locator('input[type="file"]');
  await fileInput.setInputFiles('tests/fixtures/sample-5pages.pdf');

  // 4. Verify metrics display
  await page.waitForSelector('[data-testid="pdf-metrics-display"]');
  const eta = page.locator('[data-testid="eta-display"]');
  await expect(eta).toBeVisible();
  await expect(eta).toContainText('remaining');

  // 5. Wait for completion
  await page.waitForSelector('[data-testid="progress-percentage"]:has-text("100%")', {
    timeout: 120000,
  });

  // 6. Verify notification
  await page.goto('http://localhost:3000/notifications');
  await expect(page.locator('text=PDF Ready')).toBeVisible();

  // 7. Verify preferences API
  const response = await page.request.get('http://localhost:8080/api/v1/notifications/preferences');
  expect(response.status()).toBe(200);
  const prefs = await response.json();
  expect(prefs.inAppOnDocumentReady).toBe(true);
});
```

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| 404 on /metrics endpoint | Restart backend: `dotnet run` |
| 404 on /preferences endpoint | Restart backend to load new routes |
| Upload fails | Check Postgres, Qdrant, Redis running |
| ETA not showing | Verify PdfMetricsDisplay component used |
| No notification appears | Check preferences enabled, restart backend |
| Migration error | `dotnet ef database update --context MeepleAiDbContext` |

---

## Test Results Template

**Date**: ___________
**Tester**: ___________

| Test | Pass | Fail | Notes |
|------|------|------|-------|
| Upload PDF | ☐ | ☐ | |
| Progress bar visible | ☐ | ☐ | |
| ETA displays | ☐ | ☐ | |
| ETA updates | ☐ | ☐ | |
| Metrics API responds | ☐ | ☐ | |
| State durations accurate | ☐ | ☐ | |
| Notification created | ☐ | ☐ | |
| Preferences API works | ☐ | ☐ | |

**Overall Result**: ☐ PASS ☐ FAIL

**Issues Found**: _____________________

---

**Next**: Execute test and report results.
