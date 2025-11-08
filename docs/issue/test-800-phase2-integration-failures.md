# TEST-800 Phase 2: Integration Test Failures

## Overview
After fixing 37 unit test failures in Phase 1, 40 integration test failures remain. These failures are infrastructure-related, requiring investigation of Testcontainers stability and service integration issues.

## Phase 1 Summary (Completed)
**Fixed**: 37 unit tests
- UserManagementService: 3 tests (PasswordHash mock)
- ConfigurationHelper: 21 tests (Moq extension methods)
- ChatExportService: 10 tests (filename sanitization)
- DataMasking: 3 tests (JWT/connection redaction)

**Commit**: 5c4c2523 "fix(tests): Resolve 37 unit test failures (TEST-800 Phase 1)"

## Phase 2: Remaining Integration Test Failures (40 tests)

### Category 1: N8n/Chess Webhook Integration (22 tests)

**Root Cause**: Testcontainers PostgreSQL connection instability
- Error: `System.IO.IOException: Unable to read data from the transport connection`
- Error: `SocketException: Connessione interrotta dal software del computer host`
- Location: `QdrantRagTestFixture.WaitForDatabaseMigrationsAsync()`

**Failing Tests:**
```
WebhookFlow_ResponseFormat_MatchesStandardizedPayload
WebhookFlow_WithValidSession_ReturnsExplanation
WebhookFlow_GameWithoutContent_ReturnsNoResults
WebhookFlow_WithoutGameId_ReturnsBadRequest
WebhookFlow_WithoutSession_ReturnsUnauthorized
N8nConfig_CreateAndRetrieve_Success

ChessWebhookFlow_ResponseFormat_MatchesStandardizedPayload
ChessWebhookFlow_WithValidSession_ReturnsAnalysis
ChessWebhookFlow_WithoutQuestion_ReturnsBadRequest
ChessWebhookFlow_OpeningQuestion_ReturnsOpeningInfo
ChessWebhookFlow_WithFenPosition_ReturnsPositionalAnalysis
ChessWebhookFlow_WithChatId_PersistsConversation
ChessWebhookFlow_WithoutSession_ReturnsUnauthorized

PostAgentsExplain_WhenAuthenticated_ReturnsExplanation
PostAgentsExplain_WhenUnauthenticated_ReturnsUnauthorized
PostAgentsExplain_WithChatId_PersistsToChat
PostAgentsExplain_WithoutGameId_ReturnsBadRequest
PostAgentsExplain_WithEmptyTopic_ReturnsErrorMessage
PostAgentsExplain_WithoutIndexedContent_ReturnsNoResults
PostAgentsExplain_TracksTokenUsage

AskChessAgent_SimpleRulesQuestion_ReturnsAnswerWithSources
AskChessAgent_PositionAnalysisWithFEN_ReturnsAnalysisAndMoves
AskChessAgent_OpeningQuestion_ReturnsExplanation
AskChessAgent_TacticalQuestion_ReturnsExplanationWithExamples
AskChessAgent_InvalidFEN_ReturnsWarning
AskChessAgent_EmptyQuestion_ReturnsBadRequest
AskChessAgent_WithoutAuthentication_ReturnsUnauthorized
AskChessAgent_ReturnsTokenUsage
AskChessAgent_SameQuestionTwice_ReturnsCachedResponse
```

**Investigation Needed:**
1. QdrantRagTestFixture PostgreSQL connection retry logic
2. Testcontainers lifetime management
3. Docker container health check timing
4. Test isolation and cleanup procedures

**Test Files:**
- `tests/Api.Tests/N8nWebhookIntegrationTests.cs`
- `tests/Api.Tests/ChessAgentEndpointsTests.cs`
- `tests/Api.Tests/Fixtures/QdrantRagTestFixture.cs`

### Category 2: 2FA Integration Tests (3 tests)

**Root Cause**: Server returning 500 Internal Server Error instead of 401 Unauthorized

**Failing Tests:**
```
Verify_ValidBackupCode_CreatesSessionAndMarksCodeUsed
Verify_InvalidTempSessionToken_Returns401
Verify_ExpiredTempSession_Returns401
```

**Error Pattern:**
- Expected: `HttpStatusCode.Unauthorized (401)`
- Actual: `HttpStatusCode.InternalServerError (500)`

