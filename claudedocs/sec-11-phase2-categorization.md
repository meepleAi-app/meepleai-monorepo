# SEC-11 Phase 2: Generic Exception Catch Categorization

**Objective**: Refactor 180 generic `catch (Exception ex)` blocks to improve error handling specificity and code quality.

**Status**: Analysis Complete | Execution Pending

---

## Executive Summary

| Category | Count | Priority | Estimated Effort |
|----------|-------|----------|------------------|
| **HIGH: Service Method Fixes** | 85 | 🔴 Critical | 20-25 hours |
| **MEDIUM: Resilience Patterns** | 42 | 🟡 Important | 8-10 hours |
| **LOW: Endpoint Handlers** | 47 | 🟢 Recommended | 4-6 hours |
| **SKIP: Already Filtered** | 6 | ⚪ N/A | 0 hours |
| **Total** | 180 | - | **32-41 hours** |

---

## 🔴 HIGH PRIORITY: Service Method Fixes (85 catches)

These service methods should catch **specific exceptions** based on their operations:

### AI/RAG Services (15 catches)

#### EmbeddingService.cs (3 catches)
- **Lines**: 102, 251, 316
- **Operations**: HTTP calls to OpenRouter/Ollama, JSON deserialization
- **Specific Exceptions**:
  ```csharp
  catch (HttpRequestException ex) // Network failures
  catch (TaskCanceledException ex) // Timeouts (already has one)
  catch (JsonException ex) // Invalid API response
  ```
- **Effort**: 1.5 hours

#### RagService.cs (4 catches)
- **Lines**: 233, 374, 622, 792
- **Operations**: Vector search, LLM calls, DB queries
- **Specific Exceptions**:
  ```csharp
  catch (HttpRequestException ex) // LLM API failures
  catch (InvalidOperationException ex) // No search results
  catch (DbUpdateException ex) // Database errors
  ```
- **Effort**: 2 hours

#### QdrantService.cs (10 catches)
- **Lines**: 52, 119, 204, 290, 337, 404, 459, 547, 639, 685
- **Operations**: Vector DB calls, gRPC communication
- **Specific Exceptions**:
  ```csharp
  catch (RpcException ex) // Qdrant gRPC errors
  catch (InvalidOperationException ex) // Collection doesn't exist
  catch (ArgumentException ex) // Invalid vector dimensions
  ```
- **Effort**: 3 hours

#### LlmService.cs (3 catches)
- **Lines**: 202, 274, 385
- **Operations**: HTTP calls to LLM providers, streaming responses
- **Specific Exceptions**:
  ```csharp
  catch (HttpRequestException ex) // Network failures
  catch (TaskCanceledException ex) // Timeouts
  catch (JsonException ex) // Invalid response format
  ```
- **Effort**: 1.5 hours

### PDF Processing Services (18 catches)

#### PdfStorageService.cs (11 catches)
- **Lines**: 168, 241, 262, 273, 482, 536, 632, 647, 752, 767, 796
- **Operations**: File I/O, DB transactions, blob storage
- **Specific Exceptions**:
  ```csharp
  catch (IOException ex) // File access errors
  catch (UnauthorizedAccessException ex) // Permission denied
  catch (DbUpdateException ex) // Database save failures
  catch (InvalidOperationException ex) // File not found
  ```
- **Effort**: 4 hours

#### PdfTextExtractionService.cs (2 catches)
- **Lines**: 127, 179
- **Operations**: PDF parsing with Docnet
- **Specific Exceptions**:
  ```csharp
  catch (InvalidOperationException ex) // Corrupted PDF
  catch (ArgumentException ex) // Invalid page number
  catch (NotSupportedException ex) // Unsupported PDF features
  ```
- **Effort**: 1 hour

#### PdfValidationService.cs (3 catches)
- **Lines**: 152, 178, 288
- **Operations**: File validation, magic byte checks
- **Specific Exceptions**:
  ```csharp
  catch (IOException ex) // File read errors
  catch (InvalidOperationException ex) // Validation failures
  catch (ArgumentException ex) // Invalid parameters
  ```
- **Effort**: 1.5 hours

#### PdfTableExtractionService.cs (2 catches)
- **Lines**: 46, 882
- **Operations**: Table detection, iText7 operations
- **Specific Exceptions**:
  ```csharp
  catch (InvalidOperationException ex) // PDF parsing errors
  catch (NotSupportedException ex) // Unsupported table format
  ```
