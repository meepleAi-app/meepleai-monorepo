# 🔐 [MEDIUM] Implement CSP Nonce Generation for Enhanced XSS Protection

## Summary

Implement Content-Security-Policy (CSP) nonce generation to remove `'unsafe-inline'` and `'unsafe-eval'` from the CSP policy, significantly improving XSS protection while maintaining React/Next.js functionality.

**Related**: #1447 (SecurityHeadersMiddleware implementation)
**Priority**: 🟡 MEDIUM
**Complexity**: High
**Estimated Time**: 2-3 days

## Current State

The current CSP policy includes security compromises:
```
script-src 'self' 'unsafe-inline' 'unsafe-eval'
```

These directives significantly reduce XSS protection:
- `'unsafe-inline'`: Allows inline scripts (XSS vector)
- `'unsafe-eval'`: Allows `eval()` and dynamic code execution (injection vector)

## Proposed Solution

### 1. Nonce Generation Middleware

Implement middleware to generate cryptographically random nonces per request:

```csharp
public class CspNonceMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Generate cryptographically random nonce (128 bits base64)
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

        // Store in HttpContext.Items for access by views/pages
        context.Items["csp-nonce"] = nonce;

        // Add to CSP header
        context.Response.OnStarting(() =>
        {
            var cspPolicy = $"script-src 'self' 'nonce-{nonce}'; style-src 'self' 'nonce-{nonce}'";
            context.Response.Headers.Append("Content-Security-Policy", cspPolicy);
            return Task.CompletedTask;
        });

        await _next(context);
    }
}
```

### 2. Next.js Integration

Update Next.js configuration to inject nonces:

**middleware.ts**:
```typescript
export function middleware(request: NextRequest) {
    const nonce = request.headers.get('x-csp-nonce');
    const response = NextResponse.next();
    response.headers.set('x-csp-nonce', nonce || '');
    return response;
}
```

**next.config.js**:
```javascript
module.exports = {
    async headers() {
        return [
            {
                source: '/:path*',
                headers: [
                    {
                        key: 'Content-Security-Policy',
                        value: "script-src 'self' 'nonce-{NONCE_PLACEHOLDER}'"
                    }
                ]
            }
        ];
    }
};
```

### 3. React Component Updates

Update script tags to include nonce attribute:

```tsx
export default function RootLayout({ children, nonce }: { children: React.ReactNode, nonce?: string }) {
    return (
        <html lang="it">
            <head>
                <Script
                    src="/path/to/script.js"
                    nonce={nonce}
                    strategy="beforeInteractive"
                />
            </head>
            <body>{children}</body>
        </html>
    );
}
```

### 4. Updated CSP Policy

**Before** (current):
```
script-src 'self' 'unsafe-inline' 'unsafe-eval'
```

**After** (with nonces):
```
script-src 'self' 'nonce-{random}'
```

## Implementation Tasks

### Backend (ASP.NET Core)

- [ ] Create `CspNonceMiddleware` for nonce generation
- [ ] Add nonce to `HttpContext.Items["csp-nonce"]`
- [ ] Update `SecurityHeadersMiddleware` to use nonce from context
- [ ] Add configuration option `EnableCspNonces` (default: false for gradual rollout)
- [ ] Pass nonce to frontend via response header or cookie

### Frontend (Next.js)

- [ ] Create Next.js middleware to capture CSP nonce
- [ ] Update `_document.tsx` or `layout.tsx` to inject nonce in scripts
- [ ] Add nonce prop to all `<Script>` components
- [ ] Update inline event handlers to use nonce-compatible patterns
- [ ] Test HMR (Hot Module Replacement) with CSP nonces

### Testing

- [ ] Unit tests for nonce generation (randomness, length, base64 encoding)
- [ ] Integration tests for nonce propagation (backend → frontend)
- [ ] E2E tests for all pages with CSP nonces enabled
- [ ] CSP violation monitoring in browser console
- [ ] Performance testing (nonce generation overhead)

### Documentation

- [ ] Update `docs/06-security/security-headers.md` with nonce implementation
- [ ] Add migration guide from unsafe-inline to nonces
- [ ] Document troubleshooting steps for CSP violations
- [ ] Update CLAUDE.md with new security features

## Acceptance Criteria

✅ Nonce generated per request with cryptographically random values (128+ bits)
✅ Nonce passed to frontend and injected in all script/style tags
✅ CSP policy updated to use nonces instead of unsafe-inline/unsafe-eval
✅ All frontend pages load without CSP violations
✅ HMR (Hot Module Replacement) works correctly in development
✅ Tests written with ≥90% coverage
✅ Documentation complete with migration guide
✅ Zero increase in page load time (<5ms nonce generation overhead)
✅ OWASP ZAP scan rating remains A or improves to A+

## Security Benefits

- **XSS Protection**: Blocks all inline scripts without nonce → prevents injection attacks
- **Code Injection Prevention**: Removes eval() support → eliminates dynamic code execution vectors
- **OWASP Compliance**: Meets OWASP CSP Level 2 recommendations
- **Defense in Depth**: Complements input validation and output encoding

## Risks & Mitigation

| Risk | Mitigation |
|------|------------|
| Breaking existing inline scripts | Audit all inline scripts, convert to external files or add nonces |
| Third-party scripts fail | Whitelist specific script sources in CSP |
| HMR breaks in development | Use CSP report-only mode in development |
| Performance overhead | Use cached nonce for multiple requests in same connection |

## Rollout Strategy

1. **Phase 1**: Implement nonce generation (backend only)
2. **Phase 2**: Add CSP report-only mode with nonces (monitor violations)
3. **Phase 3**: Fix all CSP violations in frontend
4. **Phase 4**: Enable enforcing CSP with nonces in production
5. **Phase 5**: Remove unsafe-inline/unsafe-eval from policy

## References

- [OWASP CSP Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Content_Security_Policy_Cheat_Sheet.html)
- [Next.js CSP Nonces](https://nextjs.org/docs/app/building-your-application/configuring/content-security-policy)
- [MDN CSP Nonces](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Content-Security-Policy/script-src#nonce)
- [CSP Level 3 Specification](https://www.w3.org/TR/CSP3/)

---

**Labels**: `security`, `enhancement`, `priority:medium`, `csp`, `xss-protection`
**Blocked by**: None
**Blocks**: None
