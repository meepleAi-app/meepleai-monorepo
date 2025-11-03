# TEST-651: Remaining Test Failures Analysis (78 tests)

**Date**: 2025-11-03
**Status**: After #648-650 fixes + rebuild
**Previous**: 225 total → 160 fixed → 65 expected remaining
**Actual**: 78 remaining (13 more than expected)

## Summary by Category

| Category | Count | % | Priority | Estimated Time |
|----------|-------|---|----------|----------------|
| RAG/QA Service | 24 | 31% | HIGH | 4-5h |
| Logging/Sanitization | 10 | 13% | MEDIUM | 2-3h |
| PDF Processing | 7 | 9% | HIGH | 2-3h |
| Cache Warming | 5 | 6% | MEDIUM | 1-2h |
| Integration Scenarios | 4 | 5% | HIGH | 2-3h |
| Admin/Quality Endpoints | 4 | 5% | MEDIUM | 1-2h |
| N8n Templates | 3 | 4% | LOW | 1h |
| Authorization | 3 | 4% | HIGH | 1h |
| Other | 18 | 23% | MIXED | 3-4h |
| **TOTAL** | **78** | **100%** | | **17-25h** |

## Detailed Breakdown

### 1. RAG/QA Service (24 tests) - Priority: HIGH

**Root Cause**: Mock configuration issues with LLM/Embedding services, cache behavior mismatches

**Tests**:
- AskAsync_* (16 tests): Various RAG scenarios
- ExplainAsync_* (8 tests): Explanation generation

**Pattern**:
```csharp
// Issue: Mock not returning expected values
_mockLlmService.Setup(x => x.GenerateAsync(...))
    .ReturnsAsync(new LlmResponse { ... });

// Fix: Adjust mock to match actual service behavior
_mockLlmService.Setup(x => x.GenerateAsync(
    It.IsAny<string>(),
    It.IsAny<string>(),
    It.IsAny<CancellationToken>()))
    .ReturnsAsync(new LlmResponse {
        Content = "Expected content",
        Success = true
    });
```

**Estimated**: 4-5 hours

### 2. Logging/Sanitization (10 tests) - Priority: MEDIUM

**Root Cause**: Log redaction/sanitization assertions don't match current implementation

**Tests**:
- LogEvent_WithConnectionString_RedactsPassword
- LogEvent_WithApiKeyInString_RedactsKey
- LogEvent_WithMultipleSensitiveFields_RedactsAll
- LogEvent_WithSensitivePassword_RedactsInLogs
- InvokeAsync_LogsSanitizedPath*
- Request_WithAuthentication_LogsUserContext
- Request_WithNoAuthentication_LogsWithCorrelationId
- MultipleRequests_HaveUniqueCorrelationIds
- Logger_ConfiguredWithEnvironment_UsesCorrectLogLevel

**Pattern**:
```csharp
// Issue: Assertion expects exact redaction format
logs.Should().Contain("password=***");

// Fix: Use flexible matching or update to actual format
logs.Should().Match("*password*REDACTED*");
```

**Estimated**: 2-3 hours

### 3. PDF Processing (7 tests) - Priority: HIGH

**Root Cause**: PDF extraction error handling doesn't match expectations

**Tests**:
- ExtractPagedTextAsync_CorruptedPdf_ReturnsStructuredError (duplicate)
- ExtractPagedTextAsync_EmptyPages_HandledGracefully
- ExtractPagedTextAsync_EmptyPdf_HandledGracefully
- ExtractPagedTextAsync_LargePage_CapturedCorrectly
- ExtractPagedTextAsync_MultiPagePdf_ReturnsAccuratePageNumbers
- ExtractPagedTextAsync_NullFilePath_ReturnsError
- ExtractPagedTextAsync_SmallPage_ProcessedCorrectly
- IndexPdfAsync_EmbeddingFailure_ReturnsFailureWithError
- IndexPdfAsync_WithValidExtractedText_IndexesSuccessfully

**Pattern**:
```csharp
// Issue: Error structure changed
result.Error.Should().Contain("corrupted");

// Fix: Match new error format
result.Should().NotBeNull();
result.Success.Should().BeFalse();
result.ErrorMessage.Should().Contain("PDF is corrupted");
```

**Estimated**: 2-3 hours

### 4. Cache Warming (5 tests) - Priority: MEDIUM

**Root Cause**: Background service timing/scope disposal issues

**Tests**:
- ExecuteAsync_Startup_WarmsTop50Queries
- ExecuteAsync_AlreadyCached_SkipsQuery
- ExecuteAsync_MultipleGames_RespectsGameIsolation
- ExecuteAsync_LlmFailure_ContinuesWarmingRemainingQueries
- ExecuteAsync_ServiceScope_CreatesAndDisposesCorrectly (+ 2 related)

