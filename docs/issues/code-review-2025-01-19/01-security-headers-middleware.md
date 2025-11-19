# 🔐 [Security] Implement SecurityHeadersMiddleware

**Priority**: 🔴 CRITICAL
**Complexity**: Low
**Estimated Time**: 4-6 hours
**Dependencies**: None

## 🎯 Objective

Implement middleware to add HTTP security headers to all responses, protecting the application from XSS, clickjacking, MIME sniffing, and other common vulnerabilities.

## 📋 Context

**Source**: Code Review Backend-Frontend Interactions
**Issue**: Missing critical security headers (CSP, HSTS, X-Frame-Options, etc.)
**Impact**: High - Protects against OWASP Top 10 vulnerabilities

## 🔒 Security Headers to Implement

### 1. Content-Security-Policy (CSP)
Prevents XSS and injection attacks
```
default-src 'self';
script-src 'self' 'unsafe-inline' 'unsafe-eval';
style-src 'self' 'unsafe-inline';
img-src 'self' data: https:;
font-src 'self' data:;
connect-src 'self' http://localhost:3000 http://localhost:5080;
frame-ancestors 'none'
```

### 2. Strict-Transport-Security (HSTS)
Forces HTTPS (production only)
```
max-age=31536000; includeSubDomains
```

### 3. X-Frame-Options
Prevents clickjacking
```
DENY
```

### 4. X-Content-Type-Options
Prevents MIME sniffing
```
nosniff
```

### 5. X-XSS-Protection
Browser XSS filter (legacy support)
```
1; mode=block
```

### 6. Referrer-Policy
Controls referrer information
```
strict-origin-when-cross-origin
```

### 7. Permissions-Policy
Limits browser features
```
geolocation=(), microphone=(), camera=()
```

## ✅ Task Checklist

### Implementation
- [ ] Create `apps/api/src/Api/Middleware/SecurityHeadersMiddleware.cs`
- [ ] Implement logic for all 7 headers
- [ ] Skip HSTS for localhost/development
- [ ] Register middleware in `WebApplicationExtensions.cs` (before CORS)
- [ ] Add optional configuration in `appsettings.json`

### Testing
- [ ] Integration test to verify headers in response
- [ ] Test HSTS skip in development
- [ ] Test all 7 headers present
- [ ] Verify headers on SSE streaming endpoints

### Documentation
- [ ] Create `docs/06-security/security-headers.md`
- [ ] Update `SECURITY.md` with new headers
- [ ] Document configuration in README
- [ ] ADR for CSP choices (unsafe-inline, etc.)

### Validation
- [ ] OWASP ZAP scan to verify headers
- [ ] Manual test with browser DevTools
- [ ] Verify CORS + Security Headers compatibility

## 📁 Files to Create/Modify

```
apps/api/src/Api/Middleware/SecurityHeadersMiddleware.cs (NEW)
apps/api/src/Api/Extensions/WebApplicationExtensions.cs (MODIFY)
apps/api/tests/Api.Tests/Middleware/SecurityHeadersMiddlewareTests.cs (NEW)
docs/06-security/security-headers.md (NEW)
SECURITY.md (UPDATE)
```

## 🔗 References

- [OWASP Secure Headers Project](https://owasp.org/www-project-secure-headers/)
- [MDN CSP Guide](https://developer.mozilla.org/en-US/docs/Web/HTTP/CSP)
- [MDN Security Headers](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers#security)
- [ASP.NET Core Security Best Practices](https://learn.microsoft.com/en-us/aspnet/core/security/)

## 📊 Acceptance Criteria

- ✅ All 7 headers present in every HTTP response
- ✅ HSTS skipped in development (localhost)
- ✅ CSP allows React frontend to function
- ✅ Test coverage >= 90%
- ✅ Complete documentation
- ✅ OWASP ZAP scan passes with A rating

## 🏷️ Labels

`priority: critical`, `type: security`, `area: backend`, `effort: small`, `sprint: 1`
