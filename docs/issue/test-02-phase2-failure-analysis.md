# TEST-02 Phase 2: Test Failure Analysis

**Issue**: #391 - Increase backend test coverage to 90%
**Phase**: 2 of 4 - Systematic Test Fix
**Date**: 2025-10-17
**Status**: 🔧 IN PROGRESS

## Summary

After Phase 1 fixes, **50 tests are still failing** out of 781 total tests (718 passing, 13 skipped).

**Current Pass Rate**: 93.6% (718/768 non-skipped tests)
**Target**: Fix high-ROI test failures to enable accurate coverage measurement

---

## Test Failure Categories (Prioritized by ROI)

### Category 1: PDF Delete Endpoint Routing ❗ **HIGH PRIORITY**
**Count**: 6 failures
**Impact**: High - Core functionality tests
**Effort**: Low - Likely simple routing fix
**ROI**: ⭐⭐⭐⭐⭐

**Failure Pattern**: All tests return `NotFound` (404) instead of expected `NoContent` (204) or `Forbidden` (403)

**Tests**:
1. `DeletePdf_EditorCanDeleteOwnPdf` - Expected: NoContent, Actual: NotFound
2. `DeletePdf_AdminCanDeleteAnyUsersPdf` - Expected: NoContent, Actual: NotFound
3. `DeletePdf_UserCanDeleteOwnPdf` - Expected: NoContent, Actual: NotFound
4. `DeletePdf_EditorCannotDeleteOtherUsersPdf` - Expected: Forbidden, Actual: NotFound
5. `AuditLog_AccessDeniedIsLogged` - Expected: Forbidden, Actual: NotFound
6. `DeletePdf_UserCannotDeleteOtherUsersPdf` - Expected: Forbidden, Actual: NotFound

**Root Cause Hypothesis**:
- DELETE endpoint might not be registered in `Program.cs`
- Route mismatch (e.g., `/api/v1/pdf/{id}` vs `/pdf/{id}`)
- Endpoint exists but requires different HTTP method

**Test File**: `RlsAndAuditEndpointsTests.cs`

**Fix Strategy**:
1. Check `Program.cs` for PDF delete endpoint registration (search for `MapDelete.*pdf`)
2. Verify route pattern matches test expectations
3. Ensure endpoint is in correct API version group (`v1Api`)
4. Run single test to verify fix

---

### Category 2: Authentication Endpoint Validation ⚠️ **MEDIUM-HIGH PRIORITY**
**Count**: 12 failures
**Impact**: High - Critical auth functionality
**Effort**: Medium - Multiple validation scenarios
**ROI**: ⭐⭐⭐⭐

**Failure Pattern**: Validation tests not returning expected `BadRequest` responses

**Tests**:
1. `PostAuthLogin_CreatesNewSessionEachTime` - New session creation
2. `PostAuthRegister_WithMissingDisplayName_ReturnsBadRequest` - Validation
3. `PostAuthLogin_WithEmptyPassword_ReturnsBadRequest` - Validation
4. `PostAuthLogin_WithValidCredentials_CreatesSessionWithCorrectExpiration` - Session expiry
5. `PostAuthRegister_WithInvalidEmailFormat_ReturnsBadRequest` - Email validation
6. `PostAuthRegister_WithWhitespaceOnlyDisplayName_ReturnsBadRequest` - Validation
7. `PostAuthLogin_WithEmptyEmail_ReturnsBadRequest` - Validation
8. `PostAuthRegister_WithTooLongDisplayName_ReturnsBadRequest` - Length validation
9. `GetAuthMe_WithExpiredSession_ReturnsUnauthorized` - Session expiry
10. `PostAuthRegister_WithEmptyDisplayName_ReturnsBadRequest` - Validation
11. `DELETE_AdminSessionId_AsAdmin_Returns200AndRevokesSession` - Session management
12. `DELETE_AdminUsersUserIdSessions_AsAdmin_Returns200AndRevokesAllSessions` - Bulk revoke
13. `GET_UsersMeSessions_AsUnauthenticated_Returns401` - Auth check
14. `GET_UsersMeSessions_AsAuthenticatedUser_Returns200WithOwnSessions` - Session list

**Root Cause Hypothesis**:
- Request validation attributes missing or not triggered
- Endpoint model binding not enforcing constraints
- Session management endpoints routing issues

**Test File**: Multiple (`SessionManagementEndpointsTests.cs`, auth endpoint tests)

**Fix Strategy**:
1. Verify `[Required]`, `[EmailAddress]`, `[StringLength]` attributes on DTOs
2. Check `ModelState.IsValid` checks in endpoints
3. Verify session expiry logic in `AuthService`
4. Run tests individually to isolate specific validation failures

---

### Category 3: Logging Integration Tests 📝 **MEDIUM PRIORITY**
**Count**: 7 failures
**Impact**: Medium - Known Phase 1 issue (TestCorrelator)
**Effort**: Medium - Requires TestCorrelator configuration fix
**ROI**: ⭐⭐⭐