- **Effort**: 1 hour

### Cache Services (14 catches)

#### HybridCacheService.cs (4 catches)
- **Lines**: 106, 130, 171, 216
- **Operations**: Redis operations, memory cache
- **Specific Exceptions**:
  ```csharp
  catch (RedisConnectionException ex) // Redis unavailable
  catch (RedisTimeoutException ex) // Redis timeout
  catch (InvalidOperationException ex) // Cache invalidation errors
  ```
- **Effort**: 2 hours

#### AiResponseCacheService.cs (2 catches)
- **Lines**: 59, 87
- **Operations**: HybridCache lookups, JSON serialization
- **Specific Exceptions**:
  ```csharp
  catch (RedisConnectionException ex) // Redis failures
  catch (JsonException ex) // Cache deserialization errors
  ```
- **Effort**: 1 hour

#### SessionCacheService.cs (3 catches)
- **Lines**: 47, 78, 98
- **Operations**: Session data caching
- **Specific Exceptions**:
  ```csharp
  catch (RedisConnectionException ex)
  catch (JsonException ex)
  ```
- **Effort**: 1.5 hours

#### CacheWarmingService.cs (3 catches)
- **Lines**: 91, 157, 202
- **Operations**: Background cache warming
- **Specific Exceptions**:
  ```csharp
  catch (DbUpdateException ex) // Database query errors
  catch (RedisConnectionException ex) // Cache storage failures
  catch (InvalidOperationException ex) // Warmup logic errors
  ```
- **Effort**: 1.5 hours

#### RedisFrequencyTracker.cs (3 catches)
- **Lines**: 50, 88, 117
- **Operations**: Redis sorted sets
- **Specific Exceptions**:
  ```csharp
  catch (RedisConnectionException ex)
  catch (RedisTimeoutException ex)
  ```
- **Effort**: 1 hour

### Database Services (8 catches)

#### ConfigurationService.cs (5 catches)
- **Lines**: 135, 387, 399 (rollback), 484, 598, 611 (rollback)
- **Operations**: DB transactions, version tracking
- **Specific Exceptions**:
  ```csharp
  catch (DbUpdateException ex) // Save failures
  catch (DbUpdateConcurrencyException ex) // Concurrent updates
  catch (InvalidOperationException ex) // Transaction errors
  ```
- **Effort**: 2.5 hours

#### PromptManagementService.cs (3 catches)
- **Lines**: 387, 401 (rollback), 566, 580 (rollback)
- **Operations**: DB transactions, prompt versioning
- **Specific Exceptions**:
  ```csharp
  catch (DbUpdateException ex)
  catch (DbUpdateConcurrencyException ex)
  ```
- **Effort**: 1.5 hours

### Authentication/Security Services (10 catches)

#### OAuthService.cs (1 catch)
- **Lines**: 504
- **Operations**: HTTP OAuth calls, token exchange
- **Specific Exceptions**:
  ```csharp
  catch (HttpRequestException ex) // OAuth provider failures
  catch (JsonException ex) // Invalid token response
  catch (UnauthorizedAccessException ex) // Invalid credentials
  ```
- **Effort**: 0.5 hours

#### EncryptionService.cs (2 catches)
- **Lines**: 38, 61
- **Operations**: DataProtection API, encryption/decryption
- **Specific Exceptions**:
  ```csharp
  catch (CryptographicException ex) // Decryption failures
  catch (InvalidOperationException ex) // Key unavailable
  ```
- **Effort**: 1 hour

#### ApiKeyAuthenticationService.cs (1 catch)
- **Lines**: 208
- **Operations**: Database lookups, PBKDF2 verification
- **Specific Exceptions**:
  ```csharp
  catch (DbUpdateException ex) // Database errors
  catch (CryptographicException ex) // Hash verification failures
  ```
- **Effort**: 0.5 hours

#### SessionAutoRevocationService.cs (2 catches)
- **Lines**: 59, 93
- **Operations**: Background task, DB updates
- **Specific Exceptions**:
  ```csharp
  catch (DbUpdateException ex)
  catch (InvalidOperationException ex) // Transaction errors
  ```
- **Effort**: 1 hour

