# API Test Fix Plan (Incremental)

Last updated: 2025-10-28

## Snapshot Status
- Authentication issues (401) addressed via session cookie middleware.
- API Key management endpoints: green (25/25 passed).
- Integration suite improved (28 passed / 68 total) but several domains still failing:
  - PDF ingest/extraction/formatting
  - Logging redaction/correlation
  - Rate limiting headers + profiles
  - Chess webhook scenarios
  - Admin analytics/QA endpoints

Logs to reference:
- Full (partial, timed out): `apps/api/test-run-full-latest.log`
- Integration after auth fix: `apps/api/test-namespace-Api.Tests.Integration.after.log`
- Admin requests: `apps/api/test-run-AdminRequestsEndpointsTests.log`
- API keys: `apps/api/test-run-ApiKeyManagementEndpointsTests-2.log`

## Completed Fixes
- Session cookie authentication middleware
  - Files: `apps/api/src/Api/Middleware/SessionAuthenticationMiddleware.cs`, `apps/api/src/Api/Extensions/WebApplicationExtensions.cs`
- RuleSpec tests cookie handling (use `AddCookies`)
  - Files: `RuleSpecHistoryIntegrationTests.cs`, `RuleSpecUpdateEndpointTests.cs`
- API key endpoints stability
  - Persist test API keys, track cleanup, quota metadata in tests
  - EF client-projection fix in `ApiKeyManagementService.ListApiKeysAsync`

## Next Focus (Block-by-Block)

### 1) Logging & Correlation Id (small, high signal)
- Goals:
  - Fix redaction tests (connection strings, passwords, API keys)
  - Ensure correlation id is present for unauthenticated requests
- Likely areas:
  - `apps/api/src/Api/Middleware/LogValueSanitizer.cs`
  - Serilog enrichers/config in `LoggingConfiguration`
  - Test correlator usage in `Api.Tests/Logging/*`
- Commands:
  - `./.dotnet/dotnet test apps/api/MeepleAI.Api.sln --filter "FullyQualifiedName~Api.Tests.Logging" -v n`
- Artifacts:
  - `apps/api/test-namespace-Api.Tests.Logging.log`

### 2) Rate Limiting Headers & Profiles
- Goals:
  - Confirm headers emitted and role-based quotas applied (Anon/User/Editor/Admin)
- Likely areas:
  - `IRateLimitService` + any middleware/filter adding headers
  - Redis script evaluation mock (già impostato in WebApplicationFactoryFixture)
- Commands:
  - `./.dotnet/dotnet test apps/api/MeepleAI.Api.sln --filter "FullyQualifiedName~RateLimiting" -v n`
- Artifacts:
  - `apps/api/test-namespace-RateLimiting.log`

### 3) PDF Ingest/Extraction Pipeline
- Goals:
  - Stabilize PDF upload validation and extraction (Docnet/Tesseract), table parsing and formatting
- Likely areas:
  - `PdfValidationService`, `PdfTextExtractionService`, `PdfTableExtractionService`, `PdfStorageService`
  - MIME/magic-bytes validation; file size limits; Docnet runtime selection; OCR fallback flags
- Commands:
  - `./.dotnet/dotnet test apps/api/MeepleAI.Api.sln --filter "FullyQualifiedName~Pdf" -v n`
- Artifacts:
  - `apps/api/test-namespace-Pdf.log`

### 4) Chess Webhook Scenarios
- Goals:
  - Response format, opening info, FEN position analysis, conversation persistence
- Likely areas:
  - `ChessWebhookIntegrationTests`, `ChessAgentService`, routing in `AiEndpoints`
- Commands:
  - `./.dotnet/dotnet test apps/api/MeepleAI.Api.sln --filter "FullyQualifiedName~ChessWebhook" -v n`
- Artifacts:
  - `apps/api/test-namespace-ChessWebhook.log`

### 5) Admin Analytics / QA Endpoints
- Goals:
  - Date range filtering, pagination, quality metrics aggregation
- Likely areas:
  - `AiRequestLogService`, `AgentFeedbackService`, admin endpoints queries
- Commands:
  - `./.dotnet/dotnet test apps/api/MeepleAI.Api.sln --filter "FullyQualifiedName~AdminStats|Quality" -v n`
- Artifacts:
  - `apps/api/test-namespace-Admin-Quality.log`

### 6) Final Integration Sweep
- Goals:
  - Re-run `Api.Tests.Integration` and address residuals
- Commands:
  - `./.dotnet/dotnet test apps/api/MeepleAI.Api.sln --filter "FullyQualifiedName~Api.Tests.Integration" -v n`
- Artifacts:
  - `apps/api/test-namespace-Api.Tests.Integration.final.log`

## Cadence e Note
- Procedere per blocchi: eseguire batch, fissare root-cause, validare, passare al successivo.
- Mantenere i log di ogni batch in `apps/api/` con nomi coerenti.
- Evitare refactor ampi non necessari; correzioni locali e mirate.
- Tenere d’occhio i warning EF/Serilog per indizi sui fallimenti.

## Comandi Rapidi
- Full (lungo): `./.dotnet/dotnet test apps/api/MeepleAI.Api.sln -v n | tee apps/api/test-run-full.log`
- Classe singola: `./.dotnet/dotnet test apps/api/MeepleAI.Api.sln --filter "FullyQualifiedName~<ClassName>" -v n`
- Namespace: `./.dotnet/dotnet test apps/api/MeepleAI.Api.sln --filter "FullyQualifiedName~<Namespace>" -v n`

