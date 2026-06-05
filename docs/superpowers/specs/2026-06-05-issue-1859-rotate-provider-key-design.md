# Issue #1859 — `POST /api/v1/admin/providers/{name}/rotate-key`

**Status**: Design APPROVED 2026-06-05
**Owner**: BC `Administration` (cross-cutting con `KnowledgeBase` LlmClients)
**Tracks**: SP5 F4-C3 carry-forward · S3 strict 2FA cutover follow-up
**Related**: #1834 (FE re-skin Providers, MERGED) · #1597 (S3 strict 2FA, MERGED) · #936 (provider probe/quota infra)

---

## 1. Goal

Permettere al **superadmin** di sostituire una API key di un provider LLM (DeepSeek, OpenRouter, …) **runtime senza redeploy**, con i seguenti garantori:

1. **Auth gate**: ruolo `superadmin` obbligatorio
2. **Step-up gate**: TOTP recente (≤5 minuti) obbligatorio, indipendente dal flag globale `TwoFactor:StrictMode`
3. **Pre-flight validation**: probe della nuova key contro il provider prima di persistere
4. **Atomicity**: rotazione = transazione singola (validate → encrypt → upsert → audit); fallimento ⇒ rollback completo, old key preservata
5. **Audit traceability**: row in `audit_outbox` con before/after fingerprint + step-up token reference
6. **Rate-limit**: 1 rotazione per provider ogni 24h (anti-abuse + safety net)
7. **Zero downtime**: dopo rotation, la nuova key è effettiva dal prossimo request al provider (cache flush istantaneo)

**Non-goal**: rotazione automatica policy-based, calendar reminders, multi-key rolling, programmatic key generation chiamando l'API del provider (DeepSeek/OpenRouter non supportano rotation programmatica via REST; l'admin ottiene la nuova key dal dashboard provider e la incolla nel nostro modal).

---

## 2. Architecture Decisions

### D-1 — Hybrid DB-backed override + env-var fallback (Approved)

**Decisione**: introdurre tabella `provider_credentials` cifrata via `IDataProtector`. Un nuovo servizio `IProviderCredentialResolver` viene interrogato dai `ILlmClient` per ottenere la API key da usare per il provider corrente. La risoluzione segue questa cascata:

1. **DB-active-row** (se esiste riga `is_active=true` per il provider) → usa `encrypted_api_key` decrypted
2. **Env-var fallback** (`DEEPSEEK_API_KEY`, `OPENROUTER_API_KEY`, …) → behavior attuale
3. **Throw** `ProviderCredentialNotConfiguredException` (502) se nessuna delle due è configurata

**Razionale**:
- Nessun breaking change su deployment esistenti senza key in DB (env-var resta source-of-truth implicito)
- Rotation runtime senza redeploy: l'inserimento della nuova row + flush cache è atomico
- Recovery: in caso di chiave DB compromessa, basta `DELETE FROM provider_credentials WHERE is_active=true` per tornare al fallback env-var

**Alternative considerate e respinte**:
- **A) Full DB-backed credential vault**: full migration env-var → DB-only con expiration policy e auto-rotation. ~31-43h effort, over-engineered per #1859.
- **B) MVP audit-only "intent" log**: endpoint logga solo "rotation requested", SecOps gestisce env-var update manuale + redeploy. ~6-8h ma non risolve il job-to-be-done (rotation runtime).
- **C) In-memory store con persistence on shutdown**: lossy, no audit trail, anti-pattern.
- **D) Secret file hot-reload**: richiede write su file system dal process app, security anti-pattern.

### D-2 — Force-strict step-up indipendente dal flag globale (Approved)

**Decisione**: estendere `RequireTwoFactorAttribute` con campo `bool ForceStrict = false`. Il `TwoFactorEnforcementBehavior` controlla `strictMode \|\| attr.ForceStrict` per decidere se procedere con shadow-log o blocking-throw. `RotateProviderKeyCommand` viene decorato con `[RequireTwoFactor(MaxAgeMinutes = 5, ForceStrict = true, Reason = "Provider key rotation requires fresh 2FA")]`.

**Razionale**:
- Provider key rotation è security-critical: deve bloccare **sempre**, anche in dev/test dove `TwoFactor:StrictMode` può essere OFF
- L'attribute change è minimal (1 campo) e retro-compatibile (default `false` preserva behavior esistente)
- Riusabile per altri command futuri che richiedono strict step-up (delete user, rotate user password, ecc)

**Side effect**: il behavior `TwoFactorEnforcementBehavior` viene modificato in 1 punto (linea 102-107). Test esistenti sui command già decorati senza `ForceStrict` non si rompono (default `false` ⇒ stesso path shadow).

