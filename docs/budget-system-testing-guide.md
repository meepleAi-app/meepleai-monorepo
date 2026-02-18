# Budget Display System - Testing Guide

**PR**: https://github.com/DegrassiAaron/meepleai-monorepo/pull/4665
**Branch**: `feature/budget-display-system`
**Status**: ✅ Code complete, ready for testing

---

## Quick Start Testing

### Prerequisites

1. **Start Docker Desktop**
2. Ensure database credentials in `infra/secrets/database.secret`

### Start System

```bash
# 1. Start infrastructure
cd infra
docker compose up -d postgres redis qdrant

# 2. Start API (Terminal 1)
cd ../apps/api/src/Api
dotnet run
# Wait for: "Now listening on: http://127.0.0.1:8080"

# 3. Start Frontend (Terminal 2)
cd ../../../../apps/web
pnpm dev
# Wait for: "Ready on http://localhost:3000"
```

---

## Test Scenarios

### 1. User Budget Display

**Test**: Budget badge appears in chat header

```bash
# 1. Navigate to http://localhost:3000/chat
# 2. Look for badge in header: "💳 50 credits"
# 3. Hover over badge - tooltip should show:
#    - Daily: 50 / 100
#    - Weekly: 9,800 / 10,000
#    - Resets in: XX hours
```

**Expected Behavior**:
- Badge visible and clickable
- Color: Green (>50% remaining)
- Tooltip shows detailed breakdown
- Auto-refreshes every 5 minutes

### 2. Budget Dashboard

**Test**: Full dashboard with usage charts

```bash
# Navigate to http://localhost:3000/dashboard/budget
```

**Expected Content**:
- Overview cards (daily/weekly credits)
- Progress bars with percentages
- Usage breakdown section
- Tier upgrade CTA section
- Reset timers

### 3. Admin Budget Panel

**Test**: Admin monitoring in playground (admin users only)

```bash
# Navigate to http://localhost:3000/admin/playground
# Look for "Budget Overview (Admin)" panel
```

**Expected Content**:
- OpenRouter balance in € (may be €0.00 if not configured)
- Daily budget: $X / $50.00 (X%)
- Weekly budget: $Y / $200.00 (Y%)
- Progress bars
- Critical alerts if >95%

### 4. API Endpoint Testing

**Test**: User budget endpoint

```bash
# Get test user ID from database
docker exec -i meepleai-postgres psql -U postgres -d meepleai -c \
  "SELECT user_id FROM user_token_usage LIMIT 1;"

# Test endpoint (requires authentication)
# Login first to get session cookie:
curl -c cookies.txt -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"password"}'

# Get budget status
curl -b cookies.txt http://localhost:8080/api/v1/budget/user/{userId} | jq

# Expected response:
{
  "creditsRemaining": 50,
  "dailyLimit": 100,
  "weeklyLimit": 10000,
  "dailyResetIn": "11h 24m",
  "weeklyResetIn": "6d 11h",
  "isBlocked": false,
  "dailyCreditsUsed": 50,
  "weeklyCreditsUsed": 200
}
```

### 5. Budget Exhaustion & Fallback

**Test**: Verify free model fallback when budget exhausted

```bash
# Step 1: Exhaust user's daily budget
docker exec -i meepleai-postgres psql -U postgres -d meepleai -c \
  "UPDATE user_token_usage SET daily_credits_used = 100 WHERE user_id = '{userId}';"

# Step 2: Send a chat message
# Navigate to http://localhost:3000/chat
# Send a question to any agent

# Step 3: Check API logs for fallback message
docker logs meepleai-api --tail=50 | grep -i "budget exhausted"

# Expected log:
# "User {userId} budget exhausted, using fallback model: claude-3.5-sonnet → llama-3.2-3b-instruct:free"

# Step 4: Verify user receives notification
# Chat should show: "Budget low - using free model (llama-3.2-3b-instruct:free)"
```

### 6. Credit Deduction

**Test**: Credits decrease after LLM requests

```bash
# Step 1: Check initial credits
curl -b cookies.txt http://localhost:8080/api/v1/budget/user/{userId} | jq .creditsRemaining

# Step 2: Send a chat message (costs ~1-5 credits typically)
# Use chat UI or API endpoint

# Step 3: Check updated credits
curl -b cookies.txt http://localhost:8080/api/v1/budget/user/{userId} | jq .creditsRemaining

# Expected: creditsRemaining decreased by 1-5
```