**Investigation Needed:**
1. TempSessionService error handling
2. TotpService verification exception handling
3. Middleware error catching in 2FA endpoints
4. Proper 401 vs 500 error classification

**Test File:**
- `tests/Api.Tests/Integration/TwoFactorAuthEndpointsTests.cs`

### Category 3: Setup Guide Integration Tests (5 tests)

**Root Cause**: RAG/LLM service integration issues

**Failing Tests:**
```
GivenAuthenticatedUser_WhenRequestingSetupGuide_ThenReturnsStructuredGuide
GivenRagData_WhenRequestingSetupGuide_ThenIncludesConfidenceScore
GivenNonExistentGame_WhenRequestingSetupGuide_ThenReturnsDefaultGuide
GivenSetupGuideGeneration_WhenComplete_ThenIncludesTokenUsage
GivenSetupGuideRequest_WhenComplete_ThenLogsRequest
```

**Investigation Needed:**
1. SetupGuideService RAG integration
2. LLM service mock/configuration
3. Test fixture data seeding
4. Response structure validation

**Test File:**
- `tests/Api.Tests/SetupGuideEndpointsTests.cs`

### Category 4: Misc Integration Tests (10 tests)

**Failing Tests:**
```
GivenStreamingQaRequest_WhenComplete_ThenLogsRequest
GivenChatId_WhenRequestingSetupGuide_ThenLogsToChat
GivenGameSetup_WhenRequestingGuide_ThenIncludesEstimatedTime
GivenMultipleUsers_WhenRequestingSetupGuideConcurrently_ThenAllSucceed
GivenNoGameId_WhenRequestingSetupGuide_ThenReturnsBadRequest
QaEndpoint_ConcurrentRequests_AllLogged
PostIngestPdf_WhenFileSizeExceedsMaximum_ReturnsBadRequest
PostComment_CreatesVersionLevelComment_ForAdmin
GetPdfText_WithoutAuthentication_ReturnsUnauthorized
GetLogs_ReturnsLatestEntriesFromAiRequestLogService
RegisterLoginLogout_RoundTrip_Succeeds
TryDestructure_WithEmptyString_ReturnsOriginalValue
LogEvent_WithMultipleSensitiveFields_RedactsAll
```

**Investigation Needed:**
1. Testcontainers fixture initialization
2. Service integration error handling
3. Authentication middleware behavior
4. Logging destructuring policies

## Recommended Approach for Phase 2

### Step 1: Fix Testcontainers Stability
**Priority**: HIGH (affects 22+ tests)
**Estimated**: 2-3 hours

Actions:
1. Review `QdrantRagTestFixture.cs` connection handling
2. Add retry logic with exponential backoff
3. Increase connection timeouts
4. Ensure proper container health checks
5. Add connection pooling configuration

### Step 2: Fix 2FA Error Handling
**Priority**: MEDIUM (affects 3 tests)
**Estimated**: 1 hour

Actions:
1. Review TempSessionService exception handling
2. Add try-catch in 2FA endpoints to return 401 for auth failures
3. Ensure proper error middleware configuration
4. Test with invalid/expired tokens

### Step 3: Fix Setup Guide/QA Integration
**Priority**: MEDIUM (affects 15 tests)
**Estimated**: 2-3 hours

Actions:
1. Review test fixture seeding for RAG content
2. Verify LLM service configuration in tests
3. Check OpenRouter API key availability
4. Add proper mocks for external services

### Step 4: Fix Misc Integration Tests
**Priority**: LOW (affects 10 tests)
**Estimated**: 1-2 hours

Actions:
1. Individual investigation per test
2. Fix specific service integration issues
3. Review logging destructuring policies

## Total Phase 2 Estimate
**Time**: 6-9 hours
**Complexity**: High (infrastructure + integration)
**Risk**: Medium (Testcontainers changes affect many tests)

## Success Criteria
- All 40 integration tests passing
- 0 test failures in full suite
- No Testcontainers connection errors
- Proper error handling (401 vs 500)
- Clean test output with no warnings

## Notes
- Phase 1 already fixed all unit test failures
- Remaining failures are ALL integration tests
- Most issues are infrastructure-related, not code bugs
- Consider CI/CD implications for Testcontainers stability

---
*Generated during TEST-800 Phase 1 completion*
*Date: 2025-11-08*
