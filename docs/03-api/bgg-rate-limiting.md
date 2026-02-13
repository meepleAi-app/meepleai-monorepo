# BGG API Rate Limiting

**Issue**: #4275 - BGG Rate Limiting with User Quota Tracking
**Implementation**: Tier-based request limits to prevent API abuse

---

## Overview

BGG (BoardGameGeek) API endpoints are rate-limited per user tier to:
1. Prevent individual users from exhausting shared IP quota
2. Distribute BGG API capacity fairly across user base
3. Provide clear feedback on remaining quota

---

## Rate Limits by Tier

| Tier | Requests/Minute | Use Case |
|------|-----------------|----------|
| **Free** | 5 | Casual users, basic searches |
| **Normal** | 10 | Regular users, moderate activity |
| **Premium** | 20 | Power users, frequent searches |
| **Editor** | 15 | Content editors, moderate bulk operations |
| **Admin** | Unlimited | Administrators (bypassed) |

**Window**: 60 seconds (sliding window)
**Algorithm**: Token bucket via Redis

---

## Affected Endpoints

All endpoints under `/api/v1/bgg/*` are rate-limited:

- `GET /api/v1/bgg/search?query={term}` - Search BGG catalog
- `GET /api/v1/bgg/games/{bggId}` - Get game details
- `GET /api/v1/bgg/thumbnails` - Batch thumbnail fetch

**Note**: Admin-only endpoints (`/api/v1/admin/bgg-queue/*`) are NOT rate-limited.

---

## Response Headers

### Success Response (200 OK)
```http
X-RateLimit-Limit: 10
X-RateLimit-Remaining: 7
X-RateLimit-Reset: 1707840060
```

**Headers**:
- `X-RateLimit-Limit`: Maximum requests allowed in window
- `X-RateLimit-Remaining`: Requests remaining in current window
- `X-RateLimit-Reset`: Unix timestamp when limit resets

### Rate Limited Response (429 Too Many Requests)
```http
HTTP/1.1 429 Too Many Requests
X-RateLimit-Limit: 10
X-RateLimit-Remaining: 0
X-RateLimit-Reset: 1707840060
Retry-After: 45

{
  "error": "Rate limit exceeded",
  "message": "BGG API limit exceeded. Maximum 10 requests per minute for Normal tier.",
  "retryAfter": 45,
  "limit": 10,
  "remaining": 0,
  "reset": 1707840060
}
```

**Headers**:
- `Retry-After`: Seconds to wait before retrying
- Other headers same as success response

---

## Client Implementation

### Checking Rate Limit Status

**Before Request**:
```typescript
// Frontend: Check headers from previous request
const headers = response.headers;
const remaining = parseInt(headers.get('X-RateLimit-Remaining') || '0');
const reset = parseInt(headers.get('X-RateLimit-Reset') || '0');

if (remaining === 0) {
  const resetDate = new Date(reset * 1000);
  const waitSeconds = Math.ceil((resetDate.getTime() - Date.now()) / 1000);
  console.log(`Rate limit reached. Resets in ${waitSeconds}s`);
}
```

### Handling 429 Responses

**Automatic Retry** (with exponential backoff):
```typescript
async function bggSearchWithRetry(query: string, maxRetries = 3) {
  for (let i = 0; i < maxRetries; i++) {
    const response = await fetch(`/api/v1/bgg/search?query=${query}`);

    if (response.status === 429) {
      const retryAfter = parseInt(response.headers.get('Retry-After') || '60');
      console.log(`Rate limited. Waiting ${retryAfter}s...`);
      await new Promise(resolve => setTimeout(resolve, retryAfter * 1000));
      continue; // Retry
    }

    return response;
  }

  throw new Error('Max retries exceeded');
}
```

### UI Feedback

**Rate Limit Indicator** (recommended):
```typescript
function RateLimitIndicator({ headers }: { headers: Headers }) {
  const limit = parseInt(headers.get('X-RateLimit-Limit') || '0');
  const remaining = parseInt(headers.get('X-RateLimit-Remaining') || '0');
  const reset = parseInt(headers.get('X-RateLimit-Reset') || '0');

  if (limit === 0) return null; // No rate limit info

  const percentRemaining = (remaining / limit) * 100;
  const resetDate = new Date(reset * 1000);
  const waitSeconds = Math.ceil((resetDate.getTime() - Date.now()) / 1000);

  return (
    <div className={getColorClass(percentRemaining)}>
      🔍 {remaining}/{limit} searches
      {remaining === 0 && ` (resets in ${waitSeconds}s)`}
    </div>
  );
}

function getColorClass(percent: number): string {
  if (percent > 50) return 'text-green-600';
  if (percent > 20) return 'text-yellow-600';
  return 'text-red-600';
}
```

---

## Configuration

### appsettings.json

```json
{
  "BggRateLimit": {
    "FreeTier": 5,
    "NormalTier": 10,
    "PremiumTier": 20,
    "EditorTier": 15,
    "WindowSeconds": 60,
    "AdminBypass": true,
    "EnableMetrics": true
  }
}
```

