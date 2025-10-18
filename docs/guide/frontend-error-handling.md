# Frontend Error Handling Guide

**Issue**: #293 (OPS-03)
**Date**: 2025-10-16
**Status**: Implemented

## Overview

This guide covers the comprehensive error handling system implemented in the MeepleAI frontend, including error boundaries, toast notifications, retry logic, and client-side logging integrated with the backend observability stack.

## Architecture

### Components

```
┌─────────────────────────────────────────────────────────────┐
│                      Frontend Application                     │
│  ┌────────────────────────────────────────────────────────┐ │
│  │              ErrorBoundary (Root)                      │ │
│  │  ┌──────────────────────────────────────────────────┐ │ │
│  │  │            Application Pages                     │ │ │
│  │  │                                                  │ │ │
│  │  │  - API calls via apiEnhanced                    │ │ │
│  │  │  - Automatic retry with exponential backoff     │ │ │
│  │  │  - Error tracking with correlation IDs          │ │ │
│  │  └──────────────────────────────────────────────────┘ │ │
│  │                                                        │ │
│  │  Toast Notifications (Global)                         │ │
│  └────────────────────────────────────────────────────────┘ │
│           │                    │                    │        │
│           │                    │                    │        │
│      Rendering              API                  User       │
│       Errors              Errors              Feedback      │
└───────────┼────────────────────┼────────────────────┼────────┘
            │                    │                    │
            ▼                    ▼                    ▼
    ┌──────────────┐     ┌──────────────┐     ┌──────────────┐
    │ ErrorBoundary│     │  Logger      │     │   Toast      │
    │  Fallback    │     │  Service     │     │   System     │
    └──────────────┘     └──────────────┘     └──────────────┘
            │                    │
            └──────────┬─────────┘
                       ▼
              ┌──────────────────┐
              │  Backend Logging │
              │  /api/v1/logs/   │
              └──────────────────┘
```

## Core Components

### 1. Error Classes (`lib/errors.ts`)

Custom error types for structured error handling:

```typescript
import { ApiError, NetworkError, ValidationError } from '@/lib/errors';

// API errors with status codes
throw new ApiError('Not found', 404, '/api/v1/games', 'GET', 'correlation-123');

// Network errors (connection failures)
throw new NetworkError('Connection failed', '/api/v1/games');

// Validation errors
throw new ValidationError('Email is required', 'email');
```

**Features**:
- User-friendly error messages
- Automatic retryability detection
- Error severity classification
- UUID sanitization for logging
- Correlation ID tracking

### 2. Enhanced API Client (`lib/api-enhanced.ts`)

Drop-in replacement for the basic API client with retry logic:

```typescript
import { apiEnhanced } from '@/lib/api-enhanced';

// Automatic retry with exponential backoff
const games = await apiEnhanced.get<Game[]>('/api/v1/games');

// Custom retry configuration
const game = await apiEnhanced.get<Game>('/api/v1/games/123', {
  retry: {
    maxAttempts: 5,
    initialDelayMs: 2000,
    maxDelayMs: 30000,
    backoffMultiplier: 2
  }
});

// Skip retry for specific requests
const result = await apiEnhanced.post('/api/v1/games', data, {
  skipRetry: true
});

// Get full response with metadata (correlation ID, status code)
const response = await apiEnhanced.getWithMetadata<Game>('/api/v1/games/123');
console.log(response.correlationId);
```json
**Features**:
- Automatic retry with exponential backoff (default: 3 attempts)
- Intelligent retry decisions (retries 5xx, 408, 429, network errors)
- Correlation ID extraction and tracking
- Request timeout support
- Graceful degradation
- Logging integration

**Default Retry Configuration**:
```typescript
{
  maxAttempts: 3,
  initialDelayMs: 1000,
  maxDelayMs: 10000,
  backoffMultiplier: 2
}
```json
**Backoff Schedule**:
- Attempt 1: Immediate
- Attempt 2: 1000ms delay
- Attempt 3: 2000ms delay
- Attempt 4: 4000ms delay
- ...capped at maxDelayMs

### 3. Client-Side Logger (`lib/logger.ts`)

Structured logging service with batching and remote logging:

```typescript
import { logger } from '@/lib/logger';

// Set correlation ID for all subsequent logs
logger.setCorrelationId('correlation-123');

// Log at different levels
logger.debug('Debug info', { component: 'GameList', metadata: { count: 10 } });
logger.info('User action', { component: 'GameList', action: 'fetchGames' });
logger.warn('Slow response', { component: 'API', metadata: { duration: 3000 } });

// Log errors with full context
try {
  await apiEnhanced.get('/api/v1/games');
} catch (error) {
  logger.error('Failed to fetch games', error as Error, {
    component: 'GameList',
    action: 'fetchGames',
    metadata: { userId: '123' }
  });
}

// Manual flush (automatic batching every 5s)
logger.flush();
```json
**Features**:
- Structured logging with context
- Automatic batching (default: 10 logs or 5s interval)
- Remote logging to `/api/v1/logs/client`
- Correlation ID propagation
- Error sanitization (removes sensitive data)
- Console logging in development
- Graceful handling of logging failures

