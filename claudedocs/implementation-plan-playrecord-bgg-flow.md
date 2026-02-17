# Implementation Plan: PlayRecord + BGG Integration Flow

**Created**: 2026-02-13
**Epic**: #3887 (Play Records) + BGG Integration
**Total Issues**: 4 sequential issues
**Estimated Duration**: 6-7 giorni
**Target Branch**: `frontend-dev` (parent for all features)

---

## 🎯 Overview

Implementare il flusso completo di creazione PlayRecord con integrazione BGG search:
1. Game search autocomplete in wizard
2. BGG rate limiting backend
3. Inline BGG search dialog
4. E2E test coverage

**Success Metrics**:
- ✅ Flusso seamless senza interruzioni
- ✅ Rate limiting BGG efficace
- ✅ Test coverage ≥85% frontend, ≥90% backend
- ✅ Performance: Full flow <10s

---

## Issue #1: Game Search Autocomplete (#4273)

### Branch Strategy
```bash
git checkout frontend-dev && git pull
git checkout -b feature/issue-4273-game-search-autocomplete
git config branch.feature/issue-4273-game-search-autocomplete.parent frontend-dev
```

### Implementation Steps

#### Step 1: Create Game Search API Hook (30min)
**File**: `apps/web/src/lib/hooks/use-game-search.ts`
```typescript
// New file
export function useGameSearch(query: string) {
  // Search across UserLibrary + SharedGames + PrivateGames
  // Debounced 300ms
  // Returns: GameSearchResult[] with source badge
}
```

**Tasks**:
- [ ] Create hook with debounce (use-debounce)
- [ ] API call: `GET /api/v1/games/search?q={query}`
- [ ] React Query integration
- [ ] Return type: `{ id, name, source: 'library'|'catalog'|'private' }`

#### Step 2: Create GameCombobox Component (1h)
**File**: `apps/web/src/components/play-records/GameCombobox.tsx`
```typescript
// New component using shadcn/ui Combobox
interface GameComboboxProps {
  value?: string;
  onSelect: (gameId: string) => void;
  onNotFound?: () => void;
}
```

**Tasks**:
- [ ] shadcn/ui Combobox base
- [ ] Integrate useGameSearch hook
- [ ] Display results with badges
- [ ] Empty state: "Not found? [Search BGG](#)"
- [ ] Loading skeleton
- [ ] Keyboard navigation

#### Step 3: Replace Dropdown in SessionCreateForm (30min)
**File**: `apps/web/src/components/play-records/SessionCreateForm.tsx`

**Changes**:
```diff
- <Select>
-   <SelectItem value="placeholder">Loading...</SelectItem>
- </Select>
+ <GameCombobox
+   value={form.watch('gameId')}
+   onSelect={(id) => form.setValue('gameId', id)}
+   onNotFound={() => setShowBggSearch(true)}
+ />
```

**Tasks**:
- [ ] Remove old Select component
- [ ] Add GameCombobox import
- [ ] Wire up form state
- [ ] Add onNotFound callback

#### Step 4: Backend Search Endpoint (1h)
**File**: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/SearchGamesQuery.cs`

**Tasks**:
- [ ] Create SearchGamesQuery + Handler
- [ ] Search: UserLibrary (userId) + SharedGames + PrivateGames (userId)
- [ ] Return: `{ Id, Name, Source, ImageUrl }[]`
- [ ] Endpoint: `GET /api/v1/games/search`
- [ ] Unit tests

#### Step 5: Component Tests (1h)
**Files**:
- `apps/web/__tests__/play-records/components/GameCombobox.test.tsx`
- `apps/web/__tests__/play-records/components/SessionCreateForm.test.tsx` (update)

**Tasks**:
- [ ] Test: search returns results
- [ ] Test: debounce works (300ms)
- [ ] Test: badges display correctly
- [ ] Test: empty state shows "Search BGG"
- [ ] Test: keyboard navigation
- [ ] Update SessionCreateForm tests

#### Step 6: Integration Test (30min)
**File**: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Queries/SearchGamesQueryTests.cs`