### 7. Admin Budget Overview

**Test**: Admin can see system-wide budget

```bash
# Login as admin
curl -c admin-cookies.txt -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@meepleai.dev","password":"admin-password"}'

# Get admin overview
curl -b admin-cookies.txt http://localhost:8080/api/v1/admin/budget/overview | jq

# Expected response:
{
  "openRouterBalanceEuros": 45.50,
  "dailySpendUsd": 0.02,
  "weeklySpendUsd": 0.08,
  "dailyBudgetUsd": 50.00,
  "weeklyBudgetUsd": 200.00,
  "dailyUsagePercent": 0,
  "weeklyUsagePercent": 0
}
```

---

## Database Verification

### Check Schema

```bash
# Verify credit columns exist
docker exec -i meepleai-postgres psql -U postgres -d meepleai -c '\d user_token_usage'

# Should show:
# - daily_credits_used (numeric 18,2)
# - weekly_credits_used (numeric 18,2)
# - last_daily_reset (timestamp with time zone)
# - last_weekly_reset (timestamp with time zone)

# Verify tier limits
docker exec -i meepleai-postgres psql -U postgres -d meepleai -c '\d token_tiers'

# Should show:
# - daily_credits_limit (numeric 18,2)
# - weekly_credits_limit (numeric 18,2)
```

### Check Data

```bash
# View tier credit limits
docker exec -i meepleai-postgres psql -U postgres -d meepleai -c \
  "SELECT name, daily_credits_limit, weekly_credits_limit FROM token_tiers ORDER BY name;"

# View user usage
docker exec -i meepleai-postgres psql -U postgres -d meepleai -c \
  "SELECT user_id, daily_credits_used, weekly_credits_used, last_daily_reset
   FROM user_token_usage LIMIT 5;"
```

---

## Performance Testing

### Cache Behavior

```bash
# Test 1: First request (cache miss)
time curl -b cookies.txt http://localhost:8080/api/v1/budget/user/{userId}
# Expected: ~50-100ms (database query)

# Test 2: Second request (cache hit)
time curl -b cookies.txt http://localhost:8080/api/v1/budget/user/{userId}
# Expected: <10ms (from HybridCache)

# Test 3: After 5 minutes (cache expired)
sleep 300
time curl -b cookies.txt http://localhost:8080/api/v1/budget/user/{userId}
# Expected: ~50-100ms (cache refresh)
```

### Load Testing

```bash
# Concurrent budget checks (verify no race conditions)
for i in {1..10}; do
  curl -b cookies.txt http://localhost:8080/api/v1/budget/user/{userId} &
done
wait

# All requests should return consistent data
```

---

## Edge Cases to Test

### 1. Midnight Reset (Daily)

```bash
# Set reset time to 1 minute ago
docker exec -i meepleai-postgres psql -U postgres -d meepleai -c \
  "UPDATE user_token_usage
   SET last_daily_reset = NOW() - INTERVAL '25 hours',
       daily_credits_used = 80
   WHERE user_id = '{userId}';"

# Send a request (should trigger auto-reset)
# Check that daily_credits_used resets to just the new request cost
```

### 2. Monday Reset (Weekly)

```bash
# Set weekly reset to last week
docker exec -i meepleai-postgres psql -U postgres -d meepleai -c \
  "UPDATE user_token_usage
   SET last_weekly_reset = NOW() - INTERVAL '8 days',
       weekly_credits_used = 8000
   WHERE user_id = '{userId}';"

# Send a request (should trigger auto-reset)
```

### 3. Tier Change

```bash
# Upgrade user from Free to Pro
# Daily limit should change: 100 → 5,000 credits

# Check before
curl -b cookies.txt http://localhost:8080/api/v1/budget/user/{userId} | jq .dailyLimit
# Expected: 100

# Change tier (admin action)
# Then check after
curl -b cookies.txt http://localhost:8080/api/v1/budget/user/{userId} | jq .dailyLimit
# Expected: 5000
```

### 4. Budget Service Failure (Fail-Open)

```bash
# Simulate service failure by stopping database temporarily
docker compose stop postgres

# Send chat message
# Expected: Request succeeds (fail-open), uses requested model

# Check logs
docker logs meepleai-api | grep "Budget check failed"
# Expected: "Budget check failed for user {userId}, assuming budget available"
```

