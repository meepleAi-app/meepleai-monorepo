# Frontend Error Handling

**Status**: Active
**Last Updated**: 2025-11-21
**Owner**: Frontend Team

## Overview

This document describes the frontend error handling strategy, including specialized handling for API rate limiting with user-friendly countdown timers.

## API Error Classes

All API errors extend from the base `ApiError` class and include correlation IDs for distributed tracing.

### Base Classes

```typescript
// Base error with correlation ID support
class ApiError extends Error {
  statusCode?: number;
  correlationId?: string;
  response?: Response;
  endpoint?: string;
  timestamp: string;
}
```

### Specialized Error Types

| Error Class | Status Code | Use Case |
|-------------|-------------|----------|
| `UnauthorizedError` | 401 | Authentication required or session expired |
| `ForbiddenError` | 403 | Authenticated but lacks permission |
| `NotFoundError` | 404 | Resource does not exist |
| `ValidationError` | 422 | Request validation failed |
| `RateLimitError` | 429 | Too many requests (see below) |
| `ServerError` | 500/502/503 | Backend internal error |
| `NetworkError` | 0 | Request failed before reaching server |

## Rate Limiting UX

### Overview

Rate limiting errors (HTTP 429) receive special UX treatment with:
- User-friendly countdown timers
- Automatic UI re-enablement when limit expires
- Accessible messages for screen readers
- Visual feedback during wait periods

### RateLimitError Class

Enhanced with user-friendly methods for displaying countdown information:

```typescript
class RateLimitError extends ApiError {
  retryAfter?: number; // Seconds until retry is allowed

  // Get retry delay in seconds
  getRetryAfterSeconds(): number;

  // Get timestamp when retry is allowed
  getRetryAfterDate(): Date;

  // Get user-friendly message with countdown
  getUserFriendlyMessage(remainingSeconds?: number): string;

  // Check if retry is currently allowed
  canRetryNow(): boolean;

  // Get remaining seconds until retry
  getRemainingSeconds(): number;

  // Parse Retry-After header (supports seconds and HTTP-date formats)
  static parseRetryAfter(value: string | null): number | undefined;
}
```

**Example Messages**:
- 1 second: "Too many requests. Please wait 1 second."
- 45 seconds: "Too many requests. Please wait 45 seconds."
- 60 seconds: "Too many requests. Please wait 1 minute."
- 150 seconds: "Too many requests. Please wait 3 minutes."
- 0 seconds: "You can now retry your request."

### useRateLimitHandler Hook

React hook for managing rate limit state with automatic countdown:

```typescript
const {
  isRateLimited,      // Whether rate limit is active
  remainingSeconds,   // Countdown value
  message,            // User-friendly message
  handleError,        // Call when RateLimitError occurs
  reset,              // Manually reset state
  error               // Original RateLimitError
} = useRateLimitHandler();
```

**Features**:
- Automatic 1-second countdown timer
- Auto-resets when countdown reaches 0
- Cleans up timers on unmount
- Handles rapid successive errors

**Example Usage**:

```typescript
function MyComponent() {
  const { isRateLimited, message, handleError } = useRateLimitHandler();

  const handleSubmit = async () => {
    try {
      await api.submitData();
    } catch (error) {
      if (error instanceof RateLimitError) {
        handleError(error);
      }
    }
  };

  return (
    <RateLimitedButton
      isRateLimited={isRateLimited}
      message={message}
      onClick={handleSubmit}
    >
      Submit
    </RateLimitedButton>
  );
}
```

### RateLimitBanner Component

Displays rate limit notifications with countdown timer:

```typescript
<RateLimitBanner
  message={message}              // User-friendly message
  remainingSeconds={remaining}   // Countdown value
  showCountdown={true}           // Show countdown timer
  dismissible={false}            // Allow manual dismissal
  onDismiss={() => {}}           // Dismiss callback
  className="custom-class"       // Additional CSS classes
/>
```

**Features**:
- Destructive (red/error) styling
- Live countdown display
- ARIA live regions for screen readers
- Optional manual dismissal
- Auto-hides when countdown expires

**Accessibility**:
- `role="alert"` for screen readers
- `aria-live="polite"` for dynamic updates
- `aria-atomic="true"` for complete message reading
- Clear dismiss button labels

### RateLimitedButton Component

Button wrapper that automatically disables during rate limit periods:

```typescript
<RateLimitedButton
  isRateLimited={isRateLimited}
  remainingSeconds={remaining}
  message={message}
  showCountdownInButton={true}   // Show countdown in button text
  originalText="Submit"          // Original button text
  onClick={handleClick}
>
  Submit
</RateLimitedButton>
```

**Features**:
- Auto-disables when `isRateLimited={true}`
- Optional countdown in button text ("Submit (45s)")
- Tooltip with full rate limit message
- All standard Button component props supported
- Respects explicit `disabled` prop

**Button Text Modes**:
1. **Default**: "Submit" (no countdown in text)
2. **With countdown + originalText**: "Submit (45s)"
3. **With countdown, no originalText**: "Wait 45s"

