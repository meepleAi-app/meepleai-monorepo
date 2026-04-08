# PR #267 Follow-up Bug Fixes — Design Spec

**Date**: 2026-04-08
**Branch**: `fix/pr-267-followup-bugs` from `main-dev`
**PR target**: `main-dev`
**Author**: Claude (brainstorming session with user)

## Summary

Fix 4 bugs di codice + 1 setup/documentation bug rilevati durante test manuale post-PR #267 (`feat(rulebook): bulk import automation for 19 board games`). I bug sono indipendenti dal merge ma emersi nel corso dello stesso test. Tutto il lavoro è in un'unica PR per mantenere il context di review coeso.

## Scope

| # | Bug | Severity | BC | Out-of-scope? |
|---|-----|----------|----|----|
| 1 | `StartGameNightSessionCommandHandler` passa lista participant vuota | Medium | GameManagement | No |
| 2 | `GameNightEventRepository.MapToDomain` usa `Enum.Parse` non sicuro (3 occorrenze) | Medium | GameManagement | No |
| 3 | Colonne `search_vector` mancanti dalla InitialCreate migration | Low (env) | Infrastructure | No |
| 4 | Retrieval PDF per shared game usa `pdf_documents.SharedGameId` invece di JOIN tramite `games.SharedGameId` | Medium | DocumentProcessing | No |
| 5 | Model Ollama `llama3:8b` non installato → RAG risposta vuota silenziosa | Low (env) | Setup + KnowledgeBase | No |

## Branch & Git Strategy

```bash
git checkout main-dev && git pull
git checkout -b fix/pr-267-followup-bugs
git config branch.fix/pr-267-followup-bugs.parent main-dev
```

Un commit per bug (6 commits totali, incluso docs):
1. `test: regression tests for PR #267 follow-up bugs` — tutti i test falliti, TDD red state
2. `fix(game-management): auto-seed organizer participant in StartGameNightSessionCommandHandler`
3. `fix(game-management): safe enum parsing with Corrupted fallback in GameNightEventRepository`
4. `fix(infra): add search_vector tsvector columns migration`
5. `fix(document-processing): correct PDF retrieval queries for shared games`
6. `fix(knowledge-base): surface missing-model errors in Ollama client + setup docs`

## Fix 1 — Empty Participants in StartGameNightSessionCommandHandler

### Root cause
`StartGameNightSessionCommandHandler.cs:54` dispaccia `CreateSessionCommand` con `new List<ParticipantDto>()`. Il `CreateSessionCommandValidator` richiede:
- `.NotEmpty()` — almeno 1 participant
- `.Must(p => p.Any(participant => participant.IsOwner))` — almeno 1 owner
- `DisplayName` non vuoto, ≤50 chars

Il cross-BC dispatch fallisce validazione → `ValidationException`.

### Fix (approccio D: combo opzionale + auto-seed)

**Step 1** — Estendere il command:
```csharp
internal record StartGameNightSessionCommand(
    Guid GameNightId,
    Guid GameId,
    string GameTitle,
    Guid UserId,
    IReadOnlyList<ParticipantDto>? Participants = null
) : ICommand<StartGameNightSessionResult>;
```

**Step 2** — Handler: auto-seed dall'organizer se `Participants` è null/empty. Nuovo field DI: `IUserRepository` (o service equivalente con lookup by ID).

```csharp
var participants = command.Participants?.ToList() ?? new List<ParticipantDto>();
if (participants.Count == 0)
{
    var organizer = await _userRepository.GetByIdAsync(command.UserId, ct).ConfigureAwait(false)
        ?? throw new NotFoundException("User", command.UserId.ToString());

    var displayName = !string.IsNullOrWhiteSpace(organizer.DisplayName)
        ? organizer.DisplayName
        : organizer.Email;

    participants.Add(new ParticipantDto
    {
        Id = Guid.NewGuid(),
        UserId = command.UserId,
        DisplayName = displayName,
        IsOwner = true,
        JoinOrder = 0
    });
}

// Safety runtime check in caso di participants custom senza owner
if (!participants.Any(p => p.IsOwner))
    throw new ConflictException("At least one participant must be the session owner.");

var createResult = await _mediator.Send(new CreateSessionCommand(
    command.UserId,
    command.GameId,
    "GameSpecific",
    DateTime.UtcNow,
    null,
    participants), cancellationToken).ConfigureAwait(false);
```

