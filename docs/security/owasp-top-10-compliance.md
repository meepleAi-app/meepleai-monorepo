# OWASP Top 10 Compliance Guide

**MeepleAI Security Posture** - Come MeepleAI previene le vulnerabilità OWASP Top 10

---

## 📋 OWASP Top 10 (2021) Compliance Matrix

| # | Vulnerability | Status | Mitigations |
|---|---------------|--------|-------------|
| A01 | Broken Access Control | ✅ Protected | Authorization middleware, role-based access |
| A02 | Cryptographic Failures | ✅ Protected | BCrypt, PBKDF2, TLS/SSL |
| A03 | Injection | ✅ Protected | Parameterized queries, FluentValidation |
| A04 | Insecure Design | ✅ Protected | Threat modeling, secure architecture |
| A05 | Security Misconfiguration | ⚠️ Monitored | Auto-config, secrets management |
| A06 | Vulnerable Components | ⚠️ Monitored | Dependabot, Semgrep scanning |
| A07 | Authentication Failures | ✅ Protected | Strong password policy, 2FA, rate limiting |
| A08 | Data Integrity Failures | ✅ Protected | HMAC signatures, integrity checks |
| A09 | Logging Failures | ✅ Protected | Centralized logging, audit trails |
| A10 | SSRF | ✅ Protected | URL validation, whitelist |

---

## A01: Broken Access Control 🔐

### Vulnerabilities Prevented

**Authorization Middleware**:
```csharp
// Global authorization requirement
app.MapPost("/api/v1/games", async (CreateGameCommand cmd, IMediator mediator) =>
    await mediator.Send(cmd))
.RequireAuthorization("AdminPolicy");  // ✅ Explicit authorization
```

**Role-Based Access Control (RBAC)**:
```csharp
public class AdminPolicy : IAuthorizationRequirement { }

public class AdminPolicyHandler : AuthorizationHandler<AdminPolicy>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdminPolicy requirement)
    {
        if (context.User.IsInRole("Admin"))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
```

**Resource-Level Authorization**:
```csharp
// Verify user owns resource before allowing access
var document = await _db.Documents.FindAsync(documentId);
if (document.UploadedBy != currentUserId)
    throw new ForbiddenException("You don't own this document");
```

---

## A02: Cryptographic Failures 🔑

### Protections Implemented

**Password Hashing (BCrypt)**:
```csharp
public record PasswordHash
{
    public string Value { get; init; }

    public static PasswordHash Create(string plaintext)
    {
        // BCrypt with cost factor 12 (2^12 = 4096 rounds)
        var hash = BCrypt.Net.BCrypt.HashPassword(plaintext, workFactor: 12);
        return new PasswordHash { Value = hash };
    }

    public bool Verify(string plaintext)
    {
        return BCrypt.Net.BCrypt.Verify(plaintext, Value);
    }
}
```

**API Key Hashing (PBKDF2)**:
```csharp
public static string HashApiKey(string key, byte[] salt)
{
    using var pbkdf2 = new Rfc2898DeriveBytes(
        password: key,
        salt: salt,
        iterations: 10_000,  // 10K iterations
        hashAlgorithm: HashAlgorithmName.SHA256
    );
    return Convert.ToBase64String(pbkdf2.GetBytes(32));
}
```

**TLS/SSL Enforcement**:
```csharp
// Production HTTPS redirect
app.UseHttpsRedirection();

// HSTS header (force HTTPS for 1 year)
app.UseHsts();  // Strict-Transport-Security: max-age=31536000
```

**Secrets Management**:
- ✅ Secrets stored in `.secret` files (gitignored)
- ✅ Never committed to repository
- ✅ Environment-specific encryption
- ✅ Auto-rotation recommended (90 days)

---

## A03: Injection Attacks 💉

### SQL Injection Prevention

**Parameterized Queries (EF Core)**:
```csharp
// ✅ SAFE - EF Core parameterizes automatically
var users = await _db.Users
    .Where(u => u.Email == email)  // Parameterized
    .ToListAsync();

// ❌ UNSAFE - Raw SQL without parameters (NEVER DO THIS)
var users = await _db.Users
    .FromSqlRaw($"SELECT * FROM Users WHERE Email = '{email}'")
    .ToListAsync();
```

**FluentValidation (Input Sanitization)**:
```csharp
public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);  // Prevent oversized input

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128)  // Prevent DOS via huge passwords
            .Matches(@"[A-Z]")   // Uppercase
            .Matches(@"[a-z]")   // Lowercase
            .Matches(@"[0-9]")   // Digit
            .Matches(@"[@$!%*?&]");  // Special char
    }
}
```

**XSS Prevention (Frontend)**:
```typescript
// ✅ React escapes by default
function UserProfile({ name }: { name: string }) {
  return <h1>{name}</h1>;  // Auto-escaped, XSS-safe
}

// ✅ For rich text, use sanitization library
import DOMPurify from 'dompurify';

function RichContent({ html }: { html: string }) {
  const sanitized = DOMPurify.sanitize(html);
  return <div dangerouslySetInnerHTML={{ __html: sanitized }} />;
}
```

