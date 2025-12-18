# Issue #2184 - Exception Handling Categorization

## Analisi Campioni (3 file rappresentativi)

### 1. HandleOAuthCallbackCommandHandler.cs ✅
**Pattern**: Command Handler con catch specifici
```csharp
catch (ValidationException ex) { /* specifico */ }
catch (DomainException ex) { /* specifico */ }
catch (Exception ex) { /* fallback inaspettati */ }
```
**Azione**: PRAGMA + Justification "COMMAND HANDLER PATTERN"

### 2. PdfUploadQuotaService.cs ⚠️
**Pattern**: Infrastructure service con fail-open
- 7 catch generali
- Già ha 1 pragma S2139 (riga 335)
- Potenziali eccezioni specifiche: RedisConnectionException, RedisTimeoutException
**Azione**: REFACTOR parziale + PRAGMA per fail-open

### 3. RedisBackgroundTaskOrchestrator.cs ✅
**Pattern**: Background task con catch specifici
```csharp
catch (OperationCanceledException ex) { /* specifico */ }
```
**Azione**: VERIFICA se ci sono catch generali da giustificare

---

## 📊 CATEGORIZZAZIONE COMPLETA (71 file)

### CATEGORIA A: CQRS Handlers (Command/Query)
**Conteggio stimato**: ~45 file (63%)
**Pattern**: Catch specifici (ValidationException, DomainException) + catch generale per unexpected

**File rappresentativi**:
- Authentication/Application/Commands/**/*.cs
- Authentication/Application/Queries/**/*.cs
- DocumentProcessing/Application/Commands/**/*.cs
- DocumentProcessing/Application/Handlers/**/*.cs
- KnowledgeBase/Application/Handlers/**/*.cs
- Administration/Application/Handlers/**/*.cs

**Refactoring**:
- ✅ Mantieni catch specifici esistenti
- ➕ Aggiungi #pragma CA1031 con justification "COMMAND HANDLER PATTERN"
- 📝 Template justification:
```csharp
#pragma warning disable CA1031 // Do not catch general exception types
// Justification: COMMAND HANDLER PATTERN - CQRS handler boundary
// Specific exceptions (ValidationException, DomainException) caught separately.
// Generic catch handles unexpected infrastructure failures (DB, network, memory)
// to prevent exception propagation to API layer. Returns Result<T> pattern.
catch (Exception ex)
{
    _logger.LogError(ex, "Unexpected error in {Handler}", nameof(HandlerName));
    return Result.Failure("An unexpected error occurred");
}
#pragma warning restore CA1031
```

---

### CATEGORIA B: Infrastructure Services (Redis, External APIs)
**Conteggio stimato**: ~15 file (21%)
**Pattern**: Fail-open, resilience, non-critical operations

**File rappresentativi**:
- PdfUploadQuotaService.cs (7 catch)
- InfrastructureHealthService.cs (2 catch)
- InfrastructureDetailsService.cs (5 catch)
- RedisOAuthStateStore.cs (3 catch)
- InfisicalSecretsClient.cs (4 catch)

**Refactoring**:
- 🔧 **Priorità ALTA**: Sostituisci con eccezioni specifiche dove possibile
  - Redis: `RedisConnectionException`, `RedisTimeoutException`
  - HTTP: `HttpRequestException`, `TaskCanceledException`
  - Secrets: `UnauthorizedAccessException`, `InvalidOperationException`
- ➕ Aggiungi pragma per fail-open pattern rimanenti
- 📝 Template justification (fail-open):
```csharp
#pragma warning disable CA1031
// Justification: FAIL-OPEN PATTERN - Infrastructure resilience
// If Redis/service fails, allow operation to continue (prioritize availability).
// Specific connection/timeout exceptions could be caught separately for retry logic.
catch (Exception ex)
{
    _logger.LogWarning(ex, "Service failure, using fail-open strategy");
    return DefaultValue; // or allow request through
}
#pragma warning restore CA1031
```

---

### CATEGORIA C: Event Handlers & Background Tasks
**Conteggio stimato**: ~6 file (8%)
**Pattern**: Domain events, scheduled tasks, background processing

**File rappresentativi**:
- DashboardCacheInvalidationEventHandler.cs
- ApiKeyUsedEventHandler.cs
- TwoFactorDisabledEventHandler.cs
- UsedTotpCodeCleanupTask.cs
- GenerateReportJob.cs (3 catch)

