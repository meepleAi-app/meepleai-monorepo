# Shared Game Translations — Backend Foundation + Admin Endpoints (sub-PR 1/3)

> **Status**: DESIGN APPROVED — 2026-06-15
> **Tracker issue**: [#2339](https://github.com/meepleAi-app/meepleai-monorepo/issues/2339)
> **Origin**: Q4 closure di PR [#2334](https://github.com/meepleAi-app/meepleai-monorepo/pull/2334) (seed KB coverage evaluation)
> **Sub-PR position**: 1 di 3 — BE foundation + admin endpoints. Sub-PR 2 = FE (`useGameTitle()` hook). Sub-PR 3 = seed data (translations IT curate).

## 1. Contesto

Durante l'allineamento del seed SP4 al snapshot DB (PR #2334) è emerso che:

- `shared_games.title` è una singola colonna varchar(500) — canonical EN.
- `OpenRouterTranslationService` esiste (`Api.Infrastructure.Translation.OpenRouterTranslationService`) ma è wired SOLO per traduzione delle risposte LLM in `AskQuestionQueryHandler`, NON per game titles del catalogo.
- Lo snapshot DB ha `Catan` (EN, con KB) e `I Coloni di Catan` (IT, senza KB) come due rows separate — sintomo della mancanza di un layer translation.

Questa spec implementa il foundation BE per supportare game titles localizzati senza duplicare le rows nel catalogo principale.

## 2. Decisioni locked (output brainstorming 2026-06-15)

| Q | Decisione utente | Implicazione design |
|---|---|---|
| Scope | BE foundation + admin endpoints (~3gg) | Skip FE hook + seed data (sub-PR follow-up) |
| DTO shape | "Both" (title canonical + translations array) | Niente ILocaleProvider middleware questa PR. Resolver legge solo translation, lascia title canonical |
| Wiring | Tutti 4 query handler | `GetAllGames` + `Search` + `GetNewGames` + `GetGameById` enrichano `Translations[]` |

## 3. Architettura

```
┌─────────────────────────────────────────────────────────────────────┐
│ BoundedContext: SharedGameCatalog                                   │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  Domain                                                             │
│  ├─ SharedGame (aggregate root, esistente)                          │
│  └─ SharedGameTranslation (NEW aggregate)                           │
│       └─ Value Object: Locale (ISO 639-1 validated)                 │
│                                                                     │
│  Application                                                        │
│  ├─ Queries (4 handler wired to enrich DTO)                         │
│  ├─ Commands (NEW: Add/Update/Delete translation)                   │
│  └─ Services                                                        │
│       └─ GameTitleResolver (batch fetch translations, enrich DTOs)  │
│                                                                     │
│  Infrastructure                                                     │
│  ├─ Entity: SharedGameTranslationEntity (EF Core)                   │
│  ├─ EntityConfiguration: tabella + indices + FK cascade              │
│  ├─ Repository: ISharedGameTranslationRepository                    │
│  └─ Migration: AddSharedGameTranslations                            │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

**Design rationale**:

- **Separate aggregate** invece di Owned Entity di SharedGame: admin endpoints operano su translation singola senza caricare full SharedGame (performance + scope chiaro).
- **Unique constraint DB-level** `UNIQUE (shared_game_id, locale) WHERE NOT is_deleted` per invariante "max 1 translation attiva per locale per game".
- **Batch fetch in resolver**: 1 query per N giochi (no N+1).
- **No locale resolution BE-side**: tutti i translations sempre inclusi nel DTO. FE follow-up sceglie quale mostrare via `useGameTitle()` hook.

**Trade-off**: payload più grande (4 translations × paginazione 10 = ~40 oggetti nested), ma elimina cache invalidation locale-aware e middleware ILocaleProvider per ora. Può arrivare in follow-up se decidiamo silent-overwrite mode in futuro.

## 4. Schema DB

**Tabella `shared_game_translations`** (snake_case allineato a `shared_games` parent):

```sql
CREATE TABLE shared_game_translations (
  id                 UUID         NOT NULL PRIMARY KEY,
  shared_game_id     UUID         NOT NULL REFERENCES shared_games(id) ON DELETE CASCADE,
  locale             VARCHAR(10)  NOT NULL,            -- ISO 639-1 ('it', 'en', 'es', 'fr', 'de', 'en-GB', ...)
  title              VARCHAR(500) NOT NULL,
  description        TEXT         NULL,
  source             VARCHAR(32)  NOT NULL DEFAULT 'manual',
                                                       -- 'manual' | 'auto-openrouter' | 'community'
  created_at         TIMESTAMPTZ  NOT NULL DEFAULT now(),
  created_by         UUID         NULL,
  updated_at         TIMESTAMPTZ  NULL,
  updated_by         UUID         NULL,
  is_deleted         BOOLEAN      NOT NULL DEFAULT false,
  deleted_at         TIMESTAMPTZ  NULL,
  deleted_by         UUID         NULL,
  -- Concurrency: xmin (Postgres system column, ADR-060 pattern — no schema work)

  CONSTRAINT uq_active_translation_per_locale
    UNIQUE NULLS NOT DISTINCT (shared_game_id, locale)
);

CREATE INDEX ix_translations_locale            ON shared_game_translations(locale)         WHERE NOT is_deleted;
CREATE INDEX ix_translations_shared_game_id    ON shared_game_translations(shared_game_id) WHERE NOT is_deleted;
CREATE INDEX ix_translations_source            ON shared_game_translations(source)         WHERE NOT is_deleted;
```

**Decisioni di campo**:

| Field | Decisione | Motivazione |
|---|---|---|
| `locale` VARCHAR(10) | string al livello DB, VO al livello Domain | Permette future estensioni regionali (`it-IT`, `en-GB`) senza migration |
| `description` nullable | Opzionale | Non tutti i giochi hanno descrizioni IT curate. MVP solo title |
| `source` VARCHAR(32) enum-as-string | EF Core EntityConfiguration: `.Property(t => t.Source).HasConversion<string>().HasMaxLength(32)` per round-trip C# enum ↔ DB string. Più estensibile di PG enum |
| `is_deleted` partial unique index | `WHERE NOT is_deleted` | Permette ri-creare una translation dopo soft-delete, no conflict |
| `xmin` concurrency | System column, no schema | Allineato ADR-060 + Issues #2305 / #2306 |

**Migration name**: `AddSharedGameTranslations` (EF Core convention, allinea a `BackfillSharedGameHasKnowledgeBaseFlag` pattern recente).

## 5. Domain Model

**Entity** `SharedGameTranslation` come aggregate root separato:

```csharp
namespace MeepleAi.Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

public sealed class SharedGameTranslation
{
    public Guid   Id              { get; private set; }
    public Guid   SharedGameId    { get; private set; }
    public Locale Locale          { get; private set; }
    public string Title           { get; private set; }
    public string? Description    { get; private set; }
    public TranslationSource Source { get; private set; }

    public DateTimeOffset CreatedAt   { get; private set; }
    public Guid?           CreatedBy  { get; private set; }
    public DateTimeOffset? UpdatedAt  { get; private set; }
    public Guid?           UpdatedBy  { get; private set; }

    public bool             IsDeleted { get; private set; }
    public DateTimeOffset?  DeletedAt { get; private set; }
    public Guid?            DeletedBy { get; private set; }

    public uint Xmin { get; private set; } // ConcurrencyToken via EntityConfiguration

    private SharedGameTranslation() { Title = null!; Locale = null!; }

    public static SharedGameTranslation Create(
        Guid sharedGameId,
        Locale locale,
        string title,
        string? description,
        TranslationSource source,
        Guid? createdBy,
        DateTimeOffset now)
    {
        if (sharedGameId == Guid.Empty)
            throw new ArgumentException("SharedGameId required", nameof(sharedGameId));
        if (locale.Equals(Locale.CanonicalEn))
            throw new InvalidLocaleException("Canonical EN title stored on shared_games.title, not translations");
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title required", nameof(title));
        if (title.Length > 500)
            throw new ArgumentException("Title max 500 chars", nameof(title));

        return new SharedGameTranslation
        {
            Id           = Guid.NewGuid(),
            SharedGameId = sharedGameId,
            Locale       = locale,
            Title        = title.Trim(),
            Description  = description?.Trim(),
            Source       = source,
            CreatedAt    = now,
            CreatedBy    = createdBy,
            IsDeleted    = false
        };
    }

    public void UpdateTitle(string newTitle, Guid? updatedBy, DateTimeOffset now)
    {
        if (IsDeleted) throw new InvalidOperationException("Cannot update deleted translation");
        if (string.IsNullOrWhiteSpace(newTitle)) throw new ArgumentException("Title required", nameof(newTitle));
        if (newTitle.Length > 500) throw new ArgumentException("Title max 500 chars", nameof(newTitle));

        Title     = newTitle.Trim();
        UpdatedAt = now;
        UpdatedBy = updatedBy;
    }

    public void UpdateDescription(string? newDescription, Guid? updatedBy, DateTimeOffset now)
    {
        if (IsDeleted) throw new InvalidOperationException("Cannot update deleted translation");
        Description = newDescription?.Trim();
        UpdatedAt   = now;
        UpdatedBy   = updatedBy;
    }

    public void SoftDelete(Guid? deletedBy, DateTimeOffset now)
    {
        if (IsDeleted) return; // idempotent
        IsDeleted = true;
        DeletedAt = now;
        DeletedBy = deletedBy;
    }

    public void Restore(Guid? restoredBy, DateTimeOffset now)
    {
        if (!IsDeleted) return;
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
        UpdatedAt = now;
        UpdatedBy = restoredBy;
    }
}
```

**Value Object** `Locale`:

```csharp
namespace MeepleAi.Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

public sealed record Locale
{
    private static readonly Regex IsoFormat = new(@"^[a-z]{2}(-[A-Z]{2})?$", RegexOptions.Compiled);

    public string Value { get; }

    private Locale(string value) { Value = value; }

    public static Locale Create(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidLocaleException("Locale cannot be empty");

        var normalized = raw.Trim().ToLowerInvariant();
        if (normalized.Length == 5)
            normalized = normalized[..3] + normalized[3..].ToUpperInvariant();

        if (!IsoFormat.IsMatch(normalized))
            throw new InvalidLocaleException($"Invalid ISO 639-1 locale: {raw}");

        return new Locale(normalized);
    }

    public static readonly Locale CanonicalEn = new("en");

    public override string ToString() => Value;
}
```

**Enum** `TranslationSource` (string-backed):

```csharp
public enum TranslationSource
{
    Manual,           // → "manual" (default, admin-curated)
    AutoOpenRouter,   // → "auto-openrouter" (auto-generated via translation service)
    Community         // → "community" (community-sourced, future)
}
```

**Domain exceptions** (in `Application/Exceptions/`):

```csharp
public sealed class InvalidLocaleException(string msg) : Exception(msg);
public sealed class TranslationNotFoundException(Guid gameId, string locale)
    : NotFoundException($"Translation for game {gameId} locale {locale} not found");
public sealed class TranslationAlreadyExistsException(Guid gameId, string locale)
    : ConflictException($"Translation for game {gameId} locale {locale} already exists");
```

**Note**: nessun domain event in scope MVP. Possono arrivare in follow-up se serve audit trail (`TranslationCuratedEvent`).

## 6. Application Layer

### 6.1 DTO contract

```csharp
public record SharedGameDto(
    Guid Id,
    string Title,              // canonical EN (UNCHANGED)
    string? Description,
    string? Publisher,
    int YearPublished,
    int MinPlayers,
    int MaxPlayers,
    int PlayingTimeMinutes,
    decimal? ComplexityRating,
    decimal? AverageRating,
    string? ImageUrl,
    string? ThumbnailUrl,
    bool HasKnowledgeBase,
    // ... altri campi esistenti ...
    IReadOnlyList<SharedGameTranslationDto> Translations  // NEW (sempre popolato, default empty)
);

public record SharedGameTranslationDto(
    string Locale,             // "it", "en-GB", ecc.
    string Title,
    string? Description,
    string Source              // "manual" | "auto-openrouter" | "community"
);
```

### 6.2 Service `GameTitleResolver`

```csharp
public interface IGameTitleResolver
{
    Task<IReadOnlyList<SharedGameDto>> EnrichAsync(
        IReadOnlyList<SharedGameDto> games,
        CancellationToken ct);
}

public sealed class GameTitleResolver(ISharedGameTranslationRepository repo) : IGameTitleResolver
{
    public async Task<IReadOnlyList<SharedGameDto>> EnrichAsync(
        IReadOnlyList<SharedGameDto> games,
        CancellationToken ct)
    {
        if (games.Count == 0) return games;

        var ids = games.Select(g => g.Id).ToArray();
        // Repository SHALL filter `is_deleted = false` internally.
        // GetByGameIdsAsync returns only active translations.
        var translations = await repo.GetByGameIdsAsync(ids, ct);
        // → Dictionary<Guid, List<SharedGameTranslationDto>>

        return games
            .Select(g => g with
            {
                Translations = translations.TryGetValue(g.Id, out var t) ? t : []
            })
            .ToList();
    }
}
```

**Important**: resolver e admin GET endpoints SHALL escludere translations soft-deleted dal response. La query EF deve usare `.Where(t => !t.IsDeleted)` o un global query filter sull'entity. Admin endpoint per riaccedere a soft-deleted è OUT OF SCOPE — non implementato in questa PR.

DI: `builder.Services.AddScoped<IGameTitleResolver, GameTitleResolver>();`

### 6.3 Wire nei 4 query handler esistenti

Pattern identico per `GetAllGames`, `Search`, `GetNewGames`, `GetGameById`:

```csharp
public sealed class GetAllGamesQueryHandler(
    ISharedGameRepository sharedGameRepo,
    IGameTitleResolver titleResolver)
    : IRequestHandler<GetAllGamesQuery, IReadOnlyList<SharedGameDto>>
{
    public async Task<IReadOnlyList<SharedGameDto>> Handle(GetAllGamesQuery request, CancellationToken ct)
    {
        var games = await sharedGameRepo.GetAllAsync(...);     // existing
        var dtos  = games.Select(g => g.ToDto()).ToList();     // existing mapping
        return await titleResolver.EnrichAsync(dtos, ct);      // NEW
    }
}
```

`GetGameByIdQueryHandler` wrappa singolo gioco in lista di 1, enrich, unwrap.

### 6.4 Commands

```csharp
public record AddGameTranslationCommand(
    Guid GameId,
    string Locale,
    string Title,
    string? Description,
    string Source
) : IRequest<Guid>;            // returns translation ID

public record UpdateGameTranslationCommand(
    Guid GameId,
    string Locale,
    string Title,
    string? Description,
    uint Xmin                  // optimistic concurrency
) : IRequest<Unit>;

public record DeleteGameTranslationCommand(
    Guid GameId,
    string Locale,
    uint Xmin
) : IRequest<Unit>;
```

**Validators** (FluentValidation):
- `AddGameTranslationCommandValidator`: Locale.Create() valid + GameId not empty + Title 1-500 chars + Source ∈ enum + GameId exists (async DB check) + no duplicate active translation per locale (async DB check).
- `UpdateGameTranslationCommandValidator`: stesso + translation esiste + xmin != 0.
- `DeleteGameTranslationCommandValidator`: GameId + Locale + Xmin not zero.

**Exception → HTTP mapping** (esistente middleware):
- `InvalidLocaleException` → 400 BadRequest
- `TranslationNotFoundException` → 404 NotFound
- `TranslationAlreadyExistsException` → 409 Conflict
- `DbUpdateConcurrencyException` → 409 Conflict con `X-Warning-Code: concurrent-edit`

### 6.5 Queries (read-side, admin-facing)

```csharp
public record GetGameTranslationsQuery(Guid GameId) : IRequest<IReadOnlyList<SharedGameTranslationDto>>;

public record GetGameTranslationByLocaleQuery(Guid GameId, string Locale)
    : IRequest<SharedGameTranslationDetailDto>;

public record SharedGameTranslationDetailDto(
    Guid Id, string Locale, string Title, string? Description, string Source,
    DateTimeOffset CreatedAt, Guid? CreatedBy,
    DateTimeOffset? UpdatedAt, Guid? UpdatedBy,
    uint Xmin // include xmin for client to use on PUT/DELETE
);
```

## 7. Admin Endpoints

Base path: `/api/v1/admin/games/{gameId}/translations`. Tutti `RequireAuthorization("AdminOnly")` (policy esistente).

| Method | Route | Body | Response | Codes |
|---|---|---|---|---|
| **POST** | `/api/v1/admin/games/{gameId}/translations` | `{ locale, title, description?, source }` | `{ id, locale, title, description, source }` | 201 / 400 / 404 (game) / 409 (duplicate) |
| **GET** | `/api/v1/admin/games/{gameId}/translations` | — | `[{ locale, title, description, source, createdAt, ... }]` | 200 / 404 (game) |
| **GET** | `/api/v1/admin/games/{gameId}/translations/{locale}` | — | `{ ..., xmin }` | 200 / 404 |
| **PUT** | `/api/v1/admin/games/{gameId}/translations/{locale}` | `{ title, description?, xmin }` | `{ ..., source }` | 200 / 400 / 404 / 409 |
| **DELETE** | `/api/v1/admin/games/{gameId}/translations/{locale}` | header `If-Match: <xmin>` o body `{ xmin }` | — | 204 / 404 / 409 |

**Routing registration** (CQRS rule per CLAUDE.md: solo `IMediator.Send()`):

```csharp
public static class SharedGameTranslationEndpoints
{
    public static IEndpointRouteBuilder MapSharedGameTranslationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/games/{gameId:guid}/translations")
                       .RequireAuthorization("AdminOnly")
                       .WithTags("Admin: Game Translations");

        group.MapPost("/", async (Guid gameId, AddTranslationRequest body, IMediator m) =>
        {
            var id = await m.Send(new AddGameTranslationCommand(
                gameId, body.Locale, body.Title, body.Description, body.Source));
            return Results.Created($"/api/v1/admin/games/{gameId}/translations/{body.Locale}", new { id });
        });

        group.MapGet("/", async (Guid gameId, IMediator m) =>
            Results.Ok(await m.Send(new GetGameTranslationsQuery(gameId))));

        group.MapGet("/{locale}", async (Guid gameId, string locale, IMediator m) =>
            Results.Ok(await m.Send(new GetGameTranslationByLocaleQuery(gameId, locale))));

        group.MapPut("/{locale}", async (Guid gameId, string locale, UpdateTranslationRequest body, IMediator m) =>
        {
            await m.Send(new UpdateGameTranslationCommand(
                gameId, locale, body.Title, body.Description, body.Xmin));
            return Results.Ok();
        });

        group.MapDelete("/{locale}", async (Guid gameId, string locale, DeleteTranslationRequest body, IMediator m) =>
        {
            await m.Send(new DeleteGameTranslationCommand(gameId, locale, body.Xmin));
            return Results.NoContent();
        });

        return app;
    }
}
```

## 8. Testing Strategy

**Coverage target**: ≥85% sui nuovi componenti (per CLAUDE.md backend target 90%+ stretch).

| Layer | Test type | Cosa | Esempi chiave |
|---|---|---|---|
| **Domain** | Unit (xUnit) | Entity factory invariants + VO validation | `Locale.Create("xx")` throws · `Locale.Create("en")` factory equals `CanonicalEn` · `Create(canonical en locale)` throws · `Create(null title)` throws · `SoftDelete()` idempotent · `Restore()` lifecycle |
| **Application — Validators** | Unit | FluentValidation rule per command | `AddGameTranslationCommandValidator`: invalid locale + empty title + duplicate active per locale (async mock) + non-existent gameId |
| **Application — Handlers** | Unit (mocked repo) | Command handler flow | Happy path + GameNotFound + Conflict on duplicate + concurrent edit (`DbUpdateConcurrencyException` → 409) + Update on soft-deleted throws InvalidOperation |
| **Application — Resolver** | Unit (mocked repo) | `GameTitleResolver.EnrichAsync` | Batch fetch single SQL call (asserted via mock) · empty list returns empty · no translations → empty array on DTO · multiple locales per game grouped correctly · soft-deleted translations excluded |
| **Infrastructure — Repository** | Integration (Testcontainers Postgres) | EF Core queries + soft-delete filter + xmin | `AddAsync` + `GetByGameIdsAsync` (batch, only `is_deleted=false`) + `GetByGameIdAndLocaleAsync` + `Update` con xmin concurrency · unique constraint enforce via DB exception su duplicate (active) · partial unique index permette ri-create dopo soft-delete |
| **Endpoints** | Integration (Testcontainers + WebApplicationFactory) | Full HTTP roundtrip | POST 201 + 409 duplicate · GET 200 + 404 · PUT 200 + 409 concurrent · DELETE 204 + 404 · 403 Forbidden non-admin · validation 400 (empty title, invalid locale) |
| **Query handler wiring** | Integration | I 4 query handler restituiscono Translations[] | Seed game + translation, chiama `GET /catalog/games/new`, verifica response include translation IT |

**Test file layout** (allineato CLAUDE.md):

```
tests/Api.Tests/
├── Unit/SharedGameCatalog/
│   ├── Domain/SharedGameTranslationTests.cs
│   ├── Domain/LocaleTests.cs
│   ├── Application/AddGameTranslationCommandValidatorTests.cs
│   ├── Application/AddGameTranslationCommandHandlerTests.cs
│   ├── Application/UpdateGameTranslationCommandHandlerTests.cs
│   ├── Application/DeleteGameTranslationCommandHandlerTests.cs
│   └── Application/GameTitleResolverTests.cs
└── Integration/SharedGameCatalog/
    ├── SharedGameTranslationRepositoryIntegrationTests.cs
    ├── SharedGameTranslationEndpointsIntegrationTests.cs
    └── GameTitleResolverWiringIntegrationTests.cs   ← verifica 4 query handler enrich
```

**Trait categorizzazione** (per CLAUDE.md `--filter` patterns):
- `[Trait("Category", "Unit")]` o `[Trait("Category", "Integration")]`
- `[Trait("BoundedContext", "SharedGameCatalog")]`

## 9. Acceptance Criteria

- [ ] Migration EF `AddSharedGameTranslations` applicata su dev DB con successo (no warnings)
- [ ] Entity `SharedGameTranslation` + VO `Locale` + enum `TranslationSource` + 3 exceptions shipped
- [ ] `ISharedGameTranslationRepository` implementato con: `AddAsync`, `UpdateAsync`, `SoftDeleteAsync`, `GetByGameIdAsync`, `GetByGameIdAndLocaleAsync`, `GetByGameIdsAsync` (batch)
- [ ] `IGameTitleResolver` implementato + DI wire'd
- [ ] 4 query handler enrichano `Translations[]` (verificato via integration test)
- [ ] 5 admin endpoints registered + `RequireAuthorization("AdminOnly")` + smoke 200/4xx codes verified
- [ ] 3 commands + validators + handlers + exception mapping verified
- [ ] Unit tests ≥85% coverage sui nuovi componenti (verify via `dotnet test /p:CollectCoverage=true`)
- [ ] Integration tests con Testcontainers Postgres passano locale (no skipped)
- [ ] CodeQL + lint pulito
- [ ] Issue #2339 aggiornata con sub-PR 1/3 closure note + scope rimanente per sub-PR 2 e 3

## 10. Out of Scope (esplicito non-goals)

- ❌ FE hook `useGameTitle()` (sub-PR 2/3)
- ❌ FE DTO TypeScript update + grep+replace di consumer
- ❌ `ILocaleProvider` middleware Accept-Language parser (mai necessario dato "Both" DTO shape)
- ❌ Seed translations IT per 13 giochi SP4 (sub-PR 3/3)
- ❌ Bulk admin operations (batch POST/PUT)
- ❌ Translation history / contributor tracking dettagliato
- ❌ Auto-translation via `OpenRouterTranslationService` invoke
- ❌ Locale chain fallback (`it-IT` → `it` → `en`) — non necessario con "Both" shape
- ❌ Domain events (`TranslationAddedEvent` ecc.)
- ❌ Cache distributed dei translations (single-server Redis per ora)
- ❌ Endpoint amministrativo per riaccedere a translation soft-deleted (Restore admin endpoint) — soft-delete è terminale dal punto di vista API in questa PR. Per ri-aggiungere translation di una locale rimossa, POST crea nuovo record (partial unique index lo permette).

## 11. Effort Breakdown

| Componente | Effort |
|---|---|
| Migration + Entity + EntityConfiguration | 0.3gg |
| Domain entity + VO + exceptions | 0.3gg |
| Repository + integration tests | 0.5gg |
| Commands + Validators + Handlers + unit tests | 0.8gg |
| `GameTitleResolver` + unit tests | 0.4gg |
| Wire 4 query handlers + integration tests | 0.4gg |
| 5 Admin endpoints + integration tests | 0.5gg |
| Spec doc update + PR + final review | 0.3gg |
| **Totale** | **~3.5gg** (entro target ~3gg, leggero overrun) |

## 12. References

- Issue tracker: [#2339](https://github.com/meepleAi-app/meepleai-monorepo/issues/2339)
- PR origine Q4 closure: [#2334](https://github.com/meepleAi-app/meepleai-monorepo/pull/2334)
- Seed KB coverage spec: `docs/for-developers/specs/2026-06-14-seed-kb-coverage-evaluation.md` §9 Q4
- Translation service existing: `apps/api/src/Api/Infrastructure/Translation/OpenRouterTranslationService.cs`
- Current SharedGame DTO: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/SharedGameDto.cs`
- Frontend i18n setup: `apps/web/src/lib/i18n/ssr.ts` + `apps/web/src/locales/it.json`
- ADR-060 (xmin concurrency pattern): `docs/for-claude/architecture/adr/adr-060-live-session-persistence.md`
- BGG ban / ADR-059: `docs/for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md`

## 13. Sub-PR pipeline

| # | Sub-PR | Scope | Effort | Dependency |
|---|---|---|---|---|
| 1 | **This PR** | BE foundation + admin endpoints | ~3.5gg | — |
| 2 | FE hook + DTO + grep+replace | useGameTitle() + TS types + UI consumers | ~1gg | depends on (1) merged |
| 3 | Seed translations IT | data.json gameTranslations array + 45-translations.sh + 13 IT curate | ~0.5gg | depends on (1) merged |

**Issue #2339 closure**: solo dopo merge di sub-PR 1, 2, 3 (tutti shipped + verified).
