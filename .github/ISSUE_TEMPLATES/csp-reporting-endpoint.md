# 🔐 [LOW] Implement CSP Violation Reporting Endpoint

## Summary

Implement a CSP (Content-Security-Policy) violation reporting endpoint to monitor and analyze security policy violations in production, enabling proactive security monitoring and policy refinement.

**Related**: #1447 (SecurityHeadersMiddleware implementation)
**Priority**: 🟢 LOW
**Complexity**: Low-Medium
**Estimated Time**: 1 day

## Current State

The current CSP policy has no violation reporting mechanism:
```
Content-Security-Policy: default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; ...
```

**Missing**:
- No `report-uri` directive
- No `report-to` directive (CSP Level 3)
- No violation monitoring
- No alerting on policy violations

## Proposed Solution

### 1. CSP Reporting Endpoint

Implement API endpoint to receive CSP violation reports:

```csharp
[ApiController]
[Route("api/v1/csp")]
public class CspReportingController : ControllerBase
{
    private readonly ILogger<CspReportingController> _logger;
    private readonly ICspViolationService _cspService;

    [HttpPost("report")]
    [AllowAnonymous]
    public async Task<IActionResult> ReportViolation([FromBody] CspViolationReport report)
    {
        // Validate report
        if (report == null || string.IsNullOrEmpty(report.DocumentUri))
        {
            return BadRequest("Invalid CSP violation report");
        }

        // Log violation
        _logger.LogWarning(
            "CSP Violation: {ViolatedDirective} on {DocumentUri}. Blocked: {BlockedUri}, Source: {SourceFile}:{LineNumber}",
            report.ViolatedDirective,
            report.DocumentUri,
            report.BlockedUri,
            report.SourceFile,
            report.LineNumber);

        // Store violation for analysis
        await _cspService.RecordViolationAsync(report);

        // Alert if critical violation
        if (IsCriticalViolation(report))
        {
            await _cspService.SendAlertAsync(report);
        }

        return Ok();
    }

    private bool IsCriticalViolation(CspViolationReport report)
    {
        // Critical if inline-script or eval attempted
        return report.ViolatedDirective?.Contains("script-src") == true &&
               (report.BlockedUri?.Contains("inline") == true ||
                report.BlockedUri?.Contains("eval") == true);
    }
}
```

### 2. CSP Violation Model

```csharp
public class CspViolationReport
{
    [JsonPropertyName("document-uri")]
    public string DocumentUri { get; set; } = string.Empty;

    [JsonPropertyName("violated-directive")]
    public string ViolatedDirective { get; set; } = string.Empty;

    [JsonPropertyName("blocked-uri")]
    public string BlockedUri { get; set; } = string.Empty;

    [JsonPropertyName("source-file")]
    public string? SourceFile { get; set; }

    [JsonPropertyName("line-number")]
    public int? LineNumber { get; set; }

    [JsonPropertyName("column-number")]
    public int? ColumnNumber { get; set; }

    [JsonPropertyName("status-code")]
    public int StatusCode { get; set; }

    [JsonPropertyName("effective-directive")]
    public string? EffectiveDirective { get; set; }
}
```

### 3. Update CSP Policy

Add reporting directives to SecurityHeadersOptions:

```csharp
public class SecurityHeadersOptions
{
    // ... existing properties ...

    /// <summary>
    /// CSP report endpoint URI (CSP Level 2 - deprecated but widely supported)
    /// </summary>
    public string? CspReportUri { get; set; }

    /// <summary>
    /// CSP report-to group name (CSP Level 3 - modern browsers)
    /// </summary>
    public string? CspReportToGroup { get; set; }
}
```

**Updated CSP Policy**:
```
Content-Security-Policy:
    default-src 'self';
    script-src 'self' 'unsafe-inline' 'unsafe-eval';
    ...
    report-uri /api/v1/csp/report;
    report-to csp-endpoint
```

### 4. Report-To Header (CSP Level 3)