#### PasswordResetService.cs (1 catch)
- **Lines**: 115
- **Operations**: Token generation, email sending
- **Specific Exceptions**:
  ```csharp
  catch (DbUpdateException ex)
  catch (InvalidOperationException ex) // Token generation errors
  catch (SmtpException ex) // Email send failures
  ```
- **Effort**: 0.5 hours

### Search Services (5 catches)

#### KeywordSearchService.cs (2 catches)
- **Lines**: 125, 199
- **Operations**: PostgreSQL full-text search
- **Specific Exceptions**:
  ```csharp
  catch (Npgsql.NpgsqlException ex) // PostgreSQL errors
  catch (InvalidOperationException ex) // Query construction errors
  ```
- **Effort**: 1 hour

#### HybridSearchService.cs (1 catch)
- **Lines**: 87
- **Operations**: Combines vector + keyword search
- **Specific Exceptions**:
  ```csharp
  catch (InvalidOperationException ex) // Search orchestration errors
  catch (RpcException ex) // Qdrant failures
  catch (Npgsql.NpgsqlException ex) // PostgreSQL errors
  ```
- **Effort**: 0.5 hours

### Alerting/Notification Services (3 catches)

#### EmailAlertChannel.cs (1 catch)
- **Lines**: 86
- **Operations**: SMTP email sending
- **Specific Exceptions**:
  ```csharp
  catch (SmtpException ex) // Email send failures
  catch (InvalidOperationException ex) // Configuration errors
  ```
- **Effort**: 0.5 hours

#### PagerDutyAlertChannel.cs (1 catch)
- **Lines**: 83
- **Operations**: HTTP API calls to PagerDuty
- **Specific Exceptions**:
  ```csharp
  catch (HttpRequestException ex) // Network failures
  catch (JsonException ex) // Invalid response
  ```
- **Effort**: 0.5 hours

#### AlertingService.cs (1 catch)
- **Lines**: 116
- **Operations**: Alert channel orchestration
- **Specific Exceptions**:
  ```csharp
  catch (InvalidOperationException ex) // Channel not configured
  catch (TimeoutException ex) // Alert send timeout
  ```
- **Effort**: 0.5 hours

### Chess/Game Logic Services (5 catches)

#### ChessAgentService.cs (2 catches)
- **Lines**: 138, 190
- **Operations**: Chess move validation, FEN parsing
- **Specific Exceptions**:
  ```csharp
  catch (ArgumentException ex) // Invalid FEN string
  catch (InvalidOperationException ex) // Illegal move
  ```
- **Effort**: 1 hour

#### ChessKnowledgeService.cs (3 catches)
- **Lines**: 142, 176, 195
- **Operations**: Chess opening database, move analysis
- **Specific Exceptions**:
  ```csharp
  catch (ArgumentException ex) // Invalid move notation
  catch (InvalidOperationException ex) // Position not found
  ```
- **Effort**: 1.5 hours

### Miscellaneous Services (7 catches)

#### BggApiService.cs (2 catches)
- **Lines**: 147, 197
- **Operations**: HTTP API calls, XML parsing
- **Specific Exceptions**:
  ```csharp
  catch (HttpRequestException ex)
  catch (XmlException ex) // Invalid BGG XML response
  catch (TaskCanceledException ex) // Timeout
  ```
- **Effort**: 1 hour

#### N8nTemplateService.cs (2 catches)
- **Lines**: 99, 163
- **Operations**: HTTP n8n API, JSON template parsing
- **Specific Exceptions**:
  ```csharp
  catch (HttpRequestException ex)
  catch (JsonException ex)
  ```
- **Effort**: 1 hour

#### PromptEvaluationService.cs (5 catches)
- **Lines**: 260, 312, 548, 756, 812
- **Operations**: File I/O, JSON parsing, RAG queries
- **Specific Exceptions**:
  ```csharp
  catch (IOException ex) // File operations
  catch (JsonException ex) // Dataset parsing
  catch (InvalidOperationException ex) // Evaluation logic errors
  ```
- **Note**: Line 108 already has filtered catch
- **Effort**: 2.5 hours

---

## 🟡 MEDIUM PRIORITY: Resilience Patterns (42 catches)

These catches are **legitimate resilience patterns** but need **documentation** explaining why generic catching is acceptable.

### Observability/Metrics (12 catches)

