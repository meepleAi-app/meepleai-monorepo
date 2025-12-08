# Security Patterns & Best Practices

**Version**: 2.0 (Consolidated)
**Last Updated**: 2025-12-08
**Status**: Production Security Standards
**Location**: Consolidated from 6 security remediation guides

---

## Table of Contents

1. [Overview](#overview)
2. [Resource Management Patterns](#resource-management-patterns)
3. [Credential Handling Patterns](#credential-handling-patterns)
4. [Input Validation Patterns](#input-validation-patterns)
5. [Output Encoding Patterns](#output-encoding-patterns)
6. [Error Handling Patterns](#error-handling-patterns)
7. [CodeQL Integration](#codeql-integration)
8. [Security Checklist](#security-checklist)
9. [Related Documentation](#related-documentation)

---

## Overview

Consolidated security patterns for MeepleAI from CodeQL analysis and security audits. These patterns prevent common vulnerabilities and maintain code security standards.

### Security Categories

| Pattern Category | CWE Coverage | Severity | Files Consolidated |
|------------------|--------------|----------|-------------------|
| **Resource Management** | CWE-404, CWE-775 | Medium | disposable-resource-leak-remediation.md |
| **Credential Handling** | CWE-798, CWE-259 | Critical | hardcoded-credentials-remediation.md |
| **Null Safety** | CWE-476 | High | null-reference-remediation.md |
| **Input Validation** | CWE-20, CWE-1287 | Critical | incomplete-sanitization-prevention.md, regex-sanitization-guide.md |
| **Output Encoding** | CWE-117, CWE-93 | Medium | log-forging-prevention.md |

---

## Resource Management Patterns

### Problem: Disposable Resource Leaks

**CWE-404**: Improper Resource Shutdown
**Risk**: Resource exhaustion, memory leaks, file handle exhaustion

### Pattern 1: Always Use `using` Statement

**❌ BAD**:
```csharp
public async Task<string> ReadFileAsync(string path)
{
    var stream = File.OpenRead(path);
    var reader = new StreamReader(stream);
    return await reader.ReadToEndAsync();
    // LEAK: stream and reader never disposed
}
```

**✅ GOOD**:
```csharp
public async Task<string> ReadFileAsync(string path)
{
    using var stream = File.OpenRead(path);
    using var reader = new StreamReader(stream);
    return await reader.ReadToEndAsync();
    // Automatically disposed when method exits
}
```

### Pattern 2: Use IHttpClientFactory

**❌ BAD**:
```csharp
public class MyService
{
    public async Task<string> FetchDataAsync()
    {
        var client = new HttpClient(); // LEAK: New instance every call
        return await client.GetStringAsync("https://api.example.com");
    }
}
```

**✅ GOOD**:
```csharp
public class MyService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public MyService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> FetchDataAsync()
    {
        var client = _httpClientFactory.CreateClient();
        return await client.GetStringAsync("https://api.example.com");
    }
}
```

### Pattern 3: Nested Using Statements

**✅ GOOD**:
```csharp
public async Task ProcessPdfAsync(Stream pdfStream)
{
    using var memoryStream = new MemoryStream();
    await pdfStream.CopyToAsync(memoryStream);

    using var document = PdfDocument.Open(memoryStream);
    using var page = document.GetPage(1);

    // All disposed in reverse order
}
```

### Common Disposable Types

**Always dispose**:
- `Stream` (FileStream, MemoryStream, etc.)
- `StreamReader`, `StreamWriter`
- `HttpClient` (use IHttpClientFactory instead)
- `SqlConnection`, `DbContext`
- `BitmapImage`, `Image`
- Custom classes implementing `IDisposable`

---

## Credential Handling Patterns

### Problem: Hardcoded Credentials

**CWE-798**: Use of Hard-coded Credentials
**Risk**: Credential exposure, unauthorized access, compliance violations

### Pattern 1: Use Configuration/Secrets

**❌ BAD**:
```csharp
// Hardcoded password
var connectionString = "Server=localhost;Database=meepleai;User=admin;Password=SuperSecret123!";
```

**✅ GOOD**:
```csharp
// From configuration
var connectionString = _configuration.GetConnectionString("Postgres");

// From environment variable
var apiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");

// From Docker secrets
var password = File.ReadAllText("/run/secrets/postgres-password");
```

### Pattern 2: User Secrets (Development)

**Setup**:
```bash
# Initialize user secrets
dotnet user-secrets init

# Set secret
dotnet user-secrets set "OpenRouter:ApiKey" "your-key-here"
```

**Usage**:
```csharp
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Automatically loads user secrets in development
        services.Configure<OpenRouterOptions>(Configuration.GetSection("OpenRouter"));
    }
}
```

### Pattern 3: Never Log Credentials

**❌ BAD**:
```csharp
_logger.LogInformation("Connecting with password: {Password}", password);
_logger.LogDebug("API Key: {ApiKey}", apiKey);
```

**✅ GOOD**:
```csharp
_logger.LogInformation("Connecting to database");
_logger.LogDebug("API request authenticated"); // Don't log key
```

### Credential Storage

| Environment | Method | Location |
|-------------|--------|----------|
| **Development** | User Secrets | `~/.microsoft/usersecrets/` |
| **Staging** | Docker Secrets | `./secrets/staging/` |
| **Production** | Docker Secrets | `./secrets/prod/` |
| **CI/CD** | GitHub Secrets | GitHub Actions encrypted |

See: [Environment Variables Guide](./environment-variables-production.md)

---

## Input Validation Patterns

### Problem: Incomplete Sanitization

**CWE-20**: Improper Input Validation
**CWE-1287**: Improper Validation of Specified Type
**Risk**: SQL injection, XSS, command injection

### Pattern 1: Validate All User Input

**❌ BAD**:
```csharp
public IActionResult GetGame(string gameId)
{
    // No validation
    var game = _repository.GetById(gameId);
    return Ok(game);
}
```

**✅ GOOD**:
```csharp
public IActionResult GetGame(string gameId)
{
    // Validate format
    if (!Guid.TryParse(gameId, out var parsedId))
    {
        return BadRequest("Invalid game ID format");
    }

    var game = _repository.GetById(parsedId);
    if (game == null)
    {
        return NotFound();
    }

    return Ok(game);
}
```

### Pattern 2: Use FluentValidation

**✅ GOOD**:
```csharp
public class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(255);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Matches(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])")
            .WithMessage("Password must contain uppercase, lowercase, digit, and special char");

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(100)
            .Matches(@"^[a-zA-Z0-9\s_-]+$")
            .WithMessage("Display name contains invalid characters");
    }
}
```

See: [ADR-012: FluentValidation CQRS](../01-architecture/adr/adr-012-fluentvalidation-cqrs.md)

### Pattern 3: Regex Sanitization

**Problem**: Incomplete regex validation allows bypass

**❌ BAD**:
```csharp
// Incomplete: Allows "user@evil.com.attacker.com"
var emailRegex = new Regex(@"^\w+@\w+\.\w+");
```

**✅ GOOD**:
```csharp
// Complete: Anchors prevent bypass
var emailRegex = new Regex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$");

// Or use built-in
var addr = new MailAddress(email); // Throws if invalid
```

**Regex Security Checklist**:
- ✅ Use `^` and `$` anchors
- ✅ Validate complete string, not substring
- ✅ Avoid overly permissive patterns (`.+`, `.*`)
- ✅ Test with malicious inputs

**Common Bypasses**:
```
Pattern: ^\w+@\w+\.\w+
Bypass: attacker@evil.com.fake.com  ✗

Pattern: @example\.com
Bypass: attacker@evil.com@example.com  ✗

Pattern: ^[a-z]+$
Bypass: UPPERCASE  ✗ (case-sensitive)
```

### Pattern 4: File Upload Validation

**✅ GOOD**:
```csharp
public async Task<IActionResult> UploadPdf(IFormFile file)
{
    // 1. Validate file exists
    if (file == null || file.Length == 0)
    {
        return BadRequest("No file provided");
    }

    // 2. Validate file size (50MB max)
    if (file.Length > 50 * 1024 * 1024)
    {
        return BadRequest("File too large (max 50MB)");
    }

    // 3. Validate extension
    var allowedExtensions = new[] { ".pdf" };
    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (!allowedExtensions.Contains(extension))
    {
        return BadRequest("Only PDF files allowed");
    }

    // 4. Validate MIME type
    if (file.ContentType != "application/pdf")
    {
        return BadRequest("Invalid file type");
    }

    // 5. Validate actual content (magic bytes)
    using var stream = file.OpenReadStream();
    var header = new byte[4];
    await stream.ReadAsync(header);
    if (!header.SequenceEqual(new byte[] { 0x25, 0x50, 0x44, 0x46 })) // %PDF
    {
        return BadRequest("File is not a valid PDF");
    }

    // Process file...
}
```

**Defense in Depth**:
1. Extension check (basic)
2. MIME type check (client-provided)
3. Magic bytes check (content verification)
4. Virus scan (if applicable)
5. Size limit (DoS prevention)

---

## Output Encoding Patterns

### Problem: Log Forging

**CWE-117**: Improper Output Neutralization for Logs
**Risk**: Log injection, log tampering, misleading audit trails

### Pattern 1: Sanitize User Input Before Logging

**❌ BAD**:
```csharp
_logger.LogInformation("User {Email} logged in", userInput);
// If userInput = "admin\nHACKED: Admin logged in"
// Creates fake log entry!
```

**✅ GOOD**:
```csharp
var sanitizedEmail = userInput.Replace("\n", "").Replace("\r", "");
_logger.LogInformation("User {Email} logged in", sanitizedEmail);

// Or use structured logging (auto-escapes)
_logger.LogInformation("User logged in: {Email}", new { Email = userInput });
```

### Pattern 2: Use Structured Logging

**✅ GOOD (Serilog)**:
```csharp
_logger.LogInformation(
    "Login attempt for {Email} from {IpAddress} at {Timestamp}",
    email,      // Auto-escaped
    ipAddress,  // Auto-escaped
    DateTime.UtcNow
);
```

**Output** (JSON, safe):
```json
{
  "Message": "Login attempt for admin@example.com from 192.168.1.1",
  "Email": "admin@example.com",
  "IpAddress": "192.168.1.1",
  "Timestamp": "2025-12-08T10:30:00Z"
}
```

### Pattern 3: Validate Log Inputs

**✅ GOOD**:
```csharp
public void LogUserAction(string userInput)
{
    // Validate before logging
    if (string.IsNullOrWhiteSpace(userInput) || userInput.Length > 200)
    {
        _logger.LogWarning("Invalid user input for logging (length: {Length})", userInput?.Length ?? 0);
        return;
    }

    // Sanitize
    var sanitized = Regex.Replace(userInput, @"[\r\n]", "");

    _logger.LogInformation("User action: {Action}", sanitized);
}
```

---

## Error Handling Patterns

### Problem: Null Reference Exceptions

**CWE-476**: NULL Pointer Dereference
**Risk**: Application crashes, DoS, data corruption

### Pattern 1: Null-Conditional Operator

**❌ BAD**:
```csharp
public string GetUserEmail(User user)
{
    return user.Email; // NullReferenceException if user is null
}
```

**✅ GOOD**:
```csharp
public string? GetUserEmail(User? user)
{
    return user?.Email; // Returns null safely
}
```

### Pattern 2: Null Checks with Early Return

**✅ GOOD**:
```csharp
public async Task<IActionResult> GetGame(Guid gameId)
{
    var game = await _repository.GetByIdAsync(gameId);
    if (game == null)
    {
        return NotFound($"Game {gameId} not found");
    }

    return Ok(game); // game guaranteed non-null here
}
```

### Pattern 3: Nullable Reference Types

**✅ GOOD** (C# 9+):
```csharp
#nullable enable

public class GameService
{
    // Compiler enforces null checks
    public Game? FindGame(Guid id)  // May return null
    {
        return _repository.Find(id);
    }

    public Game GetGame(Guid id)  // Never returns null
    {
        return _repository.Find(id) ?? throw new NotFoundException();
    }
}
```

**Enable in .csproj**:
```xml
<PropertyGroup>
  <Nullable>enable</Nullable>
</PropertyGroup>
```

---

## CodeQL Integration

### Suppressing False Positives

**When to Suppress**:
- ✅ Verified false positive (with documentation)
- ✅ Accepted risk (with justification)
- ✅ CodeQL limitation (known issue)

**How to Suppress**:

```csharp
// Suppress with explanation
#pragma warning disable CA1062 // Validate parameter is non-null
public void ProcessData(string data)
{
    // Justification: data is validated by FluentValidation before this method
    var processed = data.ToUpper();
}
#pragma warning restore CA1062
```

**Documentation Required**:
```csharp
// SECURITY: False positive suppression
// CodeQL: CWE-476 (Null Reference)
// Justification: Parameter validated by middleware before controller action
// Verified: 2025-12-08
// Reviewer: Security Team
#pragma warning disable CA1062
```

See: [CodeQL False Positive Management](./codeql-false-positive-management.md)

### Common False Positives

| CodeQL Rule | Common FP Scenario | Mitigation |
|-------------|-------------------|------------|
| **Null Reference** | Parameter validated by middleware | Document validation flow |
| **Resource Leak** | Disposed by DI container | Add comment explaining lifecycle |
| **SQL Injection** | Using parameterized queries | Use EF Core or Dapper |
| **Hardcoded Credentials** | Example/test data | Move to test fixtures |

---

## Security Checklist

### Code Review Checklist

**Resource Management**:
- [ ] All `IDisposable` objects disposed (`using` statement)
- [ ] `IHttpClientFactory` used instead of `new HttpClient()`
- [ ] Database connections properly closed
- [ ] File handles released

**Credential Handling**:
- [ ] No hardcoded passwords, API keys, or tokens
- [ ] Credentials loaded from configuration/secrets
- [ ] Sensitive data never logged
- [ ] Environment-specific credentials (dev/staging/prod)

**Input Validation**:
- [ ] All user input validated (length, format, type)
- [ ] FluentValidation rules for DTOs
- [ ] Regex patterns use anchors (`^`, `$`)
- [ ] File uploads validated (size, type, content)

**Output Encoding**:
- [ ] User input sanitized before logging
- [ ] Structured logging used (Serilog)
- [ ] HTML output encoded (Razor auto-encodes)
- [ ] JSON responses use serializer (not string concat)

**Null Safety**:
- [ ] Nullable reference types enabled
- [ ] Null checks for all nullable parameters
- [ ] Early returns for null cases
- [ ] Use null-conditional operators (`?.`, `??`)

**Authentication & Authorization**:
- [ ] All endpoints require authentication (except public)
- [ ] Authorization policies enforced
- [ ] Session management secure (httpOnly, secure cookies)
- [ ] API keys validated and rate-limited

See: [Security Testing Strategy](./security-testing-strategy.md)

---

## Related Documentation

### Security Policies & Strategies
- **[SECURITY.md](../../SECURITY.md)** - Security policy and reporting
- **[Security Testing Strategy](./security-testing-strategy.md)** - Comprehensive testing
- **[OAuth Security](./oauth-security.md)** - OAuth implementation security
- **[2FA Security Assessment](./2fa-security-assessment-issue-576.md)** - 2FA security (Issue #576)

### CodeQL & Scanning
- **[CodeQL False Positive Management](./codeql-false-positive-management.md)** - Managing FPs
- **[Code Scanning Remediation Summary](./code-scanning-remediation-summary.md)** - Historical fixes
- **[CodeQL C# Status](./codeql-csharp-status-2025-11-18.md)** - Current status

### Configuration & Deployment
- **[Environment Variables Production](./environment-variables-production.md)** - Production secrets
- **[Security Headers](./security-headers.md)** - HTTP security headers (ADR-010)
- **[CORS Configuration](../03-api/cors-configuration.md)** - CORS whitelist (ADR-011)

### Architecture
- **[Security Testing](../02-development/testing/specialized/security-testing.md)** - Security test patterns
- **[ADR-010: Security Headers](../01-architecture/adr/adr-010-security-headers-middleware.md)** - Headers middleware
- **[ADR-011: CORS Whitelist](../01-architecture/adr/adr-011-cors-whitelist-headers.md)** - CORS policy

---

## Quick Reference

### Common Security Violations

| Violation | Pattern | Fix |
|-----------|---------|-----|
| **Resource Leak** | `new HttpClient()` | Use `IHttpClientFactory` |
| **Hardcoded Password** | `Password="secret"` | Use configuration |
| **Log Injection** | `Log(userInput)` | Sanitize `\n`, `\r` |
| **Null Reference** | `user.Email` | `user?.Email` |
| **SQL Injection** | String concat SQL | Use EF Core or parameters |
| **XSS** | Unencoded HTML | Use Razor (auto-encodes) |

### Secure Coding Standards

**DO**:
- ✅ Validate all input
- ✅ Use parameterized queries
- ✅ Dispose IDisposable objects
- ✅ Load secrets from configuration
- ✅ Enable nullable reference types
- ✅ Use structured logging

**DON'T**:
- ❌ Trust user input
- ❌ Hardcode credentials
- ❌ Ignore null checks
- ❌ Create new HttpClient instances
- ❌ Log sensitive data
- ❌ Disable security features

---

## Changelog

### 2025-12-08: Documentation Consolidation

**Changes**:
- ✅ Consolidated 6 security remediation guides:
  - disposable-resource-leak-remediation.md
  - hardcoded-credentials-remediation.md
  - null-reference-remediation.md
  - log-forging-prevention.md
  - incomplete-sanitization-prevention.md
  - regex-sanitization-guide.md
- ✅ Organized by pattern category (Resource, Credential, Validation, Encoding, Error)
- ✅ Added comprehensive code examples
- ✅ Added security checklist for code review
- ✅ Added quick reference table
- ✅ Updated all cross-references

---

**Version**: 2.0 (Post-Consolidation)
**Last Updated**: 2025-12-08
**Patterns Covered**: 20+ security patterns
**CWE Coverage**: CWE-404, CWE-798, CWE-476, CWE-117, CWE-20, CWE-1287
**Documentation**: Single comprehensive security patterns guide
