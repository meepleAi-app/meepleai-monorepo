# [SECURITY] Hardcoded Database Credentials in Fallback Configuration

## Priority: P2 (Medium)
## Severity: WARNING
## Category: Hardcoded Credentials
## CWE: CWE-798 - Use of Hard-coded Credentials
## OWASP: A07:2021 - Identification and Authentication Failures

---

## Executive Summary

Two files contain hardcoded PostgreSQL credentials (`postgres:postgres`) as fallback values when environment variables are not configured. While these are development-only defaults, they pose a security risk in misconfigured staging/production environments where environment variables fail to load.

---

## Vulnerability Details

### Affected Files

#### 1. ObservabilityServiceExtensions.cs
- **File:** `apps/api/src/Api/Extensions/ObservabilityServiceExtensions.cs`
- **Line:** 91
- **Code:**
```csharp
healthCheckConnectionString ?? "Host=localhost;Database=meepleai;Username=postgres;Password=postgres"
```

#### 2. MeepleAiDbContextFactory.cs
- **File:** `apps/api/src/Api/Infrastructure/MeepleAiDbContextFactory.cs`
- **Line:** 15
- **Code:**
```csharp
?? "Host=localhost;Database=meepleai;Username=postgres;Password=postgres"
```

### Description

Both files use the null-coalescing operator (`??`) to provide fallback connection strings when `ConnectionStrings:Postgres` is not found in configuration. The fallback contains:
- **Username:** `postgres` (default superuser)
- **Password:** `postgres` (weak, commonly used default)
- **Database:** `meepleai`
- **Host:** `localhost`

### Risk Scenarios

1. **Misconfigured Staging Environment:**
   - Environment variables fail to load (typo, deployment script error)
   - Application falls back to hardcoded credentials
   - If a PostgreSQL instance is running with default credentials, unauthorized access is possible

2. **Docker Compose Defaults:**
   - Development environment uses `postgres:postgres` by design
   - If accidentally deployed to staging/production, credentials are known

3. **Configuration Drift:**
   - Developer forgets to set environment variables
   - Application silently uses insecure defaults

### Risk Level
- **Likelihood:** Low (requires misconfiguration)
- **Impact:** High (database compromise if default credentials exist)
- **Environment-Specific Risk:**
  - **Development:** Low (expected behavior)
  - **Staging:** Medium (misconfiguration possible)
  - **Production:** High (critical if fallback is used)

---

## Recommended Fixes

### Solution 1: Fail-Fast Approach (Recommended)

Remove fallback entirely and throw exception if configuration is missing:

#### ObservabilityServiceExtensions.cs
```csharp
// Line 91 - BEFORE:
healthCheckConnectionString ?? "Host=localhost;Database=meepleai;Username=postgres;Password=postgres"

// AFTER:
healthCheckConnectionString ?? throw new InvalidOperationException(
    "Health check database connection string not configured. " +
    "Set CONNECTIONSTRINGS__POSTGRES environment variable or appsettings.json ConnectionStrings:Postgres.")
```

#### MeepleAiDbContextFactory.cs
```csharp
// Line 15 - BEFORE:
?? "Host=localhost;Database=meepleai;Username=postgres;Password=postgres"

// AFTER:
?? throw new InvalidOperationException(
    "Database connection string not configured. " +
    "Set CONNECTIONSTRINGS__POSTGRES environment variable.")
```

**Benefits:**
- Forces proper configuration in all environments
- Prevents silent security failures
- Clear error messages guide developers/ops teams
- No risk of accidental default credential usage

### Solution 2: Development-Only Fallback (Alternative)

Use conditional compilation to allow fallback only in DEBUG builds:

```csharp
#if DEBUG
    ?? "Host=localhost;Database=meepleai;Username=postgres;Password=postgres"
#else
    ?? throw new InvalidOperationException(
        "Database connection string not configured. Set CONNECTIONSTRINGS__POSTGRES.")
#endif
```

**Benefits:**
- Preserves developer convenience (no env vars needed locally)
- Forces configuration in Release builds
- Clear separation of dev vs prod behavior

**Drawbacks:**
- More complex code
- Could still be misconfigured if Release build is used locally

### Solution 3: Safer Fallback Credentials (Not Recommended)

Use obviously invalid credentials to make misconfiguration apparent:

```csharp
?? "Host=localhost;Database=meepleai;Username=CONFIGURE_ME;Password=CHANGE_THIS_PASSWORD_NOW"
```

**Drawbacks:**
- Still allows silent failure (worse than fail-fast)
- Misleading error messages (connection fails vs config fails)
- Not recommended

---

## Implementation Steps

### Phase 1: Code Changes (1 hour)
1. Update `ObservabilityServiceExtensions.cs:91`
2. Update `MeepleAiDbContextFactory.cs:15`
3. Choose Solution 1 (fail-fast) or Solution 2 (dev-only fallback)
4. Add XML documentation comments explaining the requirement:
```csharp
/// <summary>
/// Gets database connection string from configuration.
/// </summary>
/// <exception cref="InvalidOperationException">
/// Thrown when ConnectionStrings:Postgres is not configured in appsettings.json
/// or CONNECTIONSTRINGS__POSTGRES environment variable.
/// </exception>
```

### Phase 2: Configuration Validation (30 minutes)
5. Update startup code to validate configuration early:
```csharp
// In Program.cs, after builder.Build():
var connectionString = app.Configuration.GetConnectionString("Postgres");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException(
        "Database connection string not configured. Application cannot start.");
}
```

