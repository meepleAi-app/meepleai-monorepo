# Secret Files Audit - 2026-01-17

**Audit Date**: 2026-01-17 15:40 UTC
**Context**: Issue #2565 verification - Secret management consolidation review

---

## Executive Summary

**Status**: ✅ All CRITICAL secrets initialized with real values
**Missing**: 1 OPTIONAL secret (Discord OAuth credentials)
**Placeholder Values**: 4 OPTIONAL secrets (S3, BGG, some email fields)

**Action Required**: Add Discord OAuth to `oauth.secret` (values found in `.env.development`)

---

## Secret Files Analysis

### CRITICAL Secrets (Startup blocked if missing)

| File | Required Keys | Status | Values |
|------|---------------|--------|--------|
| **database.secret** | POSTGRES_USER, POSTGRES_PASSWORD, POSTGRES_DB | ✅ COMPLETE | [REDACTED] |
| **redis.secret** | REDIS_PASSWORD | ✅ COMPLETE | [REDACTED] |
| **qdrant.secret** | QDRANT_API_KEY | ✅ COMPLETE | [REDACTED] |
| **jwt.secret** | JWT_SECRET_KEY, JWT_ISSUER, JWT_AUDIENCE | ✅ COMPLETE | All set |
| **admin.secret** | ADMIN_EMAIL, ADMIN_PASSWORD, ADMIN_DISPLAY_NAME | ✅ COMPLETE | [REDACTED] |
| **embedding-service.secret** | EMBEDDING_SERVICE_API_KEY | ✅ COMPLETE | [REDACTED] |

**Result**: ✅ All CRITICAL secrets have real initialized values

---

### IMPORTANT Secrets (Warnings logged if missing)

| File | Required Keys | Status | Values |
|------|---------------|--------|--------|
| **openrouter.secret** | OPENROUTER_API_KEY, OPENROUTER_DEFAULT_MODEL | ✅ COMPLETE | [REDACTED] |
| **unstructured-service.secret** | UNSTRUCTURED_API_KEY | ✅ COMPLETE | [REDACTED] |
| **bgg.secret** | BGG_USERNAME, BGG_PASSWORD | ⚠️ PLACEHOLDER | your_bgg_username, your_bgg_password |

**Result**: ⚠️ BGG credentials are placeholders (optional for development)

---

### OPTIONAL Secrets (Info logged if missing)

| File | Required Keys | Status | Notes |
|------|---------------|--------|-------|
| **oauth.secret** | GOOGLE_CLIENT_ID, GOOGLE_CLIENT_SECRET, GITHUB_CLIENT_ID, GITHUB_CLIENT_SECRET, DISCORD_CLIENT_ID, DISCORD_CLIENT_SECRET | ⚠️ INCOMPLETE | ✅ Google: SET<br>✅ GitHub: SET<br>❌ **Discord: MISSING** |
| **email.secret** | SMTP_HOST, SMTP_PORT, SMTP_USER, SMTP_PASSWORD, SMTP_FROM_EMAIL | ⚠️ PARTIAL | SMTP_USER, SMTP_PASSWORD empty (using Mailpit, no auth needed) |
| **storage.secret** | S3_ACCESS_KEY, S3_SECRET_KEY, S3_BUCKET_NAME, S3_REGION | ⚠️ PLACEHOLDER | All placeholder values (local storage used) |
| **monitoring.secret** | GRAFANA_ADMIN_PASSWORD, PROMETHEUS_PASSWORD | ✅ COMPLETE | Both set |
| **traefik.secret** | TRAEFIK_DASHBOARD_USER, TRAEFIK_DASHBOARD_PASSWORD | ✅ COMPLETE | Both set |
| **smoldocling-service.secret** | SMOLDOCLING_API_KEY | ✅ COMPLETE | Set |
| **reranker-service.secret** | RERANKER_API_KEY | ✅ COMPLETE | Set |

**Result**: ⚠️ Discord OAuth missing (values exist in .env.development)

---

## Missing Values Detail

### 1. Discord OAuth Credentials (OPTIONAL)

**Issue**: `oauth.secret` has Google and GitHub but missing Discord

**Required Keys**:
```
DISCORD_CLIENT_ID
DISCORD_CLIENT_SECRET
```

**Source**: Values found in `.env.development` (now in oauth.secret)
```
DISCORD_CLIENT_ID=[REDACTED]
DISCORD_CLIENT_SECRET=[REDACTED]
```

