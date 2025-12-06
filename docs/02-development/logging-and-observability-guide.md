# 📊 Guida Logging e Observability per Sviluppatori

**Versione**: 1.0
**Data**: 2025-11-22
**Target**: Sviluppatori Backend MeepleAI
**Livello**: Beginner → Advanced

---

## 📋 Indice

1. [Quick Start](#quick-start)
2. [Structured Logging](#structured-logging)
3. [Error Handling](#error-handling)
4. [Domain Events](#domain-events)
5. [Custom Metrics](#custom-metrics)
6. [Distributed Tracing](#distributed-tracing)
7. [Alerting](#alerting)
8. [Best Practices](#best-practices)
9. [Troubleshooting](#troubleshooting)

---

## 🚀 Quick Start

### Prerequisiti

Servizi Docker necessari (già configurati in `infra/docker-compose.yml`):

```bash
# Start observability stack
cd infra
docker compose up -d seq jaeger prometheus grafana

# Verify services
curl http://localhost:8180/health  # HyperDX
curl http://localhost:8180/        # HyperDX UI
curl http://localhost:9090/         # Prometheus
curl http://localhost:3001/         # Grafana
```

**URLs**:
- **HyperDX (Logs)**: http://localhost:8180
- **HyperDX (Traces)**: http://localhost:8180
- **Prometheus (Metrics)**: http://localhost:9090
- **Grafana (Dashboards)**: http://localhost:3001

### Primo Log

```csharp
public class MyService
{
    private readonly ILogger<MyService> _logger;

    public MyService(ILogger<MyService> logger)
    {
        _logger = logger;
    }

    public async Task ProcessOrderAsync(Guid orderId)
    {
        // ✅ GOOD: Structured logging with template
        _logger.LogInformation(
            "Processing order {OrderId} for user {UserId}",
            orderId,
            userId);

        // ❌ BAD: String concatenation
        _logger.LogInformation($"Processing order {orderId}");
    }
}
```

**Perché structured logging?**
- ✅ Parametri indexati separatamente in HyperDX (query: `OrderId = "abc-123"`)
- ✅ Performance migliore (no string allocation)
- ✅ Type-safe

---

## 📝 Structured Logging

### Log Levels

| Level | Quando Usare | Esempio |
|-------|--------------|---------|
| `Trace` | Debugging dettagliato (dev only) | "Entering method GetUserById" |
| `Debug` | Info diagnostiche (dev/staging) | "Cache miss for key: user:123" |
| `Information` | Eventi normali business logic | "User logged in successfully" |
| `Warning` | Situazioni anomale non critiche | "Retry attempt 2/3 for API call" |
| `Error` | Errori gestiti, operation failed | "Failed to send email: SMTP timeout" |
| `Critical` | Errori fatali, app crash | "Database connection lost" |

### Esempi Pratici

#### Information (Eventi Normali)

```csharp
// User actions
_logger.LogInformation(
    "User {UserId} logged in from IP {IpAddress}",
    userId,
    ipAddress);

// Business events
_logger.LogInformation(
    "PDF {FileName} uploaded successfully. Size: {FileSizeBytes} bytes, Pages: {PageCount}",
    fileName,
    fileSizeBytes,
    pageCount);

// Background jobs
_logger.LogInformation(
    "Weekly evaluation job started. Evaluating {QueryCount} queries",
    queryCount);
```

#### Warning (Situazioni Anomale)

```csharp
// Rate limiting
_logger.LogWarning(
    "Rate limit exceeded for user {UserId}. Retry after {RetryAfterSeconds}s",
    userId,
    retryAfterSeconds);

// Fallback logic
_logger.LogWarning(
    "Primary LLM provider {Provider} failed. Falling back to {FallbackProvider}",
    "OpenRouter",
    "Ollama");

// Validation issues
_logger.LogWarning(
    "PDF quality score {QualityScore} below threshold {Threshold}. File: {FileName}",
    0.65,
    0.70,
    fileName);
```

#### Error (Operazioni Fallite)

```csharp
// Caught exceptions
try
{
    await _emailService.SendAsync(email);
}
catch (SmtpException ex)
{
    _logger.LogError(ex,
        "Failed to send email to {Recipient}. Subject: {Subject}",
        email.To,
        email.Subject);
    // Handle or rethrow
}

// Business errors
_logger.LogError(
    "Payment processing failed for order {OrderId}. Reason: {Reason}",
    orderId,
    "Insufficient funds");
```

#### Critical (Errori Fatali)

```csharp
// Database connection lost
_logger.LogCritical(
    "Database connection lost. ConnectionString: {ConnectionString}",
    LogValueSanitizer.Sanitize(connectionString));  // Sanitize sensitive data!

// External dependency down
_logger.LogCritical(
    "Qdrant vector store unavailable. Url: {QdrantUrl}",
    qdrantUrl);
```

### LogContext (Request-Scoped Properties)

Aggiungi proprietà a tutti i log in un contesto:

```csharp
public class MyMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        using (LogContext.PushProperty("CorrelationId", context.TraceIdentifier))
        using (LogContext.PushProperty("UserId", GetUserId(context)))
        using (LogContext.PushProperty("UserAgent", context.Request.Headers.UserAgent))
        {
            _logger.LogInformation("Request started");  // Include automaticamente CorrelationId, UserId, UserAgent
            await next(context);
            _logger.LogInformation("Request completed");
        }
    }
}
```

### Sensitive Data Handling

**MAI loggare**:
- ❌ Password (plaintext)
- ❌ Token di autenticazione
- ❌ API keys
- ❌ Credit card numbers
- ❌ Email complete (GDPR)

**Come gestire**:

```csharp
// ✅ GOOD: Redaction automatica via SensitiveDataDestructuringPolicy
_logger.LogInformation(
    "User registered with email {Email}",
    email);  // Loggato come: "u***@example.com"

// ✅ GOOD: Manual sanitization
_logger.LogWarning(
    "Login failed for email {Email}",
    LogValueSanitizer.Sanitize(email));

// ✅ GOOD: Use IDs, not sensitive data
_logger.LogInformation(
    "Payment processed for user {UserId}",
    userId);  // Non loggare payment details!

// ❌ BAD: Logging password
_logger.LogDebug($"User password: {password}");  // NEVER!
```

---

## ⚠️ Error Handling

### Exception Hierarchy

```
Exception (System)
├─ HttpException (Custom - Middleware/Exceptions)
│  ├─ NotFoundException (404)
│  ├─ BadRequestException (400)
│  ├─ ConflictException (409)
│  ├─ ForbiddenException (403)
│  └─ UnauthorizedHttpException (401)
│
├─ DomainException (SharedKernel/Domain)
│  └─ ValidationException (SharedKernel/Domain)
│
└─ PdfProcessingException (Services)
   ├─ PdfExtractionException
   ├─ PdfValidationException
   └─ PdfStorageException
```

### Quando Usare Ogni Exception

#### DomainException (Business Rule Violations)

```csharp
// In domain entities/aggregates
public void Enable2FA(TotpSecret totpSecret)
{
    if (IsTwoFactorEnabled)
        throw new DomainException("Two-factor authentication is already enabled");

    // ... logic
}

// In command handlers
public async Task<Result> Handle(CreateGameCommand command, CancellationToken ct)
{
    var existingGame = await _gameRepository.GetByNameAsync(command.Name, ct);
    if (existingGame != null)
        throw new DomainException($"Game with name '{command.Name}' already exists");

    // ... logic
}
```

**Mapped to**: HTTP 400 Bad Request

#### ValidationException (Input Validation)

```csharp
// In value objects
public Email(string value)
{
    if (string.IsNullOrWhiteSpace(value))
        throw new ValidationException(nameof(Email), "Email cannot be empty");

    if (!Regex.IsMatch(value, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        throw new ValidationException(nameof(Email), "Email format is invalid");

    Value = value.ToLowerInvariant();
}

// In domain services
public void ValidatePdfQuality(PdfExtractionResult result)
{
    if (result.QualityScore < _minimumThreshold)
        throw new ValidationException(
            nameof(result.QualityScore),
            $"PDF quality score {result.QualityScore} below minimum threshold {_minimumThreshold}");
}
```

**Mapped to**: HTTP 400 Bad Request

#### NotFoundException (Resource Not Found)

```csharp
public async Task<GameDto> GetGameByIdAsync(Guid gameId)
{
    var game = await _gameRepository.GetByIdAsync(gameId);
    if (game == null)
        throw new NotFoundException($"Game with ID '{gameId}' not found");

    return MapToDto(game);
}
```

**Mapped to**: HTTP 404 Not Found

#### Custom Exceptions

```csharp
// PDF processing
public async Task<string> ExtractTextAsync(Stream pdfStream)
{
    try
    {
        // ... extraction logic
    }
    catch (Exception ex)
    {
        throw new PdfExtractionException(
            "Failed to extract text from PDF",
            innerException: ex);
    }
}
```

### Exception Handling Pattern

#### In Endpoints (Minimal - Let Middleware Handle)

```csharp
// ✅ GOOD: Let ApiExceptionHandlerMiddleware handle it
group.MapGet("/games/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
{
    var query = new GetGameByIdQuery(id);
    var result = await mediator.Send(query, ct);  // May throw NotFoundException
    return Results.Json(result);  // Middleware converts to HTTP 404
});

// ❌ BAD: Catching and returning manually
group.MapGet("/games/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
{
    try
    {
        var query = new GetGameByIdQuery(id);
        var result = await mediator.Send(query, ct);
        return Results.Json(result);
    }
    catch (NotFoundException)
    {
        return Results.NotFound();  // Duplicate logic, no metrics, no logging
    }
});
```

#### In Command/Query Handlers

```csharp
public async Task<GameDto> Handle(GetGameByIdQuery query, CancellationToken ct)
{
    var game = await _gameRepository.GetByIdAsync(query.GameId, ct);

    // ✅ GOOD: Throw domain exception (caught by middleware)
    if (game == null)
        throw new NotFoundException($"Game with ID '{query.GameId}' not found");

    return MapToDto(game);
}
```

#### In Domain Services (Business Logic)

```csharp
public async Task<PaymentResult> ProcessPaymentAsync(Order order)
{
    // ✅ GOOD: Try/catch for external dependencies
    try
    {
        var response = await _paymentGateway.ChargeAsync(order.Total, order.PaymentMethod);
        return PaymentResult.Success(response.TransactionId);
    }
    catch (HttpRequestException ex)
    {
        // Log error
        _logger.LogError(ex,
            "Payment gateway request failed for order {OrderId}. Amount: {Amount}",
            order.Id,
            order.Total);

        // Record metrics
        MeepleAiMetrics.RecordApiError(ex, 502, "/payments", isUnhandled: false);

        // Return business result (don't throw if recoverable)
        return PaymentResult.Failure("Payment gateway unavailable. Please try again later.");
    }
    catch (Exception ex)
    {
        // Unexpected error - log and rethrow
        _logger.LogError(ex, "Unexpected error processing payment for order {OrderId}", order.Id);
        throw;  // Will be caught by ApiExceptionHandlerMiddleware
    }
}
```

### Error Response Format

Tutti gli errori gestiti da `ApiExceptionHandlerMiddleware` ritornano:

```json
{
  "error": "validation_error",
  "message": "One or more validation errors occurred",
  "errors": {
    "Email": ["Email is required"],
    "Password": ["Password must be at least 8 characters"]
  },
  "correlationId": "0HN7GQJK3QJ9K",
  "timestamp": "2025-11-22T14:35:27.123Z",
  "stackTrace": null  // Only in Development environment
}
```

**Correlation ID** può essere usato per cercare logs in HyperDX:

```
# In HyperDX search
CorrelationId = "0HN7GQJK3QJ9K"
```

---

## 🎯 Domain Events

### Quando Usare Domain Events

**✅ Usa Domain Events per**:
- Audit trail (chi ha fatto cosa e quando)
- Notifiche cross-bounded-context
- Side effects asincroni (email, webhooks, analytics)
- Event sourcing / CQRS read models

**❌ Non usare Domain Events per**:
- Logica core del domain (usa metodi diretti)
- Transazioni atomiche (eventi dispatched DOPO commit)
- Sincronizzazione immediata

### Creare un Domain Event

**Step 1**: Definire l'evento

```csharp
namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Raised when a new game is created in the catalog.
/// </summary>
public record GameCreatedEvent(
    Guid GameId,
    string Name,
    int MinPlayers,
    int MaxPlayers,
    int MinAge) : DomainEventBase;
```

**Step 2**: Aggiungere l'evento nell'aggregate

```csharp
public class Game : AggregateRoot<Guid>
{
    public Game(Guid id, string name, int minPlayers, int maxPlayers, int minAge)
        : base(id)
    {
        Name = name;
        MinPlayers = minPlayers;
        MaxPlayers = maxPlayers;
        MinAge = minAge;
        CreatedAt = DateTime.UtcNow;

        // ✅ Raise domain event
        AddDomainEvent(new GameCreatedEvent(
            GameId: id,
            Name: name,
            MinPlayers: minPlayers,
            MaxPlayers: maxPlayers,
            MinAge: minAge));
    }
}
```

**Step 3**: Creare l'event handler

```csharp
namespace Api.BoundedContexts.GameManagement.Application.EventHandlers;

public class GameCreatedEventHandler : INotificationHandler<GameCreatedEvent>
{
    private readonly ILogger<GameCreatedEventHandler> _logger;
    private readonly INotificationService _notificationService;

    public GameCreatedEventHandler(
        ILogger<GameCreatedEventHandler> logger,
        INotificationService notificationService)
    {
        _logger = logger;
        _notificationService = notificationService;
    }

    public async Task Handle(GameCreatedEvent notification, CancellationToken ct)
    {
        // Log audit trail
        _logger.LogInformation(
            "Game {GameId} '{GameName}' created at {OccurredAt}. Players: {MinPlayers}-{MaxPlayers}, Age: {MinAge}+",
            notification.GameId,
            notification.Name,
            notification.OccurredAt,
            notification.MinPlayers,
            notification.MaxPlayers,
            notification.MinAge);

        // Send notification (async side effect)
        await _notificationService.SendAsync(
            title: "New Game Added",
            message: $"'{notification.Name}' is now available in the catalog",
            ct);

        // Could also: update read model, send webhook, track analytics, etc.
    }
}
```

**Step 4**: Eventi dispatched automaticamente

```csharp
// In command handler
public async Task<GameDto> Handle(CreateGameCommand command, CancellationToken ct)
{
    var game = new Game(
        id: Guid.NewGuid(),
        name: command.Name,
        minPlayers: command.MinPlayers,
        maxPlayers: command.MaxPlayers,
        minAge: command.MinAge);

    await _gameRepository.AddAsync(game, ct);
    await _unitOfWork.SaveChangesAsync(ct);  // ← Eventi dispatched qui!

    return MapToDto(game);
}
```

**`UnitOfWork.SaveChangesAsync` fa**:
1. Commit transazione database
2. Raccoglie domain events da tutti gli aggregates
3. Dispatch eventi via MediatR (async)
4. Clear events dagli aggregates

### Esempi di Domain Events Esistenti

```csharp
// Authentication
PasswordChangedEvent(Guid UserId)
TwoFactorEnabledEvent(Guid UserId, int BackupCodesCount)
SessionRevokedEvent(Guid SessionId, Guid UserId, string? Reason)
OAuthAccountLinkedEvent(Guid UserId, string Provider, string ProviderUserId)

// PDF Processing
PdfUploadedEvent(Guid DocumentId, string FileName, long FileSizeBytes)
PdfExtractionCompletedEvent(Guid DocumentId, int PageCount, double QualityScore)

// Workflow
WorkflowErrorLoggedEvent(Guid WorkflowErrorId, string WorkflowName, string ErrorType)
```

### Pattern: Multi-Handler per Stesso Evento

Un evento può avere più handler (decoupling):

```csharp
// Handler 1: Audit logging
public class GameCreatedAuditHandler : INotificationHandler<GameCreatedEvent>
{
    public async Task Handle(GameCreatedEvent notification, CancellationToken ct)
    {
        await _auditLog.LogAsync("Game.Created", notification);
    }
}

// Handler 2: Analytics tracking
public class GameCreatedAnalyticsHandler : INotificationHandler<GameCreatedEvent>
{
    public async Task Handle(GameCreatedEvent notification, CancellationToken ct)
    {
        await _analytics.TrackAsync("game_created", new { gameId = notification.GameId });
    }
}

// Handler 3: Cache invalidation
public class GameCreatedCacheHandler : INotificationHandler<GameCreatedEvent>
{
    public async Task Handle(GameCreatedEvent notification, CancellationToken ct)
    {
        await _cache.InvalidateAsync($"games:list");
    }
}
```

Tutti e 3 verranno eseguiti automaticamente quando `GameCreatedEvent` viene dispatched.

---

## 📈 Custom Metrics

### Recording Metrics

MeepleAI usa **OpenTelemetry Metrics** con helper methods in `MeepleAiMetrics.cs`.

#### API Error Metrics

```csharp
// Automatically recorded by ApiExceptionHandlerMiddleware
// You don't need to call this manually in most cases
MeepleAiMetrics.RecordApiError(
    exception: ex,
    httpStatusCode: 500,
    endpoint: "/api/v1/games",
    isUnhandled: true);
```

**Prometheus query**:
```promql
rate(meepleai_api_errors_total[5m])
```

#### RAG Request Metrics

```csharp
var sw = Stopwatch.StartNew();

try
{
    var response = await _ragService.AskQuestionAsync(query, gameId);

    // ✅ Record success
    MeepleAiMetrics.RecordRagRequest(
        durationMs: sw.Elapsed.TotalMilliseconds,
        gameId: gameId,
        success: true);

    // Record confidence score
    MeepleAiMetrics.ConfidenceScore.Record(response.Confidence);

    // Record tokens used
    MeepleAiMetrics.TokensUsed.Record(response.TokensUsed);

    return response;
}
catch (Exception ex)
{
    // Record failure
    MeepleAiMetrics.RecordRagRequest(
        durationMs: sw.Elapsed.TotalMilliseconds,
        gameId: gameId,
        success: false);

    // Record error by type
    MeepleAiMetrics.RagErrorsTotal.Add(1, new TagList
    {
        { "error.type", ex.GetType().Name },
        { "game.id", gameId }
    });

    throw;
}
```

**Prometheus queries**:
```promql
# Average RAG duration
avg(meepleai_rag_request_duration)

# Success rate
sum(rate(meepleai_rag_requests_total{success="true"}[5m])) /
sum(rate(meepleai_rag_requests_total[5m]))

# P95 confidence score
histogram_quantile(0.95, meepleai_rag_confidence_score_bucket)
```

#### PDF Processing Metrics

```csharp
// Upload attempt
MeepleAiMetrics.RecordPdfUploadAttempt(
    status: "success",  // or "validation_failed", "storage_failed"
    fileSizeBytes: fileStream.Length);

// Extraction stage (3-stage pipeline)
var sw = Stopwatch.StartNew();
var result = await _unstructuredExtractor.ExtractAsync(pdfStream);
sw.Stop();

MeepleAiMetrics.RecordPdfExtractionStage(
    stageName: "Unstructured",
    success: result.Success,
    durationMs: sw.Elapsed.TotalMilliseconds,
    qualityScore: result.QualityScore);

// Pipeline steps
MeepleAiMetrics.RecordPdfPipelineStep(
    step: "chunking",
    durationMs: chunkingDuration,
    count: chunks.Count);

MeepleAiMetrics.RecordPdfPipelineStep(
    step: "embedding",
    durationMs: embeddingDuration);

MeepleAiMetrics.RecordPdfPipelineStep(
    step: "indexing",
    durationMs: indexingDuration);
```

**Prometheus queries**:
```promql
# Extraction success rate by stage
sum(rate(meepleai_pdf_extraction_stage_success[5m])) by (stage) /
sum(rate(meepleai_pdf_extraction_stage_attempts[5m])) by (stage)

# Average quality score by stage
avg(meepleai_pdf_quality_score) by (stage)

# P95 extraction duration
histogram_quantile(0.95, meepleai_pdf_extraction_stage_duration_bucket)
```

#### Cache Metrics

```csharp
// Cache access
var cacheKey = $"game:{gameId}";
var cached = await _cache.GetAsync<GameDto>(cacheKey);

if (cached != null)
{
    MeepleAiMetrics.RecordCacheAccess(isHit: true, cacheType: "L1");
    return cached;
}
else
{
    MeepleAiMetrics.RecordCacheAccess(isHit: false, cacheType: "L1");
    var game = await _gameRepository.GetByIdAsync(gameId);
    await _cache.SetAsync(cacheKey, game, TimeSpan.FromMinutes(5));
    return game;
}
```

**Prometheus queries**:
```promql
# Cache hit rate
sum(rate(meepleai_cache_hits_total[5m])) /
(sum(rate(meepleai_cache_hits_total[5m])) + sum(rate(meepleai_cache_misses_total[5m])))
```

#### Agent Metrics

```csharp
var sw = Stopwatch.StartNew();

try
{
    var result = await _chessAgent.AnalyzePositionAsync(fen);

    MeepleAiMetrics.RecordAgentInvocation(
        agentType: "chess",
        durationMs: sw.Elapsed.TotalMilliseconds,
        success: true);

    return result;
}
catch (Exception ex)
{
    MeepleAiMetrics.RecordAgentInvocation(
        agentType: "chess",
        durationMs: sw.Elapsed.TotalMilliseconds,
        success: false);
    throw;
}
```

#### LLM Token Usage Metrics (Issue #1694)

```csharp
// Record LLM token usage with OpenTelemetry GenAI semantic conventions
var llmResult = await _llmService.GenerateCompletionAsync(systemPrompt, userPrompt, ct);

if (llmResult.Usage != null && llmResult.Cost != null)
{
    var tokenUsage = TokenUsage.FromLlmResult(llmResult.Usage, llmResult.Cost);

    // Automatically records with OpenTelemetry GenAI conventions:
    // - gen_ai.client.token.usage (with gen_ai.token.type=input/output)
    // - gen_ai.client.operation.duration
    // - meepleai.llm.cost.usd
    MeepleAiMetrics.RecordLlmTokenUsage(
        promptTokens: tokenUsage.PromptTokens,
        completionTokens: tokenUsage.CompletionTokens,
        totalTokens: tokenUsage.TotalTokens,
        modelId: tokenUsage.ModelId,
        provider: tokenUsage.Provider,
        operationDurationMs: sw.Elapsed.TotalMilliseconds,
        costUsd: tokenUsage.EstimatedCost);
}
```

**Prometheus queries**:
```promql
# Total prompt tokens by model
sum(rate(gen_ai_client_token_usage{gen_ai_token_type="input"}[5m])) by (gen_ai_request_model)

# Total completion tokens by model
sum(rate(gen_ai_client_token_usage{gen_ai_token_type="output"}[5m])) by (gen_ai_request_model)

# Total LLM cost by model
sum(rate(meepleai_llm_cost_usd[5m])) by (model_id)

# LLM operation duration P95
histogram_quantile(0.95, gen_ai_client_operation_duration_bucket)
```

#### Agent Token Usage and Cost Tracking

```csharp
// For handlers that invoke agents WITH LLM calls
var tokenUsage = TokenUsage.FromLlmResult(llmResult.Usage, llmResult.Cost);

MeepleAiMetrics.RecordAgentInvocationWithTokens(
    agentType: "RagAgent",
    tokenUsage: tokenUsage,
    durationMs: sw.Elapsed.TotalMilliseconds,
    success: true);
```

**Prometheus queries**:
```promql
# Agent token usage by type
sum(rate(meepleai_agent_tokens_total[5m])) by (agent_type)

# Agent cost by type
sum(rate(meepleai_agent_cost_usd[5m])) by (agent_type)
```

### Creating Custom Metrics

Se hai bisogno di una nuova metrica:

**Step 1**: Aggiungere in `MeepleAiMetrics.cs`

```csharp
/// <summary>
/// Counter for custom feature X usage
/// </summary>
public static readonly Counter<long> FeatureXUsageTotal = Meter.CreateCounter<long>(
    name: "meepleai.feature_x.usage.total",
    unit: "usages",
    description: "Total number of Feature X usages");

/// <summary>
/// Histogram for Feature X duration
/// </summary>
public static readonly Histogram<double> FeatureXDuration = Meter.CreateHistogram<double>(
    name: "meepleai.feature_x.duration",
    unit: "ms",
    description: "Feature X execution duration");
```

**Step 2**: Usare nel codice

```csharp
public async Task<Result> ExecuteFeatureXAsync()
{
    var sw = Stopwatch.StartNew();

    // ... logic ...

    // Record metrics with tags
    var tags = new TagList
    {
        { "user.role", userRole },
        { "success", true }
    };

    MeepleAiMetrics.FeatureXUsageTotal.Add(1, tags);
    MeepleAiMetrics.FeatureXDuration.Record(sw.Elapsed.TotalMilliseconds, tags);

    return result;
}
```

**Step 3**: Query in Prometheus

```promql
# Usage by role
sum(rate(meepleai_feature_x_usage_total[5m])) by (user_role)

# P95 duration
histogram_quantile(0.95, meepleai_feature_x_duration_bucket)
```

---

## 🔍 Distributed Tracing

### Activity Sources

MeepleAI definisce **Activity Sources** custom per tracing di dominio:

```csharp
public static class MeepleAiActivitySources
{
    public static readonly ActivitySource Api = new("MeepleAI.Api");
    public static readonly ActivitySource Rag = new("MeepleAI.RAG");
    public static readonly ActivitySource VectorSearch = new("MeepleAI.VectorSearch");
    public static readonly ActivitySource PdfProcessing = new("MeepleAI.PdfProcessing");
    public static readonly ActivitySource Cache = new("MeepleAI.Cache");
}
```

### Creating Spans

```csharp
using (var activity = MeepleAiActivitySources.Rag.StartActivity("ProcessRagQuery"))
{
    // Set tags (metadata)
    activity?.SetTag("query", query);
    activity?.SetTag("game.id", gameId);
    activity?.SetTag("user.role", userRole);

    // Nested span (child)
    using (var vectorActivity = MeepleAiActivitySources.VectorSearch.StartActivity("VectorSearch"))
    {
        vectorActivity?.SetTag("collection", collectionName);
        vectorActivity?.SetTag("limit", limit);

        var results = await _vectorStore.SearchAsync(query, limit);

        vectorActivity?.SetTag("results.count", results.Count);
        vectorActivity?.SetTag("top_score", results.FirstOrDefault()?.Score ?? 0);
    }

    // Another nested span
    using (var llmActivity = MeepleAiActivitySources.Rag.StartActivity("LLMGeneration"))
    {
        llmActivity?.SetTag("model", modelId);
        llmActivity?.SetTag("provider", provider);

        var response = await _llmService.GenerateAsync(prompt);

        llmActivity?.SetTag("tokens.used", response.TokensUsed);
        llmActivity?.SetTag("confidence", response.Confidence);
    }

    activity?.SetTag("total.duration_ms", sw.ElapsedMilliseconds);
    activity?.SetTag("success", true);
}
```

**Risultato in HyperDX**:

```
Trace: 5f3d7c8a9b2e1f4d
Span: ProcessRagQuery (429ms)
  Tags: query="Come si muove il cavallo?", game.id="abc-123", success=true
  ├─ Span: VectorSearch (87ms)
  │    Tags: collection="game_rules", limit=10, results.count=8, top_score=0.92
  └─ Span: LLMGeneration (312ms)
       Tags: model="gpt-4", provider="openrouter", tokens.used=245, confidence=0.87
```

### Error Tracking in Spans

```csharp
using (var activity = MeepleAiActivitySources.Rag.StartActivity("ProcessRagQuery"))
{
    try
    {
        // ... logic ...
    }
    catch (Exception ex)
    {
        // Mark span as error
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.SetTag("error.type", ex.GetType().Name);
        activity?.SetTag("error.message", ex.Message);

        // Record exception event
        activity?.RecordException(ex);

        throw;
    }
}
```

### Correlation with Logs

Ogni richiesta HTTP ha un `TraceId` automatico (W3C Trace Context):

```csharp
// In logs
_logger.LogInformation(
    "Processing request with TraceId: {TraceId}",
    Activity.Current?.TraceId.ToString());

// Automatically available in HttpContext
_logger.LogInformation(
    "Request started. CorrelationId: {CorrelationId}",
    httpContext.TraceIdentifier);  // = TraceId
```

**Cerca in HyperDX**:
```
CorrelationId = "5f3d7c8a9b2e1f4d"
```

**Cerca in HyperDX**:
```
Trace ID: 5f3d7c8a9b2e1f4d
```

Stesso `TraceId` in entrambi!

---

## 🚨 Alerting

### Sending Alerts

```csharp
public class MyService
{
    private readonly IAlertingService _alertingService;

    public async Task ProcessCriticalOperationAsync()
    {
        try
        {
            // ... logic ...
        }
        catch (Exception ex)
        {
            // Send critical alert
            await _alertingService.SendAlertAsync(
                severity: AlertSeverity.Critical,
                title: "Critical Operation Failed",
                message: $"Operation failed with error: {ex.Message}",
                category: "System",
                metadata: new Dictionary<string, string>
                {
                    { "operation", "ProcessCriticalOperation" },
                    { "exception.type", ex.GetType().Name },
                    { "stack_trace", ex.StackTrace ?? "" }
                },
                cancellationToken: ct);

            throw;
        }
    }
}
```

### Alert Severities

| Severity | Quando Usare | Canali | Esempio |
|----------|--------------|--------|---------|
| `Info` | Eventi informativi | Log | "Backup completed successfully" |
| `Warning` | Situazioni anomale non critiche | Log + Email | "Cache hit rate below 50%" |
| `Error` | Errori che richiedono attenzione | Log + Email + Slack | "Payment gateway timeout" |
| `Critical` | Errori fatali, richiede intervento immediato | Log + Email + Slack + PagerDuty | "Database connection lost" |

### Alert Throttling

**Default**: 60 minuti per stesso `(Severity, Title, Category)`

```csharp
// First alert: sent to all channels
await _alertingService.SendAlertAsync(
    severity: AlertSeverity.Error,
    title: "Payment Gateway Timeout",
    category: "External");

// ... 10 minutes later, same alert
await _alertingService.SendAlertAsync(
    severity: AlertSeverity.Error,
    title: "Payment Gateway Timeout",  // Same title
    category: "External");              // Same category
// → THROTTLED (not sent to channels, only logged)

// ... 61 minutes later
await _alertingService.SendAlertAsync(
    severity: AlertSeverity.Error,
    title: "Payment Gateway Timeout",
    category: "External");
// → SENT (throttle window expired)
```

### Querying Alert History

```csharp
// Get active (unresolved) alerts
var query = new GetActiveAlertsQuery(
    severity: AlertSeverity.Critical,  // Optional filter
    category: "System");               // Optional filter

var activeAlerts = await _mediator.Send(query);

// Get alert history
var historyQuery = new GetAlertHistoryQuery(
    startDate: DateTime.UtcNow.AddDays(-7),
    endDate: DateTime.UtcNow,
    severity: null,  // All severities
    category: null); // All categories

var history = await _mediator.Send(historyQuery);
```

### Resolving Alerts

```csharp
// Mark alert as resolved
var command = new ResolveAlertCommand(
    alertId: alertId,
    resolvedBy: userId,
    resolution: "Database connection restored after restart");

var result = await _mediator.Send(command);
```

---

## ✅ Best Practices

### 1. Structured Logging

```csharp
// ✅ GOOD: Structured with parameters
_logger.LogInformation(
    "User {UserId} uploaded PDF {FileName}. Size: {FileSizeBytes} bytes",
    userId,
    fileName,
    fileSizeBytes);

// ❌ BAD: String interpolation
_logger.LogInformation($"User {userId} uploaded PDF {fileName}. Size: {fileSizeBytes} bytes");

// ❌ BAD: String concatenation
_logger.LogInformation("User " + userId + " uploaded PDF " + fileName);
```

### 2. Log Levels

```csharp
// ✅ GOOD: Appropriate level
_logger.LogInformation("User logged in successfully");  // Normal business event
_logger.LogWarning("Retry attempt 2/3 for API call");   // Anomaly but not error
_logger.LogError(ex, "Failed to send email");           // Operation failed

// ❌ BAD: Wrong level
_logger.LogError("User logged in successfully");  // Not an error!
_logger.LogInformation(ex, "Failed to send email");  // This IS an error!
```

### 3. Exception Logging

```csharp
// ✅ GOOD: Log exception with context
try
{
    await _service.ProcessAsync(data);
}
catch (Exception ex)
{
    _logger.LogError(ex,
        "Failed to process data for user {UserId}. DataId: {DataId}",
        userId,
        dataId);
    throw;
}

// ❌ BAD: Log exception without context
catch (Exception ex)
{
    _logger.LogError(ex, "Error");  // No context!
    throw;
}

// ❌ BAD: Log exception message (loses stack trace)
catch (Exception ex)
{
    _logger.LogError(ex.Message);  // Use ex, not ex.Message!
    throw;
}
```

### 4. Sensitive Data

```csharp
// ✅ GOOD: No sensitive data
_logger.LogInformation("User {UserId} changed password", userId);

// ✅ GOOD: Sanitized
_logger.LogWarning(
    "Login failed for email {Email}",
    LogValueSanitizer.Sanitize(email));

// ❌ BAD: Sensitive data exposed
_logger.LogDebug("User password: {Password}", password);  // NEVER!
_logger.LogInformation("Credit card: {CreditCard}", creditCard);  // NEVER!
```

### 5. Metrics Tags

```csharp
// ✅ GOOD: Low cardinality tags
var tags = new TagList
{
    { "user.role", "admin" },        // Low cardinality (3-4 values)
    { "endpoint", "/api/v1/games" }, // Use route template, not actual path
    { "success", true }              // Boolean (2 values)
};

// ❌ BAD: High cardinality tags (kills Prometheus performance)
var tags = new TagList
{
    { "user.id", userId },       // High cardinality (millions of values)
    { "email", email },          // High cardinality
    { "timestamp", timestamp }   // Infinite cardinality!
};
```

### 6. Distributed Tracing

```csharp
// ✅ GOOD: Descriptive span names, relevant tags
using (var activity = MeepleAiActivitySources.Rag.StartActivity("ProcessRagQuery"))
{
    activity?.SetTag("query", query);
    activity?.SetTag("game.id", gameId);
    // ... logic ...
}

// ❌ BAD: Generic span names, no tags
using (var activity = MeepleAiActivitySources.Rag.StartActivity("Process"))
{
    // No tags!
    // ... logic ...
}
```

### 7. Error Handling

```csharp
// ✅ GOOD: Throw domain exceptions, let middleware handle
public async Task<Game> GetGameByIdAsync(Guid gameId)
{
    var game = await _repository.GetByIdAsync(gameId);
    if (game == null)
        throw new NotFoundException($"Game with ID '{gameId}' not found");
    return game;
}

// ❌ BAD: Catching and returning null (loses error context)
public async Task<Game?> GetGameByIdAsync(Guid gameId)
{
    try
    {
        return await _repository.GetByIdAsync(gameId);
    }
    catch (Exception)
    {
        return null;  // What went wrong? No idea!
    }
}
```

### 8. Domain Events

```csharp
// ✅ GOOD: Raise events for significant business events
public void CompleteOrder(Order order)
{
    order.MarkAsCompleted();
    AddDomainEvent(new OrderCompletedEvent(order.Id, order.Total));
}

// ❌ BAD: Raising events for trivial changes
public void UpdateLastSeenAt()
{
    LastSeenAt = DateTime.UtcNow;
    AddDomainEvent(new LastSeenUpdatedEvent(Id, LastSeenAt));  // Too noisy!
}
```

---

## 🔧 Troubleshooting

### Logs non appaiono in HyperDX

**Verifica**:
1. HyperDX è running: `curl http://localhost:8180/health`
2. `SEQ_URL` configurato in environment variables o appsettings
3. Log level in configuration: `Logging:LogLevel:HyperDX` almeno `Debug`

**Debug**:
```bash
# Check HyperDX logs
docker logs seq

# Check app logs (console sink)
docker logs meepleai-api

# Check Serilog configuration
# In appsettings.json:
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "HyperDX": "Debug"  # ← Ensure this is set
    }
  }
}
```

### Traces non appaiono in HyperDX

**Verifica**:
1. HyperDX è running: `curl http://localhost:8180/`
2. `OTEL_EXPORTER_OTLP_ENDPOINT` configurato: `http://jaeger:4318`
3. Activity Source registrato in `ObservabilityServiceExtensions.cs`

**Debug**:
```bash
# Check HyperDX logs
docker logs jaeger

# Check if spans are created
using (var activity = MeepleAiActivitySources.Api.StartActivity("Test"))
{
    _logger.LogInformation("Activity TraceId: {TraceId}", activity?.TraceId);
}
```

### Metriche non appaiono in Prometheus

**Verifica**:
1. Prometheus è running: `curl http://localhost:9090/`
2. Endpoint `/metrics` esposto: `curl http://localhost:8080/metrics`
3. Meter registrato: `.AddMeter(MeepleAiMetrics.MeterName)`

**Debug**:
```bash
# Check Prometheus targets
curl http://localhost:9090/api/v1/targets

# Check if metric exists
curl http://localhost:8080/metrics | grep meepleai

# Query in Prometheus UI
meepleai_rag_requests_total
```

### Correlation ID non trovato

**Verifica**:
1. `HttpContext.TraceIdentifier` usato per correlation
2. LogContext enrich con CorrelationId

**Debug**:
```csharp
// In logs, sempre includere TraceIdentifier
_logger.LogInformation(
    "Request {Method} {Path}. TraceId: {TraceId}",
    context.Request.Method,
    context.Request.Path,
    context.TraceIdentifier);

// Search in HyperDX
CorrelationId = "abc-123" or TraceIdentifier = "abc-123"
```

### Alert non inviato

**Verifica**:
1. `Alerting:Enabled` = true in appsettings
2. Canale configurato (Email/Slack/PagerDuty)
3. Alert non throttled (60min window)

**Debug**:
```csharp
// Check alert was logged
_logger.LogInformation("Alert sent: {AlertId}", alertId);

// Query alert history
var query = new GetAlertHistoryQuery(
    startDate: DateTime.UtcNow.AddHours(-1),
    endDate: DateTime.UtcNow);
var history = await _mediator.Send(query);

// Check throttling
// If same (Severity, Title, Category) sent <60min ago → throttled
```

---

## 📚 Risorse Aggiuntive

### Documentazione

- [Serilog Documentation](https://github.com/serilog/serilog/wiki)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/instrumentation/net/)
- [Prometheus Query Language](https://prometheus.io/docs/prometheus/latest/querying/basics/)
- [HyperDX Tracing](https://www.jaegertracing.io/docs/)
- [HyperDX Query Language](https://docs.datalust.co/docs/the-seq-query-language)

### Code References

- **Logging**: `apps/api/src/Api/Logging/LoggingConfiguration.cs`
- **Metrics**: `apps/api/src/Api/Observability/MeepleAiMetrics.cs`
- **Tracing**: `apps/api/src/Api/Extensions/ObservabilityServiceExtensions.cs`
- **Error Handling**: `apps/api/src/Api/Middleware/ApiExceptionHandlerMiddleware.cs`
- **Domain Events**: `apps/api/src/Api/SharedKernel/Domain/Entities/AggregateRoot.cs`
- **Alerting**: `apps/api/src/Api/BoundedContexts/Administration/`

### Dashboard URLs (Local Development)

- **HyperDX** (Logs): http://localhost:8180
- **HyperDX** (Traces): http://localhost:8180
- **Prometheus** (Metrics): http://localhost:9090
- **Grafana** (Dashboards): http://localhost:3001
  - Username: `admin`
  - Password: `admin` (change on first login)

---

**Happy Logging! 📊✨**

Se hai domande o problemi, consulta la [Code Review](../code-reviews/LOGGING-EVENTS-SYSTEM-REVIEW-2025-11-22.md) per dettagli tecnici approfonditi.