**Step 3** — User lookup cross-BC: `GameManagement` non può dipendere direttamente da `IUserRepository` (Authentication BC). Tre opzioni, in ordine di preferenza:
  1. **(Preferito)** Dispatch MediatR di `Authentication.GetUserByIdQuery` — la query è `internal` ma nel medesimo assembly, quindi accessibile. Zero cross-BC coupling a livello architetturale perché il dispatch è type-erased via `IMediator`.
  2. **Fallback A** — Se per qualche ragione la query non è usabile, creare un nuovo `IUserLookupService` nel SharedKernel con metodo `GetDisplayNameAsync(Guid userId)` che ritorna `{DisplayName, Email}` e implementarlo in Authentication infrastructure.
  3. **Fallback B** — Iniettare `IUserRepository` direttamente (accetta il cross-BC coupling, stesso pattern usato già altrove nel codebase se esiste — verificare in plan phase).

La plan phase sceglie l'opzione 1 di default, con fallback a 2 se il dispatch MediatR internal causa problemi a build o test.

**Step 4** — Route endpoint: `POST /api/v1/game-nights/{id}/sessions/start` non cambia. Backwards compatible (participants opzionale, default null).

### Tests
- **Unit** `StartGameNightSessionCommandHandlerTests`:
  - `Handle_WithNullParticipants_AutoSeedsOrganizer`
  - `Handle_WithEmptyParticipants_AutoSeedsOrganizer`
  - `Handle_WithProvidedParticipants_UsesThemDirectly`
  - `Handle_WithProvidedParticipantsLackingOwner_ThrowsConflict`
  - `Handle_OrganizerNotFound_ThrowsNotFound`
- **Integration** `GameNightSessionsEndpointTests.POST_start_creates_session_with_auto_seeded_organizer` (Testcontainers)

## Fix 2 — Unsafe Enum.Parse in GameNightEventRepository

### Root cause
Tre occorrenze di `Enum.Parse` senza `TryParse` in `GameNightEventRepository.cs:MapToDomain`:
- Line 182: `Enum.Parse<GameNightStatus>(entity.Status)`
- Line 216: `Enum.Parse<RsvpStatus>(r.Status)`
- Line 234: `Enum.Parse<GameNightSessionStatus>(s.Status)`

Lo status è persistito come string (convertito via `.ToString()` in `MapToPersistence`). Se il DB contiene un valore legacy, un typo, o un valore rimosso da una futura versione dell'enum → `ArgumentException` → 500.

### Fix — Add `Corrupted` quarantine state to each enum

**Step 1** — Estendere ciascun enum con valore `Corrupted` (numericamente alto, non-conflittuale):

```csharp
// GameNightStatus.cs
public enum GameNightStatus
{
    Draft = 0,
    Published = 1,
    Cancelled = 2,
    Completed = 3,
    Corrupted = 999
}

// RsvpStatus.cs
public enum RsvpStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2,
    Maybe = 3,
    Corrupted = 999
}

// GameNightSessionStatus.cs
public enum GameNightSessionStatus
{
    Pending = 0,
    InProgress = 1,
    Completed = 2,
    Skipped = 3,
    Corrupted = 999
}
```

**Step 2** — Helper privato in `GameNightEventRepository`:

