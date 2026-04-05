# Auth Error Messages Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Correggere 4 bug nella gestione degli errori del flusso di login: messaggi DomainException silenziati, 2FA verify senza body, validazione duplicata nell'endpoint e dead code nel handler.

**Architecture:** Tutti i fix sono localizzati — middleware (1 file), endpoint 2FA (1 file), endpoint login (1 file), handler login (1 file). Nessuna modifica al dominio o all'infrastruttura. TempSessionService usa PostgreSQL (DB condiviso) — F-001 dello spec-panel era un falso allarme.

**Tech Stack:** .NET 9, ASP.NET Minimal APIs, MediatR, FluentValidation, xUnit, FluentAssertions, Moq

---

## File map

| File | Tipo | Modifica |
|------|------|---------|
| `apps/api/src/Api/Middleware/ApiExceptionHandlerMiddleware.cs` | Modifica | Propagare `ex.Message` per `DomainException` |
| `apps/api/tests/Api.Tests/Middleware/ApiExceptionHandlerMiddlewareTests.cs` | Modifica | Aggiungere test per propagazione messaggio |
| `apps/api/src/Api/Routing/TwoFactorEndpoints.cs` | Modifica | Restituire JSON con `ErrorMessage` invece di 401 vuoto |
| `apps/api/src/Api/Routing/AuthenticationEndpoints.cs` | Modifica | Rimuovere check `IsNullOrWhiteSpace` duplicato |
| `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Login/LoginCommandHandler.cs` | Modifica | Rimuovere dead code e commenti fuorvianti |

---

## Task 1: Fix DomainException — propagare il messaggio nel middleware

**Problema:** `MapExceptionToResponse` in `ApiExceptionHandlerMiddleware.cs:238` hardcoda il messaggio `"The request could not be processed"` per tutte le `DomainException`, ignorando `ex.Message`. Questo fa sì che il client non riceva mai il messaggio "Account is locked. Please try again in X minute(s)." dal login handler.

**Files:**
- Modify: `apps/api/src/Api/Middleware/ApiExceptionHandlerMiddleware.cs:214-283`
- Modify: `apps/api/tests/Api.Tests/Middleware/ApiExceptionHandlerMiddlewareTests.cs`

- [ ] **Step 1: Scrivi il test failing — DomainException propaga il messaggio**

Apri `apps/api/tests/Api.Tests/Middleware/ApiExceptionHandlerMiddlewareTests.cs`.
Aggiungi questo test DOPO il test `InvokeAsync_DomainException_Returns400` (riga 184):

```csharp
[Fact]
public async Task InvokeAsync_DomainException_PropagatesMessage()
{
    // Arrange
    var exception = new DomainException("Invalid email or password");
    var middleware = new ApiExceptionHandlerMiddleware(
        next: (context) => throw exception,
        _loggerMock.Object,
        _environmentMock.Object);

    _httpContext.Response.Body = new MemoryStream();

    // Act
    await middleware.InvokeAsync(_httpContext);

    // Assert
    _httpContext.Response.StatusCode.Should().Be(400);
    _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
    using var reader = new StreamReader(_httpContext.Response.Body);
    var responseBody = await reader.ReadToEndAsync(TestCancellationToken);
    using var errorResponse = ParseErrorResponse(responseBody);

    errorResponse.RootElement.GetProperty("error").GetString().Should().Be("domain_error");
    errorResponse.RootElement.GetProperty("message").GetString().Should().Be("Invalid email or password");
}

[Theory]
[InlineData("Invalid email or password")]
[InlineData("Account is locked. Please try again in 3 minute(s).")]
[InlineData("Account has been locked due to too many failed login attempts. Please try again in 15 minutes.")]
[InlineData("Account is suspended")]
public async Task InvokeAsync_DomainException_PropagatesAllLoginMessages(string loginErrorMessage)
{
    // Arrange
    var exception = new DomainException(loginErrorMessage);
    var middleware = new ApiExceptionHandlerMiddleware(
        next: (context) => throw exception,
        _loggerMock.Object,
        _environmentMock.Object);

    _httpContext.Response.Body = new MemoryStream();

    // Act
    await middleware.InvokeAsync(_httpContext);

    // Assert
    _httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
    using var reader = new StreamReader(_httpContext.Response.Body);
    var responseBody = await reader.ReadToEndAsync(TestCancellationToken);
    using var errorResponse = ParseErrorResponse(responseBody);

    errorResponse.RootElement.GetProperty("message").GetString().Should().Be(loginErrorMessage);
}
```