**Configuration**:
```typescript
import { getLogger } from '@/lib/logger';

const logger = getLogger({
  enableConsole: true,
  enableRemote: true,
  remoteEndpoint: '/api/v1/logs/client',
  minLevel: LogLevel.INFO,
  batchSize: 10,
  flushIntervalMs: 5000
});
```

### 4. Error Boundary (`components/ErrorBoundary.tsx`)

React error boundary to catch rendering errors:

```typescript
import { ErrorBoundary } from '@/components/ErrorBoundary';

// Basic usage
<ErrorBoundary>
  <YourComponent />
</ErrorBoundary>

// With custom fallback
<ErrorBoundary
  fallback={<div>Custom error UI</div>}
  onError={(error, errorInfo) => console.log('Error caught:', error)}
  componentName="GameList"
  showDetails={process.env.NODE_ENV === 'development'}
>
  <YourComponent />
</ErrorBoundary>

// With fallback function
<ErrorBoundary
  fallback={(error, reset) => (
    <div>
      <h2>Error: {error.message}</h2>
      <button onClick={reset}>Try Again</button>
    </div>
  )}
>
  <YourComponent />
</ErrorBoundary>
```

**Features**:
- Catches React rendering errors
- Automatic error logging
- Customizable fallback UI
- Error reset functionality
- Error details for development
- Component stack traces
- Integration with logger service

**useErrorHandler Hook**:
```typescript
import { useErrorHandler } from '@/components/ErrorBoundary';

function MyComponent() {
  const { handleError, clearError, error } = useErrorHandler();

  const fetchData = async () => {
    try {
      const data = await apiEnhanced.get('/api/v1/data');
    } catch (error) {
      handleError(error as Error); // Will be caught by nearest error boundary
    }
  };

  return <div>...</div>;
}
```

### 5. Toast Notifications (`components/Toast.tsx`, `hooks/useToast.ts`)

Non-blocking toast notifications for user feedback:

```typescript
import { useToast } from '@/hooks/useToast';

function MyComponent() {
  const { success, error, warning, info, toasts, dismiss } = useToast();

  const handleSuccess = () => {
    success('Success!', 'Operation completed successfully');
  };

  const handleError = () => {
    error('Error!', 'Something went wrong', 0); // 0 = no auto-dismiss
  };

  const handleWarning = () => {
    warning('Warning!', 'Please review your input');
  };

  const handleInfo = () => {
    info('Info', 'New feature available');
  };

  return (
    <div>
      <button onClick={handleSuccess}>Success</button>
      <button onClick={handleError}>Error</button>
      <ToastContainer toasts={toasts} onDismiss={dismiss} position="top-right" />
    </div>
  );
}
```json
**Features**:
- 4 types: success, error, warning, info
- Auto-dismiss with configurable duration (default: 5s)
- Manual dismiss
- Toast stacking
- Smooth animations
- Accessible (ARIA attributes)
- Multiple positioning options

**Positioning Options**:
- `top-right` (default)
- `top-left`
- `bottom-right`
- `bottom-left`
- `top-center`
- `bottom-center`

### 6. Error Modal (`components/ErrorModal.tsx`)

Modal dialog for detailed error information:

```typescript
import { ErrorModal } from '@/components/ErrorModal';
import { useState } from 'react';

function MyComponent() {
  const [error, setError] = useState<Error | null>(null);

  const handleAction = async () => {
    try {
      await apiEnhanced.post('/api/v1/action', data);
    } catch (err) {
      setError(err as Error);
    }
  };

  const handleRetry = () => {
    setError(null);
    handleAction();
  };

  return (
    <>
      <button onClick={handleAction}>Perform Action</button>
      <ErrorModal
        isOpen={!!error}
        onClose={() => setError(null)}
        error={error}
        title="Action Failed"
        showDetails={process.env.NODE_ENV === 'development'}
        onRetry={handleRetry}
      />
    </>
  );
}
```

**Features**:
- Detailed error information
- User-friendly messages
- Technical details (development only)
- Retry functionality
- Keyboard navigation (Escape to close)
- Accessibility compliant
- Backdrop click to close

## Integration

### App-Level Integration

The error handling system is integrated at the app level in `_app.tsx`:

```typescript
import { ErrorBoundary } from '../components/ErrorBoundary';
import { ToastContainer } from '../components/Toast';
import { useToast } from '../hooks/useToast';

function AppContent({ Component, pageProps }: AppProps) {
  const { toasts, dismiss } = useToast();

  return (
    <>
      <Component {...pageProps} />
      <ToastContainer toasts={toasts} onDismiss={dismiss} position="top-right" />
    </>
  );
}

export default function App(props: AppProps) {
  return (
    <ErrorBoundary
      componentName="App"
      showDetails={process.env.NODE_ENV === 'development'}
    >
      <AppContent {...props} />
    </ErrorBoundary>
  );
}
```sql
### Migration from Basic API Client

To migrate existing code to use the enhanced API client:

**Before**:
```typescript
import { api } from '@/lib/api';

const games = await api.get<Game[]>('/api/v1/games');
```

**After**:
```typescript
import { apiEnhanced } from '@/lib/api-enhanced';

const games = await apiEnhanced.get<Game[]>('/api/v1/games');
// Now includes automatic retry, logging, and correlation ID tracking
```

## Best Practices

### 1. Error Handling in Components

```typescript
function GameList() {
  const [games, setGames] = useState<Game[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);
  const { error: showError } = useToast();

  useEffect(() => {
    const fetchGames = async () => {
      try {
        setLoading(true);
        const data = await apiEnhanced.get<Game[]>('/api/v1/games');
        setGames(data || []);
      } catch (err) {
        const error = err as Error;
        setError(error);

        // Show user-friendly toast
        if (error instanceof ApiError) {
          showError('Failed to load games', error.getUserMessage());
        } else if (error instanceof NetworkError) {
          showError('Connection Error', error.getUserMessage());
        } else {
          showError('Error', 'An unexpected error occurred');
        }

        // Error is already logged by apiEnhanced
      } finally {
        setLoading(false);
      }
    };

    fetchGames();
  }, [showError]);

  if (loading) return <LoadingSpinner />;
  if (error) return <ErrorMessage error={error} onRetry={fetchGames} />;

  return <div>{/* Render games */}</div>;
}
```

### 2. Error Boundaries for Sections

Wrap individual sections with error boundaries to prevent full page crashes:

```typescript
function Dashboard() {
  return (
    <div>
      <ErrorBoundary componentName="GameList">
        <GameList />
      </ErrorBoundary>

      <ErrorBoundary componentName="UserProfile">
        <UserProfile />
      </ErrorBoundary>

      <ErrorBoundary componentName="ActivityFeed">
        <ActivityFeed />
      </ErrorBoundary>
    </div>
  );
}
```

### 3. Custom Retry Logic

For critical operations, implement custom retry logic:

```typescript
async function criticalOperation() {
  const maxAttempts = 5;
  let lastError: Error | null = null;

  for (let attempt = 1; attempt <= maxAttempts; attempt++) {
    try {
      const result = await apiEnhanced.post('/api/v1/critical', data, {
        retry: { maxAttempts: 1 } // Disable internal retry
      });
      return result;
    } catch (err) {
      lastError = err as Error;

      if (err instanceof ApiError && !err.isRetryable()) {
        break; // Don't retry non-retryable errors
      }

      if (attempt < maxAttempts) {
        const delay = Math.min(1000 * Math.pow(2, attempt), 30000);
        await new Promise(resolve => setTimeout(resolve, delay));
      }
    }
  }

  throw lastError;
}
```

### 4. Error Logging Context

Always provide context when logging errors:

```typescript
try {
  await apiEnhanced.delete(`/api/v1/games/${gameId}`);
} catch (error) {
  logger.error(
    'Failed to delete game',
    error as Error,
    {
      component: 'GameManager',
      action: 'deleteGame',
      metadata: {
        gameId,
        userId: currentUser.id,
        timestamp: Date.now()
      }
    }
  );
  throw error; // Re-throw if needed
}
```

### 5. Validation Errors

Handle validation errors gracefully:

```typescript
function GameForm() {
  const [errors, setErrors] = useState<Record<string, string>>({});
  const { error: showError } = useToast();

  const handleSubmit = async (data: GameData) => {
    // Client-side validation
    const validationErrors: Record<string, string> = {};
    if (!data.name) validationErrors.name = 'Name is required';
    if (!data.players) validationErrors.players = 'Number of players is required';

    if (Object.keys(validationErrors).length > 0) {
      setErrors(validationErrors);
      throw new ValidationError('Validation failed', undefined, validationErrors);
    }

    try {
      await apiEnhanced.post('/api/v1/games', data);
      showSuccess('Game created successfully');
    } catch (err) {
      if (err instanceof ApiError && err.statusCode === 400) {
        showError('Validation Error', 'Please check your input');
      } else {
        showError('Error', 'Failed to create game');
      }
    }
  };

  return (
    <form onSubmit={handleSubmit}>
      {/* Form fields with error display */}
    </form>
  );
}
```