**Tasks**:
- [ ] Setup: Seed UserLibrary + SharedGames + PrivateGames
- [ ] Test: search finds all sources
- [ ] Test: filters by current user
- [ ] Test: returns correct source labels

### DoD
- [ ] GameCombobox component created and tested
- [ ] useGameSearch hook functional
- [ ] Backend SearchGamesQuery endpoint working
- [ ] SessionCreateForm updated
- [ ] Unit tests ≥85% coverage
- [ ] Integration tests ≥90% coverage
- [ ] PR created to `frontend-dev`
- [ ] Code review passed
- [ ] Merged to `frontend-dev`
- [ ] Issue #4273 closed on GitHub

### Testing Checklist
```bash
# Frontend tests
cd apps/web
pnpm test GameCombobox
pnpm test SessionCreateForm
pnpm typecheck && pnpm lint

# Backend tests
cd apps/api/src/Api
dotnet test --filter "SearchGamesQuery"

# Manual test
pnpm dev  # Navigate to /play-records/new
```

---

## Issue #2: BGG Rate Limiting (#4275)

### Branch Strategy
```bash
git checkout frontend-dev && git pull
git checkout -b feature/issue-4275-bgg-rate-limiting
git config branch.feature/issue-4275-bgg-rate-limiting.parent frontend-dev
```

### Implementation Steps

#### Step 1: Rate Limit Configuration (30min)
**File**: `apps/api/src/Api/appsettings.json`
```json
{
  "BggRateLimit": {
    "FreeTier": 5,
    "NormalTier": 10,
    "PremiumTier": 20,
    "EditorTier": 15,
    "AdminBypass": true,
    "WindowSeconds": 60,
    "EnableMetrics": true
  }
}
```

**File**: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Configuration/BggRateLimitOptions.cs`
```csharp
// New file
public class BggRateLimitOptions
{
    public int FreeTier { get; set; }
    public int NormalTier { get; set; }
    // ... etc
}
```

#### Step 2: Rate Limit Middleware (2h)
**File**: `apps/api/src/Api/Infrastructure/Middleware/BggRateLimitMiddleware.cs`

**Tasks**:
- [ ] Sliding window algorithm using Redis
- [ ] Key: `bgg:ratelimit:{userId}`
- [ ] Increment on BGG API calls
- [ ] Check quota based on user tier
- [ ] Set response headers: X-RateLimit-*
- [ ] Return 429 with Retry-After when exceeded
- [ ] Admin bypass logic
- [ ] Fail-open if Redis unavailable

**Implementation Pattern**:
```csharp
public class BggRateLimitMiddleware
{
    public async Task InvokeAsync(HttpContext context, IDistributedCache cache)
    {
        // Only apply to /api/v1/bgg/* endpoints
        if (!context.Request.Path.StartsWithSegments("/api/v1/bgg"))
            return await _next(context);

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var tier = context.User.FindFirstValue("tier");

        var limit = GetLimitForTier(tier);
        var key = $"bgg:ratelimit:{userId}";

        // Sliding window check
        var current = await GetCurrentCount(cache, key);

        if (current >= limit && !IsAdmin(context.User))
        {
            context.Response.StatusCode = 429;
            context.Response.Headers["Retry-After"] = "60";
            return;
        }

        await IncrementCounter(cache, key, 60);

        // Set rate limit headers
        context.Response.Headers["X-RateLimit-Limit"] = limit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = (limit - current - 1).ToString();

        await _next(context);
    }
}
```

#### Step 3: Register Middleware (15min)
**File**: `apps/api/src/Api/Program.cs`

```diff
+ builder.Services.Configure<BggRateLimitOptions>(
+     builder.Configuration.GetSection("BggRateLimit"));
+
+ app.UseMiddleware<BggRateLimitMiddleware>();
```

#### Step 4: Unit Tests (1h)
**File**: `apps/api/tests/Api.Tests/Infrastructure/Middleware/BggRateLimitMiddlewareTests.cs`

**Tests**:
- [ ] Within quota → Allow request + set headers
- [ ] Quota exceeded → Return 429 + Retry-After
- [ ] Admin user → Bypass rate limit
- [ ] Redis unavailable → Fail-open (allow request)
- [ ] Counter increments correctly
- [ ] TTL expires after 60s

#### Step 5: Integration Tests (1h)
**File**: `apps/api/tests/Api.Tests/Integration/BggRateLimitIntegrationTests.cs`

**Tests**:
- [ ] Setup: TestContainer Redis
- [ ] Test: 10 requests in 1min → Last request gets 429
- [ ] Test: Wait 60s → Counter resets
- [ ] Test: Different users have separate quotas
- [ ] Test: Response headers correct

#### Step 6: Metrics & Monitoring (30min)
**File**: `apps/api/src/Api/Infrastructure/Middleware/BggRateLimitMiddleware.cs`

**Tasks**:
- [ ] Log rate limit hits: `_logger.LogWarning("BGG rate limit exceeded", userId, tier)`
- [ ] Metrics counter: `bgg_ratelimit_exceeded_total{tier="Normal"}`
- [ ] Prometheus endpoint: `/metrics`

### DoD
- [ ] BggRateLimitMiddleware created and tested
- [ ] Configuration in appsettings.json
- [ ] Middleware registered in Program.cs
- [ ] Rate limit headers in all BGG responses
- [ ] 429 responses with Retry-After
- [ ] Admin bypass working
- [ ] Fail-open if Redis down
- [ ] Unit tests ≥90% coverage
- [ ] Integration tests with Redis
- [ ] Metrics enabled
- [ ] PR created to `frontend-dev`
- [ ] Code review passed
- [ ] Merged to `frontend-dev`
- [ ] Issue #4275 closed on GitHub

### Testing Checklist
```bash
# Unit tests
dotnet test --filter "BggRateLimitMiddleware"