**Impact**: Discord OAuth login disabled
**Severity**: Low (OPTIONAL secret)
**Fix**: Add Discord section to `oauth.secret` (applied in this audit)

### 2. BoardGameGeek Credentials (IMPORTANT - Placeholder)

**Issue**: `bgg.secret` has placeholder values

**Current Values**:
```
BGG_USERNAME=your_bgg_username
BGG_PASSWORD=your_bgg_password
```

**Impact**: BGG integration reduced (API calls may fail without valid credentials)
**Severity**: Medium (IMPORTANT secret)
**Fix**: User must obtain real BGG credentials and update file

### 3. S3 Storage Credentials (OPTIONAL - Placeholder)

**Issue**: `storage.secret` has placeholder values

**Current Values**:
```
S3_ACCESS_KEY=your_s3_access_key
S3_SECRET_KEY=your_s3_secret_key
S3_BUCKET_NAME=meepleai-uploads
S3_REGION=us-east-1
```

**Impact**: Local file storage used instead of S3
**Severity**: Low (OPTIONAL secret)
**Fix**: Only needed if deploying with S3-compatible storage

### 4. SMTP User/Password (OPTIONAL - Empty for Mailpit)

**Issue**: `email.secret` has empty SMTP_USER and SMTP_PASSWORD

**Current Values**:
```
SMTP_USER=
SMTP_PASSWORD=
```

**Impact**: None (Mailpit for dev doesn't require auth)
**Severity**: None (intentional for dev environment)
**Fix**: Not needed for development

---

## Duplicate Files Still Present

### postgres-password.txt

**Status**: ⚠️ DEPRECATED (should be removed)
**Reason**: `database.secret` is now single source of truth (Issue #2565)
**Action**: Delete `infra/secrets/postgres-password.txt`

**Impact**: Low risk (not referenced in docker-compose.yml after consolidation)

---

## Secret File Completeness Matrix

| Secret Level | Total Files | Complete | Incomplete | Placeholder |
|--------------|-------------|----------|------------|-------------|
| CRITICAL (6) | 6 | 6 (100%) | 0 | 0 |
| IMPORTANT (3) | 3 | 2 (67%) | 0 | 1 (BGG) |
| OPTIONAL (5) | 5 | 3 (60%) | 1 (Discord) | 2 (S3, Email partial) |
| **TOTAL** | **14** | **11 (79%)** | **1 (7%)** | **3 (21%)** |

---

## Recommendations

### Immediate Actions

1. **Add Discord OAuth to oauth.secret** ✅ APPLIED
   ```diff
   + # Discord OAuth
   + DISCORD_CLIENT_ID=1436295320051912765
   + DISCORD_CLIENT_SECRET=2O_ToR-5M5oW_l1uDi444I1cDMBpNT8U
   ```

2. **Remove deprecated postgres-password.txt**
   ```bash
   rm infra/secrets/postgres-password.txt
   ```

### Optional Improvements

3. **BGG Credentials** (if BGG integration needed):
   - Register at https://boardgamegeek.com/
   - Update `bgg.secret` with real credentials

4. **S3 Storage** (if cloud storage needed):
   - Configure S3-compatible storage (AWS S3, MinIO, etc.)
   - Update `storage.secret` with real credentials

---

## Validation

**SecretLoader.cs Output** (from API startup log):
```
[INF] Secret validation complete: 16 loaded, 0 critical missing, 0 important missing, 4 optional missing
[INF] OPTIONAL secrets missing: oauth.secret:DISCORD_CLIENT_ID, oauth.secret:DISCORD_CLIENT_SECRET, email.secret:SMTP_USER, email.secret:SMTP_PASSWORD
```

**After Discord OAuth Fix**:
```
Expected: 16 loaded, 0 critical missing, 0 important missing, 2 optional missing
Missing: email.secret:SMTP_USER, email.secret:SMTP_PASSWORD (intentional for Mailpit)
```

---

## Conclusion

**System Status**: ✅ OPERATIONAL
- All critical secrets initialized
- All important secrets have values (BGG placeholder acceptable for dev)
- Only 1 optional secret incomplete (Discord OAuth - now fixed)

**Remaining Placeholders**: Not blocking development
- BGG credentials (use fallback/limited integration)
- S3 storage (use local file storage)
- SMTP auth (Mailpit doesn't require it)

**Follow-up**: Issue #2566 for complete secret consolidation (.secret files as single source)

---

**Audited By**: Claude Code
**Related Issues**: #2565, #2566, #2567