```csharp
private TEnum ParseEnumSafe<TEnum>(
    string rawValue,
    TEnum corruptedFallback,
    string entityId,
    string fieldName) where TEnum : struct, Enum
{
    if (Enum.TryParse<TEnum>(rawValue, ignoreCase: false, out var parsed)
        && Enum.IsDefined(typeof(TEnum), parsed))
    {
        return parsed;
    }

    _logger.LogError(
        "Corrupted {EnumType} value '{RawValue}' for entity {EntityId}.{FieldName}. Mapped to {Fallback}.",
        typeof(TEnum).Name, rawValue, entityId, fieldName, corruptedFallback);

    return corruptedFallback;
}
```

**Step 3** — Sostituire le 3 `Enum.Parse` con chiamate a `ParseEnumSafe`:
```csharp
var status = ParseEnumSafe(entity.Status, GameNightStatus.Corrupted, entity.Id.ToString(), nameof(entity.Status));
// ...
status: ParseEnumSafe(r.Status, RsvpStatus.Corrupted, r.Id.ToString(), nameof(r.Status)),
// ...
status: ParseEnumSafe(s.Status, GameNightSessionStatus.Corrupted, s.Id.ToString(), nameof(s.Status)),
```

**Step 4** — Iniettare `ILogger<GameNightEventRepository>` nel costruttore. Update DI.

**Step 5** — Domain invariants: verificare che le transizioni di stato in `GameNightEvent` (es. `Publish()`, `Cancel()`, `Complete()`) **rifiutino** l'input se `Status == Corrupted`. Il dominio resta read-only per entità corrotte:
```csharp
public void Publish()
{
    if (Status == GameNightStatus.Corrupted)
        throw new InvalidOperationException("Cannot operate on corrupted game night. Manual intervention required.");
    // ... existing logic
}
```

**Step 6** — Query filters: i use case che filtrano per status attivi (es. `GetUpcomingAsync` filtra per `Published`) già escludono naturalmente `Corrupted`. Verificare che nessun endpoint esponga entità corrotte a utenti finali — se sì, filtrarle nella repo o aggiungere un admin endpoint dedicato per diagnosi.

### Tests
- **Unit** `GameNightEventRepositoryTests`:
  - `MapToDomain_WithValidStatus_ParsesCorrectly` (regression)
  - `MapToDomain_WithInvalidGameNightStatus_FallsBackToCorrupted_AndLogs`
  - `MapToDomain_WithInvalidRsvpStatus_FallsBackToCorrupted_AndLogs`
  - `MapToDomain_WithInvalidSessionStatus_FallsBackToCorrupted_AndLogs`
- **Unit** `GameNightEventTests`:
  - `Publish_WhenCorrupted_ThrowsInvalidOperationException`
  - `Cancel_WhenCorrupted_ThrowsInvalidOperationException`

## Fix 3 — Missing `search_vector` tsvector columns migration

### Root cause
Entity config fa `builder.Ignore(e => e.SearchVector)` con commento "managed by trigger" ma:
- `20260403142547_InitialCreate` non crea né la colonna né il trigger
- Legacy `infra/init/api-migrations-20251118.sql:388,571` le creava come `text` (tipo sbagliato — il codice usa operatore `@@` che richiede `tsvector`)
- `TextChunkSearchService.cs:45` esegue `tc.search_vector @@ plainto_tsquery('english', ...)` → rotto su ogni fresh DB
- Il pattern corretto esiste già in `PgVectorStoreAdapter.cs:375`: `GENERATED ALWAYS AS (to_tsvector('english', text_content)) STORED`

### Fix — Nuova EF migration `AddSearchVectorColumns`

```bash
cd apps/api/src/Api
dotnet ef migrations add AddSearchVectorColumns
```