### Phase 3: Documentation (30 minutes)
6. Update `docs/SECURITY.md` with required environment variables
7. Update `README.md` setup instructions
8. Update `infra/env/*.env.example` files
9. Add configuration validation to startup checklist

### Phase 4: Testing (1 hour)
10. Test with missing environment variables (should fail fast)
11. Test with correct environment variables (should work)
12. Test Docker Compose setup (should use explicit credentials)
13. Update CI/CD scripts if needed

---

## Testing Strategy

### Local Testing

```bash
# Test 1: Remove environment variable (should fail)
unset CONNECTIONSTRINGS__POSTGRES
cd apps/api/src/Api
dotnet run
# Expected: InvalidOperationException with clear message

# Test 2: Set environment variable (should work)
export CONNECTIONSTRINGS__POSTGRES="Host=localhost;Port=5432;Database=meepleai;Username=postgres;Password=postgres"
dotnet run
# Expected: Application starts successfully

# Test 3: Docker Compose (should use explicit config)
cd infra
docker compose up postgres
cd ../apps/api/src/Api
dotnet run
# Expected: Application connects using environment variables
```

### CI/CD Testing

Ensure all CI workflows have proper environment variables:
- `.github/workflows/ci.yml` - Check database configuration
- `.github/workflows/security-scan.yml` - Verify no fallback credentials

### Manual Security Review

```bash
# Search for any remaining hardcoded credentials:
grep -r "Password=.*postgres" apps/api/
grep -r "Username=.*postgres" apps/api/ | grep -v ".cs:" | grep -v "// "

# Expected: No matches (except in test files if applicable)
```

---

## Impact Assessment

### Security Impact
- ✅ Eliminates hardcoded credential risk
- ✅ Forces proper configuration in all environments
- ✅ Prevents silent security failures
- ✅ Improves security posture (defense-in-depth)

### Operational Impact
- ⚠️ Requires environment variables to be set in all environments
- ✅ Clearer error messages when misconfigured
- ✅ Easier to audit (fail-fast vs silent fallback)

### Developer Experience Impact
- ⚠️ **Solution 1 (fail-fast):** Requires env vars in local dev
- ✅ **Solution 2 (dev-only):** No change for local dev
- ✅ Documentation improvements clarify requirements

---

## Configuration Requirements

### Environment Variables

All environments must set:

```bash
# Linux/macOS
export CONNECTIONSTRINGS__POSTGRES="Host=localhost;Port=5432;Database=meepleai;Username=meepleai;Password=your_secure_password"

# Windows PowerShell
$env:CONNECTIONSTRINGS__POSTGRES="Host=localhost;Port=5432;Database=meepleai;Username=meepleai;Password=your_secure_password"

# Docker Compose (infra/.env.dev)
POSTGRES_PASSWORD=your_secure_password
```

### appsettings.json (Alternative)

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=meepleai;Username=meepleai;Password=your_secure_password"
  }
}
```

**Note:** Never commit `appsettings.json` with real passwords to Git. Use `appsettings.Development.json` (gitignored) or environment variables.

---

## Related Security Improvements

### Password Management Best Practices

1. **Use Strong Passwords:**
   ```bash
   # Generate secure random password:
   openssl rand -base64 32
   ```

2. **Use Secrets Management:**
   - **Azure Key Vault** (production)
   - **GitHub Secrets** (CI/CD)
   - **Docker Secrets** (Swarm/Kubernetes)
   - **HashiCorp Vault** (enterprise)

3. **Rotate Credentials Regularly:**
   - Database passwords should be rotated quarterly
   - Document rotation procedure in `docs/SECURITY.md`

4. **Least Privilege:**
   ```sql
   -- Create application-specific user (not superuser):
   CREATE USER meepleai WITH PASSWORD 'secure_password';
   GRANT CONNECT ON DATABASE meepleai TO meepleai;
   GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO meepleai;
   ```

---

## Definition of Done

- [ ] Hardcoded credentials removed from `ObservabilityServiceExtensions.cs`
- [ ] Hardcoded credentials removed from `MeepleAiDbContextFactory.cs`
- [ ] Fail-fast exception handling implemented (or dev-only fallback)
- [ ] XML documentation comments added
- [ ] Configuration validation added to startup
- [ ] `docs/SECURITY.md` updated with required env vars
- [ ] `README.md` setup instructions updated
- [ ] `infra/env/*.env.example` files updated
- [ ] Local testing completed (missing config fails, correct config works)
- [ ] CI/CD workflows verified (env vars configured)
- [ ] Code review approved
- [ ] Security scan re-run (Semgrep confirms no hardcoded credentials)

---

## Effort Estimate
- **Code Changes:** 1 hour
- **Configuration Validation:** 30 minutes
- **Documentation:** 30 minutes
- **Testing:** 1 hour
- **Total:** 3 hours

---

## References

- **CWE-798:** https://cwe.mitre.org/data/definitions/798.html
- **OWASP Secrets Management:** https://cheatsheetseries.owasp.org/cheatsheets/Secrets_Management_Cheat_Sheet.html
- **ASP.NET Core Configuration:** https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/
- **12-Factor App - Config:** https://12factor.net/config

---

## Related Issues
- #264 - SEC-04: Security Audit Implementation
- #307 - SEC-03: Security Scanning Pipeline

---

**Detected by:** Semgrep Security Analysis
**Report Date:** 2025-11-04
**Assignee:** TBD
**Labels:** `security`, `credentials`, `p2-medium`, `backend`, `configuration`