**Failure Pattern**: TestCorrelator not capturing log events despite tests running

**Tests**:
1. `LogEvent_WithConnectionString_RedactsPassword` - Sensitive data redaction
2. `LogEvent_WithApiKeyInString_RedactsKey` - API key redaction
3. `Logger_ConfiguredWithEnvironment_UsesCorrectLogLevel` - Log level config
4. `LogEvent_WithSensitivePassword_RedactsInLogs` - Password redaction
5. `LogEvent_WithMultipleSensitiveFields_RedactsAll` - Multi-field redaction
6. `Request_WithNoAuthentication_LogsWithCorrelationId` - Correlation ID enrichment

**Root Cause**:
- **Known from Phase 1**: LoggingIntegrationTests EF Core conflict was resolved, but TestCorrelator sink doesn't capture logs
- LoggingTestFactory properly inherits from WebApplicationFactoryFixture
- ConfigureLogging() sets up TestCorrelator sink correctly
- **Issue**: TestCorrelator context not propagating to WebApplicationFactory logs

**Test File**: `Logging/LoggingIntegrationTests.cs`
**Test Factory**: `LoggingTestFactory` (extends `WebApplicationFactoryFixture`)

**Fix Strategy**:
1. Investigate why TestCorrelator.GetLogEventsFromCurrentContext() returns empty
2. Try alternative: Use in-memory log sink instead of TestCorrelator
3. Consider capturing logs via ITestOutputHelper
4. May require custom log sink implementation for integration tests

---

### Category 4: Qdrant Vector Search Integration 🔍 **MEDIUM PRIORITY**
**Count**: 8 failures
**Impact**: High - Core RAG functionality
**Effort**: High - Testcontainers setup
**ROI**: ⭐⭐⭐

**Failure Pattern**: Qdrant integration tests failing (likely Testcontainers issues)

**Tests**:
1. `IndexDocumentChunksAsync_WithMultiplePages_IndexesCorrectly` - Multi-page indexing
2. `SearchAsync_WithLimitParameter_RespectsLimit` - Search result limiting
3. `IndexDocumentChunksAsync_WithValidChunks_IndexesSuccessfully` - Basic indexing
4. `SearchAsync_WithoutTenantFilter_ReturnsResults` - Search without tenant filter
5. `SearchAsync_WithDifferentGame_ReturnsNoResults` - Game filtering
6. `DeleteDocumentAsync_AfterIndexing_RemovesDocument` - Document deletion
7. `SearchAsync_AfterIndexing_ReturnsRelevantResults` - Search relevance
8. `EnsureCollectionExistsAsync_WhenCollectionMissing_CreatesCollectionAndIndexes` - Collection creation

**Root Cause Hypothesis**:
- Qdrant Testcontainer not starting properly on Windows
- Connection string/URL mismatch
- Collection initialization timing issues
- Qdrant version compatibility

**Fix Strategy**:
1. Check if Qdrant Testcontainer is running: `docker ps`
2. Verify Qdrant health: `curl http://localhost:6333/healthz`
3. Add container startup wait logic if needed
4. Check test base class initialization (IAsyncLifetime)

---

### Category 5: Streaming QA Precision Issues 🎯 **LOW PRIORITY**
**Count**: 3 failures
**Impact**: Low - Floating point precision edge case
**Effort**: Low - Simple fix
**ROI**: ⭐⭐

**Failure Pattern**: Floating point precision mismatch + cancellation not working

**Tests**:
1. `AskStreamAsync_CalculatesConfidenceFromMaxScore` - Expected: 0.95, Actual: 0.9499999880790...
2. `AskStreamAsync_WithSuccessfulFlow_EmitsCorrectEventSequence` - Expected: 0.95, Actual: 0.9499999880790...
3. `AskStreamAsync_SupportsCancellation` - Expected: OperationCanceledException, Actual: No exception

**Root Cause**:
- **Precision**: Float/double rounding differences (0.95 vs 0.9499999880790...)
- **Cancellation**: CancellationToken not properly handled in streaming method

**Test File**: `StreamingQaServiceTests.cs`

**Fix Strategy**:
1. Use `Assert.Equal(expected, actual, precision: 0.01)` for confidence assertions
2. Add proper `ct.ThrowIfCancellationRequested()` in `AskStreamAsync` implementation
3. Verify cancellation token propagation in `IAsyncEnumerable<T>` streaming

---

### Category 6: PDF Text Extraction Tests 📄 **SKIP** (Environment-Specific)
**Count**: 8 failures
**Impact**: Low - Tests pass in CI (Linux with libgdiplus)
**Effort**: High - Windows native dependency issues
**ROI**: ⭐ (Skip/Document)