Contenuto `Up()`:
```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // text_chunks.search_vector: generated from Content column
    migrationBuilder.Sql("""
        ALTER TABLE text_chunks
        ADD COLUMN IF NOT EXISTS search_vector tsvector
        GENERATED ALWAYS AS (to_tsvector('english', "Content")) STORED;

        CREATE INDEX IF NOT EXISTS idx_text_chunks_search_vector
        ON text_chunks USING gin (search_vector);
    """);

    // pdf_documents.search_vector: generated from ExtractedText + FileName
    migrationBuilder.Sql("""
        ALTER TABLE pdf_documents
        ADD COLUMN IF NOT EXISTS search_vector tsvector
        GENERATED ALWAYS AS (
            to_tsvector('english',
                coalesce("ExtractedText", '') || ' ' || coalesce("FileName", ''))
        ) STORED;

        CREATE INDEX IF NOT EXISTS idx_pdf_documents_search_vector
        ON pdf_documents USING gin (search_vector);
    """);
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql("DROP INDEX IF EXISTS idx_text_chunks_search_vector;");
    migrationBuilder.Sql("ALTER TABLE text_chunks DROP COLUMN IF EXISTS search_vector;");
    migrationBuilder.Sql("DROP INDEX IF EXISTS idx_pdf_documents_search_vector;");
    migrationBuilder.Sql("ALTER TABLE pdf_documents DROP COLUMN IF EXISTS search_vector;");
}
```

### Idempotency
`IF NOT EXISTS` su colonne e indici → safe per DB dove il fix manuale è già stato applicato (locale, staging, prod se mai applicato).

### Entity config update
Aggiornare i commenti in `TextChunkEntityConfiguration.cs:24-26` e `PdfDocumentEntityConfiguration.cs:62-64`:
```csharp
// AI-14: Hybrid search - PostgreSQL GENERATED stored tsvector column
// Computed from Content / (ExtractedText + FileName), ignored by EF Core since it's DB-managed
builder.Ignore(e => e.SearchVector);
```

### Tests
- **Integration** `HybridSearchSchemaTests`:
  - `TextChunks_SearchVectorColumn_IsPopulatedFromContent` — insert chunk, query `search_vector IS NOT NULL`
  - `PdfDocuments_SearchVectorColumn_IsPopulatedFromExtractedText` — insert pdf doc, same check
  - `SearchVectorIndex_ExistsOnTextChunks` — query `pg_indexes`
- **Integration** `TextChunkSearchServiceTests.SearchAsync_AfterMigration_ReturnsMatchingChunks` — full hybrid search flow

## Fix 4 — PDF retrieval for shared games (option C)

### Root cause (approccio C)
Query lato retrieval usano `WHERE pdf_documents.SharedGameId = @sharedId`, ma il flusso di upload popola `pdf_documents.GameId` (tramite `games.SharedGameId = @shared`), lasciando `pdf_documents.SharedGameId = null` per i documenti caricati via catalogo condiviso. Il link corretto è attraverso la tabella `games`.

La write-side è **corretta** e non va toccata. Il workaround SQL manuale applicato dall'utente era necessario perché la query di lettura non traversava il join.

### Phase 0 — Audit

Prima di scrivere codice, eseguire audit sistematico:

```bash
grep -rn "PdfDocuments.*SharedGameId" apps/api/src/Api --include="*.cs"
grep -rn "pdf_documents.*SharedGameId" apps/api/src/Api --include="*.cs"
grep -rn "\.SharedGameId ==" apps/api/src/Api/BoundedContexts/DocumentProcessing --include="*.cs"
grep -rn "\.SharedGameId ==" apps/api/src/Api/BoundedContexts/KnowledgeBase --include="*.cs"
```

Per ogni occorrenza, classificare:
- **Write** (`pdfDoc.SharedGameId = ...`) → OK, lasciare
- **Read** (`Where(p => p.SharedGameId == sharedId)`) → **candidato fix**

Il plan conterrà la lista completa dei call site individuati nell'audit.

### Fix pattern per ciascun sito rotto

**Prima**:
```csharp
var pdfs = _db.PdfDocuments
    .Where(p => p.SharedGameId == sharedGameId)
    .ToList();
```

**Dopo** (JOIN via games.SharedGameId):
```csharp
var pdfs = _db.PdfDocuments
    .Where(p => _db.Games.Any(g => g.Id == p.GameId && g.SharedGameId == sharedGameId))
    .ToList();
```