### Command Injection Prevention

**Safe External Process Execution**:
```csharp
// ✅ SAFE - No shell interpretation
var process = Process.Start(new ProcessStartInfo
{
    FileName = "pdftotext",
    Arguments = $"-layout \"{escapedFilePath}\"",  // Quoted
    UseShellExecute = false  // Important!
});

// ❌ UNSAFE - Shell injection possible
var process = Process.Start("sh", $"-c pdftotext {filePath}");
```

---

## A04: Insecure Design 🏗️

### Secure Design Patterns

**Threat Modeling**:
- ✅ Authentication context threat model documented
- ✅ RAG system security review (prompt injection prevention)
- ✅ PDF upload size limits (max 50MB)
- ✅ Rate limiting per endpoint

**Defense in Depth**:
```
Layer 1: Firewall (UFW) → Block unauthorized ports
Layer 2: Traefik → SSL/TLS termination, rate limiting
Layer 3: ASP.NET Middleware → Authentication, authorization
Layer 4: FluentValidation → Input validation
Layer 5: Domain Layer → Business rule enforcement
Layer 6: Database → Constraints, triggers
```

**Fail Securely**:
```csharp
// Default deny policy
[Authorize]  // Default: require authentication
public class SecureEndpoints
{
    // Explicit allow for public endpoints
    [AllowAnonymous]
    public async Task<IResult> PublicEndpoint() { }
}
```

---

## A05: Security Misconfiguration ⚙️

### Configuration Hardening

**Security Headers (ADR-010)**:
```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Add(
        "Content-Security-Policy",
        "default-src 'self'; script-src 'self'; style-src 'self'"
    );
    await next();
});
```

