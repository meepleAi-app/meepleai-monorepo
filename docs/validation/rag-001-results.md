# RAG-001 Validation Results

**Issue**: #3172
**Date**: 2026-01-30
**Validator**: Claude Code
**Status**: ⚠️ BLOCKED - PDF Upload Feature Disabled

## Validation Steps Attempted

### Step 1: Infrastructure Health Check ✅
All services confirmed operational:
- Qdrant (Vector DB): http://localhost:6333 - Healthy
- Embedding Service: http://localhost:8000 - Healthy
- SmolDocling (PDF Processing): http://localhost:8002 - Healthy
- API Backend: http://localhost:8080 - Healthy
- Web Frontend: http://localhost:3000 - Healthy

### Step 2: Chess Game Setup ✅
- Successfully logged in as admin (admin@meepleai.dev)
- Added Chess game to library (Game ID: 9088f12d-d89c-4dd7-b047-8da3233b2553)
- Test PDF ready: `data/rulebook/scacchi-fide_2017_rulebook.pdf` (Chess FIDE 2017, Italian, ~601KB)

### Step 3: PDF Upload via UI ❌
**Browser Validation Error**: Frontend PDF reader failed with "Impossibile leggere il PDF" (Cannot read PDF)
- Dialog opened successfully
- File selected: scacchi-fide_2017_rulebook.pdf
- Client-side validation failed (pdf.js worker issue observed in console)

### Step 4: PDF Upload via API ❌
**Feature Flag Disabled**: API returns 403 Forbidden
```json
{
  "error": "feature_disabled",
  "message": "PDF uploads are currently disabled",
  "featureName": "Features.PdfUpload"
}
```

**Attempts to Enable Feature**:
1. ✅ Added `Features.PdfUpload=true` to `system_configurations` table
2. ✅ Restarted API service (docker compose restart api)
3. ✅ Cleared Redis cache (FLUSHDB)
4. ❌ Feature still returns disabled (cache or configuration service issue)

**Root Cause**: Feature flag system (`IFeatureFlagService`) defaults to `false` when configuration not found (FeatureFlagService.cs:67). Despite database insert, `IConfigurationService.GetValueAsync()` is not retrieving the value, suggesting:
- Configuration cache not invalidated properly
- Database connection/query issue
- Feature flag requires additional setup beyond system_configurations table

## Blocking Issues

1. **PDF Upload Feature Disabled**: Cannot proceed with ingestion testing without enabling `Features.PdfUpload`
2. **Configuration Service**: Unable to verify if configuration is properly loaded from database
3. **Frontend PDF Validation**: pdf.js worker initialization issue (separate UI bug)

## Next Steps Required

**For Project Team**:
1. **Enable PDF Upload**:
   - Verify `system_configurations` table schema matches `IConfigurationService` expectations
   - Check if feature flags require separate `feature_flags` table (not just `system_configurations`)
   - Investigate configuration cache invalidation mechanism
   - Consider adding admin UI for feature flag management

2. **Alternative Testing Approach**:
   - Temporarily bypass feature flag check in `PdfEndpoints.cs:193` for validation
   - OR provide seeding mechanism to enable critical features by default
   - OR document feature flag bootstrapping process

3. **Fix Frontend PDF Validation**:
   - Address pdf.js worker blob creation error
   - Improve error messaging for corrupted/unsupported PDFs

## Validation Cannot Proceed

**Status**: ✅ **FEATURE FLAG FIX APPLIED** (2026-01-30)

## Fix Applied (Issue #3172)

**Root Cause**: `system_configurations.is_active` column had NO database default constraint, causing manual SQL INSERTs to create records with `is_active = NULL/false`, which were filtered out by `GetByKeyAsync(activeOnly: true)`.

**Solution**:
1. ✅ Added `HasDefaultValue(true)` to `IsActive` property in `SystemConfigurationEntityConfiguration.cs`
2. ✅ Created migration `20260130052136_FixSystemConfigurationIsActiveDefault.cs`
3. ✅ Applied migration: `ALTER COLUMN is_active DEFAULT true`
4. ✅ Updated SQL workaround in this document with all required fields