```csharp
// In SecurityHeadersMiddleware
if (!string.IsNullOrEmpty(_options.CspReportToGroup))
{
    var reportTo = new
    {
        group = _options.CspReportToGroup,
        max_age = 86400, // 24 hours
        endpoints = new[]
        {
            new { url = _options.CspReportUri }
        }
    };

    headers.Append("Report-To", JsonSerializer.Serialize(reportTo));
}
```

## Implementation Tasks

### Backend

- [ ] Create `CspReportingController` with POST endpoint
- [ ] Create `CspViolationReport` model with JSON serialization
- [ ] Implement `ICspViolationService` for violation storage
- [ ] Add database table for CSP violations (optional, can use logs only)
- [ ] Update `SecurityHeadersOptions` with `CspReportUri` and `CspReportToGroup`
- [ ] Update `SecurityHeadersMiddleware` to append reporting directives
- [ ] Implement violation alerting (email/Slack) for critical violations
- [ ] Add rate limiting to prevent DoS via fake reports

### Database (Optional)

```sql
CREATE TABLE csp_violations (
    id SERIAL PRIMARY KEY,
    document_uri VARCHAR(2048) NOT NULL,
    violated_directive VARCHAR(255) NOT NULL,
    blocked_uri VARCHAR(2048),
    source_file VARCHAR(2048),
    line_number INT,
    column_number INT,
    status_code INT,
    user_agent TEXT,
    ip_address INET,
    created_at TIMESTAMP DEFAULT NOW(),
    INDEX idx_violated_directive (violated_directive),
    INDEX idx_created_at (created_at)
);
```

### Testing

- [ ] Unit tests for `CspReportingController`
- [ ] Unit tests for `CspViolationService`
- [ ] Integration tests for CSP violation reporting
- [ ] Test rate limiting of reporting endpoint
- [ ] Test alerting for critical violations
- [ ] E2E test triggering CSP violation and verifying report

### Monitoring Dashboard (Optional Enhancement)

- [ ] Create admin dashboard to view CSP violations
- [ ] Aggregate violations by directive, page, browser
- [ ] Trend analysis (violations over time)
- [ ] Export violations to CSV for analysis

### Documentation

- [ ] Update `docs/06-security/security-headers.md` with reporting section
- [ ] Document how to monitor CSP violations
- [ ] Add troubleshooting guide for common violations
- [ ] Update CLAUDE.md with CSP reporting feature

## Acceptance Criteria

✅ CSP reporting endpoint `/api/v1/csp/report` implemented
✅ Endpoint accepts POST requests with CSP violation reports
✅ Violations logged with detailed context
✅ Critical violations trigger alerts
✅ CSP policy includes `report-uri` directive
✅ Support for both `report-uri` (CSP Level 2) and `report-to` (CSP Level 3)
✅ Rate limiting prevents abuse
✅ Tests written with ≥90% coverage
✅ Documentation complete

## Configuration Example

```json
{
  "SecurityHeaders": {
    "CspPolicy": "default-src 'self'; script-src 'self' 'unsafe-inline'; report-uri /api/v1/csp/report",
    "CspReportUri": "/api/v1/csp/report",
    "CspReportToGroup": "csp-endpoint"
  },
  "CspReporting": {
    "EnableAlerting": true,
    "AlertEmail": "security@meepleai.dev",
    "AlertSlackWebhook": "https://hooks.slack.com/...",
    "CriticalViolationThreshold": 10
  }
}
```

## Benefits

- **Proactive Monitoring**: Detect XSS attempts and policy violations in real-time
- **Policy Refinement**: Identify legitimate resources blocked by overly strict policies
- **Security Insights**: Understand attack patterns and threats
- **Compliance**: Meet security audit requirements for violation tracking

## References

- [MDN CSP report-uri](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Content-Security-Policy/report-uri)
- [MDN Report-To](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Content-Security-Policy/report-to)
- [W3C CSP Level 3](https://www.w3.org/TR/CSP3/)
- [OWASP CSP Best Practices](https://cheatsheetseries.owasp.org/cheatsheets/Content_Security_Policy_Cheat_Sheet.html)

---

**Labels**: `security`, `enhancement`, `priority:low`, `csp`, `monitoring`
**Blocked by**: None
**Blocks**: None
