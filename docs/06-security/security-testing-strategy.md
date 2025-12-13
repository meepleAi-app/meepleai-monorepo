# Security Testing Strategy

**Status**: Active
**Last Updated**: 2025-12-13T10:59:23.970Z
**Related**: Issue #576 (2FA Security), OWASP A07:2021

## Overview

MeepleAI implements a **dedicated security testing workflow** to balance comprehensive security validation with efficient CI/CD pipelines.

## Architecture

### Dual-Workflow Strategy

```
┌─────────────────────────────────────────────────────────────┐
│                    CI/CD Pipeline Split                     │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  Main CI (.github/workflows/ci.yml)                        │
│  ├─ Unit Tests (70%)           ~8-12 min                   │
│  ├─ Integration Tests (20%)    included                    │
│  └─ Functional Tests (10%)     included                    │
│  ⚠️  EXCLUDES: TwoFactorSecurityPenetrationTests           │
│                                                             │
│  Security Penetration Tests                                │
│  (.github/workflows/security-penetration-tests.yml)        │
│  ├─ Brute Force Tests (6)      5-8 min                     │
│  ├─ Replay Attack Tests (5)    included                    │
│  └─ Timing Attack Tests (4)    included                    │
│  ✅ OWASP-compliant security validation                    │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

## Triggers

### Security Penetration Tests Run When:

1. **PR Labeled `security`**
   ```bash
   # Add label to PR:
   gh pr edit <number> --add-label security
   ```

2. **Security Files Changed**
   - `apps/api/src/Api/BoundedContexts/Authentication/**`
   - `apps/api/src/Api/Services/TotpService.cs`
   - `apps/api/src/Api/Services/PasswordHashingService.cs`
   - `apps/api/src/Api/Services/EncryptionService.cs`
   - Security test files
   - `docs/06-security/**`

3. **Weekly Schedule**
   - Every Sunday at 3 AM UTC
   - Ensures periodic security validation

4. **Manual Trigger**
   ```bash
   gh workflow run security-penetration-tests.yml
   ```

## Test Categories

### 1. Brute Force Prevention (6 tests)

**Purpose**: Validate protection against automated credential attacks

**Tests**:
- `BruteForce_100InvalidTotpAttempts_ShouldFail` - Mass invalid TOTP attempts
- `BruteForce_InvalidBackupCodes_ShouldTriggerAccountLockout` - Backup code exhaustion
- `BruteForce_RapidFireAttack_ShouldBeRateLimited` - Speed-based attack detection
- `BruteForce_AccountLockout_ShouldEnforceWaitPeriod` - Lockout enforcement
- `BruteForce_DistributedAttack_ShouldBeDetected` - Multi-session attacks
- `BruteForce_RepeatedFailures_ShouldGenerateSecurityAlert` - Alerting triggers

**Performance**: 100-1000 authentication attempts per test (realistic attack simulation)

### 2. Replay Attack Prevention (5 tests)

**Purpose**: Prevent OTP reuse and session token replay

**Tests**:
- `ReplayAttack_ReuseValidTotp_ShouldFail` - TOTP nonce validation
- `ReplayAttack_ExpiredTotp_ShouldFail` - Temporal expiration (±90s window)
- `ReplayAttack_ReuseBackupCode_ShouldFail` - Single-use backup codes
- `ReplayAttack_ConcurrentBackupCodeUse_ShouldPreventRace` - Race condition prevention
- `ReplayAttack_SessionTokenReplay_ShouldBeDetected` - Session token validation

**Performance**: Timestamp-based TOTP generation (no real delays)

### 3. Timing Attack Resistance (4 tests)

**Purpose**: Prevent side-channel information leakage

**Tests**:
- `TimingAttack_TotpVerification_ShouldBeConstantTime` - TOTP comparison timing
- `TimingAttack_BackupCodeVerification_ShouldBeConstantTime` - Backup code timing
- `TimingAttack_ErrorMessages_ShouldBeConsistent` - Error path timing
- `TimingAttack_ResponseTime_ShouldNotLeakInformation` - Code similarity timing

**Performance**: 100-1000 statistical samples per test (variance analysis)

## Implementation Details

### Test Optimization (Issue #1800)

**Problem**: Original `ReplayAttack_ExpiredTotp_ShouldFail` used `Task.Delay(65s)`, causing CI timeouts.

**Solution**: Generate TOTP from past timestamp instead of waiting:
```csharp
// Before (65s delay):
await Task.Delay(TimeSpan.FromSeconds(65), cancellationToken);

// After (instant):
var expiredCode = GenerateTotpCodeAtTime(secret, DateTimeOffset.UtcNow.AddSeconds(-120));
```

**Impact**:
- Test duration: **65s → 7s** (89% faster)
- CI timeout risk: **eliminated**
- Security validation: **unchanged**

### CI Exclusion Filter

Main CI excludes slow security tests:
```yaml
dotnet test \
  --filter "FullyQualifiedName!~TwoFactorSecurityPenetrationTests" \
  --logger "console;verbosity=normal" \
  --no-build
```

## Local Development

### Run All Security Tests
```bash
cd apps/api
dotnet test --filter "FullyQualifiedName~TwoFactorSecurityPenetrationTests"
```

### Run Specific Category
```bash
# Brute Force only
dotnet test --filter "FullyQualifiedName~TwoFactorSecurityPenetrationTests.BruteForce"

# Replay Attack only
dotnet test --filter "FullyQualifiedName~TwoFactorSecurityPenetrationTests.ReplayAttack"

# Timing Attack only
dotnet test --filter "FullyQualifiedName~TwoFactorSecurityPenetrationTests.TimingAttack"
```

### Run Single Test
```bash
dotnet test \
  --filter "FullyQualifiedName~TwoFactorSecurityPenetrationTests.ReplayAttack_ExpiredTotp_ShouldFail" \
  --logger "console;verbosity=detailed"
```

## OWASP Compliance

### Coverage Matrix

| OWASP Category | Tests | Status |
|----------------|-------|--------|
| A07:2021 – Authentication Failures | 15 | ✅ Complete |
| ASVS 2.0 – Auth Verification | 15 | ✅ Complete |
| WSTG-ATHN-11 – Multi-Factor Auth | 15 | ✅ Complete |

### Security Standards

- **Rate Limiting**: OWASP recommends max 5 attempts per 60 seconds
- **Account Lockout**: Enforce after 3-5 failed attempts
- **Timing Variance**: <5% variance threshold for constant-time operations
- **OTP Window**: ±90 seconds for TOTP (RFC 6238)
- **Backup Codes**: Single-use enforcement via `IsolationLevel.Serializable`

## Performance Benchmarks

### Test Suite Duration

| Environment | Duration | Notes |
|-------------|----------|-------|
| Local (Development) | 5-8 min | Full suite, verbose logging |
| CI (GitHub Actions) | 5-8 min | Optimized, parallel where safe |
| Weekly Schedule | 5-8 min | Comprehensive validation |

### Individual Test Performance

| Test Category | Avg Duration | Reason |
|---------------|--------------|--------|
| Brute Force | 2-3 min | 100-1000 authentication attempts |
| Replay Attack | 1-2 min | Timestamp-based (optimized) |
| Timing Attack | 2-3 min | Statistical sampling (1000 samples) |

## Maintenance

### Adding New Security Tests

1. **Add test to suite**:
   ```csharp
   [Trait("Category", "Security")]
   [Trait("OWASP", "A07-Authentication")]
   public async Task NewSecurityTest_Scenario_ShouldBehavior()
   {
       // AAA pattern
   }
   ```

2. **Verify exclusion in main CI**:
   ```bash
   # Should NOT appear in ci.yml output
   dotnet test --list-tests --filter "FullyQualifiedName!~TwoFactorSecurityPenetrationTests"
   ```

3. **Test in security workflow**:
   ```bash
   gh workflow run security-penetration-tests.yml
   ```

### Updating Security File Paths

Edit `.github/workflows/security-penetration-tests.yml`:
```yaml
security:
  - 'apps/api/src/Api/BoundedContexts/Authentication/**'
  - 'apps/api/src/Api/Services/NewSecurityService.cs'  # Add new path
```

## Troubleshooting

### Security Tests Not Running on PR

**Check**:
1. PR has `security` label OR security files changed
2. Workflow file syntax is valid: `gh workflow view security-penetration-tests.yml`
3. Check workflow status: `gh run list --workflow=security-penetration-tests.yml`

### Security Tests Timing Out

**Solutions**:
1. Reduce iteration count for brute force tests (local only)
2. Use timestamp-based approach for expiration tests (like `ReplayAttack_ExpiredTotp_ShouldFail`)
3. Increase timeout in workflow: `timeout-minutes: 15`

### False Positives in Timing Attack Tests

**Expected Variance**: <5% timing difference between valid/invalid code paths

**Debugging**:
```bash
dotnet test \
  --filter "FullyQualifiedName~TimingAttack" \
  --logger "console;verbosity=detailed" \
  -- xUnit.Parallelization.MaxParallelThreads=1
```

## References

- **OWASP Top 10 2024**: [A07:2021 – Identification and Authentication Failures](https://owasp.org/Top10/A07_2021-Identification_and_Authentication_Failures/)
- **OWASP ASVS**: [Authentication Verification Requirements](https://owasp.org/www-project-application-security-verification-standard/)
- **OWASP Testing Guide**: [WSTG-ATHN-11 (Multi-Factor Authentication)](https://owasp.org/www-project-web-security-testing-guide/latest/4-Web_Application_Security_Testing/04-Authentication_Testing/11-Testing_Multi-Factor_Authentication)
- **RFC 6238**: [TOTP: Time-Based One-Time Password Algorithm](https://datatracker.ietf.org/doc/html/rfc6238)

## Related Documentation

- [2FA Implementation Guide](./2fa-implementation.md)
- [Security Monitoring](./sec-08-2fa-security-monitoring.md)
- [Code Scanning Remediation](./code-scanning-remediation-summary.md)
- [Testing Strategy](../02-development/testing/testing-strategy.md)

