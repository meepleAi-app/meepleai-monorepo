# CORS Configuration

**Issue**: #1448 - CORS Header Whitelist Security Enhancement
**Status**: ✅ Implemented
**Last Updated**: 2025-11-20

## Overview

MeepleAI implements a security-hardened CORS (Cross-Origin Resource Sharing) configuration that uses explicit header whitelists instead of the permissive `AllowAnyHeader()` approach.

## Security Rationale

Using `AllowAnyHeader()` permits any custom header from cross-origin requests, which creates potential security vulnerabilities:

- **Information Disclosure**: Attackers could send arbitrary headers to probe for internal APIs
- **Header Injection**: Malicious headers could bypass security controls
- **Attack Surface**: Unnecessary headers increase the attack surface

**Solution**: Explicitly whitelist only required headers using `WithHeaders()`.

## Whitelisted Headers

The following headers are permitted in CORS requests:

| Header | Purpose | Example |
|--------|---------|---------|
| `Content-Type` | Request/response MIME type | `application/json` |
| `Authorization` | Bearer tokens, API keys | `Bearer eyJ0eXAi...` |
| `X-Correlation-ID` | Request tracing | `550e8400-e29b-41d4-a716-446655440000` |
| `X-API-Key` | API key authentication | `mpl_prod_abc123...` |

## Configuration

### Program.cs

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("web", policy =>
    {
        var corsOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? Array.Empty<string>();

        var configuredOrigins = corsOrigins
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (configuredOrigins.Length == 0)
        {
            policy.WithOrigins("http://localhost:3000");
        }
        else
        {
            policy.WithOrigins(configuredOrigins);
        }

        // Issue #1448: Whitelist specific headers instead of AllowAnyHeader()
        policy
            .WithHeaders(
                "Content-Type",
                "Authorization",
                "X-Correlation-ID",
                "X-API-Key"
            )
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
```

### appsettings.json

```json
{
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:3000",
      "https://meepleai.dev",
      "https://www.meepleai.dev"
    ]
  }
}
```

## Usage

### Frontend (Next.js)

**Valid Request (Whitelisted Headers)**:
```typescript
const response = await fetch('http://localhost:5080/api/v1/games', {
  method: 'GET',
  headers: {
    'Content-Type': 'application/json',
    'Authorization': 'Bearer ' + token,
    'X-Correlation-ID': correlationId,
  },
  credentials: 'include', // Required for cookies
});
```

**Invalid Request (Non-whitelisted Header)**:
```typescript
// ❌ This will be rejected by CORS
const response = await fetch('http://localhost:5080/api/v1/games', {
  method: 'GET',
  headers: {
    'Content-Type': 'application/json',
    'X-Custom-Debug': 'some-value', // NOT in whitelist
  },
  credentials: 'include',
});
```

### CORS Preflight

CORS preflight requests (OPTIONS) verify allowed headers:

**Request**:
```http
OPTIONS /api/v1/games HTTP/1.1
Origin: http://localhost:3000
Access-Control-Request-Method: POST
Access-Control-Request-Headers: Content-Type, Authorization
```

**Response**:
```http
HTTP/1.1 204 No Content
Access-Control-Allow-Origin: http://localhost:3000
Access-Control-Allow-Methods: GET, POST, PUT, DELETE, OPTIONS
Access-Control-Allow-Headers: Content-Type, Authorization, X-Correlation-ID, X-API-Key
Access-Control-Allow-Credentials: true
```

## Testing

### Integration Tests

Location: `apps/api/tests/Api.Tests/Integration/CorsHeaderWhitelistTests.cs`

**Test Coverage**:
- ✅ Whitelisted headers are accepted
- ✅ Non-whitelisted headers are rejected
- ✅ CORS preflight requests work correctly
- ✅ Multiple headers can be requested simultaneously
- ✅ Header names are case-insensitive
- ✅ Mixed headers (whitelisted + non-whitelisted) are handled correctly

**Run Tests**:
```bash
dotnet test --filter "FullyQualifiedName~CorsHeaderWhitelistTests"
```

### Manual Testing

**Test Preflight**:
```bash
curl -X OPTIONS http://localhost:5080/api/v1/games \
  -H "Origin: http://localhost:3000" \
  -H "Access-Control-Request-Method: GET" \
  -H "Access-Control-Request-Headers: Content-Type, Authorization" \
  -v