### Bonus — Determinismo in `FindOrCreateGameAsync`
`UploadPdfCommandHandler.cs:478` fa `Where(g => g.Id == parsedGameId || g.SharedGameId == parsedGameId).FirstOrDefaultAsync()`. Con più `games` rows che condividono lo stesso `SharedGameId` l'ordine non è deterministico. Fix: splittare in due query con preferenza ordinata.

```csharp
// Step 1: direct games.Id match (legacy / already-resolved path)
var existingGame = await _db.Games
    .FirstOrDefaultAsync(g => g.Id == parsedGameId, cancellationToken).ConfigureAwait(false);

if (existingGame == null)
{
    // Step 2: shared catalog reference — pick the deterministically oldest games row
    existingGame = await _db.Games
        .Where(g => g.SharedGameId == parsedGameId)
        .OrderBy(g => g.CreatedAt)
        .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
}

if (existingGame == null)
{
    // Step 3: auto-create from shared catalog (existing code unchanged)
    // ... lines 483-517 invariato ...
}

return (true, null, existingGame.Id);
```

### Tests
- **Integration** `UploadPdfCommandHandlerTests.Upload_WithSharedGameId_Deterministic_UsesOldestGamesRow` — seed 2 games con stesso SharedGameId, verifica CreatedAt-based ordering
- **Integration** per ogni call site corretto: regression test specifico che valida retrieval tramite JOIN (seed shared_game + games + pdf_documents)

## Fix 5 — Ollama missing model + setup docs

### Root cause
- `llama3:8b` non è pullato di default in dev. Test RAG restituisce risposta vuota senza errore visibile.
- `OllamaLlmClient.cs:165-166` logga l'errore HTTP ma il consumer (pipeline RAG) potrebbe swallow il failure e ritornare empty

### Fix — Parte 1: Backend defensive handling

**Step 1** — `OllamaLlmClient`: rilevare esplicitamente 404 (model not found) con messaggio distintivo:
```csharp
if (!response.IsSuccessStatusCode)
{
    var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

    if (response.StatusCode == System.Net.HttpStatusCode.NotFound
        && responseBody.Contains("model", StringComparison.OrdinalIgnoreCase))
    {
        _logger.LogError(
            "Ollama model '{Model}' not found. Pull it with: ollama pull {Model}",
            model, model);
        return LlmCompletionResult.CreateFailure(
            $"LLM model '{model}' is not available on the Ollama server. Run 'ollama pull {model}' on the server.");
    }

    _logger.LogError("Ollama API error: {Status} - {Body}",
        response.StatusCode, DataMasking.MaskResponseBody(responseBody));
    return LlmCompletionResult.CreateFailure(
        $"Ollama API error: {(int)response.StatusCode} ({response.StatusCode})");
}
```

**Step 2** — Pipeline RAG consumer: quando `LlmCompletionResult.IsSuccess == false`, **non** ritornare stringa vuota. Propagare un errore user-visible via `ProblemDetails` 503 `service_unavailable`. Audit del consumer durante implementazione.

### Fix — Parte 2: Setup documentation

**File da aggiornare**: `docs/operations/operations-manual.md` — sezione "Local Dev Setup":
```markdown
### Ollama LLM Setup

MeepleAI usa Ollama per inference locale. Il modello default è `llama3:8b`.

**Pull del modello** (first-time setup):
```bash
docker exec meepleai-ollama ollama pull llama3:8b
```

**Verifica**:
```bash
docker exec meepleai-ollama ollama list
# Expected: llama3:8b in the list
```

**Troubleshooting**: Se il chat RAG ritorna 503 "model not available", ri-esegui il pull sopra.
```

**File nuovo**: `infra/scripts/setup-ollama.sh` (idempotente):
```bash
#!/usr/bin/env bash
set -euo pipefail

MODEL="${OLLAMA_MODEL:-llama3:8b}"
CONTAINER="${OLLAMA_CONTAINER:-meepleai-ollama}"

if docker exec "$CONTAINER" ollama list | grep -q "$MODEL"; then
    echo "✓ Ollama model $MODEL already pulled"
    exit 0
fi

echo "Pulling Ollama model $MODEL..."
docker exec "$CONTAINER" ollama pull "$MODEL"
echo "✓ Ollama model $MODEL ready"
```