### D-3 — Probe nuova key prima di persistere (Approved)

**Decisione**: prima di scrivere la row `provider_credentials`, il command handler chiama `ProviderProbeExecutor` con la nuova key. Se il probe fallisce (status non-2xx, network error, model not found, ecc), si lancia `ProviderProbeFailedException` (HTTP 502) e si fa rollback. Old credential preserved invariata.

**Razionale**:
- Garantisce che la nuova key è valida prima di interrompere il servizio
- Evita stato corrotto (DB pensa che la nuova key è attiva, ma il provider la rifiuta)
- Riusa infrastruttura `ProviderProbeExecutor` già esistente (#936)

**Trade-off accettato**: probe aggiunge ~200-1000ms al request (a seconda del provider latency). Acceptable per un'operazione admin one-shot.

### D-4 — Audit "Level" via Details JSON, non colonna dedicata (Approved)

**Decisione**: `AuditLogEntity` non ha colonna `Level` (vedi `apps/api/src/Api/Infrastructure/Entities/Administration/AuditLogEntity.cs`). Il "Level 3" della spec originale viene rappresentato nel campo `Details` come JSON serialized:

```json
{
  "level": 3,
  "providerName": "deepseek",
  "previousKeyFingerprint": "sk-de...a3f1",
  "newKeyFingerprint": "sk-de...f7a2",
  "probeLatencyMs": 312,
  "stepUpVerifiedAt": "2026-06-05T11:00:00Z"
}
```

`Action = "ProviderKeyRotated"`, `Resource = "provider_credentials"`, `ResourceId = <new_credential_id>`, `Result = "Success"`. La row viene scritta automaticamente da `[AtomicAudit]` + `AuditLoggingBehavior` nella stessa transazione del command, via `audit_outbox`.

**Razionale**:
- Nessuna migration extra per aggiungere colonna `Level`
- JSON è searchable (PostgreSQL `details::jsonb -> 'level'`) per dashboard analytics future
- Consistente con altri audit Level 3 esistenti (impersonation, user delete)

### D-5 — No "show key once" UI: l'admin ha già la key (Approved)

**Decisione**: la spec originale di #1859 menziona `newKeyOnce: "FULL_KEY_SHOWN_ONCE_ONLY"` nel response DTO, con UI "show key in modal + checkbox 'Ho copiato la key'". Questa è una **assumption errata** della spec: l'admin INSERISCE la nuova key (l'ha ottenuta dal dashboard provider). Non c'è motivo di mostrargliela di nuovo.

Response DTO semplificato:

```csharp
public sealed record RotateProviderKeyResponseDto(
    string ProviderName,
    string NewKeyFingerprint,        // es. "sk-de...f7a2" (mai full key)
    DateTime RotatedAt,
    DateTime PreviousKeyDisabledAt); // == RotatedAt (atomic)
```

**Razionale**:
- Principle of least authority: la API non ha mai bisogno di restituire la key in chiaro (è già nota all'admin)
- Mai loggata, mai re-fetchable → riduce attack surface
- Fingerprint sufficiente per visual confirm e audit

---

## 3. Out-of-scope (deferred)

1. **Multi-key rolling** (key vecchia + key nuova entrambe valide per N giorni): out of scope. Atomic swap, una sola key attiva alla volta.
2. **Calendar-based rotation reminders** (es. "ruota la key DeepSeek ogni 90 giorni"): out of scope. Decisione manuale admin.
3. **Programmatic key generation via provider API**: out of scope (DeepSeek/OpenRouter non lo supportano in REST standard).
4. **Provider-side key revoke automation**: out of scope. L'admin DEVE revocare la old key sul dashboard provider manualmente (responsabilità human). MeepleAI semantically: "smettiamo di usare la old key da `RotatedAt`".
5. **Rotation history viewer** (UI con timeline rotazioni passate): out of scope per questa wave. Dati audit + DB query sufficienti per ora; UI dedicata in follow-up issue se richiesto.
6. **Cross-provider bulk rotation** ("ruota tutte le chiavi"): out of scope. One provider at a time.
7. **`Level` enum dedicato in `AuditLogEntity`**: deferred a futura security review (#TBD). Per ora JSON in `Details`.

---

## 4. Domain Model

### 4.1 Aggregate `ProviderCredential`

```csharp
namespace Api.BoundedContexts.Administration.Domain.Aggregates.ProviderCredentials;

public sealed class ProviderCredential
{
    public Guid Id { get; private set; }
    public ProviderName ProviderName { get; private set; }
    public string EncryptedApiKey { get; private set; }   // ciphertext, never plaintext
    public KeyFingerprint Fingerprint { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime RotatedAt { get; private set; }
    public Guid RotatedByUserId { get; private set; }
    public Guid? PreviousCredentialId { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    // Factory + state-mutation methods (Create, Deactivate) raise domain events
    public static ProviderCredential Create(
        ProviderName provider,
        string encryptedKey,
        KeyFingerprint fingerprint,
        Guid rotatedByUserId,
        Guid? previousCredentialId,
        TimeProvider timeProvider) { … }

    public void Deactivate(TimeProvider timeProvider) { … }   // raises ProviderKeyRotatedEvent
}
```

### 4.2 Value Objects

```csharp
public sealed record ProviderName
{
    public string Value { get; }
    public static ProviderName Create(string raw) { … }     // whitelist: deepseek | openrouter
    public static readonly IReadOnlySet<string> Allowed =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "deepseek", "openrouter" };
}

public sealed record KeyFingerprint
{
    public string Value { get; }                            // "sk-de...f7a2"
    public static KeyFingerprint FromPlaintext(string apiKey)
    {
        // first 5 chars + ".." + last 4 chars; min length 10 enforced
        if (string.IsNullOrEmpty(apiKey) || apiKey.Length < 10)
            throw new ArgumentException("API key too short for fingerprint", nameof(apiKey));
        return new KeyFingerprint($"{apiKey[..5]}..{apiKey[^4..]}");
    }
}
```

### 4.3 Domain Events

```csharp
public sealed record ProviderKeyRotatedEvent(
    Guid CredentialId,
    string ProviderName,
    string NewFingerprint,
    string? PreviousFingerprint,
    Guid RotatedByUserId,
    DateTime RotatedAt) : INotification;
```

Handler: `ProviderKeyRotatedEventHandler` invalida la cache `IProviderCredentialResolver` per il provider rotato (vedi §10).

---

## 5. Data Model + Migration

### 5.1 Schema

```sql
CREATE TABLE provider_credentials (
    id                      uuid          PRIMARY KEY,
    provider_name           text          NOT NULL,
    encrypted_api_key       text          NOT NULL,
    key_fingerprint         text          NOT NULL,
    is_active               boolean       NOT NULL,
    rotated_at              timestamptz   NOT NULL,
    rotated_by_user_id      uuid          NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    previous_credential_id  uuid          NULL     REFERENCES provider_credentials(id) ON DELETE SET NULL,
    row_version             bytea         NOT NULL
);

-- Garantisce 1 sola row attiva per provider; partial unique index permette N rows inattive (history)
CREATE UNIQUE INDEX ux_provider_credentials_active_one
    ON provider_credentials (provider_name)
    WHERE is_active = true;

CREATE INDEX ix_provider_credentials_rotated_at
    ON provider_credentials (provider_name, rotated_at DESC);
```

### 5.2 Migration

`apps/api/src/Api/Infrastructure/Migrations/202606050NNNNN_AddProviderCredentials.cs`

- `Up`: create table + 2 indexes
- `Down`: drop table

Nessun seed: il fallback env-var copre lo stato vuoto iniziale.

### 5.3 EntityConfiguration

```csharp
public sealed class ProviderCredentialEntityConfiguration
    : IEntityTypeConfiguration<ProviderCredential>
{
    public void Configure(EntityTypeBuilder<ProviderCredential> builder)
    {
        builder.ToTable("provider_credentials");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.ProviderName)
            .HasColumnName("provider_name")
            .HasConversion(vo => vo.Value, raw => ProviderName.Create(raw))
            .IsRequired();

        builder.Property(e => e.EncryptedApiKey).HasColumnName("encrypted_api_key").IsRequired();

        builder.Property(e => e.Fingerprint)
            .HasColumnName("key_fingerprint")
            .HasConversion(vo => vo.Value, raw => new KeyFingerprint(raw))
            .IsRequired();

        builder.Property(e => e.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(e => e.RotatedAt).HasColumnName("rotated_at").IsRequired();
        builder.Property(e => e.RotatedByUserId).HasColumnName("rotated_by_user_id").IsRequired();
        builder.Property(e => e.PreviousCredentialId).HasColumnName("previous_credential_id");
        builder.Property(e => e.RowVersion).HasColumnName("row_version").IsRowVersion();

        builder.HasIndex(e => e.ProviderName)
            .IsUnique()
            .HasFilter("is_active = true")
            .HasDatabaseName("ux_provider_credentials_active_one");

        builder.HasIndex(e => new { e.ProviderName, e.RotatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_provider_credentials_rotated_at");
    }
}
```

---

## 6. Application Layer

### 6.1 Command

```csharp
namespace Api.BoundedContexts.Administration.Application.Commands.Providers;

[RequireTwoFactor(MaxAgeMinutes = 5, ForceStrict = true,
    Reason = "Provider key rotation requires fresh 2FA")]
[AtomicAudit(Action = "ProviderKeyRotated", Resource = "provider_credentials")]
internal sealed record RotateProviderKeyCommand(
    string ProviderName,
    string NewApiKey,
    string ConfirmedProviderName,
    Guid RequestingUserId) : IRequest<RotateProviderKeyResponseDto>;
```

### 6.2 Validator

```csharp
public sealed class RotateProviderKeyCommandValidator
    : AbstractValidator<RotateProviderKeyCommand>
{
    public RotateProviderKeyCommandValidator()
    {
        RuleFor(x => x.ProviderName).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Must(ProviderName.Allowed.Contains)
                .WithMessage("Unknown provider; allowed: deepseek, openrouter");

        RuleFor(x => x.ConfirmedProviderName)
            .Equal(x => x.ProviderName)
                .WithMessage("Provider name confirmation mismatch (typo guard)");

        RuleFor(x => x.NewApiKey).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MinimumLength(10)              // fingerprint min
            .MaximumLength(512)             // anti-DoS
            .Must(NotContainWhitespace).WithMessage("API key must not contain whitespace");

        RuleFor(x => x.RequestingUserId).NotEqual(Guid.Empty);
    }

    private static bool NotContainWhitespace(string s) =>
        !string.IsNullOrEmpty(s) && !s.Any(char.IsWhiteSpace);
}
```

### 6.3 Handler (flow)

```
1. Load requesting user → check role == "superadmin" → else ForbiddenException
2. Acquire rate-limit lock (provider_name, 24h window):
   - Query: SELECT TOP 1 * FROM provider_credentials WHERE provider_name = @name ORDER BY rotated_at DESC
   - If most-recent row.RotatedAt > _timeProvider.GetUtcNow() - 24h → ConflictException (rate_limit_exceeded)
3. Probe new key:
   - executor = IProviderProbeExecutorFactory.Get(providerName)
   - result = await executor.ProbeAsync(newApiKey, ct)
   - if !result.Success → ProviderProbeFailedException (502)
4. Encrypt new key:
   - encryptedKey = IDataProtector.CreateProtector("ProviderCredentials").Protect(newApiKey)
5. Atomic DB transaction:
   - Mark previous active row as IsActive=false (if exists)
   - Insert new row IsActive=true with RotatedAt=now, RotatedByUserId=actorId
   - SaveChangesAsync (raises ProviderKeyRotatedEvent via MediatR dispatch)
6. AuditLoggingBehavior writes audit_outbox row in same transaction
7. ProviderKeyRotatedEventHandler invalidates IProviderCredentialResolver cache
8. Return RotateProviderKeyResponseDto
```

---

## 7. Infrastructure Layer

### 7.1 `IProviderCredentialResolver`

```csharp
namespace Api.BoundedContexts.Administration.Infrastructure.Services;

public interface IProviderCredentialResolver
{
    /// <summary>
    /// Resolves the active API key for the given provider. Order:
    /// 1) DB active row (decrypted)
    /// 2) Env var fallback (e.g. DEEPSEEK_API_KEY)
    /// 3) Throws ProviderCredentialNotConfiguredException
    /// </summary>
    Task<string> ResolveAsync(string providerName, CancellationToken ct);

    /// <summary>
    /// Invalidates the in-memory cache for the given provider. Called after rotation.
    /// </summary>
    void Invalidate(string providerName);
}

internal sealed class ProviderCredentialResolver : IProviderCredentialResolver
{
    private readonly IProviderCredentialRepository _repo;
    private readonly IDataProtectionProvider _protectionProvider;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ProviderCredentialResolver> _logger;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<string> ResolveAsync(string providerName, CancellationToken ct)
    {
        if (_cache.TryGetValue(CacheKey(providerName), out string? cached) && cached is not null)
            return cached;

        var active = await _repo.GetActiveAsync(providerName, ct).ConfigureAwait(false);
        if (active is not null)
        {
            var protector = _protectionProvider.CreateProtector("ProviderCredentials");
            var plaintext = protector.Unprotect(active.EncryptedApiKey);
            _cache.Set(CacheKey(providerName), plaintext, CacheTtl);
            return plaintext;
        }

        var envVar = ProviderEnvVarMap.For(providerName);
        var envValue = Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrWhiteSpace(envValue))
        {
            _cache.Set(CacheKey(providerName), envValue, CacheTtl);
            return envValue;
        }

        throw new ProviderCredentialNotConfiguredException(providerName);
    }

    public void Invalidate(string providerName) => _cache.Remove(CacheKey(providerName));

    private static string CacheKey(string provider) => $"provider_cred:{provider.ToLowerInvariant()}";
}
```

### 7.2 `ProviderEnvVarMap`

```csharp
internal static class ProviderEnvVarMap
{
    public static string For(string providerName) => providerName.ToLowerInvariant() switch
    {
        "deepseek" => "DEEPSEEK_API_KEY",
        "openrouter" => "OPENROUTER_API_KEY",
        _ => throw new UnknownProviderException(providerName)
    };
}
```

### 7.3 Repository

```csharp
public interface IProviderCredentialRepository
{
    Task<ProviderCredential?> GetActiveAsync(string providerName, CancellationToken ct);
    Task<ProviderCredential?> GetLastRotationAsync(string providerName, CancellationToken ct);
    Task AddAsync(ProviderCredential credential, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

---

## 8. API Endpoint

### 8.1 Route registration

In `apps/api/src/Api/Routing/AdminProviderEndpoints.cs`:

```csharp
group.MapPost("/{name}/rotate-key", async (
        string name,
        RotateProviderKeyRequestDto body,
        IMediator mediator,
        ClaimsPrincipal user,
        CancellationToken ct) =>
    {
        var actorIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(actorIdClaim, out var actorId))
            return Results.Unauthorized();

        var result = await mediator.Send(new RotateProviderKeyCommand(
            ProviderName: name,
            NewApiKey: body.NewApiKey,
            ConfirmedProviderName: body.ConfirmedProviderName,
            RequestingUserId: actorId), ct).ConfigureAwait(false);

        return Results.Ok(result);
    })
    .RequireAuthorization("RequireSuperAdmin")
    .RequireRateLimiting("AdminProviderRotateKey")
    .WithOpenApi();
```

### 8.2 DTOs

```csharp
public sealed record RotateProviderKeyRequestDto(
    string NewApiKey,
    string ConfirmedProviderName);

public sealed record RotateProviderKeyResponseDto(
    string ProviderName,
    string NewKeyFingerprint,
    DateTime RotatedAt,
    DateTime PreviousKeyDisabledAt);
```

### 8.3 Rate-limit policy

Aggiungere a `RateLimitingServiceExtensions`:

```csharp
options.AddPolicy("AdminProviderRotateKey", httpContext =>
{
    var providerName = httpContext.Request.RouteValues["name"]?.ToString() ?? "unknown";
    return RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: $"rotate:{providerName}",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 1,
            Window = TimeSpan.FromHours(24),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
});
```

**Note**: il rate-limit di policy ASP.NET copre il primo livello (anti-replay rapido). Il check nel handler (§6.3 step 2) è il second guard via DB query, sopravvive a restart processo / multi-instance.

---

## 9. Step-up Enforcement (`ForceStrict` extension)

### 9.1 RequireTwoFactorAttribute change

```csharp
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
internal sealed class RequireTwoFactorAttribute : Attribute
{
    public int MaxAgeMinutes { get; set; } = 30;
    public string? Reason { get; set; }

    /// <summary>
    /// When true, the behavior enforces strict step-up even if the global
    /// <c>TwoFactor:StrictMode</c> flag is OFF. Use for security-critical commands
    /// (provider key rotation, etc) that must always block on stale 2FA.
    /// Default: false (respects global flag).
    /// </summary>
    public bool ForceStrict { get; set; } = false;
}
```

### 9.2 TwoFactorEnforcementBehavior change

Linea 102-107 di `TwoFactorEnforcementBehavior.cs`:

```csharp
var strictMode = await _twoFactorConfig.GetStrictModeAsync(cancellationToken).ConfigureAwait(false);
if (!strictMode && !attr.ForceStrict)        // ← ONLY CHANGE: respect attr.ForceStrict
{
    LogShadow(actor, attr);
    return await next().ConfigureAwait(false);
}
```

Nessun altro change al behavior. Test esistenti su command senza `ForceStrict` rimangono verdi (default `false` ⇒ same path).

---

## 10. Provider Client Refactor

### 10.1 Surface impact

LlmClient implementations attualmente leggono la key da `IConfiguration` (`appsettings.json` → env var). Devono passare ad `IProviderCredentialResolver`:

```csharp
// ── BEFORE ──
public DeepSeekClient(IConfiguration cfg, …)
{
    _apiKey = cfg["AI:Providers:DeepSeek:ApiKey"];   // value of $DEEPSEEK_API_KEY
}

// ── AFTER ──
public DeepSeekClient(IProviderCredentialResolver resolver, …)
{
    _resolver = resolver;
}

public async Task<…> SendAsync(…, CancellationToken ct)
{
    var apiKey = await _resolver.ResolveAsync("deepseek", ct);
    // … use apiKey for Authorization header
}
```

### 10.2 Caching tier

`ProviderCredentialResolver` ha cache 5min (`IMemoryCache`). LlmClient può chiamare `ResolveAsync` ad ogni request senza perf penalty (cache hit ratio ~100% in steady state). Cache flush on rotation è istantaneo via event handler (§4.3 + §10.3).

### 10.3 Cache invalidation flow

```
RotateProviderKeyCommandHandler
    └─ SaveChangesAsync()
       ├─ MeepleAiDbContext dispatches ProviderKeyRotatedEvent via MediatR
       └─ ProviderKeyRotatedEventHandler.Handle(event)
              └─ _resolver.Invalidate(event.ProviderName)
                    ↓
              Next ResolveAsync("deepseek", …) ⇒ cache miss ⇒ DB lookup ⇒ new key returned
```

### 10.4 Client touched

| Client | Path | Change |
|---|---|---|
| `DeepSeekClient` | `apps/api/src/Api/Services/LlmClients/DeepSeekClient.cs` | Inject resolver, ResolveAsync in SendAsync |
| `OpenRouterClient` | `apps/api/src/Api/Services/LlmClients/OpenRouterClient.cs` | Same |
| `OllamaClient` | (no API key) | No change |

Plus probe executor:
| `OpenAiCompatibleProbeExecutor` | Idem refactor to optionally take new key via param (for rotation pre-flight probe) |

---

## 11. Audit Emission

### 11.1 `[AtomicAudit]` attribute

```csharp
[AtomicAudit(Action = "ProviderKeyRotated", Resource = "provider_credentials")]
internal sealed record RotateProviderKeyCommand(…) : IRequest<…>;
```

### 11.2 Details JSON shape

`AuditLoggingBehavior` popola `Details` con JSON serializzato (vedi OQ-1 per la verifica dell'attuale comportamento behavior vs manual build). Layout target:

```json
{
  "level": 3,
  "providerName": "deepseek",
  "previousKeyFingerprint": "sk-de...a3f1",
  "newKeyFingerprint": "sk-de...f7a2",
  "probeLatencyMs": 312,
  "stepUpVerifiedAt": "2026-06-05T11:00:00Z"
}
```

### 11.3 Step-up token reference

`AuditLogEntity.StepUpTokenId` (column esistente da S3) viene popolato dal `AuditLoggingBehavior` quando il command è gated da `RequireTwoFactor`. Reference alla session's `LastTotpVerifiedAt` event.

---

## 12. Failure Modes

| HTTP | Subcode | Trigger | Audit row |
|---|---|---|---|
| **401** | `step_up_required` | TOTP non recente (>5min) | `TwoFactorRequired` (fresh DI scope, indipendente dalla tx rolled-back) |
| **401** | `enroll_required` | Actor no 2FA enrolled | `TwoFactorRequired` (idem) |
| **401** | `unauthenticated` | No session / invalid token | No audit (middleware-level) |
| **403** | `forbidden_not_superadmin` | Actor è admin ma non superadmin | `ProviderKeyRotationDenied` (Result=Denied) |
| **400** | `provider_name_mismatch` | `confirmedProviderName ≠ {name}` | `ProviderKeyRotationDenied` (Result=Denied, Details: typo guard) |
| **400** | `invalid_provider` | `name ∉ {deepseek, openrouter}` | `ProviderKeyRotationDenied` (idem) |
| **400** | `invalid_key_format` | API key vuota / con whitespace / <10 chars / >512 chars | `ProviderKeyRotationDenied` (idem) |
| **409** | `rate_limit_exceeded` | Last rotation <24h | `ProviderKeyRotationDenied` (Details: lastRotatedAt) |
| **502** | `provider_probe_failed` | Probe nuova key non-2xx / network err | `ProviderKeyRotationDenied` (Details: probe error code) |

---

## 13. FE Wire (RotateKeyModal refactor)

### 13.1 Component changes

`apps/web/src/components/admin/providers/RotateKeyModal.tsx`:

1. Rimuovere `beAvailable = false` (BE ora c'è)
2. Aggiungere stato modal aperto/chiuso (`useState`)
3. Form fields:
   - `<input type="password" name="newApiKey" required minLength={10} maxLength={512}>`
   - `<input type="text" name="confirmedProviderName" required>` (typed-confirm)
4. Mutation hook: `useRotateProviderKey(providerName)` via React Query
   - URL: `POST /api/v1/admin/providers/{name}/rotate-key`
   - Body: `{ newApiKey, confirmedProviderName }`
   - On success: invalidate `useProviders` query, toast success con fingerprint
   - On error: map subcode → user message:
     - `step_up_required` → "Verifica 2FA scaduta, ri-autentica"
     - `provider_probe_failed` → "La nuova key non funziona; verifica sul dashboard provider"
     - `rate_limit_exceeded` → "Rotation già effettuata nelle ultime 24h"
     - default → message generico

### 13.2 E2E test

`apps/web/e2e/admin-providers-rotate-key.spec.ts`:

- Test happy path: login superadmin + step-up TOTP + open modal + paste key + confirm + verify success toast
- Test 403: login admin (non superadmin) + verify button disabled
- Test 400: type wrong provider name confirm → error message

### 13.3 Step-up flow

Se la mutation ritorna 401 `step_up_required`, il client deve:
1. Mostrare modal step-up TOTP (component esistente `StepUpTwoFactorModal` da #1597)
2. Su success step-up, retry rotation automaticamente

Pattern già presente per altre azioni S3-gated.

---

## 14. Test Strategy

### 14.1 Unit tests (BC Administration)

`apps/api/tests/Api.Tests/BoundedContexts/Administration/RotateProviderKeyCommandHandlerTests.cs`:

- `Handle_NonSuperAdmin_ThrowsForbidden`
- `Handle_SelfProviderNameMismatch_ThrowsValidation` (via validator unit test)
- `Handle_RecentRotation_ThrowsConflict_409`
- `Handle_ProbeFailure_ThrowsProviderProbeFailed_502_OldKeyPreserved`
- `Handle_HappyPath_PersistsNewActive_DeactivatesOld_RaisesEvent`
- `Handle_FirstRotation_NoPreviousCredential_NullPreviousId`
- `Handle_EncryptedKeyNotPlaintext_AssertCiphertext`

`RotateProviderKeyCommandValidatorTests.cs`:
- All 6 validator rules covered (NotEmpty, Allowed, Equal, MinLen, MaxLen, NoWhitespace)

`ProviderCredentialResolverTests.cs`:
- `ResolveAsync_DbActiveExists_ReturnsDecrypted`
- `ResolveAsync_NoDbButEnvVar_ReturnsEnvValue`
- `ResolveAsync_NeitherConfigured_Throws`
- `Invalidate_RemovesFromCache`
- `Cache_HitsForRepeatedCalls_AndExpiresAfterTtl`

`ProviderName` + `KeyFingerprint` VO tests:
- Whitelist enforcement
- Fingerprint masking format (5..4)

### 14.2 Integration tests (Testcontainers Postgres)

`apps/api/tests/Api.Tests/Integration/Administration/RotateProviderKeyEndpointIntegrationTests.cs`:

- `Post_RotateKey_HappyPath_Returns200_PersistsRow_EmitsAudit`
- `Post_RotateKey_NoStepUp_Returns401_StepUpRequired`
- `Post_RotateKey_NonSuperAdmin_Returns403`
- `Post_RotateKey_ProviderNameMismatch_Returns400`
- `Post_RotateKey_InvalidProvider_Returns400`
- `Post_RotateKey_RateLimitWithin24h_Returns409`
- `Post_RotateKey_ProbeFailure_Returns502_NoRowPersisted_OldKeyActive`
- `Post_RotateKey_AuditOutboxContainsExpectedDetails`
- `Get_ProviderQuotaAfterRotation_UsesNewKey` (integration check del cache flush)

### 14.3 E2E (Playwright, FE)

`apps/web/e2e/admin-providers-rotate-key.spec.ts` (vedi §13.2)

### 14.4 Coverage target

- Backend: 90%+ su BC `Administration/Application/Commands/Providers/` + `Infrastructure/Services/ProviderCredentialResolver`
- Frontend: 85%+ su `RotateKeyModal.tsx`

---

## 15. Migration Plan + Rollback

### 15.1 Deploy sequence

1. **PR mergiato su `main-dev`** → CI verde, integration tests pass
2. **DB migration auto-applied** at API startup (`ApplyMigrationsAsync` standard)
3. **Backward compat**: nessun deployment esistente ha row in `provider_credentials` → resolver usa env-var fallback → comportamento identico al pre-#1859
4. **Admin first rotation**: superadmin chiama l'endpoint via UI → primo row inserito → `IsActive=true` → da quel punto resolver usa DB → cache flush istantaneo

### 15.2 Rollback procedure

Se necessario rimuovere features post-deploy:

```sql
-- Disable all DB-backed creds; resolver torna a env-var
UPDATE provider_credentials SET is_active = false;
```

Per disabilitare completamente la feature (in caso di vuln):
- Rimuovere endpoint dal `AdminProviderEndpoints` (commit revert)
- DB migration rollback: `DROP TABLE provider_credentials;` (Down method)

### 15.3 Observability

- **Metric Prometheus** (net-new, da aggiungere a `MeepleAiMetrics`): `meepleai_provider_key_rotations_total{provider="…",result="success|denied|error"}` — controllato in implementation, fallback su log-only se non bloccante
- **Log structured event**: `ProviderKeyRotated` con providerName, fingerprint, actor (mask via `DataMasking.MaskEmail`), latency
- **Dashboard SP5 Admin AI** (#1722): add panel "Provider Key Rotations (last 30d)" — **out-of-scope** per questa wave, follow-up issue se richiesto

---

## 16. Open Questions

| ID | Question | Decision | Owner |
|---|---|---|---|
| **OQ-1** | `AuditLoggingBehavior` popola automaticamente `Details` con il command shape JSON, o serve custom build dei details nel handler? | Verificare in implementation phase. Se automatic, vediamo se il shape generato include i fingerprint senza esporli plaintext. Fallback: manual `Details = JsonSerializer.Serialize(...)` nel handler. | Implementer |
| **OQ-2** | Cache TTL 5min ragionevole? | Default 5min. Tunable via config se necessario. | Implementer (configurable via `appsettings.json` key `AI:Providers:CredentialCacheTtlMinutes`) |
| **OQ-3** | Multi-instance / horizontal scaling cache invalidation cross-pod? | Per ora Singolo-pod assumption (dev/staging). Multi-pod richiede Redis pub/sub o database polling. Doc come known limitation. Per ora invalidation locale + 5min TTL = max 5min staleness in multi-pod. | Implementer (docs only) |
| **OQ-4** | Test integration richiede mock provider o real provider call? | Mock provider HttpClient con `MockHttpMessageHandler`. Probe restituisce success/failure controllata. | Implementer |
| **OQ-5** | Should the endpoint emit a Slack/email notification a SecOps? | Out of scope per #1859. Audit row sufficiente; notification è UserNotifications BC follow-up. | Punt to follow-up |

---

## 17. References

- Issue: [#1859](https://github.com/meepleAi-app/meepleai-monorepo/issues/1859)
- Spec source: /sc:spec-panel review for #1834 (2026-06-03)
- Predecessor: #1834 (FE re-skin Providers, MERGED) — `RotateKeyModal` pre-built disabled
- Security infra: #1597 (S3 strict 2FA cutover, MERGED) — `TwoFactorEnforcementBehavior`, `RequireTwoFactor`, `LastTotpVerifiedAt`
- Provider probe infra: #936 (G1+G3) — `ProviderProbeExecutor`, `AdminProviderEndpoints`
- Audit infra: #1534 (audit_outbox consolidation), S1 audit columns
- Mockup: `admin-mockups/design_handoff_admin/admin/sp5-admin-providers.html`

---

## 18. Effort Estimate

| Phase | Effort |
|---|---|
| Migration + EntityConfig + Repository | ~3h |
| Domain (aggregate + VOs + events) | ~2h |
| Application (Command + Validator + Handler) | ~5h |
| `IProviderCredentialResolver` + cache + provider client refactor (2 clients) | ~4h |
| `RequireTwoFactor.ForceStrict` extension + behavior change | ~1h |
| Rate-limit policy + endpoint | ~1h |
| Unit tests (40+ tests) | ~4h |
| Integration tests (10+ scenarios) | ~3h |
| FE wire (RotateKeyModal + mutation + E2E) | ~5h |
| Code review + adjustments + PR merge | ~2h |
| **Total** | **~30h** (~4 giornate-uomo) |

Spec originale stimava 10-14h: questa è la stima realistica post-discovery (DB-backed approach + provider client refactor non era stato considerato).

---

**Approvals**:
- [x] Architecture decisions D-1..D-5 (user 2026-06-05)
- [ ] Design doc review (user)
- [ ] Implementation plan (writing-plans skill, post-approval)