```

**Expected**: `Access-Control-Allow-Headers` contains `Content-Type, Authorization, X-Correlation-ID, X-API-Key`

**Test Non-whitelisted Header**:
```bash
curl -X OPTIONS http://localhost:5080/api/v1/games \
  -H "Origin: http://localhost:3000" \
  -H "Access-Control-Request-Method: GET" \
  -H "Access-Control-Request-Headers: X-Malicious-Header" \
  -v
```

**Expected**: `Access-Control-Allow-Headers` does NOT contain `X-Malicious-Header`

## Adding New Headers

If you need to add a new whitelisted header:

1. **Evaluate Security Impact**: Ensure the header is necessary and doesn't expose sensitive information
2. **Update Configuration**: Add header to `WithHeaders()` in both:
   - `apps/api/src/Api/Program.cs`
   - `apps/api/src/Api/Extensions/WebApplicationExtensions.cs`
3. **Update Tests**: Add test cases in `CorsHeaderWhitelistTests.cs`
4. **Update Documentation**: Add header to this document's whitelisted headers table
5. **Security Review**: Request security review before merging

**Example**:
```csharp
policy
    .WithHeaders(
        "Content-Type",
        "Authorization",
        "X-Correlation-ID",
        "X-API-Key",
        "X-New-Header"  // Add new header
    )
    .AllowAnyMethod()
    .AllowCredentials();
```

## Security Considerations

### Do's ✅

- **Use explicit header whitelist** (`WithHeaders()`)
- **Validate header values** in middleware
- **Log rejected CORS requests** for monitoring
- **Use HTTPS in production** to prevent header tampering
- **Keep whitelist minimal** (principle of least privilege)

### Don'ts ❌

- **Never use `AllowAnyHeader()`** (security vulnerability)
- **Don't add headers without security review**
- **Don't expose internal debugging headers** in production
- **Don't trust header values** without validation
- **Don't bypass CORS** with server-side proxies (defeats purpose)

## Related Security Controls

CORS header whitelist works with other security controls:

- **CSRF Protection**: SameSite cookies prevent CSRF attacks
- **API Key Authentication**: X-API-Key header validated by middleware
- **Rate Limiting**: Applied after CORS validation
- **Input Validation**: Header values sanitized in middleware

## References

- **OWASP CORS**: https://owasp.org/www-community/attacks/CSRF
- **MDN CORS**: https://developer.mozilla.org/en-US/docs/Web/HTTP/CORS
- **ASP.NET Core CORS**: https://learn.microsoft.com/en-us/aspnet/core/security/cors

## Troubleshooting

### CORS Error: Header Not Allowed

**Symptom**: Browser console shows CORS error about disallowed header

**Cause**: Frontend is sending a non-whitelisted header

**Solution**: Either:
1. Remove the non-whitelisted header from frontend
2. Add header to whitelist (requires security review)

### Preflight Request Fails

**Symptom**: OPTIONS request returns 204 but browser still blocks request

**Cause**: `Access-Control-Allow-Headers` doesn't match requested headers

**Solution**: Check that all headers in `Access-Control-Request-Headers` are in the whitelist

### Credentials Not Sent

**Symptom**: Cookies/auth headers not sent with request

**Cause**: Missing `credentials: 'include'` in fetch or `withCredentials: true` in axios

**Solution**:
```typescript
// fetch
fetch(url, { credentials: 'include' })

// axios
axios.get(url, { withCredentials: true })
```

---

**Version**: 1.0
**Owner**: Security Team
**Reviewers**: Engineering Lead, DevOps Lead
