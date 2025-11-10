# AUTH-07 Code Review Findings - Phase 1

**Review Date**: 2025-10-27
**Reviewer**: Claude Code (security-engineer persona + Sequential MCP)
**Scope**: TotpService, EncryptionService, API endpoints, Frontend UI
**Status**: ❌ **NOT READY FOR PRODUCTION** - Critical security issues found

---

## 🔴 BLOCKER Issues (Must Fix Before Merge)

### 1. EncryptionService - No Authenticated Encryption
**File**: `apps/api/src/Api/Services/EncryptionService.cs`
**Line**: 30-47
**Severity**: 🔴 CRITICAL

**Issue**:
- Uses AES-256-CBC without HMAC/authentication tag
- **Vulnerability**: Padding oracle attacks, ciphertext malleability
- Attacker could modify encrypted TOTP secrets without detection

**Current Code**:
```csharp
using var aes = Aes.Create();  // Defaults to AES-CBC
aes.GenerateIV();
using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
// No HMAC, no authentication
```

**Required Fix**:
```csharp
// Option 1: Use AES-GCM (authenticated encryption)
using var aes = new AesGcm(encryptionKey);
var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
var tag = new byte[AesGcm.TagByteSizes.MaxSize];
RandomNumberGenerator.Fill(nonce);
aes.Encrypt(nonce, plainBytes, cipherBytes, tag);
// Return: nonce + ciphertext + tag

// Option 2: AES-CBC + HMAC-SHA256
// Encrypt with AES-CBC, then HMAC the ciphertext
```

**Impact**: HIGH - Compromised database + modified ciphertext = attacker controls TOTP secrets
**Timeline**: **MUST FIX in Phase 2 before production**

---

### 2. Temp Session Token Uses User ID
**File**: `apps/api/src/Api/Program.cs`
**Line**: 936, 1012
**Severity**: 🔴 CRITICAL

**Issue**:
- Login returns `sessionToken = result.User.Id` for 2FA-enabled users
- Verify endpoint accepts userId as session token
- **Vulnerability**: Anyone knowing a userId can bypass password check

**Current Code**:
```csharp
// Line 936 (login endpoint)
sessionToken = result.User.Id, // Temp token (simplified - use userId)

// Line 1012 (verify endpoint)
var userId = request.SessionToken; // Placeholder
```

**Required Fix**:
- Generate cryptographically secure random temp session token (256-bit)
- Store in Redis/memory cache with 5-min TTL
- Include userId + IP + timestamp in token payload
- Verify token validity before TOTP check

**Impact**: CRITICAL - Bypasses password authentication entirely
**Timeline**: **MUST FIX in Phase 2 before production**

---

### 3. Backup Code Race Condition
**File**: `apps/api/src/Api/Services/TotpService.cs`
**Line**: 204-212
**Severity**: 🔴 HIGH

**Issue**:
- Backup code verification loops through codes, then saves
- **Race Condition**: Two concurrent requests could use same backup code
- Single-use enforcement could be bypassed

**Current Code**:
```csharp
foreach (var storedCode in backupCodes)  // Line 204
{
    var isMatch = VerifyBackupCode(storedCode.CodeHash, backupCode);
    if (isMatch)
    {
        storedCode.IsUsed = true;  // Not atomic
        await _dbContext.SaveChangesAsync();  // Race window here
        return true;
    }
}
```

**Required Fix**:
```csharp
// Option 1: Use transaction with serializable isolation
using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
// ... verification logic
await transaction.CommitAsync();

// Option 2: Optimistic concurrency with RowVersion
// Add RowVersion to UserBackupCodeEntity

// Option 3: SQL UPDATE with WHERE clause
await _dbContext.Database.ExecuteSqlRawAsync(
    "UPDATE user_backup_codes SET is_used = TRUE WHERE id = {0} AND is_used = FALSE", codeId);
```

**Impact**: MEDIUM - Backup code could be used twice if requests are concurrent
**Timeline**: Should fix in Phase 2 before production

---

## 🟡 HIGH Priority (Fix in Phase 2)

### 4. No Input Validation on API Endpoints
**File**: `apps/api/src/Api/Program.cs`
**Lines**: 966-1089
**Severity**: 🟡 HIGH

**Issue**:
- No validation that TOTP codes are 6 digits
- No validation that backup codes are 9 chars (XXXX-XXXX)
- Malformed input reaches service layer

**Fix**:
```csharp
// Add validation before service calls
if (!Regex.IsMatch(request.Code, @"^\d{6}$|^[A-Z0-9]{4}-[A-Z0-9]{4}$"))
{
    return Results.BadRequest(new { error = "Invalid code format" });
}
```