**Pattern**:
```csharp
// Issue: Timing-dependent assertions fail
await Task.Delay(1000);
_mockCache.Verify(x => x.GetAsync(...), Times.Exactly(50));

// Fix: Use TaskCompletionSource for synchronization
var tcs = new TaskCompletionSource<bool>();
_mockCache.Setup(x => x.SetAsync(...))
    .Callback(() => tcs.SetResult(true));
await tcs.Task;
```

**Estimated**: 1-2 hours

### 5. Integration Scenarios (4 tests) - Priority: HIGH

**Root Cause**: Full-stack RAG tests with Testcontainers timing issues

**Tests**:
- Scenario_GivenQuestion_WhenContextIsIrrelevant_ThenLlmReturnsNotSpecified
- Scenario_GivenQuestion_WhenMultiplePdfsHaveContext_ThenSnippetsFromAllSources
- Scenario_GivenQuestion_WhenNoContextExists_ThenReturnsNotSpecified
- Scenario_GivenValidQuestion_WhenContextExists_ThenReturnsAnswerWithSnippets

**Pattern**:
```csharp
// Issue: Testcontainers not fully initialized
var response = await _client.PostAsync("/api/qa", content);

// Fix: Add proper wait for container readiness
await WaitForDatabaseReadyAsync();
await WaitForQdrantReadyAsync();
```

**Estimated**: 2-3 hours

### 6. Admin/Quality Endpoints (4 tests) - Priority: MEDIUM

**Tests**:
- AdminEndpoint_DateRangeFilter_ReturnsFilteredResults
- AdminEndpoint_GetLowQualityResponses_ReturnsOnlyLowQuality
- QaEndpoint_ConcurrentRequests_AllLogged
- QaEndpoint_QualityScores_StoredInDatabase

**Pattern**: Database query/seeding issues

**Estimated**: 1-2 hours

### 7. N8n Templates (3 tests) - Priority: LOW

**Tests**:
- GetTemplate_ReturnsTemplate_WhenTemplateExists
- GetTemplates_ReturnsTemplates_WhenAuthenticated
- ImportTemplate_ReturnsBadRequest_WhenMissingRequiredParameters

**Pattern**: Authentication/endpoint routing issues

**Estimated**: 1 hour

### 8. Authorization (3 tests) - Priority: HIGH

**Tests**:
- DeleteChatAsync_WhenNotOwner_ThrowsUnauthorized
- DeleteCommentAsync_WhenNotOwnerAndNotAdmin_ThrowsUnauthorizedAccessException
- UpdateCommentAsync_WhenNotOwner_ThrowsUnauthorizedAccessException

**Pattern**: Exception type changed from UnauthorizedAccessException to specific exception

**Estimated**: 1 hour

### 9. Other (18 tests) - Priority: MIXED

**Tests**:
- AskStreamAsync_SupportsCancellation
- AssertTimeNear_OutsideTolerance_Throws
- BackupCodeVerification_SerializableIsolation_PreventsDoubleUse
- CompareVersionsAsync_MarginalChanges_ReturnsManualReview
- Evaluation_WithIndexedDocuments_RetrievesRelevantResults
- GenerateQuestionsAsync_ExceptionWithFailOnErrorTrue_ThrowsException
- GetElapsedTime_CalculatesCorrectly
- HandleCallbackAsync_ExistingUserEmail_LinksOAuthAccount
- ImportTemplateAsync_* (3 tests)
- InvalidateSubsequentMessagesAsync_* (2 tests)
- + 7 more

**Pattern**: Various isolated issues requiring individual investigation

**Estimated**: 3-4 hours

## Implementation Plan

### Phase 1: High-Priority Quick Wins (6-8h)
1. Authorization (3 tests) - 1h
2. PDF Processing (7 tests) - 2-3h
3. Integration Scenarios (4 tests) - 2-3h
4. RAG/QA Service (24 tests) - Start with mock setup

### Phase 2: Medium-Priority Fixes (5-8h)
5. RAG/QA Service (complete remaining)
6. Logging/Sanitization (10 tests)
7. Cache Warming (5 tests)
8. Admin/Quality (4 tests)

### Phase 3: Low-Priority & Cleanup (4-6h)
9. N8n Templates (3 tests)
10. Other (18 tests) - Triage and fix systematically

### Phase 4: Validation & PR (2-3h)
11. Full test run with 100% pass rate
12. Update issue #651
13. Create comprehensive PR
14. Code review and merge

## Success Criteria

- [ ] All 78 test failures resolved
- [ ] 100% pass rate on full test suite
- [ ] No regressions introduced
- [ ] Issue #651 updated with final status
- [ ] PR merged and issue closed

## Notes

- Tests are concentrated in RAG/QA (31%) and Logging (13%) areas
- Many failures are mock configuration issues, not code bugs
- Integration tests need Testcontainers timing fixes
- Total estimated time: 17-25 hours (matches original 22-30h estimate)