**Accessibility**:
- `aria-disabled` attribute
- `aria-label` with rate limit message
- `title` attribute for tooltip
- Keyboard navigation support

## Complete Integration Example

```typescript
import { useRateLimitHandler } from '@/hooks/useRateLimitHandler';
import { RateLimitBanner, RateLimitedButton } from '@/components/errors';
import { RateLimitError } from '@/lib/api/core/errors';

function ChatForm() {
  const {
    isRateLimited,
    remainingSeconds,
    message,
    handleError,
    reset,
  } = useRateLimitHandler();

  const handleSubmit = async (text: string) => {
    try {
      await chatApi.sendMessage(text);
    } catch (error) {
      if (error instanceof RateLimitError) {
        handleError(error);
      } else {
        // Handle other errors
      }
    }
  };

  return (
    <div>
      {isRateLimited && (
        <RateLimitBanner
          message={message}
          remainingSeconds={remainingSeconds}
          showCountdown={true}
        />
      )}

      <form onSubmit={(e) => {
        e.preventDefault();
        handleSubmit(formData.text);
      }}>
        <input type="text" name="text" />

        <RateLimitedButton
          type="submit"
          isRateLimited={isRateLimited}
          remainingSeconds={remainingSeconds}
          message={message}
          showCountdownInButton={true}
          originalText="Send Message"
        >
          Send Message
        </RateLimitedButton>
      </form>
    </div>
  );
}
```

## Backend Integration

### Rate Limiting Middleware

The backend `RateLimitingMiddleware` returns:
- **Status**: 429 Too Many Requests
- **Header**: `Retry-After` (seconds or HTTP-date format)
- **Body**: `{ error: "Rate limit exceeded", retryAfter: 60, message: "..." }`

### Retry-After Header Formats

The `RateLimitError.parseRetryAfter()` method supports both RFC 7231 formats:

1. **Delay-seconds**: `Retry-After: 60`
2. **HTTP-date**: `Retry-After: Wed, 21 Nov 2025 13:00:00 GMT`

Both formats are automatically parsed and converted to seconds.

## Testing

### Unit Tests

```typescript
// Error class tests
describe('RateLimitError', () => {
  it('should parse Retry-After header', () => {
    const error = RateLimitError.parseRetryAfter('60');
    expect(error).toBe(60);
  });

  it('should generate user-friendly messages', () => {
    const error = new RateLimitError({ message: 'Test', retryAfter: 45 });
    expect(error.getUserFriendlyMessage()).toBe('Too many requests. Please wait 45 seconds.');
  });
});

// Hook tests
describe('useRateLimitHandler', () => {
  it('should countdown from initial value', async () => {
    const { result } = renderHook(() => useRateLimitHandler());
    const error = new RateLimitError({ message: 'Test', retryAfter: 5 });

    act(() => result.current.handleError(error));
    expect(result.current.remainingSeconds).toBe(5);

    act(() => jest.advanceTimersByTime(1000));
    await waitFor(() => expect(result.current.remainingSeconds).toBe(4));
  });
});

// Component tests
describe('RateLimitBanner', () => {
  it('should render with countdown', () => {
    render(<RateLimitBanner message="Wait 45 seconds" remainingSeconds={45} />);
    expect(screen.getByText('45s')).toBeInTheDocument();
  });
});
```

### Integration Tests

Create E2E tests that:
1. Trigger rate limit on real endpoint
2. Verify banner displays with countdown
3. Wait for countdown to expire
4. Verify button re-enables automatically
5. Verify retry succeeds

## Best Practices

### Do's ✅

- Always use `useRateLimitHandler` for consistent UX
- Display countdown timers for better user experience
- Use `RateLimitBanner` for prominent notifications
- Use `RateLimitedButton` for submit buttons
- Implement ARIA attributes for accessibility
- Test countdown behavior with fake timers
- Handle both Retry-After formats (seconds and HTTP-date)

### Don'ts ❌

- Don't show generic "Too many requests" without countdown
- Don't hide rate limit errors completely
- Don't allow users to spam retry during countdown
- Don't forget to cleanup timers on unmount
- Don't ignore accessibility requirements
- Don't hardcode retry delays (use backend Retry-After value)

## Performance Considerations

- Hook uses `setInterval` with 1-second precision (low overhead)
- Timers are automatically cleaned up on unmount
- Components only re-render once per second during countdown
- No external dependencies (pure React)

## Security Considerations

- Rate limiting is enforced server-side (frontend is UX only)
- Retry-After values come from trusted backend
- No client-side bypass of rate limits
- Correlation IDs for audit logging

## Related Documentation

- [API Specification - Rate Limiting](../03-api/board-game-ai-api-specification.md)
- [Backend Rate Limiting Middleware](../../apps/api/src/Api/Middleware/RateLimitingMiddleware.cs)
- [ADR-009: Centralized Error Handling](../01-architecture/adr/adr-009-centralized-error-handling.md)

## Support

For questions or issues:
- GitHub Issues: [Create Issue](https://github.com/DegrassiAaron/meepleai-monorepo/issues/new)
- Slack: #frontend-support
- Email: frontend-team@meepleai.dev
