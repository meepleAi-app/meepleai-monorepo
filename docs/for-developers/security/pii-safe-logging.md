# PII-Safe Logging Conventions

**Status**: active — enforced by CodeQL Models-as-Data pack + `Security Scan` workflow on every PR/push to `main-dev`/`main-staging`/`main`, with in-IDE feedback via the `Api.Analyzers.NoPiiInLogAnalyzer` Roslyn analyzer (MAI001).

**See also**: [ADR-058 — Canonical Log Sanitizers](../../for-claude/architecture/adr/adr-058-canonical-log-sanitizers.md), issues [#1181](https://github.com/meepleAi-app/meepleai-monorepo/issues/1181) (sanitizer infrastructure), [#1195](https://github.com/meepleAi-app/meepleai-monorepo/issues/1195) (CI hardening), [#1197](https://github.com/meepleAi-app/meepleai-monorepo/issues/1197) (Roslyn analyzer).

## In-IDE feedback (MAI001)

`Api.Analyzers.NoPiiInLogAnalyzer` runs at build time and emits diagnostic **MAI001** when it detects an `ILogger.Log*` call whose message template contains a PII-suggesting placeholder name (e.g. `{Email}`, `{Password}`, `{Token}`, `{Phone}`, `{Ssn}`, `{IpAddress}`) and the corresponding positional argument is NOT wrapped via the canonical `Api.Infrastructure.Security.DataMasking.Mask*` family.

The analyzer is **advisory only** — the warning is mapped via `<WarningsNotAsErrors>MAI001</WarningsNotAsErrors>` in `Api.csproj` so it never fails a build. The authoritative gate stays the CodeQL `cs/log-forging` threshold check in `security-scan.yml` (issue #1195 FU#1). Treat the IDE warning as a developer convenience: fix at write-time, do not rely on it for security enforcement.

**Phase 1 scope (current)**: Tier 3 detection — placeholder name match only, exact placeholder list above. Tier 1 (typed value objects: `Email`, `JwtToken`, etc.) and Tier 2 (parameter-name heuristic) and the auto-fix CodeFixProvider are tracked as Phase 2/3/4 follow-ups on issue #1197.

**Important — `LogSanitizer.Sanitize` is NOT a PII barrier**: it strips `\r\n\t` for log-forging (CWE-117 integrity), not PII content (CWE-359 confidentiality). The analyzer correctly flags a call wrapped via `LogSanitizer.Sanitize` if the placeholder name suggests PII. Use `DataMasking.Mask*` for PII.

---

## TL;DR

Two helpers, two concerns:

| Concern | Helper | Namespace | When |
|---|---|---|---|
| **Integrity** (log-forging, CWE-117) | `LogSanitizer.Sanitize(value)` | `Api.Helpers` | Logging any string that originated from user input (HTTP request, webhook body, CLI flag) |
| **Confidentiality** (PII exposure, CWE-359, GDPR Art. 32) | `DataMasking.Mask*` | `Api.Infrastructure.Security` | Logging email, JWT, credit card, IP address, connection string, user object, response body |

The two are **orthogonal**. A user-provided email needs **both** (`DataMasking.MaskEmail(LogSanitizer.Sanitize(rawInput))` if the field is untrusted free-form text). In practice the two pipelines rarely overlap because PII fields come from validated, typed properties.

---

## Decision tree

```
You are about to log a value via ILogger.
│
├─ Is the value a primitive (int / Guid / bool / enum)?
│   └─ Yes → log it raw. No sanitization required. CodeQL may flag a false
│            positive due to interprocedural taint; dismiss via GitHub UI
│            with link to this document.
│
├─ Does the value contain PII (email, full name, phone, JWT, IP, credit card)?
│   ├─ This is an OPERATIONAL log (debug, info, warn, error for ops/dev consumption)
│   │   └─ Apply DataMasking.Mask* (mask in-place, never the raw value)
│   │
│   └─ This is an AUDIT log (compliance event, security incident record)
│       └─ Audit logs go through a SEPARATE pipeline (AuditService).
│          Do NOT log PII via ILogger.LogInformation. See AuditService.LogAsync.
│
├─ Is the value a free-form string from user/external input
│   (request path, query string, header value, webhook body, alert type)?
│   └─ Apply LogSanitizer.Sanitize. The helper strips CR/LF/TAB to prevent
│      log-forging (CWE-117 injection vectors %0D, %0A, %0D%0A).
│
└─ Is the value an internal constant or framework-generated identifier
   (controller name, channel name, exception type)?
    └─ Log it raw. No sanitization needed.
```

---

## Examples

### Email in operational log → MaskEmail

```csharp
// Wrong
_logger.LogInformation("User {Email} created account", email);

// Right
using Api.Infrastructure.Security;
_logger.LogInformation("User {Email} created account", DataMasking.MaskEmail(email));
// → log entry: User u***r@example.com created account
```

### User-controlled string in operational log → Sanitize

```csharp
// Wrong (CWE-117 vector if alertType contains %0A — attacker can forge log lines)
_logger.LogWarning("Alert {AlertType} suppressed", alertType);

// Right
using Api.Helpers;
_logger.LogWarning("Alert {AlertType} suppressed", LogSanitizer.Sanitize(alertType));
```

### Request path → SanitizePath

```csharp
// Wrong
_logger.LogError("Request to {Path} failed", context.Request.Path);

// Right (URL-decodes %0D/%0A first, then strips them)
using Api.Helpers;
_logger.LogError("Request to {Path} failed", LogSanitizer.SanitizePath(context.Request.Path));
```

### Full user object → SanitizeUser

```csharp
using Api.Infrastructure.Security;

// SanitizeUser returns an anonymous object with Email already masked and
// sensitive fields (PasswordHash, TotpSecretEncrypted) removed.
_logger.LogDebug("Loaded user {@User}", DataMasking.SanitizeUser(user));
```

### Audit event (NOT operational log)

```csharp
// Right — full email persisted in audit pipeline, NOT in ILogger.
// AuditService.LogAsync signature: userId, action, resource, resourceId,
// result, details (string?), ipAddress?, userAgent?, cancellationToken?
await _auditService.LogAsync(
    userId: user.Id.ToString(),
    action: "PASSWORD_RESET_REQUESTED",
    resource: "User",
    resourceId: user.Id.ToString(),
    result: "Success",
    details: JsonSerializer.Serialize(new { Email = user.Email }));
// Operational log gets only the actionable summary
_logger.LogInformation("Password reset requested for {UserId}", user.Id);
```

### Structured logging with `{@object}` — beware

```csharp
// Wrong — {@User} serializes ALL public properties, including PII
_logger.LogInformation("Login attempt {@User}", user);

// Right — sanitize first
_logger.LogInformation("Login attempt {@User}", DataMasking.SanitizeUser(user));
```

---

## Available sanitizers — API reference

### `Api.Helpers.LogSanitizer` (public static)

| Method | Use |
|---|---|
| `Sanitize(string? value)` | Strip `\r\n\t` from any user-controlled string |
| `Sanitize(string? value, int maxLength)` | Same + truncate (use for long inputs) |
| `SanitizePath(PathString path)` | URL-decode first, then strip CRLF — for request paths |

### `Api.Infrastructure.Security.DataMasking` (public static)

| Method | Output example |
|---|---|
| `MaskEmail(string?)` | `j***n@example.com` |
| `MaskString(string?, int visibleChars = 4)` | `abcd...wxyz` |
| `MaskJwt(string?)` | `eyJhbGciOiJIUzI1NiIs...***` (first 20 chars + `...***`) |
| `MaskCreditCard(string?)` | `****-****-****-1234` |
| `MaskIpAddress(string?)` | `192.168.1.***` (IPv4) — GDPR-compliant |
| `RedactConnectionString(string?)` | password value redacted, other params preserved |
| `MaskResponseBody(string?, int maxLength = 500)` | inline-redacts emails, bearer tokens, api_key, password, secret + truncates |
| `SanitizeUser(UserEntity)` | anonymous object with safe fields only |

> **Note on `RedactConnectionString`**: pattern-based, best-effort. Catches `password=` / `PASSWORD=` / `pwd=` (case-insensitive) and `password:value` / `pwd:value` (key-colon form). Does NOT catch MongoDB / Redis URIs (`mongodb://user:pass@host`), URI-encoded passwords, or non-standard key names outside the three documented patterns. Treat as defense-in-depth, not a primary safeguard.

---

## How the guard rail works

1. **CodeQL Models-as-Data pack** (`.github/codeql/extensions/meepleai-sanitizers`) registers every public **string-returning** method of `LogSanitizer` and `DataMasking` as a sanitizer for `cs/exposure-of-sensitive-information` and `cs/log-forging` queries. `SanitizeUser` is the one exception — it returns an anonymous object, which doesn't fit the `Argument[0] → ReturnValue` taint-flow shape. Call sites of `SanitizeUser` are recognized via the field-level masking inside the helper (each `Email = MaskEmail(...)` field already has its own sanitizer model).
2. **`security-scan.yml`** runs CodeQL on every PR to `main-dev`/`main-staging`/`main` (Phase 3 of #1181).
3. A PR that introduces `_logger.LogInformation("{Email}", rawEmail)` without `MaskEmail` triggers a new `cs/exposure-of-sensitive-information` alert. Branch protection on the target branch should require the CodeQL check to pass.
4. Conversely, a PR that wraps the call in `DataMasking.MaskEmail(...)` is auto-recognized as safe by the MaD pack — no alert opened, no friction.

### Verifying the guard rail

CodeQL `cs/exposure-of-sensitive-information` is a taint-tracking query: it needs an actual **taint source** (user input, HTTP request data, database read), not a hard-coded literal. A meaningful synthetic test PR adds something like:

```csharp
// In an endpoint or controller
app.MapGet("/_test/pii-guard", (string email, ILogger<Program> logger) =>
{
    logger.LogInformation("DEBUG raw email {Email}", email); // email is taint source
    return Results.Ok();
});
```

…then runs CodeQL on the PR. The flow `Request query parameter → ILogger sink` without a sanitizer must open a `cs/exposure-of-sensitive-information` alert. If it doesn't, the MaD pack or workflow trigger is broken — file a bug against this convention.

The complementary positive test wraps the sink in `DataMasking.MaskEmail(email)` and confirms the alert does **not** open.

---

## When CodeQL flags a false positive

A non-PII value (int, Guid, internal constant) sometimes gets flagged because CodeQL's interprocedural taint tracking is conservative. In those cases:

1. **Verify** the value really isn't PII (cross-check with the data source — repository query, framework property, etc.)
2. **Dismiss** the alert via GitHub UI with the reason `"Won't fix — value is non-PII per docs/for-developers/security/pii-safe-logging.md"` and a link to the specific source line
3. Do **not** add `[SuppressMessage]` — that attribute is for Roslyn analyzers, not CodeQL

If a class of false positives recurs (e.g. all `int` counts logged in a hot path), consider extending the MaD pack with a `neutralModel` entry instead of per-alert dismissal.

---

## When the helpers are insufficient

If you need to log a new PII category not covered (e.g. phone numbers, IBAN, physical addresses):

1. Add a new method to `DataMasking` (e.g. `MaskPhone`)
2. Register it in `.github/codeql/extensions/meepleai-sanitizers/models/sanitizers.model.yml` as a `summaryModel` with `kind: value`
3. Update this document with the new helper
4. **Do not** create a new helper class — ADR-058 §Extension policy: one canonical sanitizer per concern.

---

## Related

- [ADR-058 — Canonical Log Sanitizers](../../for-claude/architecture/adr/adr-058-canonical-log-sanitizers.md) — architectural decision behind the two-helper design
- `.github/codeql/extensions/meepleai-sanitizers/models/sanitizers.model.yml` — CodeQL recognition pack
- `apps/api/src/Api/Helpers/LogSanitizer.cs` — source
- `apps/api/src/Api/Infrastructure/Security/DataMasking.cs` — source
- `apps/api/tests/Api.Tests/Helpers/LogSanitizerTests.cs` — coverage
- CWE-117 (Improper Output Neutralization for Logs), CWE-359 (Exposure of Private Information), GDPR Art. 32 (Security of processing)