# Integration tests
dotnet test --filter "BggRateLimitIntegration"

# Manual test
curl -H "Authorization: Bearer <token>" http://localhost:8080/api/v1/bgg/search?query=catan
# Check headers: X-RateLimit-Limit, X-RateLimit-Remaining

# Test 429
for i in {1..15}; do curl -H "Authorization: Bearer <token>" http://localhost:8080/api/v1/bgg/search?query=test; done
# Should get 429 after tier limit
```

---

## Issue #3: BGG Search Dialog (#4274)

### Branch Strategy
```bash
git checkout frontend-dev && git pull
git checkout -b feature/issue-4274-bgg-search-dialog
git config branch.feature/issue-4274-bgg-search-dialog.parent frontend-dev
```

### Implementation Steps

#### Step 1: Create BggQuickSearchDialog Component (2h)
**File**: `apps/web/src/components/play-records/BggQuickSearchDialog.tsx`

**Component Structure**:
```tsx
interface BggQuickSearchDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onGameAdded: (game: PrivateGame) => void;
}

export function BggQuickSearchDialog(props: BggQuickSearchDialogProps) {
  // Search input with debounce (500ms)
  // Rate limit display
  // Results list
  // Add to library button
}
```

**Tasks**:
- [ ] Dialog component (shadcn/ui Dialog)
- [ ] Search input with debounce
- [ ] API hook: useBggSearch
- [ ] Rate limit info display (from headers)
- [ ] Results list with cards
- [ ] Loading states (searching, adding)
- [ ] Error handling (429, network)

#### Step 2: Create useBggSearch Hook (1h)
**File**: `apps/web/src/lib/hooks/use-bgg-search.ts`

**Tasks**:
- [ ] React Query mutation for BGG search
- [ ] Extract rate limit headers from response
- [ ] Return: `{ data, isLoading, rateLimitInfo }`
- [ ] Handle 429 errors gracefully
- [ ] Cache results (1 hour)

#### Step 3: Create useAddPrivateGame Hook (30min)
**File**: `apps/web/src/lib/hooks/use-private-games.ts`

**Tasks**:
- [ ] React Query mutation: POST /api/v1/user-library/private-games
- [ ] Optimistic update (add to cache immediately)
- [ ] Invalidate queries on success
- [ ] Error handling

#### Step 4: Rate Limit UI Component (1h)
**File**: `apps/web/src/components/play-records/BggRateLimitIndicator.tsx`

**Component**:
```tsx
interface BggRateLimitIndicatorProps {
  remaining: number;
  limit: number;
  resetAt: number; // Unix timestamp
}

