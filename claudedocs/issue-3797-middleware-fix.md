# Issue #3797 - Middleware Fetch Timeout Fix

## Root Cause
Middleware `fetch()` to `/api/v1/auth/me` has NO timeout, causing indefinite hangs on dynamic routes.

## Solution: Add AbortController with 5s Timeout

### File: apps/web/middleware.ts

**Line 98-139**: Replace `isSessionCookieValid` function

```typescript
async function isSessionCookieValid(request: NextRequest, cookieValue: string): Promise<boolean> {
  const cached = sessionValidationCache.get(cookieValue);
  if (cached && cached.expiresAt > Date.now()) {
    // eslint-disable-next-line no-console
    console.log(`[middleware] Session validation CACHE HIT for ${cookieValue.substring(0, 10)}... valid=${cached.valid}`);
    return cached.valid;
  }

  const cookieHeader = request.headers.get('cookie');
  if (!cookieHeader) {
    // eslint-disable-next-line no-console
    console.log('[middleware] No cookie header found in request');
    cacheSessionValidation(cookieValue, false);
    return false;
  }

  try {
    const apiOrigins = getApiOrigins();
    const apiUrl = `${apiOrigins[0]}/api/v1/auth/me`;

    // ✅ FIX: Add AbortController with 5s timeout
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), 5000);

    // eslint-disable-next-line no-console
    console.log(`[middleware] Validating session at ${apiUrl} with cookie: ${cookieHeader.substring(0, 50)}...`);

    try {
      const response = await fetch(apiUrl, {
        headers: { cookie: cookieHeader },
        credentials: 'include',
        cache: 'no-store',
        signal: controller.signal, // ✅ ADD: Abort signal
      });

      clearTimeout(timeoutId); // ✅ Clear timeout on success

      // eslint-disable-next-line no-console
      console.log(`[middleware] Session validation response: ${response.status} ok=${response.ok}`);
      cacheSessionValidation(cookieValue, response.ok);
      return response.ok;
    } catch (fetchError) {
      clearTimeout(timeoutId);

      // ✅ Specific handling for timeout vs other errors
      if (fetchError instanceof Error && fetchError.name === 'AbortError') {
        console.error('[middleware] Session validation TIMEOUT after 5s');
      } else {
        console.error('[middleware] Session validation fetch error:', fetchError);
      }

      cacheSessionValidation(cookieValue, false);
      return false;
    }
  } catch (error) {
    console.error('[middleware] Failed to validate session cookie:', error);
    cacheSessionValidation(cookieValue, false);
    return false;
  }
}
```

## Implementation Steps

### 1. Apply Fix
```bash
cd apps/web
# Apply the fix to middleware.ts (lines 98-139)
```

### 2. Rebuild & Deploy
```bash
cd ../../infra
docker compose build web --no-cache
docker compose up -d web
```

### 3. Monitor Logs
```bash
docker compose logs -f web | grep "middleware"
```

### 4. Test Dynamic Routes
- Navigate to: `http://localhost:3000/verify-email?token=test123`
- Expected: Should fail gracefully within 5s (not 15-20s timeout)
- Check logs: Should see "Session validation TIMEOUT after 5s" if API slow

## Success Criteria
✅ Dynamic routes load in < 3s
✅ No infinite loading states
✅ Middleware timeout logs appear if API slow
✅ Email verification flow completes successfully

## Rollback Plan
If regression occurs:
```bash
git checkout apps/web/middleware.ts
docker compose build web --no-cache
docker compose restart web
```

## Related Issues
- vercel/next.js#88603 (Memory leak - fixed in Next.js 16.1.1)
- Issue #3797 (This issue - dynamic routes timeout)