- [ ] **Step 2: Esegui i test per verificare che falliscano**

```bash
cd apps/api && dotnet test tests/Api.Tests --filter "FullyQualifiedName~ApiExceptionHandlerMiddlewareTests" -v --no-build 2>&1 | tail -30
```

I due test aggiunti devono fallire con messaggio tipo:
`Expected "Invalid email or password" but found "The request could not be processed"`

- [ ] **Step 3: Applica il fix nel middleware**

Apri `apps/api/src/Api/Middleware/ApiExceptionHandlerMiddleware.cs`.
Trova il blocco `MapExceptionToResponse` (riga ~214). Modifica **solo** il case `DomainException`:

Trova:
```csharp
            DomainException => (
                StatusCodes.Status400BadRequest,
                "domain_error",
                "The request could not be processed"
            ),
```

Sostituisci con:
```csharp
            DomainException domainEx => (
                StatusCodes.Status400BadRequest,
                "domain_error",
                domainEx.Message
            ),
```

- [ ] **Step 4: Esegui tutti i test del middleware per verificare che passino**

```bash
cd apps/api && dotnet test tests/Api.Tests --filter "FullyQualifiedName~ApiExceptionHandlerMiddlewareTests" -v --no-build 2>&1 | tail -40
```

Tutti i test devono passare. Se `InvokeAsync_DomainExceptions_ReturnCorrectCodes` fallisce perché ora controlla il messaggio, va bene — verifica solo che `error` e status code siano corretti (il test esistente non controlla il messaggio).

- [ ] **Step 5: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-backend
git add apps/api/src/Api/Middleware/ApiExceptionHandlerMiddleware.cs
git add apps/api/tests/Api.Tests/Middleware/ApiExceptionHandlerMiddlewareTests.cs
git commit -m "fix(auth): propagate DomainException message in exception middleware

DomainException was always mapping to 'The request could not be processed',
discarding ex.Message. Login errors like account lockout and suspension were
never visible to clients.

Fixes: account locked message with remaining minutes now reaches the client.
Adds: 2 new tests verifying all login DomainException messages are propagated."
```

---

## Task 2: Fix 2FA verify — restituire ErrorMessage nel 401

**Problema:** `TwoFactorEndpoints.cs:135` restituisce `Results.Unauthorized()` (401 senza body) quando la verifica 2FA fallisce, scartando `verifyResult.ErrorMessage` dal handler. Il client non può distinguere tra "token scaduto" e "codice sbagliato".

**Files:**
- Modify: `apps/api/src/Api/Routing/TwoFactorEndpoints.cs:132-136`

Non esiste un test unitario per questo endpoint (è un Minimal API endpoint). Il test sarà un nuovo test di integrazione per il flusso 2FA, oppure — data la semplicità della modifica — verifichiamo manualmente la struttura del JSON.

- [ ] **Step 1: Identifica la riga da modificare**

Apri `apps/api/src/Api/Routing/TwoFactorEndpoints.cs`.
Cerca il blocco (riga ~132):

```csharp
            if (!verifyResult.Success || verifyResult.UserId == null)
            {
                logger.LogWarning("2FA verification failed: {ErrorMessage}", verifyResult.ErrorMessage);
                return Results.Unauthorized();
            }
```

- [ ] **Step 2: Applica il fix**

Sostituisci il blocco con:

```csharp
            if (!verifyResult.Success || verifyResult.UserId == null)
            {
                logger.LogWarning("2FA verification failed: {ErrorMessage}", verifyResult.ErrorMessage);
                return Results.Json(
                    new { error = "two_factor_failed", message = verifyResult.ErrorMessage ?? "Two-factor verification failed" },
                    statusCode: StatusCodes.Status401Unauthorized);
            }