### Environment-Specific Overrides

**appsettings.Testing.json** (disable for CI):
```json
{
  "BggRateLimit": {
    "FreeTier": 1000,
    "NormalTier": 1000,
    "WindowSeconds": 1
  }
}
```

**appsettings.Production.json** (stricter limits):
```json
{
  "BggRateLimit": {
    "FreeTier": 3,
    "NormalTier": 8,
    "PremiumTier": 15
  }
}
```

---

## Monitoring

### Metrics (Prometheus)

**Counters**:
- `bgg_api_requests_total{tier="Normal",status="success"}` - Total requests by tier
- `bgg_api_rate_limited_total{tier="Free"}` - 429 responses by tier

**Gauges**:
- `bgg_api_quota_remaining{user_id="..."}` - Current quota for active users

**Histograms**:
- `bgg_api_request_duration_seconds` - Request latency

### Grafana Dashboard

**Panels**:
1. **BGG API Usage by Tier** (line chart)
2. **Rate Limit Violations** (bar chart by tier)
3. **Top Users by BGG Requests** (table)
4. **Quota Exhaustion Events** (timeline)

### Alerts

**Rate Limit Abuse**:
```
Trigger: User exceeds quota 3x in 1 hour
Action: Log warning, notify admin
Severity: Medium
```

**BGG API Overuse**:
```
Trigger: >80% of users hitting rate limits
Action: Consider increasing tier limits or caching
Severity: High
```

---

## Resilience

### Fail-Open Strategy

**Redis Unavailable**:
- Request is ALLOWED (fail-open)
- Error logged with severity WARNING
- Header added: `X-RateLimit-Status: Error`
- Prevents Redis outage from blocking BGG access

### Error Handling

**Middleware Exception**:
```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "BGG rate limit check failed. Failing open.");
    context.Response.Headers["X-RateLimit-Status"] = "Error";
    await _next(context); // Allow request
}
```

---

## Testing

### Manual Testing

**Test Normal Tier** (10 req/min):
```bash
TOKEN="your_auth_token"

# Make 11 requests in 60 seconds
for i in {1..11}; do
  curl -H "Authorization: Bearer $TOKEN" \
    "http://localhost:8080/api/v1/bgg/search?query=test$i" \
    -i | grep -E "HTTP|X-RateLimit"
  sleep 5
done

# Expected:
# - Requests 1-10: 200 OK
# - Request 11: 429 Too Many Requests
```

### Unit Tests

**Coverage**: `apps/api/tests/Api.Tests/Middleware/BggRateLimitMiddlewareTests.cs`
- 12 test cases covering all tiers, bypass, errors, headers

### Integration Tests

**Coverage**: `apps/api/tests/Api.Tests/Integration/BggRateLimitIntegrationTests.cs`
- 7 test cases with real Redis and Testcontainers

---

## Best Practices

### For Developers

**✅ DO**:
- Cache BGG responses (24h) to reduce API calls
- Show quota in UI before user hits limit
- Implement retry logic with Retry-After header
- Log rate limit events for monitoring

**❌ AVOID**:
- Polling BGG API without caching
- Ignoring 429 responses
- Making BGG requests without authentication
- Bypassing rate limits in client code

### For Users

**Optimize BGG Usage**:
- Search in personal library first (autocomplete)
- Cache favorite games locally
- Upgrade to Premium tier for higher limits
- Use BGG sparingly during peak hours

---

## Troubleshooting

### Issue: "Rate limit exceeded" but user hasn't made requests

**Causes**:
- Redis key not expiring properly
- Clock skew in distributed environment
- Multiple sessions for same user

**Solutions**:
1. Check Redis TTL: `TTL bgg:ratelimit:{userId}`
2. Verify Redis time vs server time
3. Clear Redis key manually if needed

### Issue: Admin still getting rate limited

**Causes**:
- AdminBypass set to false in configuration
- User role not parsed correctly
- Middleware order issue (must be after authentication)

**Solutions**:
1. Check `BggRateLimit:AdminBypass` in appsettings.json
2. Verify user has "Admin" role in token claims
3. Ensure middleware is registered after `UseAuthentication()`

### Issue: All requests fail with 429

**Causes**:
- Redis counter stuck at high value
- Configuration limits set too low
- System time incorrect

**Solutions**:
1. Flush Redis keys: `KEYS bgg:ratelimit:*` → `DEL`
2. Check configuration values in appsettings
3. Verify server time is accurate

---

## Related

- **Issue #4275**: BGG Rate Limiting (backend)
- **Issue #4274**: BGG Search Dialog (frontend UI)
- **Issue #4276**: E2E Tests (testing)
- **Epic #3887**: Play Records
- **Redis**: Infrastructure service (docker-compose.yml)

---

**Last Updated**: 2026-02-13
**Implemented By**: BggRateLimitMiddleware.cs
**Test Coverage**: ≥90% (unit + integration)