#### CacheMetricsRecorder.cs (3 catches)
- **Lines**: 53, 83, 111
- **Pattern**: Metrics recording should never crash the application
- **Justification**:
```csharp
catch (Exception ex)
{
    // Metrics recording failures should not impact application functionality
    // Any exception type could occur during metric emission
    _logger.LogWarning(ex, "Failed to record cache metric");
}
```
- **Effort**: 0.5 hours (add comments)

#### AiRequestLogService.cs (1 catch)
- **Lines**: 71
- **Pattern**: Audit logging resilience
- **Justification**:
```csharp
catch (Exception ex)
{
    // AI request logging is best-effort; failures should not prevent API responses
    _logger.LogWarning(ex, "Failed to log AI request");
}
```
- **Effort**: 0.25 hours

#### AuditService.cs (1 catch)
- **Lines**: 46
- **Pattern**: Audit trail resilience
- **Justification**:
```csharp
catch (Exception ex)
{
    // Audit logging failures should not crash application operations
    _logger.LogError(ex, "Failed to create audit log entry");
}
```
- **Effort**: 0.25 hours

#### QualityReportService.cs (1 catch)
- **Lines**: 98
- **Pattern**: Report generation resilience
- **Effort**: 0.25 hours

### Background Services (7 catches)

#### BackgroundTaskService.cs (2 catches)
- **Lines**: 26, 56
- **Pattern**: Background job error isolation
- **Justification**:
```csharp
catch (Exception ex)
{
    // Background tasks must not crash the host process
    // All exception types need handling for resilience
    _logger.LogError(ex, "Background task failed");
}
```
- **Effort**: 0.5 hours

#### SessionAutoRevocationService.cs (already counted in HIGH)
- Background service pattern but needs specific exceptions for DB operations

#### CacheWarmingService.cs (already counted in HIGH)
- Background service but should catch specific DB/Redis exceptions

### Fallback/Degradation Patterns (12 catches)

#### HybridCacheService.cs (already counted in HIGH)
- Should distinguish Redis failures from other errors for degradation

#### PromptTemplateService.cs (3 catches)
- **Lines**: 107, 340, 449
- **Pattern**: Fallback to configuration when DB/Redis fails
- **Justification**:
```csharp
catch (Exception ex)
{
    // Graceful degradation: Fall back to appsettings.json prompts
    // Multiple failure modes (Redis, DB, deserialization) require broad catch
    _logger.LogWarning(ex, "Prompt cache lookup failed, falling back to configuration");
    return await GetPromptFromConfigurationAsync(templateName);
}
```
- **Effort**: 0.75 hours

#### RateLimitService.cs (1 catch)
- **Lines**: 128
- **Pattern**: Rate limit check should not block requests
- **Justification**:
```csharp
catch (Exception ex)
{
    // Rate limiting failures should not block requests (fail-open for availability)
    _logger.LogWarning(ex, "Rate limit check failed, allowing request");
    return true; // Fail-open
}
```
- **Effort**: 0.25 hours

### Cleanup/Disposal Patterns (10 catches)

#### PdfStorageService.cs (nested catches)
- **Lines**: 647, 767 (innerEx)
- **Pattern**: Cleanup failures during error handling
- **Justification**:
```csharp
catch (Exception innerEx)
{
    // Cleanup failures during error recovery should be logged but not re-thrown
    // Preserves original exception for caller
    _logger.LogWarning(innerEx, "Failed to clean up temporary file during error recovery");
}
```
- **Effort**: 0.5 hours

#### MdExportFormatter.cs / PdfExportFormatter.cs (2 catches)
- **Lines**: 141, 239
- **Pattern**: Export format errors shouldn't crash export process
- **Effort**: 0.5 hours

---

## 🟢 LOW PRIORITY: Endpoint Handlers (47 catches)

These are **legitimate top-level endpoint handlers** that need **justification comments** but generic catching is acceptable for API boundaries.

### Program.cs API Endpoints (47 catches)

**Pattern**: Top-level endpoint handlers that convert all exceptions to HTTP responses.

**Template Justification**:
```csharp
catch (Exception ex)
{
    // Top-level endpoint handler: Catch all exceptions to return HTTP 500
    // Specific exception handling occurs in service layer
    logger.LogError(ex, "Endpoint error: {Endpoint}", context.Request.Path);
    return Results.Problem(detail: ex.Message, statusCode: 500);
}
```

**Breakdown by Endpoint Category**:

| Category | Lines | Count | Comment Effort |
|----------|-------|-------|----------------|
| Auth endpoints | 960, 985, 1015, 1073, 1101, 1124, 1280 | 7 | 1 hour |
| Chat/QA endpoints | 1519, 1548, 1606, 1794, 1925, 2005, 2243, 2404, 2481, 2592 | 10 | 1.5 hours |
| Nested chat error logging | 1811, 1942, 2261, 2421, 2609 | 5 | 0.75 hours |
| PDF endpoints | 2799, 2850, 3008 | 3 | 0.5 hours |
| Admin endpoints | 3461, 3512, 3630, 3687, 3782, 4166, 4464 | 7 | 1 hour |
| Workflow endpoints | 4886, 4950, 5000, 5082 | 4 | 0.5 hours |
| Configuration endpoints | 5603, 5682, 5850, 5895, 5935 | 5 | 0.75 hours |
| Chess endpoints | 6615, 6676, 6743 | 3 | 0.5 hours |
| SSE streaming | 2161, 2194 | 2 | 0.5 hours |

**Total Effort**: 6 hours (add standardized comments to all 47 endpoint handlers)

---

## ⚪ SKIP: Already Filtered with `when` Clauses (6 catches)

These already use exception filters and are acceptable:

1. **ApiKeyManagementService.cs:390**: `catch (Exception ex) when (ex is FormatException or InvalidOperationException)`
2. **BggApiService.cs:227**: `catch (Exception ex) when (ex is FormatException or OverflowException)`
3. **BggApiService.cs:329**: `catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)`
4. **SensitiveDataDestructuringPolicy.cs:179**: `catch (Exception ex) when (...)`
5. **PromptEvaluationService.cs:108**: `catch (Exception ex) when (ex is not FileNotFoundException && ex is not JsonException && ex is not ArgumentException)`

**Action**: None required - these are properly filtered.

---

## 🛠️ Implementation Strategy

### Phase 2A: High-Priority Service Fixes (20-25 hours)

**Order by Impact**:
1. **AI/RAG Services** (7.5h) - Core functionality, highest usage
2. **PDF Processing** (7.5h) - User-facing, frequent errors
3. **Cache Services** (7h) - Performance-critical, affects all operations
4. **Database Services** (4h) - Data integrity concerns
5. **Auth/Security** (3.5h) - Security implications

### Phase 2B: Medium-Priority Documentation (8-10 hours)

**Order by Count**:
1. Observability/Metrics (2h) - 13 catches
2. Background Services (2h) - 7 catches
3. Fallback Patterns (3h) - 16 catches
4. Cleanup Patterns (2h) - 10 catches

### Phase 2C: Low-Priority Comments (4-6 hours)

**Batch by endpoint type**:
1. Add standardized comments to all 47 Program.cs endpoint handlers
2. Use template comment for consistency

---

## 📊 Success Metrics

| Metric | Before | Target After | Measurement |
|--------|--------|--------------|-------------|
| Generic catches (no filter) | 180 | 48 | `grep "catch (Exception ex)$" \| wc -l` |
| Specific exception catches | ~50 | ~180 | `grep "catch (\w*Exception ex)" \| wc -l` |
| Documented resilience patterns | 0 | 42 | Manual code review |
| Undocumented endpoint handlers | 47 | 0 | `grep -B2 "catch (Exception ex)" Program.cs` |

---

## 🔄 Next Steps

1. **Review this categorization** with team/stakeholders
2. **Create feature branch**: `sec-11-phase2-exception-refactoring`
3. **Execute Phase 2A** (high-priority services) - 5-day effort
4. **Execute Phase 2B** (resilience documentation) - 2-day effort
5. **Execute Phase 2C** (endpoint comments) - 1-day effort
6. **Run full test suite** to validate refactoring
7. **Update SEC-11 issue** with completion summary

**Total Estimated Calendar Time**: 8-10 working days (with testing)

---

## 📝 Notes

- **Middleware**: `ApiExceptionHandlerMiddleware.cs:34` has legitimate generic catch (top-level error handler)
- **Health Checks**: `QdrantHealthCheck.cs:35` should catch `RpcException` specifically
- **Backup Files**: `AiResponseCacheService.Redis.cs.backup` has 7 catches but file is unused
- **Test Impact**: Expect ~30-40 test updates to match new exception types

**Author**: Claude Code
**Date**: 2025-10-27
**Issue**: SEC-11 Phase 2 Planning