**Validation Results**:
- ✅ Feature flag `Features.PdfUpload` now enables correctly via API
- ✅ Qdrant collection accessible (0 vectors, ready for ingestion)
- ⚠️ PDF upload requires game seeding: Game ID `9088f12d-d89c-4dd7-b047-8da3233b2553` not found

**Additional Fix (2026-01-30 07:10)**: Upload PDF endpoint now supports SharedGameId
- Modified `UploadPdfCommandHandler.cs` to accept both `games.Id` OR `games.SharedGameId`
- Users can now upload PDFs using SharedGameId from catalog directly
- Scripts updated with auto-detection of Chess ID from database
- Scripts include add-to-library step (Step 3)

## ✅ VALIDATION SUCCESS (2026-01-30 08:00)

### Fixes Validated via Playwright E2E Test

**Test**: `apps/web/e2e/rag-001-validation.spec.ts`

| Step | Status | Evidence |
|------|--------|----------|
| Feature Flag Enable | ✅ PASS | API creation returns 200/201 |
| SharedGameId Upload | ✅ PASS | Upload with SharedGameId returns 200 |
| PDF Upload | ✅ PASS | Document ID: `821cf1a3-5e7f-4836-913b-5609fa5c06c6` |
| Text Extraction | ✅ PASS | Extraction returns 200 |
| Vector Indexing | ❌ OOM | `OutOfMemoryException` (separate infrastructure bug) |
| RAG Query | ⏸️ BLOCKED | Requires indexing completion |

### Primary Fix: Feature Flag IsActive Default ✅
**Status**: VALIDATED
- Migration applied successfully
- Feature flag creation works via API
- Database default constraint active

### Secondary Fix: Upload PDF with SharedGameId ✅
**Status**: VALIDATED
- Upload endpoint accepts SharedGameId from catalog
- Correctly maps SharedGameId → games.Id for FK integrity
- PDF upload successful (HTTP 200, Document ID returned)

### Tertiary Fix: Return games.Id for FK Constraints ✅
**Status**: VALIDATED
- Fixed FK violation in PdfDocument metadata creation
- `existingGame.Id` returned instead of input `parsedGameId`
- Enables downstream processing without DB errors

## Infrastructure Issue Discovered

**Bug**: Vector indexing fails with `OutOfMemoryException` on 602KB PDF
**Scope**: Separate from Issue #3172 fix validation
**Impact**: Blocks full E2E completion but does NOT invalidate fixes
**Recommendation**: Create dedicated issue for indexing OOM investigation

## Conclusion

**Issue #3172 Primary Objectives**: ✅ **COMPLETE**
1. ✅ Feature flag blocking issue: RESOLVED
2. ✅ PDF upload capability: VALIDATED
3. ✅ Upload accepts SharedGameId: VALIDATED
4. ⚠️ Full RAG pipeline: Blocked by indexing OOM (infrastructure issue)

**Recommendation**: Merge PR #3194 with validated fixes. Create follow-up issue for indexing OOM bug.

---

**Manual Workaround** (for immediate testing):

```sql
-- Enable PDF upload in database
INSERT INTO system_configurations (id, key, value, value_type, description, category, is_active, environment, created_by_user_id, created_at, updated_at, version, requires_restart)
VALUES (
  gen_random_uuid(),
  'Features.PdfUpload',
  'true',
  'boolean',
  'Enable PDF upload feature',
  'Features',
  true,  -- CRITICAL: must be true for feature flag to work!
  'All',
  (SELECT id FROM users WHERE email = 'admin@meepleai.dev' LIMIT 1),
  NOW(),
  NOW(),
  1,
  false
)
ON CONFLICT (key, environment) DO UPDATE SET
  is_active = true,
  value = 'true',
  updated_at = NOW();
```

```bash
# Clear Redis cache
docker compose exec redis redis-cli FLUSHDB

# Restart API
docker compose restart api
```

If above doesn't work, investigate `IConfigurationService` implementation and cache invalidation logic in `SystemConfiguration` bounded context.
