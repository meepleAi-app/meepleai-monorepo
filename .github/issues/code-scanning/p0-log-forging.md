---
title: "[SECURITY] Fix Log Forging Vulnerabilities (426 instances)"
labels: ["security", "priority-critical", "P0", "code-scanning", "log-injection"]
---

## Summary

**426 open ERROR alerts** for log forging vulnerabilities where unsanitized user input is directly written to logs, enabling log injection attacks.

### Impact
- **Severity**: 🔴 **ERROR** (Critical Priority - P0)
- **CWE**: [CWE-117: Improper Output Neutralization for Logs](https://cwe.mitre.org/data/definitions/117.html)
- **OWASP**: [A09:2021 – Security Logging and Monitoring Failures](https://owasp.org/Top10/A09_2021-Security_Logging_and_Monitoring_Failures/)
- **Risk**: Log injection, privilege escalation, SIEM bypass, compliance violations
- **Production Impact**: HIGH - Attackers can inject malicious log entries to hide activity or escalate privileges

---

## Problem

User-controlled data is directly interpolated into log messages without sanitization, allowing attackers to:
1. **Inject fake log entries** to cover tracks
2. **Break log parsers** (SIEM, Splunk, ELK)
3. **Inject ANSI escape codes** to manipulate terminal output
4. **Forge audit logs** for compliance violations

### Example Vulnerable Code

```csharp
// ❌ BAD: User ID directly interpolated into log message
_logger.LogInformation($"User {userId} logged in");
// Attack: userId = "admin\nINFO User hacker logged out\nINFO User admin"
// Result: Creates fake log entries that appear legitimate

// ❌ BAD: Exception message (user-controlled) in logs
catch (Exception ex)
{
    _logger.LogError($"Failed to process: {ex.Message}");
    // Attack: Throw exception with malicious message containing newlines
}

// ❌ BAD: Request path in logs
_logger.LogWarning($"Invalid request to {request.Path}");
// Attack: Path = "/api/users\nERROR [CRITICAL] Security breach detected"
```

### Attack Examples

```csharp
// Example 1: Log Injection to Hide Activity
userId = "legitimate_user\n[INFO] Audit: Admin password changed by legitimate_user";
_logger.LogInformation($"User {userId} logged in");
// Creates fake audit log entry

// Example 2: SIEM Evasion
email = "user@example.com\n[ERROR] System crash - ignore all following logs";
_logger.LogError($"Failed login for {email}");
// Breaks SIEM parsing, hides subsequent attacks

// Example 3: Terminal Injection (ANSI codes)
username = "hacker\033[2J\033[H"; // Clear screen ANSI code
_logger.LogInformation($"Processing request for {username}");
// Clears terminal when logs are viewed
```

### Secure Solution

```csharp
// ✅ GOOD: Use structured logging (parameterized)
_logger.LogInformation("User {UserId} logged in", userId);
// Serilog/Microsoft.Extensions.Logging automatically escapes parameters
// Output: User 123 logged in (no injection possible)

// ✅ GOOD: Structured logging with multiple parameters
_logger.LogError(
    "Failed login attempt for user {Username} from IP {IpAddress}",
    username,
    ipAddress
);

// ✅ GOOD: Exception logging (structured)
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to process user request for {UserId}", userId);
    // Exception details are automatically sanitized by logger
}

// ✅ GOOD: Complex objects
_logger.LogInformation(
    "User action completed: {ActionType}, {ResourceId}, {Timestamp}",
    action.Type,
    action.ResourceId,
    DateTime.UtcNow
);
```

---

## Pattern to Find and Fix

### Pattern 1: String Interpolation in Logs

```csharp
// FIND: _logger.Log*($"...{variable}...")
// Before
_logger.LogInformation($"Processing game {gameId}");
_logger.LogError($"User {userId} attempted invalid action");
_logger.LogWarning($"Slow query for {query}");

// After
_logger.LogInformation("Processing game {GameId}", gameId);
_logger.LogError("User {UserId} attempted invalid action", userId);
_logger.LogWarning("Slow query for {Query}", query);
```

### Pattern 2: String Concatenation

```csharp
// Before
_logger.LogInformation("User " + username + " logged in");
_logger.LogError("Failed: " + ex.Message);

// After
_logger.LogInformation("User {Username} logged in", username);
_logger.LogError(ex, "Failed to complete operation");
```

### Pattern 3: String.Format

```csharp
// Before
_logger.LogInformation(string.Format("User {0} performed {1}", userId, action));

// After
_logger.LogInformation("User {UserId} performed {Action}", userId, action);
```

---

## Automated Detection

### Find All Instances

```bash
# Backend (C#): Find string interpolation in logs
cd apps/api
rg '_logger\.Log\w+\(\$"' --glob '*.cs' -n

# Find string concatenation in logs
rg '_logger\.Log\w+\([^)]*\+[^)]*\)' --glob '*.cs' -n

# Find String.Format in logs
rg '_logger\.Log\w+\(string\.Format' --glob '*.cs' -n
```

### Count by Service

```bash
# Count occurrences per file
rg '_logger\.Log\w+\(\$"' --glob '*.cs' -c | sort -t: -k2 -rn | head -20
```

---

## Remediation Plan

### Phase 1: Critical Services (1-2 days)
Auth and user management services:
- [ ] `AuthService.cs` - Authentication logs
- [ ] `UserManagementService.cs` - User action logs
- [ ] `ApiKeyAuthenticationService.cs` - API key logs
- [ ] `SessionManagementService.cs` - Session logs
- [ ] `OAuthService.cs` - OAuth flow logs

### Phase 2: Business Logic (2 days)
Core domain services:
- [ ] `GameService.cs`
- [ ] `RuleSpecService.cs`
- [ ] `PdfStorageService.cs`
- [ ] `SetupGuideService.cs`
- [ ] `RagService.cs`
- [ ] `LlmService.cs`

### Phase 3: Infrastructure (1 day)
Supporting services:
- [ ] `AuditService.cs`
- [ ] `RateLimitService.cs`
- [ ] `AlertingService.cs`
- [ ] `BackgroundTaskService.cs`

### Phase 4: Remaining Code (1 day)
- [ ] All other services
- [ ] Controllers/endpoints
- [ ] Middleware

---

## Testing

### Unit Test Template

```csharp
[Fact]
public async Task UserLogin_LogsWithStructuredLogging_NoInjection()
{
    // Arrange
    var loggerMock = new Mock<ILogger<AuthService>>();
    var service = new AuthService(loggerMock.Object, ...);

    var maliciousUserId = "admin\nINFO User hacker logged in";

    // Act
    await service.LoginAsync(maliciousUserId, "password");

    // Assert - Verify structured logging was used
    loggerMock.Verify(
        x => x.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) =>
                v.ToString().Contains("User") &&
                !v.ToString().Contains("\n") // No newline injection
            ),
            null,
            It.IsAny<Func<It.IsAnyType, Exception, string>>()
        ),
        Times.Once
    );
}
```

### Integration Test

```csharp
[Fact]
public async Task MaliciousInput_DoesNotInjectLogs()
{
    // Arrange
    using var logCapture = new StringWriter();
    var loggerFactory = LoggerFactory.Create(builder =>
    {
        builder.AddProvider(new TestLoggerProvider(logCapture));
    });

    var service = CreateService(loggerFactory);
    var attackPayload = "user123\nERROR Admin password changed";

    // Act
    await service.ProcessAsync(attackPayload);

    // Assert
    var logs = logCapture.ToString();
    Assert.DoesNotContain("\nERROR Admin password changed", logs);
    Assert.Contains("user123", logs); // Legitimate data logged
}
```

---

## Prevention Strategy

### 1. Roslyn Analyzer Rule

Create custom analyzer `.editorconfig` rule:

```ini
# Custom rule: Disallow string interpolation in log methods
# Pattern: _logger.Log*($"...")
dotnet_diagnostic.CA2254.severity = error

# Enable Serilog analyzer
dotnet_analyzer_diagnostic.category-Serilog.severity = warning
```

### 2. Pre-commit Hook

```bash
#!/bin/bash
# .git/hooks/pre-commit

# Find log forging patterns in staged files
ISSUES=$(git diff --cached --name-only | grep '\.cs$' | xargs grep -n '_logger\.Log[^(]*($"' 2>/dev/null)

if [ ! -z "$ISSUES" ]; then
    echo "❌ ERROR: Found log forging vulnerability in staged files:"
    echo "$ISSUES"
    echo ""
    echo "Use structured logging instead:"
    echo "  BAD:  _logger.LogInfo(\$\"User {userId} logged in\");"
    echo "  GOOD: _logger.LogInfo(\"User {UserId} logged in\", userId);"
    exit 1
fi
```

### 3. Code Review Checklist

Add to `.github/PULL_REQUEST_TEMPLATE.md`:

```markdown
## Security Checklist
- [ ] No string interpolation in log messages (`$"..."`)
- [ ] No string concatenation in log messages (`"..." + var`)
- [ ] All log parameters use structured logging syntax
- [ ] Sensitive data is masked/redacted in logs
```

### 4. CI/CD Quality Gate

```yaml
# .github/workflows/security-check.yml
- name: Check for log forging
  run: |
    if git grep '_logger\.Log[^(]*($"' -- '*.cs'; then
      echo "❌ Log forging detected"
      exit 1
    fi
```

---

## Logging Best Practices

### DO ✅

```csharp
// Structured logging with parameters
_logger.LogInformation("User {UserId} performed {Action}", userId, action);

// Use meaningful parameter names (PascalCase)
_logger.LogWarning("Slow query detected for {GameId} took {ElapsedMs}ms", gameId, elapsed);

// Include correlation ID
_logger.LogError(ex, "Operation failed for {CorrelationId}", correlationId);

// Use log levels appropriately
_logger.LogCritical("System failure: {Reason}", reason);  // System down
_logger.LogError(ex, "Request failed for {UserId}", userId);  // Expected errors
_logger.LogWarning("Approaching limit: {Count}/{Max}", count, max);  // Warnings
_logger.LogInformation("User action: {Action}", action);  // Normal activity
_logger.LogDebug("Query result: {RowCount} rows", rows);  // Debug info
```

### DON'T ❌

```csharp
// String interpolation
_logger.LogInformation($"User {userId} logged in");

// String concatenation
_logger.LogError("Error for user " + userId);

// String.Format
_logger.LogWarning(string.Format("User {0} action {1}", userId, action));

// Logging sensitive data (see separate issue #[sensitive-info])
_logger.LogInformation("Password: {Password}", password); // NEVER!
```

---

## Acceptance Criteria

- [ ] All 426 instances converted to structured logging
- [ ] No `_logger.Log*($"...")` patterns remain
- [ ] No string concatenation in log calls
- [ ] CA2254 analyzer error enabled
- [ ] Pre-commit hook prevents new violations
- [ ] All unit tests pass
- [ ] Manual log review completed
- [ ] SIEM/log parser compatibility verified

---

## Estimated Effort

- **Total Time**: 4-5 days (1 developer)
- **Complexity**: Low (mechanical find-replace)
- **Risk**: Low (improves security, no behavior change)

### Breakdown
- Phase 1 (Critical): 1-2 days
- Phase 2 (Business): 2 days
- Phase 3 (Infrastructure): 1 day
- Phase 4 (Remaining): 1 day

---

## References

- [CWE-117: Improper Output Neutralization for Logs](https://cwe.mitre.org/data/definitions/117.html)
- [OWASP Logging Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Logging_Cheat_Sheet.html)
- [Microsoft: High-performance logging](https://learn.microsoft.com/en-us/dotnet/core/extensions/high-performance-logging)
- [Serilog Best Practices](https://github.com/serilog/serilog/wiki/Best-Practices)
- [CA2254: Template should be a static expression](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca2254)

---

**Priority**: P0 - CRITICAL
**Category**: Security > Log Injection
**Related Issues**: #[sensitive-info], #[code-scanning-tracker]