```

Nota: `StatusCodes` è già disponibile (namespace `Microsoft.AspNetCore.Http` usato nel file).

- [ ] **Step 3: Verifica che il progetto compili**

```bash
cd apps/api && dotnet build src/Api/Api.csproj --no-restore -warnaserror 2>&1 | tail -20
```

Deve terminare con `Build succeeded`.

- [ ] **Step 4: Esegui i test di autenticazione esistenti per verificare nessuna regressione**

```bash
cd apps/api && dotnet test tests/Api.Tests --filter "FullyQualifiedName~TwoFactor" -v --no-build 2>&1 | tail -30
```

- [ ] **Step 5: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-backend
git add apps/api/src/Api/Routing/TwoFactorEndpoints.cs
git commit -m "fix(auth): return error message body on 2FA verification failure

2FA verify endpoint returned bare 401 Unauthorized, discarding the
handler's ErrorMessage. Client could not distinguish between:
- 'Invalid or expired session token' (must restart login)
- 'Invalid verification code' (can retry code)

Now returns JSON body: { error: 'two_factor_failed', message: '...' }"
```

---

## Task 3: Rimuovere validazione duplicata nell'endpoint login

**Problema:** `AuthenticationEndpoints.cs:139-143` fa un check manuale `IsNullOrWhiteSpace(email) || IsNullOrWhiteSpace(password)` che duplica FluentValidation. Il risultato è due possibili formati di errore per la stessa condizione:
- Path A (endpoint): `400 { "error": "Email and password are required" }`
- Path B (FluentValidation): `422 { "error": "validation_error", "errors": { "Email": ["..."], "Password": ["..."] } }`

Il check manuale non sarà mai raggiunto se il body JSON è parsato correttamente, poiché FluentValidation gira nel MediatR pipeline prima del handler. Va rimosso.

**Files:**
- Modify: `apps/api/src/Api/Routing/AuthenticationEndpoints.cs` — rimuovere il check manuale email/password

- [ ] **Step 1: Individua il blocco da rimuovere**

Apri `apps/api/src/Api/Routing/AuthenticationEndpoints.cs`.
Cerca (riga ~139):

```csharp
            if (string.IsNullOrWhiteSpace(payload.Email) || string.IsNullOrWhiteSpace(payload.Password))
            {
                logger.LogWarning("Login failed: email or password is empty");
                return Results.BadRequest(new { error = "Email and password are required" });
            }
```

- [ ] **Step 2: Rimuovi il blocco — FluentValidation gestisce questo caso**

Rimuovi completamente le righe 139-143 (il blocco `if IsNullOrWhiteSpace`). Il codice che segue (costruzione del `DddLoginCommand`) rimane invariato.

Il risultato dopo la rimozione sarà:

```csharp
            if (payload == null)
            {
                logger.LogWarning("Login failed: payload is null");
                return Results.BadRequest(new { error = "Invalid request payload" });
            }

            var command = new DddLoginCommand(
                Email: payload.Email,
                Password: payload.Password,
                ...
```

- [ ] **Step 3: Verifica compilazione**

```bash
cd apps/api && dotnet build src/Api/Api.csproj --no-restore -warnaserror 2>&1 | tail -10
```

- [ ] **Step 4: Esegui i validator tests per verificare copertura**

```bash
cd apps/api && dotnet test tests/Api.Tests --filter "FullyQualifiedName~LoginCommandValidatorTests" -v --no-build 2>&1 | tail -20
```

I test del validator coprono i casi email/password vuoti via FluentValidation — devono continuare a passare.

- [ ] **Step 5: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-backend
git add apps/api/src/Api/Routing/AuthenticationEndpoints.cs
git commit -m "refactor(auth): remove duplicate email/password validation in login endpoint

The manual IsNullOrWhiteSpace check duplicated FluentValidation behavior and
produced a different error format (400 combined message vs 422 per-field errors).
FluentValidation in the MediatR pipeline handles both cases with proper per-field
error messages."
```

---

## Task 4: Rimuovere dead code e commenti fuorvianti nel LoginCommandHandler

**Problema:** `LoginCommandHandler.cs` ha due blocchi di dead code:

1. `if (string.IsNullOrWhiteSpace(command.Password)) throw new ValidationException(...)` — linea 41-43. FluentValidation gira prima, questo non viene mai raggiunto.

2. `if (command.Password.Length < 8) throw new ValidationException(...)` — linee 49-50. Idem: FluentValidation rigetta prima. Il commento su "timing attack prevention" è fuorviante perché la lunghezza minima è già validata da FluentValidation prima della DB query, invalidando l'intento.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Login/LoginCommandHandler.cs:40-50`

- [ ] **Step 1: Individua i blocchi da rimuovere**

Apri `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Login/LoginCommandHandler.cs`.

Trova questa sezione (righe ~40-50):