// Display: "🔍 7/10 searches (resets in 3min 45s)"
// Color: Green (>50%) | Yellow (20-50%) | Red (<20%)
```

**Tasks**:
- [ ] Display remaining/limit
- [ ] Countdown timer to reset
- [ ] Color coding
- [ ] Tooltip: "BGG searches are limited to prevent abuse"

#### Step 5: Integrate into SessionCreateForm (1h)
**File**: `apps/web/src/components/play-records/SessionCreateForm.tsx`

**Changes**:
```diff
+ const [showBggDialog, setShowBggDialog] = useState(false);
+
  <GameCombobox
    value={form.watch('gameId')}
    onSelect={(id) => form.setValue('gameId', id)}
+   onNotFound={() => setShowBggDialog(true)}
  />
+
+ <BggQuickSearchDialog
+   open={showBggDialog}
+   onOpenChange={setShowBggDialog}
+   onGameAdded={(game) => {
+     form.setValue('gameId', game.id);
+     setShowBggDialog(false);
+     toast.success('Game added to library');
+   }}
+ />
```

#### Step 6: Component Tests (2h)
**Files**:
- `apps/web/__tests__/play-records/components/BggQuickSearchDialog.test.tsx`
- `apps/web/__tests__/play-records/components/BggRateLimitIndicator.test.tsx`

**Test Cases**:
- [ ] Dialog opens on trigger
- [ ] Search calls API with debounce
- [ ] Results display correctly
- [ ] Add to library works
- [ ] Rate limit display updates
- [ ] 429 error shows friendly message
- [ ] Dialog closes on success
- [ ] Countdown timer works

### DoD
- [ ] BggQuickSearchDialog created and tested
- [ ] useBggSearch hook functional
- [ ] useAddPrivateGame hook functional
- [ ] BggRateLimitIndicator component
- [ ] SessionCreateForm integration complete
- [ ] Unit tests ≥85% coverage
- [ ] All TypeScript errors resolved
- [ ] PR created to `frontend-dev`
- [ ] Code review passed
- [ ] Merged to `frontend-dev`
- [ ] Issue #4274 closed on GitHub

### Testing Checklist
```bash
cd apps/web

# Component tests
pnpm test BggQuickSearchDialog
pnpm test BggRateLimitIndicator
pnpm test SessionCreateForm

# Type check
pnpm typecheck

# Lint
pnpm lint