**Impact**: MEDIUM - Unclear error messages, potential DoS with huge inputs
**Timeline**: Phase 2

---

### 5. CSRF Protection Not Verified
**File**: `apps/api/src/Api/Program.cs`
**Severity**: 🟡 HIGH

**Issue**:
- Endpoints use cookie authentication
- No explicit CSRF token validation visible
- Relies on SameSite cookie attribute

**Verification Needed**:
- Check `appsettings.json` for SameSite=Strict or Lax
- Consider requiring CSRF token for state-changing operations (enable/disable)

**Impact**: MEDIUM - CSRF attacks could enable/disable 2FA for logged-in users
**Timeline**: Verify in Phase 2

---

## 🟢 MEDIUM Priority (Nice to Have)

### 6. TOTP Replay Attack Prevention Missing
**File**: `apps/api/src/Api/Services/TotpService.cs`
**Line**: 339-348
**Severity**: 🟢 MEDIUM

**Issue**:
- Same TOTP code can be used multiple times within 60s window
- No storage of last used time step

**Fix**:
```csharp
// Store last used time step in UserEntity
public long? LastTotpTimeStep { get; set; }

// In VerifyTotpCode():
if (isValid && timeStepMatched <= user.LastTotpTimeStep)
{
    return false; // Replay attack
}
user.LastTotpTimeStep = timeStepMatched;
```

**Impact**: LOW - Rate limiting (3/min) mitigates risk
**Timeline**: Phase 2 or Phase 3

---

### 7. Insufficient Audit Logging Context
**File**: `apps/api/src/Api/Services/TotpService.cs`
**Multiple lines**: 93, 131, 167, etc.
**Severity**: 🟢 MEDIUM

**Issue**:
- Audit logs don't include IP address, user agent, correlation ID
- Harder to investigate security incidents

**Fix**:
```csharp
await _auditService.LogAsync(userId, action, resource, resourceId, result, details,
    ipAddress: context.Connection.RemoteIpAddress?.ToString(),
    userAgent: context.Request.Headers.UserAgent.ToString());
```

**Impact**: LOW - Logging works, just less detailed
**Timeline**: Phase 2

---

### 8. Frontend - Backup Codes Security
**File**: `apps/web/src/pages/settings.tsx`
**Line**: 237-245
**Severity**: 🟢 LOW

**Issue**:
- Backup codes displayed in plain text in DOM
- Risk: Screen recording, shoulder surfing, browser cache

**Suggestions**:
- Add "hide codes" toggle
- Warn about screenshots/screen recording
- Auto-hide after 60 seconds

**Impact**: LOW - Codes are single-use, user already authenticated
**Timeline**: Phase 3 (UX improvement)

---

## ✅ Security Strengths

**Cryptography**:
- ✅ PBKDF2 with 210,000 iterations (industry standard)
- ✅ Cryptographically secure random (RandomNumberGenerator)
- ✅ Constant-time comparison (FixedTimeEquals)
- ✅ Random IV for each encryption

**Rate Limiting**:
- ✅ 3 attempts/minute on verify endpoint
- ✅ Token bucket algorithm (RateLimitService)
- ✅ Prevents brute force attacks

**General Security**:
- ✅ No hardcoded secrets
- ✅ Comprehensive audit logging
- ✅ Authorization required on sensitive endpoints
- ✅ No secrets in localStorage
- ✅ Backup codes hashed at rest

---

## Overall Assessment

**Code Quality**: 8/10
**Security Posture**: 6/10 (good foundation, needs hardening)
**Production Readiness**: ❌ NO (2 BLOCKER issues)

**Recommendation**:
1. ✅ **Keep PR #573 open** for review and feedback
2. ❌ **DO NOT MERGE** until BLOCKERs fixed
3. 🔴 **Fix BLOCKER #1** (AES-GCM) in Phase 2
4. 🔴 **Fix BLOCKER #2** (Temp session) in Phase 2
5. 🟡 **Fix HIGH issues** (race condition, validation) in Phase 2
6. ✅ **Then merge** after security audit passed

**Current State**: Excellent for testing and code review. Needs security hardening before production deployment.

---

**Reviewed By**: Claude Code with security-engineer persona
**Analysis Tool**: Sequential MCP (4-step security analysis)
**Review Depth**: Deep (cryptography, concurrency, input validation, CSRF, audit)

🤖 Generated with [Claude Code](https://claude.com/claude-code)