**CORS Whitelist (ADR-011)**:
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("Production", policy =>
    {
        policy.WithOrigins("https://meepleai.com", "https://app.meepleai.com")
              .AllowCredentials()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
```

**Secret Validation**:
```csharp
// Startup validation - blocks if critical secrets missing
var secretLoader = new SecretLoader();
var validation = secretLoader.ValidateAllSecrets();
if (validation.CriticalMissing.Any())
    throw new InvalidOperationException($"Critical secrets missing: {string.Join(", ", validation.CriticalMissing)}");
```

---

## A06: Vulnerable and Outdated Components 📦

### Dependency Management

**Automated Scanning**:
```yaml
# .github/workflows/security.yml
- name: Dependency Check
  run: dotnet list package --vulnerable --include-transitive

- name: Semgrep Scan
  run: semgrep --config auto
```

**Dependabot Configuration**:
```yaml
# .github/dependabot.yml
version: 2
updates:
  - package-ecosystem: "nuget"
    directory: "/apps/api"
    schedule:
      interval: "weekly"
    open-pull-requests-limit: 10

  - package-ecosystem: "npm"
    directory: "/apps/web"
    schedule:
      interval: "weekly"
```

**Update Policy**:
- **Critical vulnerabilities**: Patch within 24 hours
- **High vulnerabilities**: Patch within 7 days
- **Medium vulnerabilities**: Patch within 30 days
- **Low vulnerabilities**: Patch in next release

---

## A07: Identification and Authentication Failures 🔓

### Protections Implemented

**Strong Password Policy**:
```csharp
// FluentValidation rules
RuleFor(x => x.Password)
    .MinimumLength(8)
    .Matches(@"[A-Z]")   // Uppercase required
    .Matches(@"[a-z]")   // Lowercase required
    .Matches(@"[0-9]")   // Digit required
    .Matches(@"[@$!%*?&]");  // Special char required
```

**Two-Factor Authentication (TOTP)**:
```csharp
// Self-hosted TOTP (OtpNet library)
var totp = new Totp(secretKey);
var code = totp.ComputeTotp();
var isValid = totp.VerifyTotp(userCode, out _, VerificationWindow.RfcSpecifiedNetworkDelay);
```

**Rate Limiting**:
```csharp
// Token bucket algorithm via Redis
[RateLimit(Requests = 5, WindowMinutes = 15)]  // Max 5 login attempts in 15 min
public async Task<IResult> Login(LoginCommand command) { }
```

**Session Security**:
```csharp
builder.Services.AddAuthentication()
    .AddCookie(options =>
    {
        options.Cookie.HttpOnly = true;      // Prevent XSS
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;  // HTTPS only
        options.Cookie.SameSite = SameSiteMode.Lax;  // CSRF protection
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });
```

---

## A08: Software and Data Integrity Failures 🔏

### Integrity Protections

**Webhook Signature Verification**:
```csharp
public bool VerifyWebhookSignature(string payload, string signature, string secret)
{
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
    var computedSignature = Convert.ToBase64String(hash);

    return CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(signature),
        Encoding.UTF8.GetBytes(computedSignature)
    );
}
```

**File Upload Validation**:
```csharp
// Validate PDF integrity
public async Task<bool> ValidatePdfIntegrityAsync(Stream stream)
{
    using var pdf = PdfDocument.Open(stream);
    return pdf.NumberOfPages > 0 && pdf.NumberOfPages <= 500;  // Sanity check
}
```

---

## A09: Security Logging and Monitoring Failures 📊

### Comprehensive Logging

**Serilog Configuration**:
```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console()
    .WriteTo.Seq("http://localhost:5341")
    .CreateLogger();
```

**Security Event Logging**:
```csharp
// Login attempt logging
_logger.LogInformation("Login attempt for {Email} from {IpAddress}", email, ipAddress);

// Failed login
_logger.LogWarning("Failed login attempt for {Email} from {IpAddress}", email, ipAddress);

// Admin action
_logger.LogInformation("Admin {UserId} deleted user {TargetUserId}", adminId, userId);
```

**Audit Trail**:
```csharp
public async Task CreateAuditLog(AuditLog log)
{
    log.UserId = _currentUser.Id;
    log.IpAddress = _httpContext.Connection.RemoteIpAddress?.ToString();
    log.Timestamp = DateTime.UtcNow;

    await _db.AuditLogs.AddAsync(log);
    await _db.SaveChangesAsync();
}
```

---

## A10: Server-Side Request Forgery (SSRF) 🌐

### SSRF Prevention

**URL Validation**:
```csharp
public bool IsValidUrl(string url)
{
    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        return false;

    // Whitelist allowed schemes
    if (uri.Scheme != "https")
        return false;

    // Block private IP ranges
    var ip = Dns.GetHostAddresses(uri.Host).FirstOrDefault();
    if (ip != null && IsPrivateIp(ip))
        return false;

    return true;
}

private bool IsPrivateIp(IPAddress ip)
{
    var bytes = ip.GetAddressBytes();

    // Block 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16, 127.0.0.0/8
    return (bytes[0] == 10) ||
           (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
           (bytes[0] == 192 && bytes[1] == 168) ||
           (bytes[0] == 127);
}
```

---

## 🧪 Security Testing

### Automated Security Scans

**Semgrep (SAST)**:
```bash
# Run Semgrep with OWASP rules
semgrep --config "p/owasp-top-ten" apps/api/

# Run with custom rules
semgrep --config .semgrep.yml apps/api/
```

**detect-secrets**:
```bash
# Scan for committed secrets
detect-secrets scan --all-files

# Audit findings
detect-secrets audit .secrets.baseline
```

**Dependency Scanning**:
```bash
# .NET vulnerabilities
dotnet list package --vulnerable --include-transitive

# NPM vulnerabilities
pnpm audit

# Fix vulnerabilities
pnpm audit --fix
```

---

## 🔒 Production Security Checklist

### Pre-Deployment

**Infrastructure**:
- [ ] Firewall configured (UFW/iptables)
- [ ] SSH key-only authentication (no passwords)
- [ ] Fail2Ban installed for brute-force protection
- [ ] SSL/TLS certificates valid (Let's Encrypt)
- [ ] Security headers configured (CSP, HSTS, X-Frame-Options)

**Application**:
- [ ] All secrets in `.secret` files (not environment variables)
- [ ] CORS whitelist configured (production domains only)
- [ ] Rate limiting enabled (Redis-backed)
- [ ] Error messages don't leak sensitive info
- [ ] Logging excludes passwords, API keys, tokens

**Database**:
- [ ] Strong database password (20+ chars)
- [ ] Network isolated (not exposed to internet)
- [ ] Backups encrypted and tested
- [ ] Query timeouts configured

### Post-Deployment

**Monitoring**:
- [ ] Grafana alerts configured (failed logins, errors)
- [ ] Prometheus metrics collecting (request rates, latencies)
- [ ] Log aggregation working (Loki)
- [ ] Security scan scheduled (weekly Semgrep)

**Validation**:
- [ ] Penetration test completed
- [ ] Vulnerability scan passed
- [ ] OWASP compliance verified
- [ ] Security headers validated (securityheaders.com)

---

## 📚 Security Resources

### Internal Documentation
- [Secrets Management](../deployment/secrets-management.md)
- [OAuth Testing](../testing/backend/oauth-testing.md)
- [Authentication Context](../bounded-contexts/authentication.md)
- [ADR-010: Security Headers](../architecture/adr/adr-010-security-headers-middleware.md)

### External Resources
- [OWASP Top 10 (2021)](https://owasp.org/Top10/)
- [OWASP Cheat Sheet Series](https://cheatsheetseries.owasp.org/)
- [ASP.NET Core Security](https://learn.microsoft.com/en-us/aspnet/core/security/)
- [BCrypt Best Practices](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html)

### Security Tools
- **Semgrep**: Static analysis (SAST)
- **detect-secrets**: Secret scanning
- **Dependabot**: Dependency updates
- **SecurityHeaders.com**: Header validation
- **SSL Labs**: SSL/TLS configuration test

---

**Last Updated**: 2026-01-18
**Maintainer**: Security Team
**Compliance**: OWASP Top 10 (2021)
**Status**: ✅ Protected