## Testing

### Unit Tests

The error handling system includes comprehensive unit tests:

- **Error utilities**: `lib/__tests__/errors.test.ts` (90+ tests)
- **Logger**: `lib/__tests__/logger.test.ts` (60+ tests)
- **Toast component**: `components/__tests__/Toast.test.tsx` (30+ tests)
- **Error boundary**: `components/__tests__/ErrorBoundary.test.tsx` (20+ tests)

Run tests:
```bash
cd apps/web
pnpm test                     # Run all tests
pnpm test:watch               # Watch mode
pnpm test:coverage            # Generate coverage report
```

### Integration Tests

Integration tests cover API error scenarios:

- **API client**: `lib/__tests__/api-enhanced.integration.test.ts`

### E2E Tests

End-to-end tests verify user-facing error flows:

- **Error handling**: `e2e/error-handling.spec.ts`

Run E2E tests:
```bash
cd apps/web
pnpm test:e2e                 # Headless mode
pnpm test:e2e:ui              # UI mode
pnpm test:e2e:report          # View report
```sql
### Coverage Targets

- Overall: 90% (enforced by Jest)
- Error utilities: 95%+
- React components: 85%+
- Integration tests: 80%+

## Observability Integration

The frontend error handling integrates with the backend observability stack (OPS-02):

### Correlation IDs

All API requests receive correlation IDs from the backend:

```typescript
const response = await apiEnhanced.getWithMetadata('/api/v1/games');
console.log(response.correlationId); // X-Correlation-Id from response

// Logger automatically uses correlation ID
logger.setCorrelationId(response.correlationId);
logger.error('Error occurred'); // Includes correlation ID
```json
### Distributed Tracing

Client-side errors are correlated with backend traces:

1. Frontend makes API request
2. Backend assigns correlation ID (trace ID)
3. Backend logs to Seq with correlation ID
4. Frontend receives correlation ID in response header
5. Frontend logs errors with same correlation ID
6. Both logs can be correlated in Seq/Jaeger

### Error Metrics

Key error metrics to monitor:

- **Error rate**: Percentage of failed requests
- **Retry rate**: Percentage of requests that required retries
- **Error types**: Distribution of ApiError, NetworkError, etc.
- **Status codes**: Distribution of HTTP status codes
- **Toast frequency**: Number of error toasts shown to users
- **Error boundary triggers**: Rendering errors caught

## Troubleshooting

### Common Issues

1. **Toasts not appearing**:
   - Verify `ToastContainer` is included in `_app.tsx`
   - Check Z-index conflicts with other elements
   - Ensure `useToast` hook is used correctly

2. **Error boundary not catching errors**:
   - Error boundaries only catch rendering errors
   - Use try-catch for async errors
   - Use `useErrorHandler` hook to throw errors to boundary

3. **Retries not working**:
   - Check error is retryable (5xx, 408, 429, network)
   - Verify retry config is not disabled
   - Check browser console for retry logs (development mode)

4. **Logs not reaching backend**:
   - Verify `/api/v1/logs/client` endpoint exists
   - Check network tab for failed POST requests
   - Ensure batching interval hasn't expired
   - Call `logger.flush()` manually for immediate send

5. **Correlation IDs not matching**:
   - Ensure backend sets `X-Correlation-Id` header
   - Verify logger correlation ID is set after API calls
   - Check backend logs have same correlation ID format

### Debug Mode

Enable debug logging in development:

```typescript
import { getLogger, LogLevel } from '@/lib/logger';

const logger = getLogger({
  enableConsole: true,
  minLevel: LogLevel.DEBUG
});

logger.debug('Debug message', { component: 'MyComponent', metadata: { foo: 'bar' } });
```

## Future Enhancements

Potential improvements for future iterations:

1. **Error analytics dashboard**: Aggregate and visualize frontend errors
2. **Sentry/Rollbar integration**: Third-party error monitoring
3. **Offline support**: Queue errors when offline, send when online
4. **Error replay**: Record user sessions leading to errors
5. **Smart retry**: Machine learning to predict retryable errors
6. **Error budgets**: Alert when error rate exceeds threshold
7. **A/B testing**: Test different error messages for user experience

## References

- **Backend Observability**: `docs/observability.md` (OPS-01)
- **OpenTelemetry**: `docs/ops-02-opentelemetry-design.md` (OPS-02)
- **API Versioning**: `docs/api-versioning.md` (API-01)
- **React Error Boundaries**: https://react.dev/reference/react/Component#catching-rendering-errors-with-an-error-boundary
- **Exponential Backoff**: https://en.wikipedia.org/wiki/Exponential_backoff

---

**Last Updated**: 2025-10-16
**Implemented By**: OPS-03