```csharp
        // Issue #2564: Validate null/empty password BEFORE repository call (efficiency)
        if (string.IsNullOrWhiteSpace(command.Password))
            throw new ValidationException("Password is required");

        // Find user by email (even for short passwords - prevents timing attacks)
        var email = new Email(command.Email);
        var user = await _userRepository.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);

        // Issue #2564: Validate password length AFTER repository call (security: prevent timing attack)
        if (command.Password.Length < 8)
            throw new ValidationException("Password must be at least 8 characters");
```

- [ ] **Step 2: Applica la pulizia**

Sostituisci l'intera sezione con:

```csharp
        var email = new Email(command.Email);
        var user = await _userRepository.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);
```

Spiegazione: il null check e il length check sono garantiti da `LoginCommandValidator` che gira prima nel MediatR pipeline. La DB query rimane nella stessa posizione.

- [ ] **Step 3: Verifica compilazione**

```bash
cd apps/api && dotnet build src/Api/Api.csproj --no-restore -warnaserror 2>&1 | tail -10
```

- [ ] **Step 4: Esegui i test del bounded context Authentication**

```bash
cd apps/api && dotnet test tests/Api.Tests --filter "BoundedContext=Authentication" -v --no-build 2>&1 | tail -40
```

Tutti i test devono passare. In particolare: i test del validator continuano a coprire i casi di password vuota/corta.

- [ ] **Step 5: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-backend
git add apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Login/LoginCommandHandler.cs
git commit -m "refactor(auth): remove dead code in LoginCommandHandler

Two ValidationException throws were unreachable — FluentValidation always
runs first in the MediatR pipeline (via ValidationBehavior). The length check
also had a misleading comment claiming timing-attack protection, which was
already defeated by FluentValidation rejecting short passwords before the DB query."
```

---

## Task 5: Verifica finale e run suite completa

- [ ] **Step 1: Esegui tutti i test Authentication + Middleware**

```bash
cd apps/api && dotnet test tests/Api.Tests \
  --filter "BoundedContext=Authentication|FullyQualifiedName~ApiExceptionHandlerMiddlewareTests" \
  --no-build -v 2>&1 | tail -50
```

- [ ] **Step 2: Esegui build completa**

```bash
cd apps/api && dotnet build --no-restore -warnaserror 2>&1 | tail -15
```

- [ ] **Step 3: Verifica comportamento end-to-end (manuale o con API client)**

Con l'API running (`dotnet run` in `apps/api/src/Api/`):

```
# Scenario 1: password sbagliata → deve tornare il messaggio
POST /api/v1/auth/login
{ "email": "test@example.com", "password": "WrongPassword1!" }
Expected: 400 { "error": "domain_error", "message": "Invalid email or password" }

# Scenario 2: account bloccato dopo 5 tentativi sbagliati
→ il 5° fallisce con:
Expected: 400 { "error": "domain_error", "message": "Account has been locked..." }
→ il 6° (lockout già attivo):
Expected: 400 { "error": "domain_error", "message": "Account is locked. Please try again in X minute(s)." }

# Scenario 3: 2FA codice sbagliato
POST /api/v1/auth/2fa/verify
{ "sessionToken": "<valid_temp_token>", "code": "000000" }
Expected: 401 { "error": "two_factor_failed", "message": "Invalid verification code" }

# Scenario 4: 2FA token scaduto
POST /api/v1/auth/2fa/verify
{ "sessionToken": "<expired_token>", "code": "123456" }
Expected: 401 { "error": "two_factor_failed", "message": "Invalid or expired session token" }

# Scenario 5: email vuota → FluentValidation (non duplicato)
POST /api/v1/auth/login
{ "email": "", "password": "ValidPass1!" }
Expected: 422 { "error": "validation_error", "errors": { "Email": ["Email is required"] } }
```

- [ ] **Step 4: Commit finale se ci sono modifiche residue**

```bash
cd D:/Repositories/meepleai-monorepo-backend
git status
```

---

## Note spec-panel: F-001 (TempSessionService) — FALSO ALLARME

Durante la review, il `TempSessionService` è stato verificato: usa **PostgreSQL** (`MeepleAiDbContext.TempSessions`) con transazione `Serializable`. Non è in-memory. Funziona correttamente in ambienti multi-istanza. Il concern F-001 dello spec-panel non si applica.

Analogamente, `OAuthStateStore` andrebbe verificato separatamente (fuori scope di questo piano).