Rendere eseguibile: `chmod +x infra/scripts/setup-ollama.sh`.

**Integrazione `make dev`**: aggiungere target `setup-ollama` in `infra/Makefile` — opzionale, non-blocking.

### Tests
- **Unit** `OllamaLlmClientTests.GenerateCompletionAsync_WhenModelNotFound_ReturnsDistinctError` — mock HttpMessageHandler che ritorna 404 con body `{"error":"model 'llama3:8b' not found"}`
- **Manual verification**: RAG chat endpoint con model mancante → response 503 con messaggio chiaro, log WARN nei API logs

## Rollout & Validation

### Pre-merge checklist
- [ ] All 4 fix commits con unit + integration test green locali
- [ ] `dotnet test --filter Category=Unit` → 100% pass
- [ ] `dotnet test --filter Category=Integration` → 100% pass (Testcontainers)
- [ ] `pnpm test` frontend (no changes expected ma sanity check)
- [ ] Migration applicata su DB locale pulito via `dotnet ef database update`
- [ ] Manual smoke test: Game Night → Start Session → autoseeded organizer → session created
- [ ] Manual smoke test: Upload PDF con shared gameId → PDF visibile in query shared game
- [ ] Manual smoke test: RAG chat con model non installato → errore 503 user-visible (non silent empty)

### Post-merge
- [ ] Deploy to staging
- [ ] Verify `search_vector` columns populated on existing data
- [ ] Update issue tracker / close related GitHub issues
- [ ] Run `infra/scripts/setup-ollama.sh` su staging/prod Ollama container

### Rollback plan
- Migration `AddSearchVectorColumns` ha `Down()` completa → `dotnet ef database update <previous>` rollback sicuro
- Fix 1/2/4 sono modifiche additive (nuovi valori enum, nuovi parametri opzionali, nuove query) → revert commit sufficiente
- Fix 5 è non-breaking (log + docs)

## Dependencies & Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| `IUserRepository` non disponibile in `GameManagement` BC | Medium | Check DI container, usa wrapper read-only se cross-BC leak è problema |
| Migration `AddSearchVectorColumns` fallisce su DB dove colonne già esistono con tipo `text` (legacy) | High | Plan include pre-check esplicito nella migration: `DO $$ BEGIN IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='text_chunks' AND column_name='search_vector' AND data_type='text') THEN ALTER TABLE text_chunks DROP COLUMN search_vector; END IF; END $$;` prima dell'`ADD COLUMN`. Idem per `pdf_documents`. Testare su copia staging prima del deploy. |
| `Corrupted` enum value viene serializzato a API → client vede valore sconosciuto | Low | Repo filtra corrupted entities da query pubbliche; admin-only endpoint per diagnosi |
| Fix 4 audit trova >10 call sites → scope creep | Medium | Se audit trova >5 siti, split in PR separata con solo Fix 4 |
| Ollama `setup-ollama.sh` non testato su Windows Git Bash | Low | Script usa solo `docker exec` e pipes standard — dovrebbe funzionare ovunque |

## Open Questions (resolved in brainstorming)

- ~~Scope: PR unica vs multiple?~~ → **PR unica**
- ~~Fix 4 approach: modificare PdfDocument.SharedGameId vs JOIN via games?~~ → **Option C: JOIN only**
- ~~Fix 1 approach: hardcoded vs lookup vs extend command?~~ → **Option D: extend + auto-seed fallback**
- ~~Fix 2 fallback: default status vs exception vs Corrupted state?~~ → **Corrupted quarantine state**
- ~~Fix 5 scope: only docs vs docs + backend defensive?~~ → **Both (docs + backend 404 handling)**
- ~~Fix 5 sub-question: also touch existing working queries in Fix 4?~~ → **No, only broken ones**