# Manual test
pnpm dev
# Navigate to /play-records/new
# Search non-existent game → Click "Search BGG"
# Verify dialog opens and works
```

---

## Issue #4: E2E Tests (#4276)

### Branch Strategy
```bash
git checkout frontend-dev && git pull
git checkout -b feature/issue-4276-playrecord-e2e
git config branch.feature/issue-4276-playrecord-e2e.parent frontend-dev
```

### Implementation Steps

#### Step 1: Setup Test Data & Mocks (1h)
**File**: `apps/web/__tests__/e2e/fixtures/bgg-mock-data.ts`

**Tasks**:
- [ ] Mock BGG search responses
- [ ] Mock rate limit headers
- [ ] Test users with different tiers
- [ ] Helper: seedPlayRecordTestData()

#### Step 2: Happy Path E2E Test (2h)
**File**: `apps/web/__tests__/e2e/play-records/play-record-bgg-integration.spec.ts`

**Test**:
```typescript
test('Create PlayRecord with BGG game search', async ({ page }) => {
  // Login as Normal user
  await login(page, normalUser);

  // Navigate to new PlayRecord
  await page.goto('/play-records/new');

  // Search for game (not in library)
  await page.fill('[data-testid="game-search"]', 'Wingspan');
  await page.waitForTimeout(500); // Debounce

  // No results → Click "Search BGG"
  await page.click('text=Search on BGG');

  // BGG dialog opens
  await expect(page.locator('[role="dialog"]')).toBeVisible();

  // Search BGG
  await page.fill('[data-testid="bgg-search-input"]', 'Wingspan');
  await page.click('[data-testid="bgg-search-submit"]');

  // Select first result
  await page.click('[data-testid="bgg-result-0"]');
  await page.click('[data-testid="add-to-library"]');

  // Verify success
  await expect(page.locator('text=Game added')).toBeVisible();

  // Verify auto-selected in wizard
  await expect(page.locator('[data-testid="selected-game"]'))
    .toContainText('Wingspan');

  // Complete wizard
  await page.click('text=Next');
  await page.fill('[data-testid="session-date"]', '2026-02-13');
  await page.click('text=Next');
  await page.click('text=Create Session');

  // Verify PlayRecord created
  await expect(page).toHaveURL(/\/play-records\/[a-f0-9-]+/);
});
```

#### Step 3: Rate Limit E2E Test (1h)
**Test**:
```typescript
test('BGG rate limit enforcement', async ({ page }) => {
  await login(page, freeUser); // 5/min quota

  await page.goto('/play-records/new');
  await page.click('text=Search on BGG');

  // Exhaust quota (5 searches)
  for (let i = 0; i < 5; i++) {
    await page.fill('[data-testid="bgg-search-input"]', `test${i}`);
    await page.click('[data-testid="bgg-search-submit"]');
    await page.waitForLoadState('networkidle');
  }

  // 6th search should fail
  await page.fill('[data-testid="bgg-search-input"]', 'test6');
  await page.click('[data-testid="bgg-search-submit"]');

  // Verify rate limit message
  await expect(page.locator('text=Rate limit reached')).toBeVisible();
  await expect(page.locator('text=5/5 searches')).toBeVisible();

  // Verify countdown timer
  await expect(page.locator('[data-testid="rate-limit-countdown"]'))
    .toBeVisible();
});
```

#### Step 4: Error Scenarios E2E (1h)
**Tests**:
- [ ] BGG API timeout → Fallback to manual
- [ ] Network error → Retry button
- [ ] Duplicate game → Use existing
- [ ] Cancel dialog → Return to wizard

#### Step 5: Visual Regression (30min)
**Tasks**:
- [ ] Screenshot: BggQuickSearchDialog (empty)
- [ ] Screenshot: BggQuickSearchDialog (results)
- [ ] Screenshot: Rate limit exhausted state
- [ ] Screenshot: Rate limit countdown

### DoD
- [ ] All 4 E2E scenarios pass
- [ ] Happy path test complete
- [ ] Rate limit test complete
- [ ] Error scenarios covered
- [ ] Visual regression baselines
- [ ] Tests run in CI/CD
- [ ] No flaky tests (3 runs green)
- [ ] PR created to `frontend-dev`
- [ ] Code review passed
- [ ] Merged to `frontend-dev`
- [ ] Issue #4276 closed on GitHub

### Testing Checklist
```bash
cd apps/web

# E2E tests
pnpm test:e2e play-records/play-record-bgg-integration

# Visual regression
pnpm test:e2e --update-snapshots  # First run
pnpm test:e2e                     # Verify no changes

