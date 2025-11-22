# ⏱️ [UX] Rate Limiting User Experience with Retry-After

**Priority**: 🟢 MEDIUM
**Complexity**: Low
**Estimated Time**: 4-6 hours
**Dependencies**: None

## 🎯 Objective

Improve user experience when rate limits are hit by displaying user-friendly messages with countdown timer based on `Retry-After` header.

## 📋 Context

**Source**: Code Review Backend-Frontend Interactions
**Issue**: Backend returns 429 with `Retry-After` header, but frontend shows generic error
**Impact**: Medium - Better UX, reduces user frustration
**Current State**: `RateLimitError` exists but no UI feedback

## 🔧 Current vs Proposed Behavior

### Current ❌
```
User triggers rate limit → Generic error toast → User confused, keeps retrying
```

### Proposed ✅
```
User triggers rate limit →
  "Too many requests. Please wait 45 seconds" + countdown →
  Button auto-re-enables after countdown →
  User informed and patient
```

## ✅ Task Checklist

### Backend (Verify Existing)
- [ ] Verify `RateLimitingMiddleware` returns `Retry-After` header
- [ ] Test header format (seconds vs HTTP-date)
- [ ] Ensure consistent across all rate-limited endpoints

### Frontend Error Handling
- [ ] Extend `RateLimitError` with `getRetryMessage()` method
- [ ] Parse `Retry-After` header (support seconds and HTTP-date formats)
- [ ] Add `getRemainingSeconds()` helper method

### React Hook
- [ ] Create `useRateLimitHandler(error: ApiError)` hook
- [ ] State: `isRateLimited`, `remainingSeconds`, `canRetry`
- [ ] Countdown timer with setInterval
- [ ] Auto-reset when countdown reaches 0
- [ ] Cleanup on unmount

### UI Components
- [ ] Create `<RateLimitBanner>` component
  - Display countdown timer
  - Show friendly message
  - Auto-dismiss when timer ends
  - Toast variant for inline errors
- [ ] Create `<RateLimitedButton>` wrapper
  - Disables button during rate limit
  - Shows countdown in button text
  - Auto-re-enables when allowed

### Integration
- [ ] Integrate `useRateLimitHandler` in API client error handling
- [ ] Add `RateLimitBanner` to global error boundary
- [ ] Wrap submit buttons with `RateLimitedButton`
- [ ] Test with real rate limit scenarios

### Testing
- [ ] Unit tests for `getRetryMessage()` and parsing logic
- [ ] Hook tests for countdown behavior
- [ ] Component tests for UI rendering
- [ ] Integration test: trigger rate limit, verify countdown
- [ ] Test HTTP-date and seconds format parsing
- [ ] Test auto-re-enable after countdown

### Documentation
- [ ] Document rate limit UX patterns
- [ ] Add examples to component docs
- [ ] Update error handling guide

## 📁 Files to Create/Modify

```
apps/web/src/lib/api/core/errors.ts (MODIFY)
└── Add getRetryMessage(), getRemainingSeconds()

apps/web/src/lib/hooks/useRateLimitHandler.ts (NEW)
apps/web/src/components/common/RateLimitBanner.tsx (NEW)
apps/web/src/components/common/RateLimitedButton.tsx (NEW)

apps/web/src/lib/hooks/__tests__/useRateLimitHandler.test.ts (NEW)
apps/web/src/components/common/__tests__/RateLimitBanner.test.tsx (NEW)
apps/web/src/components/common/__tests__/RateLimitedButton.test.tsx (NEW)

docs/04-frontend/error-handling.md (UPDATE)
```

## 💡 Implementation Examples

### Error Extension
```typescript
export class RateLimitError extends ApiError {
  public readonly retryAfter?: number;

  public getRetryMessage(): string {
    if (this.retryAfter) {
      const seconds = this.retryAfter;
      return `Too many requests. Please wait ${seconds} seconds.`;
    }
    return 'Too many requests. Please try again later.';
  }

  public getRemainingSeconds(): number {
    return this.retryAfter || 0;
  }
}
```

### Hook Usage
```typescript
const { isRateLimited, remainingSeconds, canRetry } = useRateLimitHandler(error);

if (isRateLimited) {
  return <RateLimitBanner remainingSeconds={remainingSeconds} />;
}
```

### Button Usage
```typescript
<RateLimitedButton
  onClick={handleSubmit}
  isLoading={isLoading}
  rateLimitError={error}
>
  Submit
</RateLimitedButton>
```

## 🔗 References

- [MDN Retry-After Header](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Retry-After)
- [RFC 7231 - Retry-After](https://httpwg.org/specs/rfc7231.html#header.retry-after)
- [React useInterval Hook](https://overreacted.io/making-setinterval-declarative-with-react-hooks/)

## 📊 Acceptance Criteria

- ✅ Retry-After header parsed correctly (seconds + HTTP-date)
- ✅ Countdown timer displays remaining seconds
- ✅ UI automatically re-enables when timer expires
- ✅ User-friendly error messages
- ✅ Test coverage >= 90%
- ✅ Works across all rate-limited endpoints
- ✅ Accessible (screen reader friendly)

## 🎨 Design Mockup

```
┌─────────────────────────────────────────┐
│ ⚠️ Rate Limit Exceeded                  │
│                                         │
│ Too many requests. Please wait 45s     │
│                                         │
│ [████████░░░░░░░░░░] 45 seconds        │
└─────────────────────────────────────────┘
```

## 🏷️ Labels

`priority: medium`, `type: enhancement`, `area: frontend`, `effort: small`, `ux`, `sprint: 4`