---

## UI Component Testing (Playwright)

### Budget Badge Test

```typescript
// apps/web/e2e/budget-badge.spec.ts
test('budget badge displays credits', async ({ page }) => {
  await page.goto('/chat');

  // Verify badge visible
  const badge = page.locator('[data-testid="budget-badge"]');
  await expect(badge).toBeVisible();
  await expect(badge).toContainText(/\d+ credits/);

  // Verify tooltip shows on hover
  await badge.hover();
  await expect(page.locator('text=/Daily:/i')).toBeVisible();
  await expect(page.locator('text=/Weekly:/i')).toBeVisible();
  await expect(page.locator('text=/Resets in:/i')).toBeVisible();
});
```

### Budget Dashboard Test

```typescript
// apps/web/e2e/budget-dashboard.spec.ts
test('budget dashboard loads correctly', async ({ page }) => {
  await page.goto('/dashboard/budget');

  // Verify overview cards
  await expect(page.locator('text=/Daily Credits/i')).toBeVisible();
  await expect(page.locator('text=/Weekly Credits/i')).toBeVisible();

  // Verify progress bars
  const progressBars = page.locator('[role="progressbar"]');
  await expect(progressBars).toHaveCount(2);

  // Verify tier upgrade section
  await expect(page.locator('text=/Need More Credits/i')).toBeVisible();
});
```

---

## Troubleshooting

### Budget Endpoint Returns 404

**Issue**: Route not registered
**Fix**: Verify `v1Api.MapBudgetEndpoints()` in `Program.cs` line 542
**Test**: Check Scalar docs at http://localhost:8080/scalar/v1

### Budget Badge Not Visible

**Issue**: User ID not found or service error
**Check**:
1. Browser console for errors
2. Network tab for API call status
3. User has `UserTokenUsage` record in database

### Credits Not Deducting

**Issue**: `UserBudgetService` not injected or error
**Check**:
1. API logs for "Recorded X credits" messages
2. Database: `SELECT * FROM user_token_usage WHERE user_id = '{id}'`
3. Verify `IUserBudgetService` registered in DI

### Free Model Not Used When Budget Low

**Issue**: Budget check not running or fallback logic error
**Check**:
1. API logs for "budget exhausted, using fallback"
2. Verify budget actually exhausted: `daily_credits_used >= daily_credits_limit`
3. Check `ILlmModelOverrideService` is injected

---

## Success Criteria

### ✅ All Green When:

- [ ] Budget badge visible in chat header
- [ ] Dashboard loads at `/dashboard/budget`
- [ ] Admin panel shows OpenRouter balance
- [ ] Credits decrease after LLM requests
- [ ] Budget exhaustion triggers free model fallback
- [ ] Daily reset works at midnight UTC
- [ ] Weekly reset works Monday 00:00 UTC
- [ ] Cache reduces database queries (verified in logs)
- [ ] No errors in browser console
- [ ] No errors in API logs (except expected auth failures)

---

## Rollback Plan

If issues arise after deployment:

```bash
# Revert PR merge
git revert <merge-commit-sha>

# Or rollback database migration
cd apps/api/src/Api
dotnet ef database update <PreviousMigration>
```

---

## Monitoring in Production

### Metrics to Watch

1. **Budget API Latency**: Should be <50ms (cached), <100ms (DB)
2. **Cache Hit Rate**: Should be >90% for budget queries
3. **Fallback Frequency**: How often free models used
4. **OpenRouter Balance**: Monitor daily to prevent exhaustion

### Log Messages to Monitor

```
✅ "Recorded X credits for user {userId}"
⚠️ "User {userId} budget exhausted, using fallback model"
🔍 "Budget check failed for user {userId}, assuming budget available"
```

---

## Additional Testing (Optional)

### Stress Test

```bash
# 100 concurrent requests
ab -n 100 -c 10 -C "session=..." \
  http://localhost:8080/api/v1/budget/user/{userId}

# Verify:
# - All requests succeed
# - No race conditions
# - Cache works correctly
```

### Model Fallback Coverage

Test all model mappings work:
- GPT-4 → Gemma Free
- Claude → Llama Free
- DeepSeek → Llama 3.3 Free
- Gemini → Gemma Free

---

**Testing Ready**: Once Docker Desktop is started
**Documentation**: Complete
**Support**: See `IMPLEMENTATION_STATUS.md` for troubleshooting
