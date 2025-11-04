---
title: "[SECURITY] Fix Exposure of Sensitive Information (9 instances)"
labels: ["security", "priority-critical", "P0", "code-scanning", "data-exposure"]
---

## Summary

**9 open ERROR alerts** for exposure of sensitive information (passwords, tokens, PII) in logs, error messages, or debug output.

### Impact
- **Severity**: 🔴 **ERROR** (Critical Priority - P0)
- **CWE**: [CWE-532: Insertion of Sensitive Information into Log File](https://cwe.mitre.org/data/definitions/532.html)
- **CWE**: [CWE-200: Exposure of Sensitive Information](https://cwe.mitre.org/data/definitions/200.html)
- **GDPR**: Violation of data minimization and confidentiality principles
- **Risk**: Credential theft, identity theft, compliance violations, data breaches
- **Production Impact**: CRITICAL - Exposed credentials can lead to full system compromise

---

## Problem

Sensitive data is being logged, exposed in error messages, or included in debug output, making it accessible to:
1. **Log aggregators** (Seq, Splunk, CloudWatch)
2. **Error tracking** (Sentry, Application Insights)
3. **Developers** with log access
4. **Attackers** who gain log access

### Types of Sensitive Data

❌ **Never log these**:
- **Passwords** (plaintext, hashed, or encrypted)
- **API keys, tokens, secrets**
- **Credit card numbers** (PCI-DSS violation)
- **SSN, national IDs** (PII)
- **Email addresses** (GDPR consideration)
- **Session tokens, JWTs**
- **OAuth access/refresh tokens**
- **Encryption keys**
- **Database connection strings** (with passwords)
- **User IP addresses** (without consent)
- **Health information** (HIPAA)

### Example Vulnerable Code

```csharp
// ❌ BAD: Password in logs
_logger.LogInformation("User login: {Username}, Password: {Password}", username, password);
// CRITICAL: Password exposed in all log systems!

// ❌ BAD: API key in exception
throw new Exception($"API call failed with key: {apiKey}");
// Exposed in error tracking (Sentry, AppInsights)

// ❌ BAD: JWT token in debug logs
_logger.LogDebug("Validating token: {Token}", jwtToken);
// Token exposed in logs, can be replayed

// ❌ BAD: Connection string with password
_logger.LogInformation("Connecting to: {ConnectionString}", connectionString);
// "Server=db;User=admin;Password=secret123"

// ❌ BAD: Full user object (may contain sensitive fields)
_logger.LogInformation("User data: {@User}", user);
// May expose email, phone, address, etc.

// ❌ BAD: Credit card in error
_logger.LogError("Payment failed for card {CardNumber}", cardNumber);
// PCI-DSS violation!
```

### Secure Solution

```csharp
// ✅ GOOD: Never log passwords
_logger.LogInformation("User login attempt: {Username}", username);
// Password is validated but never logged

// ✅ GOOD: Log only non-sensitive error details
catch (ApiException ex)
{
    _logger.LogError(ex, "API call failed with status {StatusCode}", ex.StatusCode);
    // No API key logged
}

// ✅ GOOD: Mask JWT tokens
_logger.LogDebug("Validating token: {TokenPrefix}...", token?.Substring(0, 10));
// Only prefix logged: "eyJhbGciOi..."

// ✅ GOOD: Redact connection strings
var sanitizedCs = new SqlConnectionStringBuilder(connectionString)
{
    Password = "***REDACTED***"
}.ToString();
_logger.LogInformation("Connecting to: {ConnectionString}", sanitizedCs);

// ✅ GOOD: Log only safe user fields
_logger.LogInformation("User action: {UserId}, {Role}", user.Id, user.Role);
// No PII logged

// ✅ GOOD: Mask credit card (PCI-DSS compliant)
var maskedCard = $"****-****-****-{cardNumber.Substring(12)}";
_logger.LogError("Payment failed for card {MaskedCard}", maskedCard);
```

---

## Data Masking Utility

Create a centralized masking utility:

```csharp
// Infrastructure/Security/DataMasking.cs
public static class DataMasking
{
    /// <summary>
    /// Masks sensitive string, keeping only first/last characters
    /// </summary>
    public static string MaskString(string? value, int visibleChars = 4)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= visibleChars * 2)
            return "***";

        return $"{value[..visibleChars]}...{value[^visibleChars..]}";
    }

    /// <summary>
    /// Masks email address
    /// </summary>
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrEmpty(email) || !email.Contains('@'))
            return "***@***.***";

        var parts = email.Split('@');
        var username = parts[0];
        var domain = parts[1];

        var maskedUsername = username.Length > 2
            ? $"{username[0]}***{username[^1]}"
            : "***";

        return $"{maskedUsername}@{domain}";
    }

    /// <summary>
    /// Masks JWT token (shows only alg/typ from header)
    /// </summary>
    public static string MaskJwt(string? token)
    {
        if (string.IsNullOrEmpty(token))
            return "***";

        // Show first 20 chars (header), hide rest
        return token.Length > 20
            ? $"{token[..20]}...***"
            : "***";
    }

    /// <summary>
    /// Masks credit card number (PCI-DSS compliant)
    /// </summary>
    public static string MaskCreditCard(string? cardNumber)
    {
        if (string.IsNullOrEmpty(cardNumber))
            return "****-****-****-****";

        var digitsOnly = new string(cardNumber.Where(char.IsDigit).ToArray());

        if (digitsOnly.Length < 4)
            return "****-****-****-****";

        var lastFour = digitsOnly[^4..];
        return $"****-****-****-{lastFour}";
    }

    /// <summary>
    /// Redacts password from connection string
    /// </summary>
    public static string RedactConnectionString(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return string.Empty;

        // Handle various formats
        var patterns = new[]
        {
            @"(password|pwd)=[^;]+",
            @"(Password|PWD)=[^;]+",
            @"(password|pwd):\w+",
        };

        var result = connectionString;
        foreach (var pattern in patterns)
        {
            result = Regex.Replace(result, pattern, "$1=***REDACTED***", RegexOptions.IgnoreCase);
        }

        return result;
    }

    /// <summary>
    /// Sanitizes user object for logging (removes sensitive fields)
    /// </summary>
    public static object SanitizeUser(UserEntity user)
    {
        return new
        {
            user.Id,
            user.Role,
            Email = MaskEmail(user.Email),
            user.CreatedAt,
            // Exclude: PasswordHash, TotpSecret, BackupCodes, etc.
        };
    }
}
```

### Usage Examples

```csharp
// Use masking utility in logs
_logger.LogInformation(
    "Token validated for user {UserId}: {TokenPrefix}",
    userId,
    DataMasking.MaskJwt(token)
);

_logger.LogError(
    "Payment failed for card {MaskedCard}",
    DataMasking.MaskCreditCard(cardNumber)
);

_logger.LogInformation(
    "User updated: {@SanitizedUser}",
    DataMasking.SanitizeUser(user)
);
```

---

## Sensitive Data Locations

### 1. Authentication Service

**Files**: `AuthService.cs`, `ApiKeyAuthenticationService.cs`, `OAuthService.cs`

```csharp
// ❌ FIND AND FIX
_logger.LogDebug("Validating password: {Password}", password);
_logger.LogInformation("API Key: {ApiKey}", apiKey);
_logger.LogDebug("OAuth token: {AccessToken}", accessToken);

// ✅ REPLACE WITH
_logger.LogDebug("Validating password for user {UserId}", userId);
_logger.LogInformation("API Key validated: {KeyPrefix}...", apiKey?.Substring(0, 10));
_logger.LogDebug("OAuth token validated for user {UserId}", userId);
```

### 2. User Management

**Files**: `UserManagementService.cs`, `SessionManagementService.cs`

```csharp
// ❌ FIND AND FIX
_logger.LogInformation("User created: {@User}", user);
_logger.LogDebug("Session token: {Token}", sessionToken);

// ✅ REPLACE WITH
_logger.LogInformation("User created: {UserId}, {Role}", user.Id, user.Role);
_logger.LogDebug("Session created for user {UserId}", userId);
```

### 3. External API Calls

**Files**: `LlmService.cs`, `BggApiService.cs`, `N8nConfigService.cs`

```csharp
// ❌ FIND AND FIX
_logger.LogDebug("Calling API with key: {ApiKey}", apiKey);
_logger.LogError(ex, "Request failed: {RequestBody}", requestJson);

// ✅ REPLACE WITH
_logger.LogDebug("Calling external API");
_logger.LogError(ex, "Request failed for endpoint {Endpoint}", endpoint);
```

---

## Automated Detection

### Find Sensitive Data in Logs

```bash
# Find password references in logs
rg -i 'logger\.Log.*password' --glob '*.cs' -n

# Find token references
rg -i 'logger\.Log.*(token|jwt|bearer|apikey|secret)' --glob '*.cs' -n

# Find email logging
rg 'logger\.Log.*email' --glob '*.cs' -i -n

# Find user object logging (may contain PII)
rg 'logger\.Log.*{@User}' --glob '*.cs' -n
```

---

## Remediation Plan

### Phase 1: Critical Secrets (HIGH PRIORITY - 0.5 days)
- [ ] Remove all password logging
- [ ] Remove all API key/token logging
- [ ] Remove all OAuth token logging
- [ ] Implement `DataMasking` utility

### Phase 2: User PII (0.5 days)
- [ ] Mask email addresses
- [ ] Remove full user object logging
- [ ] Sanitize user-related logs

### Phase 3: Payment Data (if applicable)
- [ ] Mask credit card numbers
- [ ] Review payment-related logs

### Phase 4: Connection Strings & Config (0.5 days)
- [ ] Redact database connection strings
- [ ] Redact external service URLs with credentials
- [ ] Review configuration logging

---

## Testing

### Unit Test Template

```csharp
[Fact]
public void DataMasking_MasksCreditCard_Correctly()
{
    // Arrange
    var cardNumber = "1234-5678-9012-3456";

    // Act
    var masked = DataMasking.MaskCreditCard(cardNumber);

    // Assert
    Assert.Equal("****-****-****-3456", masked);
    Assert.DoesNotContain("1234", masked);
    Assert.DoesNotContain("5678", masked);
}

[Fact]
public async Task Login_DoesNotLogPassword()
{
    // Arrange
    var loggerMock = new Mock<ILogger<AuthService>>();
    var service = new AuthService(loggerMock.Object, ...);

    // Act
    await service.LoginAsync("user@example.com", "MyPassword123!");

    // Assert - Verify password never logged
    loggerMock.Verify(
        x => x.Log(
            It.IsAny<LogLevel>(),
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) =>
                !v.ToString().Contains("MyPassword123!")
            ),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception, string>>()
        ),
        Times.AtLeastOnce
    );
}
```

### Security Audit

```bash
# Scan all logs for sensitive patterns
cd apps/api
rg -i 'password.*=|token.*:|secret.*:' --glob '*.cs' | grep -v '//.*password'
```

---

## Prevention Strategy

### 1. Serilog Destructuring Policy

Configure Serilog to automatically redact sensitive properties:

```csharp
// Program.cs
Log.Logger = new LoggerConfiguration()
    .Destructure.ByTransforming<UserEntity>(u => new
    {
        u.Id,
        u.Username,
        Email = DataMasking.MaskEmail(u.Email),
        u.Role
        // Omit: PasswordHash, TotpSecret, etc.
    })
    .WriteTo.Console()
    .WriteTo.Seq(Environment.GetEnvironmentVariable("SEQ_URL"))
    .CreateLogger();
```

### 2. Custom Log Filter

Create middleware to filter sensitive data:

```csharp
public class SensitiveDataLogFilter : ILogEventFilter
{
    private static readonly string[] SensitiveKeys =
    {
        "password", "token", "apikey", "secret", "jwt",
        "bearer", "authorization", "credentials"
    };

    public bool IsEnabled(LogEvent logEvent)
    {
        var message = logEvent.RenderMessage().ToLowerInvariant();
        return !SensitiveKeys.Any(key => message.Contains(key));
    }
}
```

### 3. Code Review Checklist

```markdown
## Security Review - Sensitive Data
- [ ] No passwords in logs
- [ ] No API keys/tokens in logs
- [ ] No email addresses (or masked)
- [ ] No full user objects logged
- [ ] Connection strings redacted
- [ ] Credit card numbers masked (if applicable)
- [ ] All exceptions sanitized before logging
```

### 4. Pre-commit Hook

```bash
#!/bin/bash
# .git/hooks/pre-commit

# Check for sensitive data in logs
ISSUES=$(git diff --cached --name-only | grep '\.cs$' | \
    xargs grep -niE 'logger.*password|logger.*token.*[^Prefix]|logger.*apikey' 2>/dev/null)

if [ ! -z "$ISSUES" ]; then
    echo "❌ ERROR: Possible sensitive data in logs:"
    echo "$ISSUES"
    exit 1
fi
```

---

## Compliance Considerations

### GDPR (EU)
- ✅ Minimize logged personal data
- ✅ Mask email addresses
- ✅ Do not log IP addresses without consent
- ✅ Implement log retention policies

### PCI-DSS (Payment Card Industry)
- ✅ Never log full credit card numbers
- ✅ Never log CVV/CVC codes
- ✅ Mask all but last 4 digits

### HIPAA (Healthcare - if applicable)
- ✅ Never log health information
- ✅ Encrypt logs at rest
- ✅ Restrict log access

---

## Acceptance Criteria

- [ ] All 9 instances fixed (no sensitive data logged)
- [ ] `DataMasking` utility implemented and tested
- [ ] All passwords/tokens/secrets removed from logs
- [ ] Email addresses masked
- [ ] Connection strings redacted
- [ ] Serilog destructuring policy configured
- [ ] Pre-commit hook prevents new violations
- [ ] Security audit completed
- [ ] Compliance review passed (GDPR, PCI-DSS if applicable)

---

## Estimated Effort

- **Total Time**: 1 day (1 developer)
- **Complexity**: Low-Medium (9 instances + utility)
- **Risk**: HIGH impact (prevents data breaches)

### Breakdown
- Phase 1 (Critical): 0.5 days
- Phase 2 (PII): 0.5 days
- Testing & verification: Included above

---

## References

- [CWE-532: Insertion of Sensitive Information into Log File](https://cwe.mitre.org/data/definitions/532.html)
- [CWE-200: Exposure of Sensitive Information](https://cwe.mitre.org/data/definitions/200.html)
- [OWASP Logging Cheat Sheet - Sensitive Data](https://cheatsheetseries.owasp.org/cheatsheets/Logging_Cheat_Sheet.html#data-to-exclude)
- [GDPR Article 5: Data Minimization](https://gdpr-info.eu/art-5-gdpr/)
- [PCI-DSS Requirement 3.4](https://www.pcisecuritystandards.org/)

---

**Priority**: P0 - CRITICAL
**Category**: Security > Data Exposure
**Related Issues**: #[log-forging], #[code-scanning-tracker]
