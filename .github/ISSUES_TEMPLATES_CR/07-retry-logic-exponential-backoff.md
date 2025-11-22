# 🔄 [Resilience] Retry Logic with Exponential Backoff

**Priority**: 🟢 MEDIUM
**Complexity**: Medium
**Estimated Time**: 6-8 hours
**Dependencies**: None

## 🎯 Objective

Implement automatic retry logic with exponential backoff in HttpClient for transient server errors (500, 502, 503) to improve application resilience.

## 📋 Context

**Source**: Code Review Backend-Frontend Interactions
**Issue**: No retry mechanism for transient failures
**Impact**: Medium - Improves reliability, reduces user-facing errors
**Current State**: Single attempt, immediate failure on 5xx errors

## 🔧 Retry Strategy

### Retryable Status Codes
- `500` Internal Server Error
- `502` Bad Gateway
- `503` Service Unavailable
- Network errors (fetch failures)

### Non-Retryable
- `4xx` Client errors (user error, don't retry)
- `429` Rate Limit (handled separately with Retry-After)
- `401/403` Auth errors (redirect to login)

### Exponential Backoff
```
Attempt 1: Immediate
Attempt 2: Wait 1s  (2^0 * 1000ms)
Attempt 3: Wait 2s  (2^1 * 1000ms)
Attempt 4: Wait 4s  (2^2 * 1000ms)
Max: 3 retries
```

## ✅ Task Checklist

### Implementation
- [ ] Create `RetryPolicy` class with configuration
- [ ] Implement `fetchWithRetry()` in HttpClient
- [ ] Add exponential backoff calculation
- [ ] Add jitter to prevent thundering herd
- [ ] Configure max retries (default: 3)
- [ ] Configure base delay (default: 1000ms)
- [ ] Configure max delay (default: 10000ms)

### Integration
- [ ] Integrate `fetchWithRetry` in all HTTP methods (GET, POST, PUT, DELETE)
- [ ] Preserve existing error handling
- [ ] Log retry attempts with correlation ID
- [ ] Add retry metrics (Prometheus counters)

### Configuration
- [ ] Add retry config to environment variables
- [ ] Make retries opt-out per request (via RequestOptions)
- [ ] Configure retryable status codes list
- [ ] Add circuit breaker threshold (optional)

### Testing
- [ ] Unit tests for exponential backoff calculation
- [ ] Test retryable vs non-retryable errors
- [ ] Test max retries limit
- [ ] Test jitter randomization
- [ ] Integration tests with mock server returning 500/502/503
- [ ] Test network error retry
- [ ] Test that 4xx errors don't retry

### Observability
- [ ] Log retry attempts with structured logging
- [ ] Add Prometheus metrics:
  - `http_client_retries_total{status_code, attempt}`
  - `http_client_retry_delay_seconds{attempt}`
- [ ] Include retry attempt in correlation ID
- [ ] Add retry context to error logs

### Documentation
- [ ] Create `docs/04-frontend/retry-policy.md`
- [ ] Document configuration options
- [ ] Add troubleshooting guide
- [ ] Explain when retries happen
- [ ] Document how to disable retries per request

## 📁 Files to Create/Modify

```
apps/web/src/lib/api/core/retryPolicy.ts (NEW)
apps/web/src/lib/api/core/httpClient.ts (MODIFY)
apps/web/src/lib/api/core/metrics.ts (NEW - Prometheus client metrics)
apps/web/src/lib/api/__tests__/retryPolicy.test.ts (NEW)
apps/web/src/lib/api/__tests__/httpClient.test.ts (MODIFY)
docs/04-frontend/retry-policy.md (NEW)
apps/web/.env.example (MODIFY - add retry config)
```

## 💡 Implementation Example

```typescript
// retryPolicy.ts
export class RetryPolicy {
  constructor(
    private maxRetries = 3,
    private baseDelay = 1000,
    private maxDelay = 10000
  ) {}

  shouldRetry(statusCode: number, attempt: number): boolean {
    if (attempt >= this.maxRetries) return false;
    return [500, 502, 503].includes(statusCode);
  }

  getDelay(attempt: number): number {
    const exponential = Math.pow(2, attempt) * this.baseDelay;
    const jitter = Math.random() * 0.3 * exponential; // 30% jitter
    return Math.min(exponential + jitter, this.maxDelay);
  }
}

// httpClient.ts
private async fetchWithRetry<T>(
  url: string,
  options: RequestInit,
  retries = 3
): Promise<Response> {
  for (let attempt = 0; attempt < retries; attempt++) {
    try {
      const response = await this.fetchImpl(url, options);

      if (this.retryPolicy.shouldRetry(response.status, attempt)) {
        const delay = this.retryPolicy.getDelay(attempt);
        logger.warn(`Retry attempt ${attempt + 1}/${retries} after ${delay}ms`);
        await this.sleep(delay);
        continue;
      }

      return response;
    } catch (error) {
      if (attempt === retries - 1) throw error;

      const delay = this.retryPolicy.getDelay(attempt);
      await this.sleep(delay);
    }
  }

  throw new NetworkError({ message: 'Max retries exceeded' });
}
```

## 🔗 References

- [Exponential Backoff Algorithm](https://en.wikipedia.org/wiki/Exponential_backoff)
- [AWS Architecture Blog - Exponential Backoff and Jitter](https://aws.amazon.com/blogs/architecture/exponential-backoff-and-jitter/)
- [Polly .NET Resilience Library](https://github.com/App-vNext/Polly) (inspiration)

## 📊 Acceptance Criteria

- ✅ Automatic retry for 500/502/503 errors
- ✅ Exponential backoff with jitter
- ✅ Max 3 retries (configurable)
- ✅ 4xx errors don't retry
- ✅ Network errors retry
- ✅ Retry attempts logged
- ✅ Prometheus metrics for retries
- ✅ Test coverage >= 95%
- ✅ Documentation complete
- ✅ Opt-out mechanism per request

## ⚙️ Configuration

```env
NEXT_PUBLIC_RETRY_MAX_ATTEMPTS=3
NEXT_PUBLIC_RETRY_BASE_DELAY=1000
NEXT_PUBLIC_RETRY_MAX_DELAY=10000
NEXT_PUBLIC_RETRY_ENABLED=true
```

## 🏷️ Labels

`priority: medium`, `type: enhancement`, `area: frontend`, `effort: medium`, `resilience`, `sprint: 5`
