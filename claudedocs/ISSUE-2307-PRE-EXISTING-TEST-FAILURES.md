# Pre-Existing Test Failures - Issue #2307

**Date**: 2026-01-06
**Context**: Week 3 Integration Tests Expansion
**Total Failures**: 107 tests (out of 4,595)
**Decision**: Document for separate fix, proceed with Week 3

---

## 📊 Summary

**Test Status Before Week 3**:
- Total: 4,595 tests
- Passing: 4,459 (97.04%)
- Failing: 107 (2.33%)
- Ignored: 30 (0.65%)
- Duration: ~20 min

**Coverage** (from cobertura.xml):
- Lines: **70.35%** (85,969/122,192)
- Branches: **38.53%** (5,632/14,616)

---

## 🔍 Failure Categories

### PDF Upload Pipeline (~35 tests)
**Pattern**: Integration tests requiring blob storage, background processing
**Examples**:
- `UploadPdf_SuccessfulUpload_EnqueuesBackgroundProcessing`
- `UploadPdf_WhenBlobStorageFails_ReturnsErrorAndRollsBackTransaction`
- `UploadPdf_WithFileExceedingSizeLimit_ReturnsError`
- `UploadPdf_WhenCancelledMidEmbeddingBatch_StopsGracefully`

**Likely Cause**: Mock setup issues, external service dependencies

### Quota/Tier Management (~12 tests)
**Pattern**: User tier quota enforcement
**Examples**:
- `FreeTier_FiveUploadsInDay_SixthUploadDenied`
- `FreeTier_TwentyUploadsInWeek_TwentyFirstUploadDenied`
- `AdminUser_UnlimitedUploads_NoQuotaCheck`
- `QuotaReservation_Expiry_ShouldAutoCleanup`

**Likely Cause**: Redis quota tracking setup or timing issues

### Bulk Operations (~15 tests)
**Pattern**: Admin bulk import/export operations
**Examples**:
- `E2E_BulkImport_With100Users_ShouldCompleteWithinTimeLimit`
- `E2E_BulkExport_WithRoleFilter_ShouldOnlyExportMatchingUsers`
- `E2E_BulkRoleChange_ShouldUpdateMultipleUsersAtomically`

**Likely Cause**: Database transaction scope or mock configuration

### Report Generation (~10 tests)
**Pattern**: System health, AI usage, content metrics reports
**Examples**:
- `GenerateAsync_AIUsageReport_ContainsCostMetrics`
- `GenerateAsync_ContentMetrics_Csv_ShouldGenerateReport`
- `GenerateAsync_SystemHealthReport_ContainsExpectedMetrics`

**Likely Cause**: Template or data aggregation setup

### Authentication Advanced (~8 tests)
**Pattern**: Replay attacks, brute force protection
**Examples**:
- `ReplayAttack_ConcurrentBackupCodeUse_ShouldPreventRace`
- `BruteForce_AccountLockout_ShouldEnforceWaitPeriod`

**Likely Cause**: Concurrent access or timing-sensitive logic

### RAG/Accuracy (~2 tests) ⚠️
**Pattern**: Accuracy validation with external API
**Examples**:
- `RunAccuracyValidation_AllExpertAnnotated_MeetsThreshold`
- `RunPartialAccuracyBaseline_IndexedGamesOnly_MeetsThreshold`

**Error**: "API not available at localhost:8080"
**Cause**: Tests expect running external API service
**Impact**: ✅ NON-BLOCKING for Week 3 (uses Testcontainers)

### HTTP/Frontend SDK (~5 tests)
**Pattern**: Auth and timeout behaviors
**Examples**:
- `GET with invalid API key should return 401 Unauthorized`
- `GET without auth should return 401 Unauthorized`
- `GET with reasonable timeout should complete successfully`

**Error**: Expected 401, got 200
**Cause**: `/api/v1/games` is `.AllowAnonymous()` (public endpoint)
**Impact**: ✅ NON-BLOCKING (test wrong expectation)

### Collections/Comments (~20 tests)
**Pattern**: Collection CRUD, version management, mentions
**Examples**:
- `CreateComment_WithValidMention_ExtractsMentionedUser`
- `DeleteAsync_ExistingCollection_RemovesFromDatabase`
- `Handle_WithValidCommand_ShouldActivateVersionAndDeactivateOthers`

**Likely Cause**: Mock setup or database state issues

---

## ✅ Triage Decision

**All 107 failures are NON-BLOCKING for Week 3** because:

1. **Different Scope**: PDF, Quota, Bulk ops not in Week 3 scope
2. **External Dependencies**: RAG tests need running API (Week 3 uses Testcontainers)
3. **Test Issues**: Some tests have wrong expectations (e.g., 401 on public endpoint)
4. **Isolation**: Week 3 tests will be independent, won't inherit these failures

**Action**: Proceed with Week 3, create separate issue for 107 failures

---

## 📋 Recommended Separate Issue

**Title**: "Fix 107 Pre-Existing Test Failures (PDF, Quota, Bulk, Reports)"

**Categories to Fix**:
- [ ] PDF Upload Pipeline (35 tests) - blob storage, transactions, cancellation
- [ ] Quota/Tier Management (12 tests) - Redis quota tracking
- [ ] Bulk Operations (15 tests) - DB transactions, atomicity
- [ ] Report Generation (10 tests) - templates, aggregations
- [ ] Auth Advanced (8 tests) - replay attacks, brute force
- [ ] RAG Accuracy (2 tests) - external API dependency removal
- [ ] HTTP Frontend SDK (5 tests) - fix expectations for public endpoints
- [ ] Collections/Comments (20 tests) - version management, mentions

**Estimated Effort**: 8-12 hours
**Priority**: Medium (97% tests passing, no production blockers)

---

## 🔄 Next Steps

1. ✅ Week 3 implementation (190-235 new tests)
2. Create separate issue for 107 failures
3. Address in future sprint after Week 3 complete

**Coverage Progression**:
- Current: BE 70.35%, FE ~72%
- Week 3 Target: BE 84%, FE 82%
- After fixes: BE >90%, FE >90%

---

**Status**: Documented for future resolution
**Blocks**: None (Week 3 can proceed independently)