**Failure Pattern**: All PDF text extraction tests fail due to Docnet.Core native library issues on Windows

**Tests**:
1. `ExtractTextAsync_NormalizesWhitespace`
2. `ExtractTextAsync_LogsInformation_OnSuccessfulExtraction`
3. `ExtractTextAsync_ExtractsTextSuccessfully_FromSimplePdf`
4. `ExtractTextAsync_ExtractsTextFromMultiplePages`
5. `ExtractTextAsync_HandlesWhitespaceOnlyPdf`
6. `ExtractTextAsync_HandlesEmptyPdf`
7. `ExtractTextAsync_CalculatesCorrectCharacterCount`
8. `ExtractTextAsync_LogsWarning_WhenNoTextExtracted`

**Root Cause**:
- Docnet.Core requires `libgdiplus` on Linux (installed in CI: `.github/workflows/ci.yml:182`)
- Windows may require different native libraries or configuration
- **Known limitation**: Tests pass in CI, fail locally on Windows without proper PDF dependencies

**Test File**: `PdfTextExtractionServiceTests.cs`

**Fix Strategy**:
1. ✅ **Document limitation**: Add comment to test class explaining Windows requirements
2. ✅ **Skip on Windows**: Add `[SkipOnWindows]` attribute or conditional skip logic
3. ❌ **Install dependencies**: Too complex for local dev (focus on CI passing)

**Recommended Action**: Document and skip - these tests validate in CI where it matters

---

### Category 7: Miscellaneous Integration Tests 🧩 **LOW PRIORITY**
**Count**: 6 failures
**Impact**: Low-Medium - Various subsystems
**Effort**: Medium - Requires individual investigation
**ROI**: ⭐⭐

**Tests**:
1. `CustomOriginsAreLoadedFromTopLevelAllowedOriginsSection` - CORS config
2. `ChessWebhookFlow_WithChatId_PersistsConversation` - n8n webhook integration
3. `GetPdfText_WithoutAuthentication_ReturnsUnauthorized` - Auth on PDF endpoint
4. `HealthCheckEndpoint_DoesNotCreateTraces` - OpenTelemetry filtering
5. `AskChessAgent_EmptyQuestion_ReturnsBadRequest` - Chess agent validation

**Fix Strategy**: Investigate individually after higher-priority fixes

---

## Prioritized Fix Order

### 🔥 **Session 1: Quick Wins** (Est: 2-3 hours)
1. ✅ **PDF Delete Endpoints** (6 tests) - Likely simple routing fix
2. ✅ **Streaming Precision** (3 tests) - Float comparison + cancellation fix

**Expected Result**: 9 tests fixed → 727 passing (94.8% pass rate)

### 🎯 **Session 2: Auth & Validation** (Est: 3-4 hours)
3. ✅ **Authentication Endpoints** (12 tests) - Add/fix validation attributes

**Expected Result**: 12 tests fixed → 739 passing (96.3% pass rate)

### 📊 **Session 3: Infrastructure** (Est: 4-6 hours)
4. ✅ **Qdrant Integration** (8 tests) - Testcontainers setup
5. ✅ **Logging Tests** (7 tests) - TestCorrelator configuration

**Expected Result**: 15 tests fixed → 754 passing (98.3% pass rate)

### 📝 **Session 4: Cleanup** (Est: 2-3 hours)
6. ✅ **Miscellaneous Tests** (6 tests) - Individual investigation
7. ✅ **Document PDF Limitation** (8 tests) - Add skip attribute + comment

**Expected Result**: All tests passing or documented → 760+ passing/skipped

---

## Coverage Measurement Plan

Once test failures are reduced to < 10:
1. Run full test suite with coverage
2. Generate HTML report with `reportgenerator`
3. Identify services/classes below 90%
4. Add targeted unit tests for uncovered scenarios

**Command**:
```bash
cd apps/api
dotnet test -p:CollectCoverage=true -p:CoverletOutputFormat=cobertura
reportgenerator -reports:coverage.cobertura.xml -targetdir:coverage-html -reporttypes:Html
```

---

## Key Learnings

1. **Prioritize by ROI**: Fix high-impact, low-effort tests first (routing, validation)
2. **Environment-specific tests**: Document and skip tests that require specific native dependencies
3. **CI/CD is source of truth**: Tests must pass in CI, local failures are acceptable if documented
4. **Systematic approach**: Fix categories one at a time to avoid context switching

---

## Next Steps

1. ✅ Fix PDF Delete endpoint routing (6 tests)
2. ✅ Fix streaming float precision + cancellation (3 tests)
3. ⏭️ Fix authentication validation (12 tests)
4. ⏭️ Investigate Qdrant Testcontainers setup (8 tests)
5. ⏭️ Fix TestCorrelator logging configuration (7 tests)

---

**Session Start**: 2025-10-17
**Estimated Completion**: Phase 2 - 2-3 days (6-8 working sessions)