**Refactoring**:
- 🔧 Verifica se hanno già catch specifici per `OperationCanceledException`
- ➕ Aggiungi pragma con justification "EVENT HANDLER PATTERN" o "BACKGROUND TASK PATTERN"
- 📝 Template justification:
```csharp
#pragma warning disable CA1031
// Justification: EVENT HANDLER PATTERN - Background event processing
// Event handlers must not throw exceptions (violates mediator pattern).
// Errors logged for monitoring; failed events don't block system.
catch (Exception ex)
{
    _logger.LogError(ex, "Event handler failed for {EventType}", typeof(Event).Name);
    // Event system continues
}
#pragma warning restore CA1031
```

---

### CATEGORIA D: Middleware & Routing
**Conteggio stimato**: ~5 file (7%)
**Pattern**: Request pipeline, authentication, rate limiting

**File rappresentativi**:
- ApiKeyAuthenticationMiddleware.cs
- SessionAuthenticationMiddleware.cs
- SecurityHeadersMiddleware.cs
- KnowledgeBaseEndpoints.cs
- TestEndpoints.cs

**Refactoring**:
- ✅ Mantieni fail-open pattern per middleware
- ➕ Aggiungi pragma con justification "MIDDLEWARE BOUNDARY PATTERN"
- 📝 Template justification:
```csharp
#pragma warning disable CA1031
// Justification: MIDDLEWARE BOUNDARY PATTERN - Fail-open on errors
// Middleware failures shouldn't crash request pipeline (self-DOS prevention).
// Authentication/rate limit errors allow request through (unauthenticated/unthrottled).
catch (Exception ex)
{
    _logger.LogWarning(ex, "Middleware failed, allowing request (fail-open)");
    await _next(context); // Continue pipeline
}
#pragma warning restore CA1031
```

---

## 🎯 PIANO DI REFACTORING IBRIDO

### FASE 1: Refactoring Eccezioni Specifiche (Categoria B - 15 file)
**Priorità**: ALTA - Migliora error handling reale

**Target**:
- PdfUploadQuotaService.cs: RedisConnectionException, RedisTimeoutException
- InfisicalSecretsClient.cs: UnauthorizedAccessException
- RedisOAuthStateStore.cs: RedisException types
- Infrastructure services: HttpRequestException, TaskCanceledException

**Stima**: 4-6 ore
**Test**: Integration tests per verifica comportamento recupero

---

### FASE 2: Pragma Justified (Categorie A, C, D - 56 file)
**Priorità**: MEDIA - Conformità CA1031

**Target**:
- 45 CQRS Handlers → COMMAND HANDLER PATTERN
- 6 Event Handlers → EVENT HANDLER PATTERN
- 5 Middleware → MIDDLEWARE BOUNDARY PATTERN

**Stima**: 2-3 ore
**Test**: Build senza warning CA1031

---

### FASE 3: Validazione & Testing
**Priorità**: CRITICA

**Azioni**:
1. Build completa: `dotnet build` (zero CA1031)
2. Test suite: `dotnet test` (coverage ≥90%)
3. Integration tests per recupero errori
4. Review manuale sample files

**Stima**: 2-3 ore

---

## 📈 METRICHE ATTESE

| Metrica | Before | After Phase 1 | After Phase 2 |
|---------|--------|---------------|---------------|
| Catch totali | 237 | 237 | 237 |
| Catch non conformi | 114 (48%) | 99 (42%) | 0 (0%) |
| Catch specifici | 123 (52%) | 138 (58%) | 138 (58%) |
| Catch pragma justified | 0 | 0 | 99 (42%) |
| Warning CA1031 | 114 | 99 | 0 |
| Conformità | 51.9% | 58.2% | 100% |

---

## ✅ VALIDAZIONE FINALE

**Checklist**:
- [ ] Zero warning CA1031
- [ ] Zero warning S2139
- [ ] Tutti i catch hanno logging con `ex` parameter
- [ ] Infrastructure services con catch specifici testati
- [ ] Coverage ≥90% mantenuto
- [ ] Build passa
- [ ] Test suite passa

---

**Generato**: 2025-12-18
**Issue**: #2184
**Approccio**: Ibrido (Refactor + Pragma)