# CI simulation
pnpm typecheck && pnpm lint && pnpm test && pnpm test:e2e
```

---

## 🔄 Execution Sequence

### Week 1: Foundation
**Day 1-2**: Issue #4273 (Game Search)
```bash
/implementa #4273
# → Branch created
# → GameCombobox + useGameSearch + backend endpoint
# → Tests pass
# → PR to frontend-dev
# → Merge
# → Issue closed
```

**Day 3**: Issue #4275 (Rate Limiting)
```bash
/implementa #4275
# → Branch created
# → BggRateLimitMiddleware + config + tests
# → Tests pass
# → PR to frontend-dev
# → Merge
# → Issue closed
```

### Week 2: Integration & Testing
**Day 4-6**: Issue #4274 (BGG Dialog)
```bash
/implementa #4274
# → Branch created
# → BggQuickSearchDialog + hooks + integration
# → Tests pass
# → PR to frontend-dev
# → Merge
# → Issue closed
```

**Day 7**: Issue #4276 (E2E Tests)
```bash
/implementa #4276
# → Branch created
# → E2E test suite
# → All tests green
# → PR to frontend-dev
# → Merge
# → Issue closed
```

---

## ✅ Final Validation Checklist

### After All 4 Issues Merged

**Functional Testing**:
```bash
# Start services
cd infra && docker compose --profile minimal up -d
cd ../apps/web && pnpm dev

# Test complete flow
1. Login as Normal user
2. Navigate to /play-records/new
3. Search "TestGame123" (non-existent)
4. Click "Search on BGG"
5. Search "Wingspan" on BGG
6. Add to library
7. Verify auto-selected
8. Complete PlayRecord wizard
9. Verify PlayRecord created

# Test rate limiting
1. Make 10 BGG searches rapidly
2. Verify 429 after tier limit
3. Verify countdown timer
4. Wait for reset
5. Verify quota restored
```

**Performance Testing**:
- [ ] Full flow <10s (excluding BGG API)
- [ ] Autocomplete search <300ms
- [ ] BGG search <3s
- [ ] Add to library <2s

**Code Quality**:
```bash
# Frontend
cd apps/web
pnpm typecheck  # 0 errors
pnpm lint       # 0 warnings
pnpm test       # All pass
pnpm test:coverage  # ≥85%

# Backend
cd apps/api/src/Api
dotnet build    # 0 errors
dotnet test     # All pass
dotnet test /p:CollectCoverage=true  # ≥90%
```

**Documentation**:
- [ ] Update `docs/11-user-flows/user-role/05-game-sessions.md`
- [ ] Update `docs/03-api/bgg-api-integration.md`
- [ ] Add screenshots to docs
- [ ] Update CHANGELOG.md

---

## 📊 Success Metrics

**Feature Completeness**:
- ✅ Seamless PlayRecord creation flow
- ✅ BGG search integrated inline
- ✅ Rate limiting prevents abuse
- ✅ E2E test coverage complete

**Performance**:
- ✅ Search autocomplete: <300ms
- ✅ BGG search: <3s (mocked in tests)
- ✅ Add to library: <2s
- ✅ Full flow: <10s

**Quality**:
- ✅ Frontend coverage: ≥85%
- ✅ Backend coverage: ≥90%
- ✅ 0 TypeScript errors
- ✅ 0 ESLint warnings
- ✅ E2E tests green

**UX**:
- ✅ No page reloads
- ✅ Clear rate limit feedback
- ✅ Graceful error handling
- ✅ Accessible (keyboard navigation)

---

## 🚀 Execution Commands

### Issue #4273 (Game Search)
```bash
/implementa https://github.com/DegrassiAaron/meepleai-monorepo/issues/4273
```

### Issue #4275 (Rate Limiting)
```bash
/implementa https://github.com/DegrassiAaron/meepleai-monorepo/issues/4275
```

### Issue #4274 (BGG Dialog)
```bash
/implementa https://github.com/DegrassiAaron/meepleai-monorepo/issues/4274
```

### Issue #4276 (E2E Tests)
```bash
/implementa https://github.com/DegrassiAaron/meepleai-monorepo/issues/4276
```

---

**Next Step**: Esegui `/implementa https://github.com/DegrassiAaron/meepleai-monorepo/issues/4273`
