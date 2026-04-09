# Session Flow v2.1 — Backend Core Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementare il backend per Session Flow v2.1: KB readiness check, avvio Session con GameNight ad-hoc, pause/resume con invariante 1-active-per-night, turn order server-side, dice roll endpoint, score update con storia nel Diary, query Diario per Session e GameNight.

**Architecture:** Estensioni al BC `SessionTracking` (nuovi command/query/endpoint) + piccole estensioni al BC `GameManagement` (`GameNightEvent.CreateAdHoc`) + nuova query in `KnowledgeBase`. Tutto CQRS via MediatR, TDD con Testcontainers per integration tests, zero breaking changes sugli aggregati esistenti.

**Tech Stack:** .NET 9 / MediatR / EF Core / FluentValidation / xUnit + Testcontainers PostgreSQL

**Spec:** `docs/superpowers/specs/2026-04-09-session-flow-v2.1.md`

**Scope:** Questo plan copre SOLO i gap backend G1–G10 della spec. Frontend (G11–G15) e E2E Playwright (G16 parziale) in plan separato.

---

## Pre-flight: verifiche da fare prima di iniziare

Prima di eseguire i task, **ogni worker deve verificare**:

- [ ] **P1**: eseguire `git status` — deve essere clean
- [ ] **P2**: eseguire `git checkout main-dev && git pull`
- [ ] **P3**: eseguire `git checkout -b feature/session-flow-v2.1-backend`
- [ ] **P4**: eseguire `git config branch.feature/session-flow-v2.1-backend.parent main-dev` (CLAUDE.md PR Rule)
- [ ] **P5**: `cd apps/api/src/Api && dotnet restore && dotnet build` deve passare
- [ ] **P6**: verificare che esistano i file:
  - `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/Session.cs`
  - `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/SessionEvent.cs`
  - `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/ScoreEntry.cs`
  - `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/DiceRoll.cs`
  - `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/GameNightEvent/GameNightEvent.cs`
  - `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/GameNightEvent/GameNightSession.cs`
  - `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Enums/GameNightStatus.cs`
  - `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/CreateSessionCommand.cs`
  - `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/RollSessionDiceCommand.cs`
  - `apps/api/src/Api/Routing/SessionEndpoints.cs`

Se qualche file manca → fermati, la spec assume realtà diversa, segnala al tech lead.

---

## File Map

### Backend — nuovi file

| File | Responsabilità |
|---|---|
| `apps/api/src/Api/Infrastructure/Migrations/<ts>_AddSessionTurnOrderAndGameNightInProgress.cs` | Migration EF Core |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKbReadinessQuery.cs` | Query CQRS |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKbReadinessQueryHandler.cs` | Handler |
| `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/KbReadinessDto.cs` | DTO risposta |
| `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/PauseSessionCommand.cs` | Command+Result+Validator |
| `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/PauseSessionCommandHandler.cs` | Handler |
| `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/ResumeSessionCommand.cs` | Command+Validator |
| `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/ResumeSessionCommandHandler.cs` | Handler |
| `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/SetTurnOrderCommand.cs` | Command+Result+Validator |
| `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/SetTurnOrderCommandHandler.cs` | Handler |
| `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/UpdateScoreCommand.cs` | Command+Result+Validator |
| `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/UpdateScoreCommandHandler.cs` | Handler |
| `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Queries/GetSessionDiaryQuery.cs` | Query+Handler |
| `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Queries/GetGameNightDiaryQuery.cs` | Query+Handler |
| `apps/api/src/Api/BoundedContexts/SessionTracking/Application/DTOs/SessionEventDto.cs` | DTO evento |
| `apps/api/src/Api/Routing/SessionFlowEndpoints.cs` | Nuovo entrypoint routing |

### Backend — file modificati

| File | Modifica |
|---|---|
| `Session.cs` | Aggiungere `TurnOrderJson`, `TurnOrderMethod`, `TurnOrderSeed`, `CurrentTurnIndex` + metodo `SetTurnOrder(...)` + metodo `AdvanceTurn()` |
| `GameNightStatus.cs` | Aggiungere valore `InProgress = 2` (shift enum ✓ no, aggiungere come nuovo) |
| `GameNightEvent.cs` | Aggiungere `CreateAdHoc` factory + `AttachAdditionalGame` + `MarkInProgress` |
| `CreateSessionCommand.cs` + handler | Aggiungere `GameNightEventId?`, `GuestNames[]`, logica ad-hoc GameNight, KB readiness pre-check |
| `RollSessionDiceCommandHandler.cs` | Dopo il salvataggio di `DiceRoll`, emettere `SessionEvent(dice_rolled)` |
| `SessionEndpoints.cs` | Aggiungere endpoint `POST /api/v1/sessions/{id}/dice-rolls` + wire ai nuovi endpoint |
| `Program.cs` | `app.MapSessionFlowEndpoints()` |
| `SessionTrackingServiceExtensions.cs` | Registrare nuovi query handler (FluentValidation + MediatR auto-discovery dovrebbe bastare) |

---

## Task 0: Test fixture seeders (prerequisite)

Questo task crea gli helper di seeding usati da TUTTI i test integration dei task successivi. Deve essere eseguito prima di Task 1.

**Files:**
- Modify: `apps/api/tests/Api.Tests/Shared/Fixtures/PostgresFixture.cs` (o fixture equivalente — verificare nome reale)

- [ ] **Step 1: Individuare il fixture base**

```bash
find apps/api/tests -name "*Fixture.cs" | xargs grep -l "MeepleAiDbContext"
```

Prendere il fixture che già espone `CreateScope()` e gestisce Testcontainers PostgreSQL. Tipicamente si chiama `PostgresFixture` o `IntegrationTestFixture`. Nel resto del plan useremo `PostgresFixture` come nome — sostituire con il nome reale.

- [ ] **Step 2: Esaminare i seeder esistenti**

```bash
grep -n "public async Task.*Seed\|public Task.*Seed" apps/api/tests/Api.Tests/Shared/Fixtures/PostgresFixture.cs
```

Verificare il pattern usato (es. `DbContext.AddAsync` + `SaveChangesAsync`). I nuovi seeder seguiranno lo stesso pattern.

- [ ] **Step 3: Aggiungere i 4 seeder helper**

Nel file del fixture, aggiungere i seguenti metodi. **⚠️ Adattare i nomi delle entità (`SharedGameEntity`, `UserEntity`, `PdfDocumentEntity`, `VectorDocumentEntity`, `UserLibraryEntryEntity`) alla realtà del codebase.** Cercare con:

```bash
grep -rn "public class.*Entity" apps/api/src/Api/Infrastructure/Entities/ | head -30
```

```csharp
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// Inside PostgresFixture class:

/// <summary>
/// Seeds a user + a shared game + a library entry + 1 indexed PDF + N vector documents.
/// Used for Session Flow v2.1 tests where KB must be ready.
/// </summary>
public async Task<(Guid userId, Guid gameId)> SeedUserWithLibraryGameAndIndexedKbAsync(
    IServiceScope scope,
    int vectorCount = 3)
{
    var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

    var userId = Guid.NewGuid();
    var gameId = Guid.NewGuid();
    var pdfId = Guid.NewGuid();

    // 1. User (adatta ai campi obbligatori)
    var user = new Api.Infrastructure.Entities.UserEntity
    {
        Id = userId,
        Email = $"test-{userId:N}@meepleai.test",
        DisplayName = "Test User",
        Role = "user",
        CreatedAt = DateTime.UtcNow
    };
    db.Users.Add(user);

    // 2. Shared game
    var game = new Api.Infrastructure.Entities.SharedGameCatalog.SharedGameEntity
    {
        Id = gameId,
        Name = $"Test Game {gameId:N}".Substring(0, 30),
        CreatedAt = DateTime.UtcNow
    };
    db.SharedGames.Add(game);

    // 3. User library entry linking user to game
    var libEntry = new Api.Infrastructure.Entities.UserLibrary.UserLibraryEntryEntity
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        SharedGameId = gameId,
        AddedAt = DateTime.UtcNow
    };
    db.UserLibraryEntries.Add(libEntry);

    // 4. Indexed PDF document for the game
    var pdf = new Api.Infrastructure.Entities.DocumentProcessing.PdfDocumentEntity
    {
        Id = pdfId,
        GameId = gameId,
        FileName = "rules.pdf",
        ProcessingState = "Indexed",
        UploadedAt = DateTime.UtcNow,
        UploadedByUserId = userId
    };
    db.PdfDocuments.Add(pdf);

    // 5. Vector documents (minimum 1 for IsReady=true)
    for (int i = 0; i < vectorCount; i++)
    {
        db.VectorDocuments.Add(new Api.Infrastructure.Entities.KnowledgeBase.VectorDocumentEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = pdfId,
            ChunkIndex = i,
            Content = $"chunk {i}",
            EmbeddingVector = new float[] { 0.1f, 0.2f, 0.3f },
            CreatedAt = DateTime.UtcNow
        });
    }

    await db.SaveChangesAsync();
    return (userId, gameId);
}

/// <summary>
/// Seeds an additional game in the same user's library with indexed KB.
/// </summary>
public async Task<Guid> SeedAnotherLibraryGameAsync(IServiceScope scope, Guid userId)
{
    var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
    var gameId = Guid.NewGuid();
    var pdfId = Guid.NewGuid();

    db.SharedGames.Add(new Api.Infrastructure.Entities.SharedGameCatalog.SharedGameEntity
    {
        Id = gameId,
        Name = $"Second Game {gameId:N}".Substring(0, 30),
        CreatedAt = DateTime.UtcNow
    });
    db.UserLibraryEntries.Add(new Api.Infrastructure.Entities.UserLibrary.UserLibraryEntryEntity
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        SharedGameId = gameId,
        AddedAt = DateTime.UtcNow
    });
    db.PdfDocuments.Add(new Api.Infrastructure.Entities.DocumentProcessing.PdfDocumentEntity
    {
        Id = pdfId,
        GameId = gameId,
        FileName = "rules2.pdf",
        ProcessingState = "Indexed",
        UploadedAt = DateTime.UtcNow,
        UploadedByUserId = userId
    });
    db.VectorDocuments.Add(new Api.Infrastructure.Entities.KnowledgeBase.VectorDocumentEntity
    {
        Id = Guid.NewGuid(),
        PdfDocumentId = pdfId,
        ChunkIndex = 0,
        Content = "chunk 0",
        EmbeddingVector = new float[] { 0.1f },
        CreatedAt = DateTime.UtcNow
    });

    await db.SaveChangesAsync();
    return gameId;
}

/// <summary>
/// Seeds user + game in library but WITHOUT indexed KB (PDF in Extracting state, 0 vectors).
/// Used for KB_NOT_READY negative tests.
/// </summary>
public async Task<(Guid userId, Guid gameId)> SeedUserWithLibraryGameButNoKbAsync(IServiceScope scope)
{
    var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
    var userId = Guid.NewGuid();
    var gameId = Guid.NewGuid();
    var pdfId = Guid.NewGuid();

    db.Users.Add(new Api.Infrastructure.Entities.UserEntity
    {
        Id = userId,
        Email = $"test-{userId:N}@meepleai.test",
        DisplayName = "Test User",
        Role = "user",
        CreatedAt = DateTime.UtcNow
    });
    db.SharedGames.Add(new Api.Infrastructure.Entities.SharedGameCatalog.SharedGameEntity
    {
        Id = gameId,
        Name = $"Game NoKB {gameId:N}".Substring(0, 30),
        CreatedAt = DateTime.UtcNow
    });
    db.UserLibraryEntries.Add(new Api.Infrastructure.Entities.UserLibrary.UserLibraryEntryEntity
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        SharedGameId = gameId,
        AddedAt = DateTime.UtcNow
    });
    db.PdfDocuments.Add(new Api.Infrastructure.Entities.DocumentProcessing.PdfDocumentEntity
    {
        Id = pdfId,
        GameId = gameId,
        FileName = "rules.pdf",
        ProcessingState = "Extracting",  // Not indexed
        UploadedAt = DateTime.UtcNow,
        UploadedByUserId = userId
    });
    // NO vector documents

    await db.SaveChangesAsync();
    return (userId, gameId);
}

/// <summary>
/// Lightweight seeder: just a game + indexed PDF + N vectors (no user, no library).
/// Used by unit-ish handler tests that don't need the full user stack.
/// </summary>
public async Task<Guid> SeedGameWithIndexedPdfAsync(MeepleAiDbContext db, int vectorCount)
{
    var gameId = Guid.NewGuid();
    var pdfId = Guid.NewGuid();

    db.SharedGames.Add(new Api.Infrastructure.Entities.SharedGameCatalog.SharedGameEntity
    {
        Id = gameId,
        Name = "SeededGame",
        CreatedAt = DateTime.UtcNow
    });
    db.PdfDocuments.Add(new Api.Infrastructure.Entities.DocumentProcessing.PdfDocumentEntity
    {
        Id = pdfId,
        GameId = gameId,
        FileName = "rules.pdf",
        ProcessingState = "Indexed",
        UploadedAt = DateTime.UtcNow,
        UploadedByUserId = Guid.NewGuid()
    });
    for (int i = 0; i < vectorCount; i++)
    {
        db.VectorDocuments.Add(new Api.Infrastructure.Entities.KnowledgeBase.VectorDocumentEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = pdfId,
            ChunkIndex = i,
            Content = $"chunk {i}",
            EmbeddingVector = new float[] { 0.1f },
            CreatedAt = DateTime.UtcNow
        });
    }
    await db.SaveChangesAsync();
    return gameId;
}

/// <summary>
/// Seeds a game with mixed PDF states (some Indexed, some Failed).
/// </summary>
public async Task<Guid> SeedGameWithMixedPdfsAsync(
    MeepleAiDbContext db, int indexedCount, int failedCount, int vectorsPerIndexed)
{
    var gameId = Guid.NewGuid();
    db.SharedGames.Add(new Api.Infrastructure.Entities.SharedGameCatalog.SharedGameEntity
    {
        Id = gameId,
        Name = "MixedGame",
        CreatedAt = DateTime.UtcNow
    });

    for (int i = 0; i < indexedCount; i++)
    {
        var pid = Guid.NewGuid();
        db.PdfDocuments.Add(new Api.Infrastructure.Entities.DocumentProcessing.PdfDocumentEntity
        {
            Id = pid,
            GameId = gameId,
            FileName = $"ok-{i}.pdf",
            ProcessingState = "Indexed",
            UploadedAt = DateTime.UtcNow,
            UploadedByUserId = Guid.NewGuid()
        });
        for (int v = 0; v < vectorsPerIndexed; v++)
        {
            db.VectorDocuments.Add(new Api.Infrastructure.Entities.KnowledgeBase.VectorDocumentEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pid,
                ChunkIndex = v,
                Content = $"chunk {v}",
                EmbeddingVector = new float[] { 0.1f },
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    for (int i = 0; i < failedCount; i++)
    {
        db.PdfDocuments.Add(new Api.Infrastructure.Entities.DocumentProcessing.PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            FileName = $"fail-{i}.pdf",
            ProcessingState = "Failed",
            UploadedAt = DateTime.UtcNow,
            UploadedByUserId = Guid.NewGuid()
        });
    }

    await db.SaveChangesAsync();
    return gameId;
}
```

- [ ] **Step 4: Verificare che i seeder compilino**

```bash
cd apps/api/tests/Api.Tests
dotnet build
```

Expected: 0 errori. Se ci sono errori di "entity not found" o "property X does not exist", **significa che i nomi reali delle entità sono diversi**: adattarli seguendo gli errori del compilatore.

- [ ] **Step 5: Commit**

```bash
git add apps/api/tests/Api.Tests/Shared/Fixtures/PostgresFixture.cs
git commit -m "test(fixtures): add Session Flow v2.1 seeders to PostgresFixture"
```

---

## Task 1: Schema Migration — TurnOrder fields + GameNightStatus.InProgress

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/Session.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Enums/GameNightStatus.cs`
- Create: `apps/api/src/Api/Infrastructure/Migrations/<timestamp>_AddSessionTurnOrderAndGameNightInProgress.cs` (via `dotnet ef`)
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Domain/Entities/SessionTurnOrderTests.cs`

- [ ] **Step 1: Leggere lo stato corrente di GameNightStatus**

```bash
cat apps/api/src/Api/BoundedContexts/GameManagement/Domain/Enums/GameNightStatus.cs
```

Verificare i valori esistenti. Dovrebbero essere: `Draft=0, Published=1, Completed=2, Cancelled=3` (o simili).

- [ ] **Step 2: Aggiungere `InProgress` a `GameNightStatus`**

Aprire `GameNightStatus.cs` e aggiungere un valore `InProgress` **in coda** per non rompere l'ordinale esistente. Se attualmente è:

```csharp
public enum GameNightStatus
{
    Draft = 0,
    Published = 1,
    Completed = 2,
    Cancelled = 3
}
```

Diventa:

```csharp
public enum GameNightStatus
{
    Draft = 0,
    Published = 1,
    Completed = 2,
    Cancelled = 3,
    /// <summary>Ad-hoc night currently in progress (spontaneous multi-game session).</summary>
    InProgress = 4
}
```

⚠️ Se i valori nel file sono diversi (es. usano flag [Flags] o ordinale diverso), adattarsi mantenendo l'invariante "non rompere il persistito".

- [ ] **Step 3: Scrivere il test failing per i nuovi campi Session**

Creare `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Domain/Entities/SessionTurnOrderTests.cs`:

```csharp
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain.Entities;

public class SessionTurnOrderTests
{
    private static Session CreateSession()
    {
        return Session.Create(
            userId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            sessionType: SessionType.GameSpecific);
    }

    [Fact]
    public void Session_NewlyCreated_HasNullTurnOrderFields()
    {
        var session = CreateSession();

        session.TurnOrderJson.Should().BeNull();
        session.TurnOrderMethod.Should().BeNull();
        session.TurnOrderSeed.Should().BeNull();
        session.CurrentTurnIndex.Should().BeNull();
    }

    [Fact]
    public void SetTurnOrder_Manual_PersistsOrderAndMethod()
    {
        var session = CreateSession();
        var p1 = session.Participants.First().Id;
        session.AddParticipant(ParticipantInfo.Create("Luca", isOwner: false, joinOrder: 2));
        var p2 = session.Participants.Last().Id;

        session.SetTurnOrder(TurnOrderMethod.Manual, order: new[] { p2, p1 }, seed: null);

        session.TurnOrderMethod.Should().Be("Manual");
        session.TurnOrderSeed.Should().BeNull();
        session.TurnOrderJson.Should().Contain(p2.ToString()).And.Contain(p1.ToString());
        session.CurrentTurnIndex.Should().Be(0);
    }

    [Fact]
    public void SetTurnOrder_Random_RequiresSeed()
    {
        var session = CreateSession();
        var p1 = session.Participants.First().Id;

        var act = () => session.SetTurnOrder(TurnOrderMethod.Random, order: new[] { p1 }, seed: null);

        act.Should().Throw<ArgumentException>().WithMessage("*seed*");
    }

}

// NOTE: Test for AdvanceTurn() is deferred to Plan 1bis together with AdvanceTurnCommand (G9).
// The domain method is also deferred — not implemented in this plan.
```

Nota: il test usa `TurnOrderMethod` enum e metodi `SetTurnOrder`, `AdvanceTurn` che ancora non esistono — è intenzionale (red phase).

- [ ] **Step 4: Creare l'enum `TurnOrderMethod`**

Creare `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Enums/TurnOrderMethod.cs`:

```csharp
namespace Api.BoundedContexts.SessionTracking.Domain.Enums;

/// <summary>
/// How turn order is determined for a session.
/// </summary>
public enum TurnOrderMethod
{
    /// <summary>Order explicitly defined by host.</summary>
    Manual = 0,
    /// <summary>Order shuffled server-side with auditable seed.</summary>
    Random = 1
}
```

- [ ] **Step 5: Aggiungere i nuovi campi e i metodi a `Session`**

In `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/Session.cs`, **dopo** la proprietà `RowVersion` (cerca `RowVersion`), aggiungere:

```csharp
    /// <summary>
    /// Turn order as JSON array of participant IDs. Null if not yet set.
    /// </summary>
    public string? TurnOrderJson { get; private set; }

    /// <summary>
    /// Method used to set the turn order: "Manual" | "Random".
    /// </summary>
    [System.ComponentModel.DataAnnotations.MaxLength(16)]
    public string? TurnOrderMethod { get; private set; }

    /// <summary>
    /// Seed used when TurnOrderMethod=Random, for audit/reproducibility.
    /// </summary>
    public int? TurnOrderSeed { get; private set; }

    /// <summary>
    /// Zero-based index of the current player in the turn order.
    /// </summary>
    public int? CurrentTurnIndex { get; private set; }
```

Poi, **in fondo alla classe Session** (prima di `UpdateAudit`), aggiungere:

```csharp
    /// <summary>
    /// Sets the turn order for this session.
    /// </summary>
    /// <param name="method">Manual or Random.</param>
    /// <param name="order">Ordered participant IDs.</param>
    /// <param name="seed">Required when method is Random (for audit).</param>
    public void SetTurnOrder(
        Api.BoundedContexts.SessionTracking.Domain.Enums.TurnOrderMethod method,
        IReadOnlyList<Guid> order,
        int? seed)
    {
        if (Status == SessionStatus.Finalized)
            throw new ConflictException("Cannot set turn order on finalized session.");

        ArgumentNullException.ThrowIfNull(order);
        if (order.Count == 0)
            throw new ArgumentException("Turn order cannot be empty.", nameof(order));

        // Verify all IDs are participants of this session
        var participantIds = _participants.Select(p => p.Id).ToHashSet();
        foreach (var id in order)
        {
            if (!participantIds.Contains(id))
                throw new ArgumentException($"Participant {id} is not in this session.", nameof(order));
        }

        if (method == Api.BoundedContexts.SessionTracking.Domain.Enums.TurnOrderMethod.Random && !seed.HasValue)
            throw new ArgumentException("Random turn order requires a seed for audit.", nameof(seed));

        TurnOrderJson = System.Text.Json.JsonSerializer.Serialize(order);
        TurnOrderMethod = method.ToString();
        TurnOrderSeed = seed;
        CurrentTurnIndex = 0;
        UpdatedAt = DateTime.UtcNow;
    }

    // AdvanceTurn() method deferred to Plan 1bis together with AdvanceTurnCommand (G9).
```

- [ ] **Step 6: Eseguire i test e verificare che passino (green phase)**

```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "FullyQualifiedName~SessionTurnOrderTests" --logger "console;verbosity=normal"
```

Expected: 3/3 PASS (Session_NewlyCreated, SetTurnOrder_Manual, SetTurnOrder_Random_RequiresSeed).

- [ ] **Step 7: Configurare il mapping EF Core per i nuovi campi**

Aprire `apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Persistence/Configurations/SessionEntityConfiguration.cs` (path esatto può variare — cercare con `find apps/api/src -name "SessionEntityConfiguration.cs"`). Aggiungere:

```csharp
builder.Property(s => s.TurnOrderJson)
    .HasColumnType("jsonb"); // PostgreSQL jsonb

builder.Property(s => s.TurnOrderMethod)
    .HasMaxLength(16);

builder.Property(s => s.TurnOrderSeed);

builder.Property(s => s.CurrentTurnIndex);
```

- [ ] **Step 8: Generare la migration**

```bash
cd apps/api/src/Api
dotnet ef migrations add AddSessionTurnOrderAndGameNightInProgress
```

Expected output: `Done. To undo this action, use 'ef migrations remove'`

- [ ] **Step 8bis: Aggiungere unique partial index per invariante 1-Active-per-GameNight**

L'invariante "max 1 Session Active per GameNight" è attualmente enforcedsolo a livello command handler — race condition possibile se 2 richieste arrivano simultaneamente. Aggiungiamo un **unique partial index PostgreSQL** come safety net.

Aprire la migration appena generata (file da Step 8) e aggiungere manualmente nel metodo `Up`:

```csharp
// Enforce invariant: at most 1 Active Session per GameNightEvent (race-condition safe)
migrationBuilder.Sql(@"
CREATE UNIQUE INDEX IF NOT EXISTS ""ix_game_night_sessions_unique_active""
ON ""GameNightSessions"" (""GameNightEventId"")
WHERE EXISTS (
    SELECT 1 FROM ""Sessions"" s
    WHERE s.""Id"" = ""GameNightSessions"".""SessionId""
    AND s.""Status"" = 0  -- SessionStatus.Active = 0
);
");
```

⚠️ I nomi tabella/colonna potrebbero usare snake_case o convention diversa. Verificare con:

```bash
docker exec -it meepleai-postgres psql -U postgres -d meepleai -c "\d GameNightSessions"
```

Se l'approccio con sub-select fallisce (alcune versioni PG richiedono predicate semplice), **alternativa**: denormalizzare un flag `IsActive: bool` su `GameNightSession` aggiornato dai PauseSession/ResumeSession handler, e creare unique index semplice:

```sql
CREATE UNIQUE INDEX IF NOT EXISTS "ix_game_night_sessions_unique_active"
ON "GameNightSessions" ("GameNightEventId") WHERE "IsActive" = TRUE;
```

Il worker sceglie l'approccio meno invasivo. In caso di denormalizzazione, aggiungere `IsActive` a `GameNightSession` + aggiornare `PauseSessionCommandHandler` e `ResumeSessionCommandHandler` in Task 5.

Nel `Down` method della migration, aggiungere:

```csharp
migrationBuilder.Sql("DROP INDEX IF EXISTS \"ix_game_night_sessions_unique_active\";");
```

Nel handler `CreateSessionCommand` (Task 4), gestire la `DbUpdateException` da violazione unique index come `ConflictException("ACTIVE_SESSION_EXISTS_IN_NIGHT")` (catch specifico, fallback sul check read-based).

- [ ] **Step 9: Verificare il file migration generato**

Aprire il file `apps/api/src/Api/Infrastructure/Migrations/<timestamp>_AddSessionTurnOrderAndGameNightInProgress.cs` e verificare che contenga:

```csharp
migrationBuilder.AddColumn<string>(
    name: "TurnOrderJson",
    table: "Sessions",
    type: "jsonb",
    nullable: true);

migrationBuilder.AddColumn<string>(
    name: "TurnOrderMethod",
    table: "Sessions",
    type: "character varying(16)",
    maxLength: 16,
    nullable: true);

migrationBuilder.AddColumn<int>(
    name: "TurnOrderSeed",
    table: "Sessions",
    type: "integer",
    nullable: true);

migrationBuilder.AddColumn<int>(
    name: "CurrentTurnIndex",
    table: "Sessions",
    type: "integer",
    nullable: true);
```

Se il nome tabella è diverso (es. `sessions` lowercase), non è un problema — EF genera in base alla convention del contesto.

- [ ] **Step 10: Applicare la migration al DB locale**

```bash
cd apps/api/src/Api
dotnet ef database update
```

Expected output: `Done.`

- [ ] **Step 11: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/Session.cs
git add apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Enums/TurnOrderMethod.cs
git add apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Persistence/Configurations/SessionEntityConfiguration.cs
git add apps/api/src/Api/BoundedContexts/GameManagement/Domain/Enums/GameNightStatus.cs
git add apps/api/src/Api/Infrastructure/Migrations/
git add apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Domain/Entities/SessionTurnOrderTests.cs
git commit -m "feat(session-tracking): add turn order fields and SetTurnOrder domain method

- TurnOrderMethod enum (Manual, Random)
- Session.TurnOrderJson/Method/Seed/CurrentTurnIndex fields
- Session.SetTurnOrder() domain method (AdvanceTurn deferred to Plan 1bis)
- GameNightStatus.InProgress for ad-hoc nights
- EF migration AddSessionTurnOrderAndGameNightInProgress
- Unique partial index enforcing '1 Active Session per GameNight'"
```

---

## Task 2: GameNightEvent — CreateAdHoc + AttachAdditionalGame

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/GameNightEvent/GameNightEvent.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/Entities/GameNightEventAdHocTests.cs`

- [ ] **Step 1: Scrivere i test failing**

Creare `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/Entities/GameNightEventAdHocTests.cs`:

```csharp
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

public class GameNightEventAdHocTests
{
    [Fact]
    public void CreateAdHoc_SetsStatusInProgress_And_AddsFirstGame()
    {
        var organizerId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var night = GameNightEvent.CreateAdHoc(organizerId, "Serata del 09/04", gameId);

        night.OrganizerId.Should().Be(organizerId);
        night.Status.Should().Be(GameNightStatus.InProgress);
        night.GameIds.Should().ContainSingle().Which.Should().Be(gameId);
        night.Title.Should().Be("Serata del 09/04");
        night.Rsvps.Should().BeEmpty();
    }

    [Fact]
    public void AttachAdditionalGame_WhenInProgress_AddsToGameIds()
    {
        var firstGame = Guid.NewGuid();
        var secondGame = Guid.NewGuid();
        var night = GameNightEvent.CreateAdHoc(Guid.NewGuid(), "Serata", firstGame);

        night.AttachAdditionalGame(secondGame);

        night.GameIds.Should().HaveCount(2).And.Contain(new[] { firstGame, secondGame });
    }

    [Fact]
    public void AttachAdditionalGame_WhenDraft_Throws()
    {
        var night = GameNightEvent.Create(
            Guid.NewGuid(),
            "Pianificata",
            DateTimeOffset.UtcNow.AddDays(1));

        var act = () => night.AttachAdditionalGame(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AttachAdditionalGame_DuplicateGameId_IsIdempotent()
    {
        var gameId = Guid.NewGuid();
        var night = GameNightEvent.CreateAdHoc(Guid.NewGuid(), "Serata", gameId);

        night.AttachAdditionalGame(gameId);

        night.GameIds.Should().ContainSingle().Which.Should().Be(gameId);
    }
}
```

- [ ] **Step 2: Eseguire il test e verificare che fallisca**

```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "FullyQualifiedName~GameNightEventAdHocTests"
```

Expected: FAIL — `CreateAdHoc` and `AttachAdditionalGame` do not exist.

- [ ] **Step 3: Implementare `CreateAdHoc` e `AttachAdditionalGame`**

Aprire `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/GameNightEvent/GameNightEvent.cs`.

Cercare la factory `Create(...)` esistente. **Dopo** di essa, aggiungere:

```csharp
    /// <summary>
    /// Creates an ad-hoc game night that skips the scheduling/RSVP flow.
    /// Used when a user spontaneously starts playing and adds more games during the same evening.
    /// Status is set directly to InProgress.
    /// </summary>
    /// <param name="organizerId">User who starts the night.</param>
    /// <param name="title">Auto-generated title (e.g., "Serata del 2026-04-09 20:00").</param>
    /// <param name="firstGameId">The game being played at night start.</param>
    public static GameNightEvent CreateAdHoc(Guid organizerId, string title, Guid firstGameId)
    {
        if (firstGameId == Guid.Empty)
            throw new ArgumentException("FirstGameId cannot be empty.", nameof(firstGameId));

        var night = new GameNightEvent(
            id: Guid.NewGuid(),
            organizerId: organizerId,
            title: title,
            scheduledAt: DateTimeOffset.UtcNow,
            description: null,
            location: null,
            maxPlayers: null,
            gameIds: new List<Guid> { firstGameId });

        night.Status = GameNightStatus.InProgress;
        return night;
    }

    /// <summary>
    /// Adds a game to an in-progress ad-hoc night.
    /// Idempotent: duplicates are ignored.
    /// </summary>
    /// <param name="gameId">Game to add.</param>
    public void AttachAdditionalGame(Guid gameId)
    {
        ThrowIfCorrupted();

        if (Status != GameNightStatus.InProgress)
            throw new InvalidOperationException(
                $"Cannot attach game to a {Status} game night. Only InProgress nights accept dynamic games.");

        if (gameId == Guid.Empty)
            throw new ArgumentException("GameId cannot be empty.", nameof(gameId));

        if (!GameIds.Contains(gameId))
        {
            GameIds.Add(gameId);
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
```

Nota: se il file usa `internal sealed class`, i nuovi membri **devono** essere `public` se vuoi testarli dall'assembly `Api.Tests`. Se l'assembly ha già `InternalsVisibleTo("Api.Tests")`, puoi mantenerli `internal`. Verificare con:

```bash
grep -r "InternalsVisibleTo.*Api.Tests" apps/api/src/Api
```

Se il risultato è presente → lascia `internal`. Altrimenti → `public`.

- [ ] **Step 4: Eseguire i test e verificare che passino**

```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "FullyQualifiedName~GameNightEventAdHocTests"
```

Expected: 4/4 PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/GameNightEvent/GameNightEvent.cs
git add apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/Entities/GameNightEventAdHocTests.cs
git commit -m "feat(game-management): add GameNightEvent.CreateAdHoc and AttachAdditionalGame

Supports ad-hoc multi-game evenings: user starts playing, later adds
more games during the same night, without scheduling/RSVP workflow."
```

---

## Task 3: GetKbReadinessQuery

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/KbReadinessDto.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKbReadinessQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKbReadinessQueryHandler.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetKbReadinessQueryHandlerTests.cs`

- [ ] **Step 1: Creare il DTO**

Creare `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/KbReadinessDto.cs`:

```csharp
namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Knowledge Base readiness status for a game.
/// A KB is "ready" when at least one PDF is Indexed and vector documents exist.
/// </summary>
public record KbReadinessDto(
    bool IsReady,
    string State,                  // "None" | "Extracting" | "Indexing" | "Indexed" | "PartiallyIndexed" | "Failed"
    int VectorDocumentCount,
    int FailedPdfCount,
    IReadOnlyList<string> Warnings
);
```

- [ ] **Step 2: Creare la query**

Creare `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKbReadinessQuery.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Checks if the Knowledge Base for a given game is ready to power an agent.
/// </summary>
/// <param name="GameId">Shared game ID.</param>
public record GetKbReadinessQuery(Guid GameId) : IRequest<KbReadinessDto>;
```

- [ ] **Step 3: Scrivere i test failing dell'handler**

Creare `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetKbReadinessQueryHandlerTests.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Infrastructure;
using Api.Tests.Shared.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

public class GetKbReadinessQueryHandlerTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public GetKbReadinessQueryHandlerTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Handle_NoPdfsForGame_ReturnsNotReady()
    {
        using var scope = _fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var handler = new GetKbReadinessQueryHandler(db);

        var result = await handler.Handle(new GetKbReadinessQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsReady.Should().BeFalse();
        result.State.Should().Be("None");
        result.VectorDocumentCount.Should().Be(0);
        result.FailedPdfCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_IndexedPdfWithVectors_ReturnsReady()
    {
        using var scope = _fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // Arrange: seed a game + indexed PDF + 5 vector documents
        var gameId = await _fixture.SeedGameWithIndexedPdfAsync(db, vectorCount: 5);

        var handler = new GetKbReadinessQueryHandler(db);
        var result = await handler.Handle(new GetKbReadinessQuery(gameId), CancellationToken.None);

        result.IsReady.Should().BeTrue();
        result.State.Should().Be("Indexed");
        result.VectorDocumentCount.Should().Be(5);
    }

    [Fact]
    public async Task Handle_IndexedPdfButNoVectors_ReturnsNotReady()
    {
        using var scope = _fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var gameId = await _fixture.SeedGameWithIndexedPdfAsync(db, vectorCount: 0);

        var handler = new GetKbReadinessQueryHandler(db);
        var result = await handler.Handle(new GetKbReadinessQuery(gameId), CancellationToken.None);

        result.IsReady.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_MixedSomeIndexedSomeFailed_ReturnsPartiallyIndexedWithWarning()
    {
        using var scope = _fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var gameId = await _fixture.SeedGameWithMixedPdfsAsync(db, indexedCount: 1, failedCount: 1, vectorsPerIndexed: 3);

        var handler = new GetKbReadinessQueryHandler(db);
        var result = await handler.Handle(new GetKbReadinessQuery(gameId), CancellationToken.None);

        result.IsReady.Should().BeTrue();
        result.State.Should().Be("PartiallyIndexed");
        result.FailedPdfCount.Should().Be(1);
        result.Warnings.Should().NotBeEmpty();
    }
}
```

⚠️ I helper `SeedGameWithIndexedPdfAsync` e `SeedGameWithMixedPdfsAsync` potrebbero non esistere. Se mancano, cercare pattern simili con:

```bash
grep -rn "SeedGame" apps/api/tests/Api.Tests/Shared
```

Se non trovi helper equivalenti, crea un helper locale nel test file che instanzia le entità necessarie (`PdfDocumentEntity` con `ProcessingState`, `VectorDocumentEntity`). Il test deve verificare la **logica dell'handler**, non il setup.

- [ ] **Step 4: Verificare che il test fallisca**

```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "FullyQualifiedName~GetKbReadinessQueryHandlerTests"
```

Expected: FAIL — handler class doesn't exist.

- [ ] **Step 5: Implementare l'handler**

Creare `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKbReadinessQueryHandler.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

public class GetKbReadinessQueryHandler : IRequestHandler<GetKbReadinessQuery, KbReadinessDto>
{
    private readonly MeepleAiDbContext _db;

    public GetKbReadinessQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<KbReadinessDto> Handle(GetKbReadinessQuery request, CancellationToken cancellationToken)
    {
        // NOTE: Adatta i nomi tabella/colonna alla tua realtà EF.
        // PdfDocumentEntity dovrebbe avere GameId (o SharedGameId) e ProcessingState.
        // VectorDocumentEntity dovrebbe avere PdfDocumentId (o GameId).

        var pdfs = await _db.PdfDocuments
            .AsNoTracking()
            .Where(p => p.GameId == request.GameId)
            .Select(p => new { p.Id, p.ProcessingState })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (pdfs.Count == 0)
        {
            return new KbReadinessDto(
                IsReady: false,
                State: "None",
                VectorDocumentCount: 0,
                FailedPdfCount: 0,
                Warnings: Array.Empty<string>());
        }

        var indexedPdfIds = pdfs
            .Where(p => string.Equals(p.ProcessingState, "Indexed", StringComparison.Ordinal))
            .Select(p => p.Id)
            .ToList();

        var failedCount = pdfs.Count(p => string.Equals(p.ProcessingState, "Failed", StringComparison.Ordinal));

        var vectorCount = indexedPdfIds.Count == 0
            ? 0
            : await _db.VectorDocuments
                .AsNoTracking()
                .CountAsync(v => indexedPdfIds.Contains(v.PdfDocumentId), cancellationToken)
                .ConfigureAwait(false);

        var isReady = vectorCount > 0 && indexedPdfIds.Count > 0;

        string state;
        var warnings = new List<string>();

        if (!isReady && pdfs.Any(p => string.Equals(p.ProcessingState, "Extracting", StringComparison.Ordinal)))
            state = "Extracting";
        else if (!isReady && pdfs.Any(p => string.Equals(p.ProcessingState, "Indexing", StringComparison.Ordinal)))
            state = "Indexing";
        else if (isReady && failedCount > 0)
        {
            state = "PartiallyIndexed";
            warnings.Add($"{failedCount} PDF document(s) failed to index — agent answers may be incomplete.");
        }
        else if (isReady)
            state = "Indexed";
        else if (failedCount == pdfs.Count)
            state = "Failed";
        else
            state = "None";

        return new KbReadinessDto(
            IsReady: isReady,
            State: state,
            VectorDocumentCount: vectorCount,
            FailedPdfCount: failedCount,
            Warnings: warnings);
    }
}
```

⚠️ **Adattamento realtà**: i nomi `_db.PdfDocuments` e `_db.VectorDocuments` potrebbero essere diversi. Cerca con:

```bash
grep -rn "DbSet<PdfDocument" apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs
grep -rn "DbSet<VectorDocument" apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs
```

E adatta. Analogamente, `ProcessingState` potrebbe essere un enum (`PdfProcessingState.Indexed`) invece di stringa — in quel caso usa il confronto enum:

```csharp
.Where(p => p.ProcessingState == PdfProcessingState.Indexed)
```

- [ ] **Step 6: Eseguire i test**

```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "FullyQualifiedName~GetKbReadinessQueryHandlerTests"
```

Expected: PASS (potrebbe servire qualche iterazione per adattare i nomi entità).

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/KbReadinessDto.cs
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKbReadinessQuery.cs
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKbReadinessQueryHandler.cs
git add apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetKbReadinessQueryHandlerTests.cs
git commit -m "feat(knowledge-base): add GetKbReadinessQuery for pre-session KB check"
```

---

## Task 4: Extend CreateSessionCommand — GameNightEventId + KB check + ad-hoc logic

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/CreateSessionCommand.cs`
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/CreateSessionCommandValidator.cs`
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/CreateSessionCommandHandler.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/Commands/CreateSessionCommandAdHocTests.cs`

- [ ] **Step 1: Estendere il record `CreateSessionCommand`**

Leggere lo stato attuale di `CreateSessionCommand.cs` (lo abbiamo già visto in pre-flight). Sostituire il record con:

```csharp
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public record CreateSessionCommand(
    Guid UserId,
    Guid GameId,
    string SessionType,
    DateTime? SessionDate,
    string? Location,
    List<ParticipantDto> Participants,
    Guid? GameNightEventId = null,
    IReadOnlyList<string>? GuestNames = null
) : ICommand<CreateSessionResult>;

public record CreateSessionResult(
    Guid SessionId,
    string SessionCode,
    List<ParticipantDto> Participants,
    Guid GameNightEventId,
    bool GameNightWasCreated,
    Guid? AgentDefinitionId,
    Guid? ToolkitId
);
```

Nota: aggiunti 2 parametri opzionali alla fine per backward compat e 4 nuovi campi al risultato.

- [ ] **Step 2: Scrivere il test failing per la logica ad-hoc**

Creare `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/Commands/CreateSessionCommandAdHocTests.cs`:

```csharp
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.Tests.Shared.Fixtures;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Commands;

public class CreateSessionCommandAdHocTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public CreateSessionCommandAdHocTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Handle_NoGameNightProvided_CreatesAdHocNightImplicitly()
    {
        using var scope = _fixture.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var (userId, gameId) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(scope);

        var cmd = new CreateSessionCommand(
            UserId: userId,
            GameId: gameId,
            SessionType: "GameSpecific",
            SessionDate: null,
            Location: null,
            Participants: new List<ParticipantDto>());

        var result = await mediator.Send(cmd);

        result.GameNightWasCreated.Should().BeTrue();
        result.GameNightEventId.Should().NotBeEmpty();

        // Verify the GameNightEvent exists and is InProgress
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var night = await db.GameNightEvents.FindAsync(result.GameNightEventId);
        night.Should().NotBeNull();
        night!.Status.Should().Be(GameNightStatus.InProgress);
        night.GameIds.Should().Contain(gameId);
    }

    [Fact]
    public async Task Handle_ExistingNightProvided_AttachesGameToNight()
    {
        using var scope = _fixture.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var (userId, gameId1) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(scope);

        // First session: create night implicitly
        var first = await mediator.Send(new CreateSessionCommand(
            userId, gameId1, "GameSpecific", null, null, new List<ParticipantDto>()));

        // Pause first session so invariant is satisfied
        await mediator.Send(new PauseSessionCommand(first.SessionId, userId));

        // Second session with a different game, same night
        var gameId2 = await _fixture.SeedAnotherLibraryGameAsync(scope, userId);
        var second = await mediator.Send(new CreateSessionCommand(
            userId, gameId2, "GameSpecific", null, null, new List<ParticipantDto>(),
            GameNightEventId: first.GameNightEventId));

        second.GameNightEventId.Should().Be(first.GameNightEventId);
        second.GameNightWasCreated.Should().BeFalse();

        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var night = await db.GameNightEvents.FindAsync(first.GameNightEventId);
        night!.GameIds.Should().Contain(new[] { gameId1, gameId2 });
    }

    [Fact]
    public async Task Handle_KbNotReady_Throws422()
    {
        using var scope = _fixture.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var (userId, gameId) = await _fixture.SeedUserWithLibraryGameButNoKbAsync(scope);

        var cmd = new CreateSessionCommand(
            userId, gameId, "GameSpecific", null, null, new List<ParticipantDto>());

        var act = () => mediator.Send(cmd);

        await act.Should().ThrowAsync<UnprocessableEntityException>()
            .Where(ex => ex.Code == "KB_NOT_READY");
    }

    [Fact]
    public async Task Handle_ActiveSessionInNight_Throws409()
    {
        using var scope = _fixture.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var (userId, gameId1) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(scope);

        var first = await mediator.Send(new CreateSessionCommand(
            userId, gameId1, "GameSpecific", null, null, new List<ParticipantDto>()));

        // Do NOT pause — first is still Active
        var gameId2 = await _fixture.SeedAnotherLibraryGameAsync(scope, userId);

        var act = () => mediator.Send(new CreateSessionCommand(
            userId, gameId2, "GameSpecific", null, null, new List<ParticipantDto>(),
            GameNightEventId: first.GameNightEventId));

        await act.Should().ThrowAsync<ConflictException>()
            .Where(ex => ex.Code == "ACTIVE_SESSION_EXISTS_IN_NIGHT");
    }

    [Fact]
    public async Task Handle_GuestNamesProvided_AddsParticipantsWithDisplayNames()
    {
        using var scope = _fixture.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var (userId, gameId) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(scope);

        var cmd = new CreateSessionCommand(
            UserId: userId,
            GameId: gameId,
            SessionType: "GameSpecific",
            SessionDate: null,
            Location: null,
            Participants: new List<ParticipantDto>(),
            GuestNames: new[] { "Luca", "Sara" });

        var result = await mediator.Send(cmd);

        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var session = await db.Sessions.Include(s => s.Participants).FirstAsync(s => s.Id == result.SessionId);
        session.Participants.Should().HaveCount(3); // Owner + 2 guests
        session.Participants.Select(p => p.DisplayName).Should().Contain(new[] { "Luca", "Sara" });
    }
}
```

⚠️ Alcune API dei test usano helper di PostgresFixture che potrebbero non esistere (`SeedUserWithLibraryGameAndIndexedKbAsync`, `SeedAnotherLibraryGameAsync`, `SeedUserWithLibraryGameButNoKbAsync`). **Se non esistono, vanno creati** nel `PostgresFixture` (o fixture simile). Il worker deve:
1. Cercare il fixture esistente: `find apps/api/tests -name "PostgresFixture.cs"`
2. Esaminare i seeder esistenti per seguire lo stesso pattern
3. Aggiungere i tre seeder helper nel fixture prima di far passare il test

Per brevità questo plan non ripete il codice del seeder — ma il worker deve **assolutamente** scriverli, non marcarli come TODO.

`UnprocessableEntityException` potrebbe chiamarsi diversamente (es. `ValidationException`, `BusinessRuleException`). Cercare:
```bash
grep -rn "class.*Exception.*: .*Exception" apps/api/src/Api/Middleware/Exceptions/
```
e adattare.

- [ ] **Step 3: Eseguire il test e verificare che fallisca**

```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "FullyQualifiedName~CreateSessionCommandAdHocTests"
```

Expected: FAIL — handler non supporta ancora `GameNightEventId`, `GuestNames`, KB check, invariante.

- [ ] **Step 4: Modificare il validator**

Aprire `CreateSessionCommandValidator.cs`. Cercare la classe `CreateSessionCommandValidator`. Aggiungere alle rules esistenti:

```csharp
RuleFor(x => x.GuestNames)
    .Must(names => names == null || names.All(n => !string.IsNullOrWhiteSpace(n) && n.Length <= 50))
    .WithMessage("Each guest name must be non-empty and max 50 characters.")
    .When(x => x.GuestNames != null);
```

- [ ] **Step 4bis: Verificare il SaveChangesAsync count esistente**

**CRITICO per transaction atomicity**. Aprire `CreateSessionCommandHandler.cs` e contare quante volte viene chiamato `SaveChangesAsync`:

```bash
grep -c "SaveChangesAsync" apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/CreateSessionCommandHandler.cs
```

Deve essere **esattamente 1** alla fine del metodo `Handle`. Se ce ne sono 2 o più, consolidare in un'unica chiamata finale, altrimenti il codice nuovo (GameNightEvent + GameNightSession + SessionEvents) potrebbe essere persistito in transazioni separate con rischio di inconsistenza.

Se l'handler usa già un Unit of Work / `IExecutionStrategy`, rispettare il pattern esistente.

- [ ] **Step 5: Modificare l'handler**

Aprire `CreateSessionCommandHandler.cs`. Trovare il metodo `Handle`. Prima di `Session.Create(...)`, aggiungere (tutto il codice, non selezioni parziali):

```csharp
// ---------- Pre-checks Session Flow v2.1 ----------

// 1. KB readiness
var kbReadiness = await _mediator.Send(
    new GetKbReadinessQuery(request.GameId),
    cancellationToken).ConfigureAwait(false);

if (!kbReadiness.IsReady)
{
    throw new UnprocessableEntityException(
        code: "KB_NOT_READY",
        message: $"Knowledge base for game {request.GameId} is not ready (state: {kbReadiness.State}).");
}

// 2. Resolve GameNightEvent
Guid gameNightEventId;
bool gameNightWasCreated;
GameNightEvent? night;

if (request.GameNightEventId.HasValue)
{
    // Attach to existing night
    night = await _db.GameNightEvents
        .FirstOrDefaultAsync(g => g.Id == request.GameNightEventId.Value, cancellationToken)
        .ConfigureAwait(false);

    if (night is null)
        throw new NotFoundException($"GameNightEvent {request.GameNightEventId.Value} not found.");

    if (night.Status != GameNightStatus.InProgress)
        throw new ConflictException(
            code: "GAMENIGHT_NOT_IN_PROGRESS",
            message: $"GameNightEvent is in status {night.Status}, cannot attach new sessions.");

    // 3. Invariant: at most 1 Active Session per GameNight
    var hasActive = await _db.GameNightSessions
        .Where(gns => gns.GameNightEventId == night.Id)
        .Join(_db.Sessions, gns => gns.SessionId, s => s.Id, (gns, s) => s)
        .AnyAsync(s => s.Status == SessionStatus.Active, cancellationToken)
        .ConfigureAwait(false);

    if (hasActive)
    {
        var activeSessionIds = await _db.GameNightSessions
            .Where(gns => gns.GameNightEventId == night.Id)
            .Join(_db.Sessions, gns => gns.SessionId, s => s.Id, (gns, s) => s)
            .Where(s => s.Status == SessionStatus.Active)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        throw new ConflictException(
            code: "ACTIVE_SESSION_EXISTS_IN_NIGHT",
            message: $"GameNight {night.Id} already has an active session. Pause it before starting a new one.",
            details: new { activeSessionIds });
    }

    night.AttachAdditionalGame(request.GameId);
    gameNightEventId = night.Id;
    gameNightWasCreated = false;
}
else
{
    // Create new ad-hoc night
    var title = $"Serata del {DateTime.UtcNow:yyyy-MM-dd HH:mm}";
    night = GameNightEvent.CreateAdHoc(request.UserId, title, request.GameId);
    _db.GameNightEvents.Add(night);
    gameNightEventId = night.Id;
    gameNightWasCreated = true;
}

// ---------- Existing Session creation logic ----------
// (Mantieni l'esistente Session.Create(...) + AddParticipant loop)
```

Poi, **dopo** la creazione della `Session` e l'add dei participant "standard", aggiungere guest names:

```csharp
// Add guest participants (no UserId)
if (request.GuestNames is { Count: > 0 })
{
    foreach (var guestName in request.GuestNames)
    {
        session.AddParticipant(
            ParticipantInfo.Create(displayName: guestName, isOwner: false, joinOrder: session.Participants.Count + 1),
            userId: null);
    }
}

// Link session to GameNight
var gns = GameNightSession.Create(
    gameNightEventId: gameNightEventId,
    sessionId: session.Id,
    gameId: request.GameId,
    gameTitle: await _db.SharedGames.Where(g => g.Id == request.GameId).Select(g => g.Name).FirstAsync(cancellationToken),
    playOrder: night!.GameIds.Count);
_db.GameNightSessions.Add(gns);

// Emit SessionEvent: session_created
var payload = System.Text.Json.JsonSerializer.Serialize(new
{
    gameId = request.GameId,
    gameNightEventId,
    gameNightWasCreated,
});
var createdEvent = SessionEvent.Create(
    sessionId: session.Id,
    eventType: "session_created",
    payload: payload,
    createdBy: request.UserId,
    source: "system",
    gameNightId: gameNightEventId);
_db.SessionEvents.Add(createdEvent);

if (gameNightWasCreated)
{
    var nightPayload = System.Text.Json.JsonSerializer.Serialize(new
    {
        gameNightEventId,
        title = night.Title,
        isAdHoc = true
    });
    var nightEvent = SessionEvent.Create(
        sessionId: session.Id,
        eventType: "gamenight_created",
        payload: nightPayload,
        createdBy: request.UserId,
        source: "system",
        gameNightId: gameNightEventId);
    _db.SessionEvents.Add(nightEvent);
}
else
{
    var attachPayload = System.Text.Json.JsonSerializer.Serialize(new { addedGameId = request.GameId });
    var attachEvent = SessionEvent.Create(
        sessionId: session.Id,
        eventType: "gamenight_game_added",
        payload: attachPayload,
        createdBy: request.UserId,
        source: "system",
        gameNightId: gameNightEventId);
    _db.SessionEvents.Add(attachEvent);
}

// Resolve agent and toolkit IDs for response (read-only, no clone)
var agentDefinitionId = await _db.PrivateGames
    .Where(pg => pg.SharedGameId == request.GameId && pg.UserId == request.UserId)
    .Select(pg => (Guid?)pg.AgentDefinitionId)
    .FirstOrDefaultAsync(cancellationToken);

var toolkitId = await _db.Toolkits
    .Where(t => t.GameId == request.GameId && (t.OwnerUserId == request.UserId || t.IsDefault))
    .OrderByDescending(t => t.OwnerUserId == request.UserId) // user override first
    .Select(t => (Guid?)t.Id)
    .FirstOrDefaultAsync(cancellationToken);
```

E aggiornare il `return` del metodo per includere i nuovi campi:

```csharp
return new CreateSessionResult(
    SessionId: session.Id,
    SessionCode: session.SessionCode,
    Participants: mappedParticipants, // riusa il mapping esistente
    GameNightEventId: gameNightEventId,
    GameNightWasCreated: gameNightWasCreated,
    AgentDefinitionId: agentDefinitionId,
    ToolkitId: toolkitId);
```

⚠️ **Dipendenze handler**: assicurarsi che il costruttore dell'handler iniettà `IMediator` (per chiamare `GetKbReadinessQuery`). Se non lo è, aggiungerlo:

```csharp
public CreateSessionCommandHandler(
    MeepleAiDbContext db,
    IMediator mediator,      // NEW
    /* altre dependency esistenti */)
{
    _db = db;
    _mediator = mediator;
    // ...
}
```

⚠️ **Nomi DbSet**: `_db.GameNightEvents`, `_db.GameNightSessions`, `_db.Sessions`, `_db.SessionEvents`, `_db.PrivateGames`, `_db.Toolkits`, `_db.SharedGames` (⚠️ potrebbe chiamarsi `SharedGameCatalog` o altro — verificare) potrebbero non esistere con questi nomi esatti. Verificare con:

```bash
grep -n "public DbSet" apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs
```

e adattare.

- [ ] **Step 6: Eseguire i test e iterare fino a PASS**

```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "FullyQualifiedName~CreateSessionCommandAdHocTests"
```

⚠️ Questo test dipende anche da `PauseSessionCommand` che non esiste ancora (Task 5). **Marcare temporaneamente il test con l'attributo Skip**:

```csharp
[Fact(Skip = "Depends on Task 5 PauseSessionCommand — remove Skip in Task 5 step 6")]
public async Task Handle_ExistingNightProvided_AttachesGameToNight()
```

Rimuoverlo al Task 5 step 6.

Obiettivo Task 4: gli altri 4 test passano.

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/CreateSession*.cs
git add apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/Commands/CreateSessionCommandAdHocTests.cs
git add apps/api/tests/Api.Tests/Shared/Fixtures/PostgresFixture.cs  # se hai aggiunto seeder
git commit -m "feat(session-tracking): extend CreateSessionCommand with ad-hoc GameNight and KB readiness

- New optional params GameNightEventId, GuestNames
- Pre-check: KB must be ready (via GetKbReadinessQuery)
- Creates ad-hoc GameNightEvent implicitly if not provided
- Invariant: at most 1 Active Session per GameNight
- Emits session_created, gamenight_created, gamenight_game_added SessionEvents
- Returns AgentDefinitionId and ToolkitId for frontend Hand composition"
```

---

## Task 5: PauseSessionCommand + ResumeSessionCommand

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/PauseSessionCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/PauseSessionCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/ResumeSessionCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/ResumeSessionCommandHandler.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/Commands/PauseResumeSessionTests.cs`

- [ ] **Step 1: Creare il command Pause**

Creare `PauseSessionCommand.cs`:

```csharp
using FluentValidation;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Pauses an Active session. Only the host can pause.
/// </summary>
public record PauseSessionCommand(Guid SessionId, Guid UserId) : IRequest;

public class PauseSessionCommandValidator : AbstractValidator<PauseSessionCommand>
{
    public PauseSessionCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}
```

- [ ] **Step 2: Creare l'handler Pause**

Creare `PauseSessionCommandHandler.cs`:

```csharp
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public class PauseSessionCommandHandler : IRequestHandler<PauseSessionCommand>
{
    private readonly MeepleAiDbContext _db;

    public PauseSessionCommandHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task Handle(PauseSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _db.Sessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found.");

        if (session.UserId != request.UserId)
            throw new ForbiddenException("Only the session owner can pause.");

        // Resolve GameNightId via GameNightSession link
        var gameNightId = await _db.GameNightSessions
            .Where(gns => gns.SessionId == session.Id)
            .Select(gns => (Guid?)gns.GameNightEventId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        session.Pause(); // existing domain method, throws if not Active

        // Emit SessionEvent
        var evt = SessionEvent.Create(
            sessionId: session.Id,
            eventType: "session_paused",
            payload: "{}",
            createdBy: request.UserId,
            source: "system",
            gameNightId: gameNightId);
        _db.SessionEvents.Add(evt);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
```

- [ ] **Step 3: Creare il command Resume (con invariante: auto-pausa altre Active nel night)**

Creare `ResumeSessionCommand.cs`:

```csharp
using FluentValidation;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Resumes a Paused session. Auto-pauses any other Active session in the same GameNight
/// to maintain the invariant "max 1 Active per night".
/// </summary>
public record ResumeSessionCommand(Guid SessionId, Guid UserId) : IRequest;

public class ResumeSessionCommandValidator : AbstractValidator<ResumeSessionCommand>
{
    public ResumeSessionCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}
```

Creare `ResumeSessionCommandHandler.cs`:

```csharp
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public class ResumeSessionCommandHandler : IRequestHandler<ResumeSessionCommand>
{
    private readonly MeepleAiDbContext _db;

    public ResumeSessionCommandHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task Handle(ResumeSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _db.Sessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found.");

        if (session.UserId != request.UserId)
            throw new ForbiddenException("Only the session owner can resume.");

        // Resolve GameNightId
        var gameNightId = await _db.GameNightSessions
            .Where(gns => gns.SessionId == session.Id)
            .Select(gns => gns.GameNightEventId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        // Auto-pause any other Active session in the same night
        var otherActive = await _db.GameNightSessions
            .Where(gns => gns.GameNightEventId == gameNightId && gns.SessionId != session.Id)
            .Join(_db.Sessions, gns => gns.SessionId, s => s.Id, (gns, s) => s)
            .Where(s => s.Status == SessionStatus.Active)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var other in otherActive)
        {
            other.Pause();
            var autoEvt = SessionEvent.Create(
                sessionId: other.Id,
                eventType: "session_paused",
                payload: "{\"reason\":\"auto_pause_on_resume\"}",
                createdBy: request.UserId,
                source: "system",
                gameNightId: gameNightId);
            _db.SessionEvents.Add(autoEvt);
        }

        session.Resume(); // existing domain method

        var resumedEvt = SessionEvent.Create(
            sessionId: session.Id,
            eventType: "session_resumed",
            payload: "{}",
            createdBy: request.UserId,
            source: "system",
            gameNightId: gameNightId);
        _db.SessionEvents.Add(resumedEvt);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
```

- [ ] **Step 4: Scrivere i test**

Creare `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/Commands/PauseResumeSessionTests.cs`:

```csharp
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.Tests.Shared.Fixtures;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Commands;

public class PauseResumeSessionTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public PauseResumeSessionTests(PostgresFixture fixture) { _fixture = fixture; }

    [Fact]
    public async Task Pause_ActiveSession_ChangesStatusAndEmitsDiaryEvent()
    {
        using var scope = _fixture.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, gameId) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(scope);

        var create = await mediator.Send(new CreateSessionCommand(
            userId, gameId, "GameSpecific", null, null, new List<ParticipantDto>()));

        await mediator.Send(new PauseSessionCommand(create.SessionId, userId));

        var session = await db.Sessions.FindAsync(create.SessionId);
        session!.Status.Should().Be(SessionStatus.Paused);

        var diary = await db.SessionEvents
            .Where(e => e.SessionId == create.SessionId && e.EventType == "session_paused")
            .ToListAsync();
        diary.Should().ContainSingle();
    }

    [Fact]
    public async Task Resume_Paused_ReactivatesAndAutoPausesOthersInNight()
    {
        using var scope = _fixture.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, game1) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(scope);
        var game2 = await _fixture.SeedAnotherLibraryGameAsync(scope, userId);

        // Session 1 created (Active)
        var s1 = await mediator.Send(new CreateSessionCommand(
            userId, game1, "GameSpecific", null, null, new List<ParticipantDto>()));
        await mediator.Send(new PauseSessionCommand(s1.SessionId, userId));

        // Session 2 in same night (Active)
        var s2 = await mediator.Send(new CreateSessionCommand(
            userId, game2, "GameSpecific", null, null, new List<ParticipantDto>(),
            GameNightEventId: s1.GameNightEventId));

        // Resume s1 → s2 should auto-pause
        await mediator.Send(new ResumeSessionCommand(s1.SessionId, userId));

        var sessionOne = await db.Sessions.FindAsync(s1.SessionId);
        var sessionTwo = await db.Sessions.FindAsync(s2.SessionId);

        sessionOne!.Status.Should().Be(SessionStatus.Active);
        sessionTwo!.Status.Should().Be(SessionStatus.Paused);
    }

    [Fact]
    public async Task Pause_NonExistentSession_ThrowsNotFound()
    {
        using var scope = _fixture.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var act = () => mediator.Send(new PauseSessionCommand(Guid.NewGuid(), Guid.NewGuid()));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Pause_NotOwner_ThrowsForbidden()
    {
        using var scope = _fixture.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var (userId, gameId) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(scope);

        var create = await mediator.Send(new CreateSessionCommand(
            userId, gameId, "GameSpecific", null, null, new List<ParticipantDto>()));

        var otherUser = Guid.NewGuid();
        var act = () => mediator.Send(new PauseSessionCommand(create.SessionId, otherUser));

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
```

- [ ] **Step 5: Eseguire i test**

```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "FullyQualifiedName~PauseResumeSessionTests"
```

Expected: 4/4 PASS.

- [ ] **Step 6: Rimuovere lo Skip dal test bloccato in Task 4**

Tornare a `CreateSessionCommandAdHocTests.cs`, rimuovere `[Fact(Skip = "...")]` e re-eseguire:

```bash
dotnet test --filter "FullyQualifiedName~CreateSessionCommandAdHocTests"
```

Expected: 5/5 PASS.

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/Pause*.cs
git add apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/Resume*.cs
git add apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/Commands/PauseResumeSessionTests.cs
git add apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/Commands/CreateSessionCommandAdHocTests.cs
git commit -m "feat(session-tracking): add PauseSessionCommand and ResumeSessionCommand

Resume auto-pauses other Active sessions in the same GameNight to
preserve the '1 Active per night' invariant. Both commands emit
session_paused / session_resumed diary events."
```

---

## Task 6: SetTurnOrderCommand (manual + random with seed)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/SetTurnOrderCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/SetTurnOrderCommandHandler.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/Commands/SetTurnOrderCommandTests.cs`

- [ ] **Step 1: Creare il command**

Creare `SetTurnOrderCommand.cs`:

```csharp
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Sets the turn order for a session. Manual = explicit order; Random = server-side shuffle with audit seed.
/// </summary>
public record SetTurnOrderCommand(
    Guid SessionId,
    Guid UserId,
    TurnOrderMethod Method,
    IReadOnlyList<Guid>? ManualOrder
) : IRequest<SetTurnOrderResult>;

public record SetTurnOrderResult(
    string Method,
    int? Seed,
    IReadOnlyList<Guid> Order
);

public class SetTurnOrderCommandValidator : AbstractValidator<SetTurnOrderCommand>
{
    public SetTurnOrderCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();

        RuleFor(x => x.ManualOrder)
            .NotNull()
            .NotEmpty()
            .When(x => x.Method == TurnOrderMethod.Manual)
            .WithMessage("ManualOrder is required when Method=Manual.");
    }
}
```

- [ ] **Step 2: Creare l'handler**

Creare `SetTurnOrderCommandHandler.cs`:

```csharp
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public class SetTurnOrderCommandHandler : IRequestHandler<SetTurnOrderCommand, SetTurnOrderResult>
{
    private readonly MeepleAiDbContext _db;

    public SetTurnOrderCommandHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<SetTurnOrderResult> Handle(SetTurnOrderCommand request, CancellationToken cancellationToken)
    {
        var session = await _db.Sessions
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found.");

        if (session.UserId != request.UserId)
            throw new ForbiddenException("Only the session owner can set turn order.");

        IReadOnlyList<Guid> order;
        int? seed;

        if (request.Method == TurnOrderMethod.Manual)
        {
            order = request.ManualOrder!;
            seed = null;
        }
        else
        {
            // Random: Fisher-Yates with cryptographically stable seed
            // (Guid.GetHashCode() is not stable across .NET runs; use RandomNumberGenerator)
            seed = System.Security.Cryptography.RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue);
            var shuffled = session.Participants.Select(p => p.Id).ToList();
            var rng = new Random(seed.Value);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }
            order = shuffled;
        }

        session.SetTurnOrder(request.Method, order, seed);

        var gameNightId = await _db.GameNightSessions
            .Where(gns => gns.SessionId == session.Id)
            .Select(gns => (Guid?)gns.GameNightEventId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            method = request.Method.ToString(),
            seed,
            participantIds = order
        });

        var evt = SessionEvent.Create(
            sessionId: session.Id,
            eventType: "turn_order_set",
            payload: payload,
            createdBy: request.UserId,
            source: "user",
            gameNightId: gameNightId);
        _db.SessionEvents.Add(evt);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new SetTurnOrderResult(request.Method.ToString(), seed, order);
    }
}
```

- [ ] **Step 3: Scrivere i test**

Creare `SetTurnOrderCommandTests.cs`:

```csharp
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.Infrastructure;
using Api.Tests.Shared.Fixtures;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Commands;

public class SetTurnOrderCommandTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public SetTurnOrderCommandTests(PostgresFixture fixture) { _fixture = fixture; }

    [Fact]
    public async Task Manual_PersistsExplicitOrder()
    {
        using var scope = _fixture.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, gameId) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(scope);

        var create = await mediator.Send(new CreateSessionCommand(
            userId, gameId, "GameSpecific", null, null, new List<ParticipantDto>(),
            GuestNames: new[] { "Luca", "Sara" }));

        var session = await db.Sessions.Include(s => s.Participants).FirstAsync(s => s.Id == create.SessionId);
        var reversed = session.Participants.Select(p => p.Id).Reverse().ToList();

        var result = await mediator.Send(new SetTurnOrderCommand(
            session.Id, userId, TurnOrderMethod.Manual, reversed));

        result.Method.Should().Be("Manual");
        result.Seed.Should().BeNull();
        result.Order.Should().BeEquivalentTo(reversed);

        var updated = await db.Sessions.FindAsync(session.Id);
        updated!.CurrentTurnIndex.Should().Be(0);
    }

    [Fact]
    public async Task Random_GeneratesSeedAndShufflesDeterministically()
    {
        using var scope = _fixture.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, gameId) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(scope);

        var create = await mediator.Send(new CreateSessionCommand(
            userId, gameId, "GameSpecific", null, null, new List<ParticipantDto>(),
            GuestNames: new[] { "A", "B", "C", "D", "E" }));

        var result = await mediator.Send(new SetTurnOrderCommand(
            create.SessionId, userId, TurnOrderMethod.Random, null));

        result.Method.Should().Be("Random");
        result.Seed.Should().NotBeNull();
        result.Order.Should().HaveCount(6); // Owner + 5 guests

        // Audit: diary entry contains the seed
        var diary = await db.SessionEvents
            .Where(e => e.SessionId == create.SessionId && e.EventType == "turn_order_set")
            .FirstAsync();
        diary.Payload.Should().Contain(result.Seed.ToString()!);
    }

    [Fact]
    public async Task Manual_EmptyOrder_FailsValidation()
    {
        using var scope = _fixture.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var act = () => mediator.Send(new SetTurnOrderCommand(
            Guid.NewGuid(), Guid.NewGuid(), TurnOrderMethod.Manual, Array.Empty<Guid>()));

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }
}
```

- [ ] **Step 4: Eseguire i test**

```bash
dotnet test --filter "FullyQualifiedName~SetTurnOrderCommandTests"
```

Expected: 3/3 PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/SetTurnOrder*.cs
git add apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/Commands/SetTurnOrderCommandTests.cs
git commit -m "feat(session-tracking): add SetTurnOrderCommand with manual and random shuffle

Random method uses Fisher-Yates with a recorded seed for audit replay.
Seed is persisted on Session and echoed in the turn_order_set diary event."
```

---

## Task 7: Dice Roll Endpoint + Diary Event Emission

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/RollSessionDiceCommandHandler.cs`
- Modify: `apps/api/src/Api/Routing/SessionEndpoints.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/Commands/RollSessionDiceDiaryEmissionTests.cs`

- [ ] **Step 1: Leggere l'handler esistente**

```bash
cat apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/RollSessionDiceCommandHandler.cs
```

Identificare il punto dove la `DiceRoll` viene salvata con successo nel DB.

- [ ] **Step 2: Scrivere il test failing per l'emission del diary event**

Creare `RollSessionDiceDiaryEmissionTests.cs`:

```csharp
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.Infrastructure;
using Api.Tests.Shared.Fixtures;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Commands;

public class RollSessionDiceDiaryEmissionTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public RollSessionDiceDiaryEmissionTests(PostgresFixture fixture) { _fixture = fixture; }

    [Fact]
    public async Task RollDice_EmitsDiceRolledDiaryEvent()
    {
        using var scope = _fixture.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, gameId) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(scope);

        var create = await mediator.Send(new CreateSessionCommand(
            userId, gameId, "GameSpecific", null, null, new List<ParticipantDto>()));

        var session = await db.Sessions.Include(s => s.Participants).FirstAsync(s => s.Id == create.SessionId);
        var ownerParticipant = session.Participants.First();

        var result = await mediator.Send(new RollSessionDiceCommand(
            SessionId: session.Id,
            ParticipantId: ownerParticipant.Id,
            RequesterId: userId,
            Formula: "2d6"));

        result.Total.Should().BeInRange(2, 12);

        var diaryEntry = await db.SessionEvents
            .Where(e => e.SessionId == session.Id && e.EventType == "dice_rolled")
            .OrderByDescending(e => e.Timestamp)
            .FirstAsync();

        diaryEntry.Payload.Should().Contain("\"formula\":\"2D6\"");
        diaryEntry.Payload.Should().Contain("\"total\":");
    }
}
```

- [ ] **Step 3: Eseguire il test e verificare che fallisca**

```bash
dotnet test --filter "FullyQualifiedName~RollSessionDiceDiaryEmissionTests"
```

Expected: FAIL — l'handler esistente non emette ancora il SessionEvent.

- [ ] **Step 4: Modificare `RollSessionDiceCommandHandler`**

Nell'handler esistente, **subito prima** del `SaveChangesAsync` finale (o subito dopo `_db.DiceRolls.Add(diceRoll)`), aggiungere:

```csharp
// Emit SessionEvent: dice_rolled (v2.1)
var gameNightId = await _db.GameNightSessions
    .Where(gns => gns.SessionId == request.SessionId)
    .Select(gns => (Guid?)gns.GameNightEventId)
    .FirstOrDefaultAsync(cancellationToken)
    .ConfigureAwait(false);

var diaryPayload = System.Text.Json.JsonSerializer.Serialize(new
{
    diceRollId = diceRoll.Id,
    formula = diceRoll.Formula,
    rolls = diceRoll.GetRolls(),
    modifier = diceRoll.Modifier,
    total = diceRoll.Total,
    label = diceRoll.Label,
    participantId = request.ParticipantId
});

var diaryEvent = SessionEvent.Create(
    sessionId: request.SessionId,
    eventType: "dice_rolled",
    payload: diaryPayload,
    createdBy: request.RequesterId,
    source: "user",
    gameNightId: gameNightId);

_db.SessionEvents.Add(diaryEvent);
```

Assicurarsi che il using statement per `SessionEvent` sia presente in cima al file:

```csharp
using Api.BoundedContexts.SessionTracking.Domain.Entities;
```

- [ ] **Step 5: Eseguire il test e verificare PASS**

```bash
dotnet test --filter "FullyQualifiedName~RollSessionDiceDiaryEmissionTests"
```

Expected: PASS.

- [ ] **Step 6: Identificare il pattern di recupero user id**

Prima di scrivere l'endpoint, cercare come gli altri endpoint autenticati recuperano l'user id dal `HttpContext`:

```bash
grep -rn "NameIdentifier\|GetUserId\|CurrentUser\|User.FindFirst" apps/api/src/Api/Routing/ | head -10
```

Prendere il pattern ricorrente (es. `httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)` oppure un helper statico tipo `CurrentUserAccessor.GetUserId(httpContext)`). **Usare quel pattern esatto** in tutti gli endpoint nuovi di questo plan. Nel resto del plan indicherò lo slot con `<GET_USER_ID_HELPER>` — sostituire con il pattern reale individuato.

- [ ] **Step 7: Aggiungere l'endpoint HTTP dice-rolls**

Aprire `apps/api/src/Api/Routing/SessionEndpoints.cs`. Trovare il metodo di estensione `MapSessionEndpoints` (o simile). Aggiungere il nuovo endpoint:

```csharp
group.MapPost("/{sessionId:guid}/dice-rolls", async (
    Guid sessionId,
    RollDiceRequestBody body,
    IMediator mediator,
    HttpContext http) =>
{
    var userId = <GET_USER_ID_HELPER>; // sostituire con il pattern individuato allo Step 6
    var cmd = new RollSessionDiceCommand(
        SessionId: sessionId,
        ParticipantId: body.ParticipantId,
        RequesterId: userId,
        Formula: body.Formula,
        Label: body.Label);

    var result = await mediator.Send(cmd);
    return Results.Created($"/api/v1/sessions/{sessionId}/dice-rolls/{result.DiceRollId}", result);
})
.WithName("RollSessionDice")
.WithOpenApi();

// ...
// body record at bottom of file or in DTOs folder:
public record RollDiceRequestBody(Guid ParticipantId, string Formula, string? Label);
```

⚠️ Cerca l'endpoint group prefix esistente in `SessionEndpoints.cs` per capire se il path base è `/api/v1/sessions` o diverso. Adatta di conseguenza.

- [ ] **Step 8: Smoke test manuale**

Lanciare l'API:

```bash
cd apps/api/src/Api
dotnet run --urls http://localhost:8080
```

Con curl (o Postman), dopo login:

```bash
# Crea session (assumi auth cookie già impostato)
curl -X POST http://localhost:8080/api/v1/sessions \
  -H "Content-Type: application/json" \
  --cookie-jar cookies.txt --cookie cookies.txt \
  -d '{"userId":"...","gameId":"...","sessionType":"GameSpecific","participants":[]}'

# Tira dado
curl -X POST http://localhost:8080/api/v1/sessions/<session-id>/dice-rolls \
  -H "Content-Type: application/json" \
  --cookie cookies.txt \
  -d '{"participantId":"<pid>","formula":"2d6"}'
```

Expected: 201 Created con body `{ diceRollId, formula: "2D6", rolls: [...], total: <2..12>, ... }`.

- [ ] **Step 9: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/RollSessionDiceCommandHandler.cs
git add apps/api/src/Api/Routing/SessionEndpoints.cs
git add apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/Commands/RollSessionDiceDiaryEmissionTests.cs
git commit -m "feat(session-tracking): emit dice_rolled diary event + add HTTP endpoint

- POST /api/v1/sessions/{id}/dice-rolls wires existing RollSessionDiceCommand
- Handler now emits SessionEvent(dice_rolled) with formula, rolls, total, participant"
```

---

## Task 8: UpdateScoreCommand with diary history

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/UpdateScoreCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/UpdateScoreCommandHandler.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/Commands/UpdateScoreCommandTests.cs`

- [ ] **Step 1: Creare il command**

Creare `UpdateScoreCommand.cs`:

```csharp
using FluentValidation;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Updates (or creates) a score entry for a participant.
/// Emits a score_updated diary event with oldValue/newValue for audit and undo.
/// </summary>
public record UpdateScoreCommand(
    Guid SessionId,
    Guid ParticipantId,
    Guid RequesterId,
    decimal NewValue,
    int? RoundNumber,
    string? Category,
    string? Reason
) : IRequest<UpdateScoreResult>;

public record UpdateScoreResult(
    Guid ScoreEntryId,
    decimal OldValue,
    decimal NewValue
);

public class UpdateScoreCommandValidator : AbstractValidator<UpdateScoreCommand>
{
    public UpdateScoreCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.ParticipantId).NotEmpty();
        RuleFor(x => x.RequesterId).NotEmpty();
        // NOTE: RoundNumber and Category are both optional per spec §4.6.
        // A "total" score without round nor category is valid for games like Azul.
        RuleFor(x => x.RoundNumber).GreaterThan(0).When(x => x.RoundNumber.HasValue);
        RuleFor(x => x.Category)
            .MaximumLength(50);
        RuleFor(x => x.Reason)
            .MaximumLength(200);
    }
}
```

- [ ] **Step 2: Creare l'handler**

Creare `UpdateScoreCommandHandler.cs`:

```csharp
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public class UpdateScoreCommandHandler : IRequestHandler<UpdateScoreCommand, UpdateScoreResult>
{
    private readonly MeepleAiDbContext _db;

    public UpdateScoreCommandHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<UpdateScoreResult> Handle(UpdateScoreCommand request, CancellationToken cancellationToken)
    {
        var session = await _db.Sessions
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found.");

        if (session.UserId != request.RequesterId)
            throw new ForbiddenException("Only the session owner can update scores.");

        var participantExists = await _db.Set<Participant>()
            .AnyAsync(p => p.Id == request.ParticipantId && p.SessionId == request.SessionId, cancellationToken)
            .ConfigureAwait(false);

        if (!participantExists)
            throw new NotFoundException($"Participant {request.ParticipantId} not found in session.");

        var existing = await _db.ScoreEntries
            .FirstOrDefaultAsync(e =>
                e.SessionId == request.SessionId
                && e.ParticipantId == request.ParticipantId
                && e.RoundNumber == request.RoundNumber
                && e.Category == request.Category,
                cancellationToken)
            .ConfigureAwait(false);

        decimal oldValue;
        ScoreEntry entry;

        if (existing is null)
        {
            oldValue = 0m;
            // ⚠️ ScoreEntry.Create currently throws if both roundNumber and category are null.
            // If the spec allows a pure "total" score, the domain factory must be relaxed first.
            // For this plan: default to a synthetic "total" category when both are null, to avoid
            // changing domain invariants in this PR.
            var effectiveCategory = request.Category;
            int? effectiveRound = request.RoundNumber;
            if (!effectiveRound.HasValue && string.IsNullOrWhiteSpace(effectiveCategory))
                effectiveCategory = "total";

            entry = ScoreEntry.Create(
                sessionId: request.SessionId,
                participantId: request.ParticipantId,
                scoreValue: request.NewValue,
                createdBy: request.RequesterId,
                roundNumber: effectiveRound,
                category: effectiveCategory);
            _db.ScoreEntries.Add(entry);
        }
        else
        {
            oldValue = existing.ScoreValue;
            existing.UpdateScore(request.NewValue);
            entry = existing;
        }

        var gameNightId = await _db.GameNightSessions
            .Where(gns => gns.SessionId == request.SessionId)
            .Select(gns => (Guid?)gns.GameNightEventId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            participantId = request.ParticipantId,
            oldValue,
            newValue = request.NewValue,
            roundNumber = request.RoundNumber,
            category = request.Category,
            reason = request.Reason
        });

        var diaryEvent = SessionEvent.Create(
            sessionId: request.SessionId,
            eventType: "score_updated",
            payload: payload,
            createdBy: request.RequesterId,
            source: "user",
            gameNightId: gameNightId);
        _db.SessionEvents.Add(diaryEvent);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new UpdateScoreResult(entry.Id, oldValue, request.NewValue);
    }
}
```

- [ ] **Step 3: Scrivere i test**

Creare `UpdateScoreCommandTests.cs`:

```csharp
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.Infrastructure;
using Api.Tests.Shared.Fixtures;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Commands;

public class UpdateScoreCommandTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public UpdateScoreCommandTests(PostgresFixture fixture) { _fixture = fixture; }

    [Fact]
    public async Task FirstUpdate_CreatesEntry_WithOldValueZero()
    {
        using var scope = _fixture.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, gameId) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(scope);

        var create = await mediator.Send(new CreateSessionCommand(
            userId, gameId, "GameSpecific", null, null, new List<ParticipantDto>()));

        var session = await db.Sessions.Include(s => s.Participants).FirstAsync(s => s.Id == create.SessionId);
        var pid = session.Participants.First().Id;

        var result = await mediator.Send(new UpdateScoreCommand(
            session.Id, pid, userId, NewValue: 45m, RoundNumber: 1, Category: null, Reason: null));

        result.OldValue.Should().Be(0m);
        result.NewValue.Should().Be(45m);

        var diary = await db.SessionEvents
            .Where(e => e.SessionId == session.Id && e.EventType == "score_updated")
            .ToListAsync();
        diary.Should().ContainSingle();
        diary[0].Payload.Should().Contain("\"oldValue\":0").And.Contain("\"newValue\":45");
    }

    [Fact]
    public async Task SecondUpdate_RecordsOldValueInDiary()
    {
        using var scope = _fixture.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, gameId) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(scope);

        var create = await mediator.Send(new CreateSessionCommand(
            userId, gameId, "GameSpecific", null, null, new List<ParticipantDto>()));

        var session = await db.Sessions.Include(s => s.Participants).FirstAsync(s => s.Id == create.SessionId);
        var pid = session.Participants.First().Id;

        await mediator.Send(new UpdateScoreCommand(session.Id, pid, userId, 45m, 1, null, null));
        var second = await mediator.Send(new UpdateScoreCommand(session.Id, pid, userId, 52m, 1, null, "correzione"));

        second.OldValue.Should().Be(45m);
        second.NewValue.Should().Be(52m);

        var diary = await db.SessionEvents
            .Where(e => e.SessionId == session.Id && e.EventType == "score_updated")
            .OrderBy(e => e.Timestamp)
            .ToListAsync();
        diary.Should().HaveCount(2);
        diary[1].Payload.Should().Contain("\"oldValue\":45").And.Contain("\"newValue\":52").And.Contain("correzione");
    }

    [Fact]
    public async Task UpdateScore_NotOwner_Forbidden()
    {
        using var scope = _fixture.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var (userId, gameId) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(scope);

        var create = await mediator.Send(new CreateSessionCommand(
            userId, gameId, "GameSpecific", null, null, new List<ParticipantDto>()));
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var session = await db.Sessions.Include(s => s.Participants).FirstAsync(s => s.Id == create.SessionId);
        var pid = session.Participants.First().Id;

        var act = () => mediator.Send(new UpdateScoreCommand(
            session.Id, pid, Guid.NewGuid(), 10m, 1, null, null));

        await act.Should().ThrowAsync<Api.Middleware.Exceptions.ForbiddenException>();
    }
}
```

- [ ] **Step 4: Eseguire e verificare PASS**

```bash
dotnet test --filter "FullyQualifiedName~UpdateScoreCommandTests"
```

Expected: 3/3 PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/UpdateScore*.cs
git add apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/Commands/UpdateScoreCommandTests.cs
git commit -m "feat(session-tracking): add UpdateScoreCommand with append-only diary history

ScoreEntry remains mutable (projection), but every change is recorded
as a score_updated SessionEvent with oldValue/newValue/reason. Undo is
achieved by sending a new UpdateScoreCommand with the previous value."
```

---

## Task 9: Diary Queries — GetSessionDiary + GetGameNightDiary

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/DTOs/SessionEventDto.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Queries/GetSessionDiaryQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Queries/GetSessionDiaryQueryHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Queries/GetGameNightDiaryQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Queries/GetGameNightDiaryQueryHandler.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/Queries/DiaryQueryTests.cs`

- [ ] **Step 1: DTO**

Creare `SessionEventDto.cs`:

```csharp
namespace Api.BoundedContexts.SessionTracking.Application.DTOs;

public record SessionEventDto(
    Guid Id,
    Guid SessionId,
    Guid? GameNightId,
    string EventType,
    DateTime Timestamp,
    string Payload,
    Guid? CreatedBy,
    string? Source
);
```

- [ ] **Step 2: GetSessionDiaryQuery**

Creare `GetSessionDiaryQuery.cs`:

```csharp
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

public record GetSessionDiaryQuery(
    Guid SessionId,
    IReadOnlyList<string>? EventTypes,
    DateTime? Since,
    int Limit = 100
) : IRequest<IReadOnlyList<SessionEventDto>>;
```

Handler:

```csharp
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

public class GetSessionDiaryQueryHandler : IRequestHandler<GetSessionDiaryQuery, IReadOnlyList<SessionEventDto>>
{
    private readonly MeepleAiDbContext _db;

    public GetSessionDiaryQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<IReadOnlyList<SessionEventDto>> Handle(GetSessionDiaryQuery request, CancellationToken cancellationToken)
    {
        var query = _db.SessionEvents
            .AsNoTracking()
            .Where(e => e.SessionId == request.SessionId && !e.IsDeleted);

        if (request.EventTypes is { Count: > 0 })
            query = query.Where(e => request.EventTypes.Contains(e.EventType));

        if (request.Since.HasValue)
            query = query.Where(e => e.Timestamp >= request.Since.Value);

        var entries = await query
            .OrderBy(e => e.Timestamp)
            .ThenBy(e => e.Id)
            .Take(request.Limit)
            .Select(e => new SessionEventDto(
                e.Id, e.SessionId, e.GameNightId, e.EventType, e.Timestamp, e.Payload, e.CreatedBy, e.Source))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entries;
    }
}
```

- [ ] **Step 3: GetGameNightDiaryQuery (UNION)**

Creare `GetGameNightDiaryQuery.cs`:

```csharp
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

public record GetGameNightDiaryQuery(
    Guid GameNightEventId,
    IReadOnlyList<string>? EventTypes,
    DateTime? Since,
    int Limit = 500
) : IRequest<IReadOnlyList<SessionEventDto>>;
```

Handler:

```csharp
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

public class GetGameNightDiaryQueryHandler : IRequestHandler<GetGameNightDiaryQuery, IReadOnlyList<SessionEventDto>>
{
    private readonly MeepleAiDbContext _db;

    public GetGameNightDiaryQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<IReadOnlyList<SessionEventDto>> Handle(GetGameNightDiaryQuery request, CancellationToken cancellationToken)
    {
        var query = _db.SessionEvents
            .AsNoTracking()
            .Where(e => e.GameNightId == request.GameNightEventId && !e.IsDeleted);

        if (request.EventTypes is { Count: > 0 })
            query = query.Where(e => request.EventTypes.Contains(e.EventType));

        if (request.Since.HasValue)
            query = query.Where(e => e.Timestamp >= request.Since.Value);

        var entries = await query
            .OrderBy(e => e.Timestamp)
            .ThenBy(e => e.Id)
            .Take(request.Limit)
            .Select(e => new SessionEventDto(
                e.Id, e.SessionId, e.GameNightId, e.EventType, e.Timestamp, e.Payload, e.CreatedBy, e.Source))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entries;
    }
}
```

- [ ] **Step 4: Test**

Creare `DiaryQueryTests.cs`:

```csharp
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.Infrastructure;
using Api.Tests.Shared.Fixtures;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Queries;

public class DiaryQueryTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public DiaryQueryTests(PostgresFixture fixture) { _fixture = fixture; }

    [Fact]
    public async Task GetSessionDiary_ReturnsEventsInChronologicalOrder()
    {
        using var scope = _fixture.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, gameId) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(scope);

        var create = await mediator.Send(new CreateSessionCommand(
            userId, gameId, "GameSpecific", null, null, new List<ParticipantDto>()));

        var session = await db.Sessions.Include(s => s.Participants).FirstAsync(s => s.Id == create.SessionId);
        var pid = session.Participants.First().Id;

        await mediator.Send(new UpdateScoreCommand(session.Id, pid, userId, 10m, 1, null, null));
        await mediator.Send(new UpdateScoreCommand(session.Id, pid, userId, 20m, 1, null, null));

        var diary = await mediator.Send(new GetSessionDiaryQuery(session.Id, null, null));

        diary.Should().HaveCountGreaterThanOrEqualTo(3); // session_created + 2 score_updated (+ optionally gamenight_created)
        diary.Select(e => e.Timestamp).Should().BeInAscendingOrder();
        diary.Where(e => e.EventType == "score_updated").Should().HaveCount(2);
    }

    [Fact]
    public async Task GetGameNightDiary_UnionsAllSessionsOfNight()
    {
        using var scope = _fixture.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, game1) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(scope);
        var game2 = await _fixture.SeedAnotherLibraryGameAsync(scope, userId);

        var s1 = await mediator.Send(new CreateSessionCommand(
            userId, game1, "GameSpecific", null, null, new List<ParticipantDto>()));
        await mediator.Send(new PauseSessionCommand(s1.SessionId, userId));
        var s2 = await mediator.Send(new CreateSessionCommand(
            userId, game2, "GameSpecific", null, null, new List<ParticipantDto>(),
            GameNightEventId: s1.GameNightEventId));

        var diary = await mediator.Send(new GetGameNightDiaryQuery(
            s1.GameNightEventId, null, null));

        diary.Should().NotBeEmpty();
        diary.Select(e => e.SessionId).Distinct().Should().HaveCount(2);
        diary.All(e => e.GameNightId == s1.GameNightEventId).Should().BeTrue();
    }

    [Fact]
    public async Task GetSessionDiary_FilteredByEventType_ReturnsOnlyMatching()
    {
        using var scope = _fixture.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, gameId) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(scope);

        var create = await mediator.Send(new CreateSessionCommand(
            userId, gameId, "GameSpecific", null, null, new List<ParticipantDto>()));
        var session = await db.Sessions.Include(s => s.Participants).FirstAsync(s => s.Id == create.SessionId);
        var pid = session.Participants.First().Id;
        await mediator.Send(new UpdateScoreCommand(session.Id, pid, userId, 10m, 1, null, null));

        var filtered = await mediator.Send(new GetSessionDiaryQuery(
            session.Id,
            EventTypes: new[] { "score_updated" },
            Since: null));

        filtered.Should().ContainSingle();
        filtered[0].EventType.Should().Be("score_updated");
    }
}
```

- [ ] **Step 5: Eseguire i test**

```bash
dotnet test --filter "FullyQualifiedName~DiaryQueryTests"
```

Expected: 3/3 PASS.

- [ ] **Step 6: Aggiungere gli endpoint in `SessionFlowEndpoints.cs`**

Creare nuovo file `apps/api/src/Api/Routing/SessionFlowEndpoints.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using MediatR;

namespace Api.Routing;

public static class SessionFlowEndpoints
{
    public static void MapSessionFlowEndpoints(this IEndpointRouteBuilder app)
    {
        var sessions = app.MapGroup("/api/v1/sessions").RequireAuthorization();
        var games = app.MapGroup("/api/v1/games").RequireAuthorization();
        var nights = app.MapGroup("/api/v1/game-nights").RequireAuthorization();

        // KB readiness
        games.MapGet("/{gameId:guid}/kb-readiness", async (Guid gameId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetKbReadinessQuery(gameId));
            return Results.Ok(result);
        })
        .WithName("GetKbReadiness")
        .WithOpenApi();

        // Pause / Resume
        sessions.MapPost("/{sessionId:guid}/pause", async (Guid sessionId, IMediator mediator, HttpContext http) =>
        {
            var userId = <GET_USER_ID_HELPER>;
            await mediator.Send(new PauseSessionCommand(sessionId, userId));
            return Results.Ok();
        })
        .WithName("PauseSession")
        .WithOpenApi();

        sessions.MapPost("/{sessionId:guid}/resume", async (Guid sessionId, IMediator mediator, HttpContext http) =>
        {
            var userId = <GET_USER_ID_HELPER>;
            await mediator.Send(new ResumeSessionCommand(sessionId, userId));
            return Results.Ok();
        })
        .WithName("ResumeSession")
        .WithOpenApi();

        // Turn order
        sessions.MapPut("/{sessionId:guid}/turn-order", async (
            Guid sessionId, SetTurnOrderRequestBody body, IMediator mediator, HttpContext http) =>
        {
            var userId = <GET_USER_ID_HELPER>;
            var method = Enum.Parse<TurnOrderMethod>(body.Method, ignoreCase: true);
            var result = await mediator.Send(new SetTurnOrderCommand(sessionId, userId, method, body.Order));
            return Results.Ok(result);
        })
        .WithName("SetTurnOrder")
        .WithOpenApi();

        // Score update
        sessions.MapPost("/{sessionId:guid}/scores", async (
            Guid sessionId, UpdateScoreRequestBody body, IMediator mediator, HttpContext http) =>
        {
            var userId = <GET_USER_ID_HELPER>;
            var result = await mediator.Send(new UpdateScoreCommand(
                sessionId, body.ParticipantId, userId, body.NewValue, body.RoundNumber, body.Category, body.Reason));
            return Results.Ok(result);
        })
        .WithName("UpdateScore")
        .WithOpenApi();

        // Session diary
        sessions.MapGet("/{sessionId:guid}/diary", async (
            Guid sessionId, string? eventTypes, DateTime? since, int? limit, IMediator mediator) =>
        {
            var types = string.IsNullOrWhiteSpace(eventTypes) ? null : eventTypes.Split(',');
            var result = await mediator.Send(new GetSessionDiaryQuery(sessionId, types, since, limit ?? 100));
            return Results.Ok(result);
        })
        .WithName("GetSessionDiary")
        .WithOpenApi();

        // GameNight diary
        nights.MapGet("/{gameNightId:guid}/diary", async (
            Guid gameNightId, string? eventTypes, DateTime? since, int? limit, IMediator mediator) =>
        {
            var types = string.IsNullOrWhiteSpace(eventTypes) ? null : eventTypes.Split(',');
            var result = await mediator.Send(new GetGameNightDiaryQuery(gameNightId, types, since, limit ?? 500));
            return Results.Ok(result);
        })
        .WithName("GetGameNightDiary")
        .WithOpenApi();
    }

    public record SetTurnOrderRequestBody(string Method, IReadOnlyList<Guid>? Order);
    public record UpdateScoreRequestBody(Guid ParticipantId, decimal NewValue, int? RoundNumber, string? Category, string? Reason);
}
```

⚠️ `<GET_USER_ID_HELPER>` = lo stesso slot identificato in Task 7 step 6. Usare il pattern reale individuato lì (es. `http.User.FindFirstValue(ClaimTypes.NameIdentifier)!.ToGuid()` o un helper custom del progetto).

- [ ] **Step 7: Registrare gli endpoint in `Program.cs`**

Aprire `apps/api/src/Api/Program.cs`. Dopo altre chiamate `app.MapXxx()` (cercare ad es. `MapSessionEndpoints`), aggiungere:

```csharp
app.MapSessionFlowEndpoints();
```

- [ ] **Step 8: Build e smoke test**

```bash
cd apps/api/src/Api
dotnet build
```

Expected: build successful, zero errori.

```bash
dotnet run --urls http://localhost:8080
```

In un altro terminale, chiamare:

```bash
curl http://localhost:8080/api/v1/games/<gameId>/kb-readiness --cookie cookies.txt
```

Expected: 200 con body `{ isReady, state, vectorDocumentCount, failedPdfCount, warnings }`.

- [ ] **Step 9: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Application/DTOs/SessionEventDto.cs
git add apps/api/src/Api/BoundedContexts/SessionTracking/Application/Queries/GetSessionDiaryQuery*.cs
git add apps/api/src/Api/BoundedContexts/SessionTracking/Application/Queries/GetGameNightDiaryQuery*.cs
git add apps/api/src/Api/Routing/SessionFlowEndpoints.cs
git add apps/api/src/Api/Program.cs
git add apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/Queries/DiaryQueryTests.cs
git commit -m "feat(session-tracking): add diary queries and SessionFlow HTTP endpoints

- GetSessionDiaryQuery / GetGameNightDiaryQuery with filtering
- SessionFlowEndpoints: kb-readiness, pause/resume, turn-order, scores, diary
- Registered in Program.cs via MapSessionFlowEndpoints"
```

---

## Task 10: Full integration test + PR

**Files:**
- Create: `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Integration/SessionFlowE2ETests.cs`

- [ ] **Step 1: Scrivere il test end-to-end happy path**

```csharp
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.Infrastructure;
using Api.Tests.Shared.Fixtures;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests.BoundedContexts.SessionTracking.Integration;

public class SessionFlowE2ETests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public SessionFlowE2ETests(PostgresFixture fixture) { _fixture = fixture; }

    [Fact]
    public async Task HappyPath_StartSession_Rolls_Scores_Diary()
    {
        using var scope = _fixture.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, gameId) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(scope);

        // 1. Start session with 2 guest players
        var create = await mediator.Send(new CreateSessionCommand(
            userId, gameId, "GameSpecific", null, null, new List<ParticipantDto>(),
            GuestNames: new[] { "Luca", "Sara" }));

        create.GameNightWasCreated.Should().BeTrue();

        var session = await db.Sessions.Include(s => s.Participants).FirstAsync(s => s.Id == create.SessionId);
        session.Participants.Should().HaveCount(3);

        // 2. Set random turn order
        var turnOrder = await mediator.Send(new SetTurnOrderCommand(
            session.Id, userId, TurnOrderMethod.Random, null));
        turnOrder.Seed.Should().NotBeNull();

        // 3. Each participant rolls dice
        foreach (var p in session.Participants)
        {
            var roll = await mediator.Send(new RollSessionDiceCommand(
                session.Id, p.Id, userId, "2d6"));
            roll.Total.Should().BeInRange(2, 12);
        }

        // 4. Update scores for each participant
        foreach (var p in session.Participants)
        {
            await mediator.Send(new UpdateScoreCommand(
                session.Id, p.Id, userId, 25m, 1, null, null));
        }

        // 5. Verify diary has: session_created, gamenight_created, turn_order_set, 3x dice_rolled, 3x score_updated
        var diary = await mediator.Send(new GetSessionDiaryQuery(session.Id, null, null, 100));

        diary.Select(e => e.EventType).Should().Contain(new[]
        {
            "session_created", "gamenight_created", "turn_order_set", "dice_rolled", "score_updated"
        });
        diary.Count(e => e.EventType == "dice_rolled").Should().Be(3);
        diary.Count(e => e.EventType == "score_updated").Should().Be(3);

        // 6. Pause and start a second game in the same night
        await mediator.Send(new PauseSessionCommand(session.Id, userId));

        var game2 = await _fixture.SeedAnotherLibraryGameAsync(scope, userId);
        var second = await mediator.Send(new CreateSessionCommand(
            userId, game2, "GameSpecific", null, null, new List<ParticipantDto>(),
            GameNightEventId: create.GameNightEventId));

        second.GameNightWasCreated.Should().BeFalse();

        // 7. GameNight diary contains events from both sessions
        var nightDiary = await mediator.Send(new GetGameNightDiaryQuery(
            create.GameNightEventId, null, null, 500));
        nightDiary.Select(e => e.SessionId).Distinct().Should().HaveCount(2);
    }
}
```

- [ ] **Step 2: Eseguire il test E2E**

```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "FullyQualifiedName~SessionFlowE2ETests"
```

Expected: PASS.

- [ ] **Step 3: Eseguire TUTTI i test del plan**

```bash
dotnet test --filter "FullyQualifiedName~SessionTurnOrderTests|GameNightEventAdHocTests|GetKbReadinessQueryHandlerTests|CreateSessionCommandAdHocTests|PauseResumeSessionTests|SetTurnOrderCommandTests|RollSessionDiceDiaryEmissionTests|UpdateScoreCommandTests|DiaryQueryTests|SessionFlowE2ETests"
```

Expected: tutti PASS.

- [ ] **Step 4: Run full suite (no regression)**

```bash
dotnet test
```

Expected: tutti i test esistenti passano ancora + quelli nuovi.

Se qualche test esistente **fallisce**, il commit Task 4 (`CreateSessionCommand` extension) potrebbe aver rotto qualcosa. Investigare (NON skippare) e fixare.

- [ ] **Step 5: Commit finale del test E2E**

```bash
git add apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Integration/SessionFlowE2ETests.cs
git commit -m "test(session-tracking): add Session Flow v2.1 end-to-end integration test

Covers full happy path: start session with guests, random turn order,
dice rolls, score updates, diary verification, pause, second game in
same night, game night diary union."
```

- [ ] **Step 6: Push branch**

```bash
git push -u origin feature/session-flow-v2.1-backend
```

- [ ] **Step 7: Aprire PR verso `main-dev`**

```bash
gh pr create --base main-dev --title "feat(session-flow): backend v2.1 implementation" --body "$(cat <<'EOF'
## Summary

Implements backend for Session Flow v2.1 (spec: `docs/superpowers/specs/2026-04-09-session-flow-v2.1.md`).

- KB readiness pre-check before starting a session
- `CreateSessionCommand` extended with ad-hoc `GameNightEvent` auto-creation
- `GameNightEvent.CreateAdHoc` + `AttachAdditionalGame` for spontaneous multi-game evenings
- `PauseSessionCommand` / `ResumeSessionCommand` with "1 Active per night" invariant
- `SetTurnOrderCommand` (manual + random with audit seed)
- Dice roll diary emission + new HTTP endpoint
- `UpdateScoreCommand` with append-only diary history
- Diary queries per-session and per-GameNight (UNION)
- New `SessionFlowEndpoints` routing

Zero breaking changes on existing aggregates. Only 4 new columns on `Session` + 1 new enum value on `GameNightStatus`.

## Test plan

- [x] Unit: Session turn order domain tests
- [x] Unit: GameNightEvent ad-hoc tests
- [x] Integration: GetKbReadinessQuery
- [x] Integration: CreateSessionCommand ad-hoc + KB check + invariant (5 scenarios)
- [x] Integration: Pause/Resume with auto-pause (4 scenarios)
- [x] Integration: SetTurnOrderCommand (3 scenarios)
- [x] Integration: Dice roll diary emission
- [x] Integration: UpdateScoreCommand (3 scenarios)
- [x] Integration: Diary queries (3 scenarios)
- [x] E2E: full happy path (SessionFlowE2ETests)
- [x] Full `dotnet test` suite — zero regressions

## Out of scope (follow-up plans)

- Frontend Contextual Hand + Session/GameNight pages
- Playwright E2E
- SSE diary stream (v2.2)
- `AdvanceTurnCommand` (can be added in Plan 1bis)
- GameNight completion cascade (can be added in Plan 1bis)
EOF
)"
```

⚠️ Verificare il comando `gh pr create` con la sintassi Windows Git Bash se disponibile. Su PowerShell si può usare `--body-file` con file temp (vedi memory: `pr body windows` → `--body-file`).

- [ ] **Step 8: Verificare che la CI passi**

```bash
gh pr checks
```

Expected: tutti i check verdi.

---

## Self-Review Checklist

Dopo aver eseguito i 10 task, il worker verifica:

- [ ] Spec coverage: ogni gap G1..G10 ha un task corrispondente?
  - G1 → Task 4+5 ✓
  - G2 → Task 2 ✓
  - G3 → Task 3 ✓
  - G4 → Task 1+6 ✓
  - G5 → Task 7 ✓
  - G6 → Task 8 ✓
  - G7 → Task 9 ✓
  - G8 → Task 5 ✓
  - G9 → ❌ (AdvanceTurn rimandato a Plan 1bis)
  - G10 → ❌ (Finalize cascade rimandato a Plan 1bis)
- [ ] No placeholder (TBD, TODO, "implement later")
- [ ] Type consistency: i nomi usati in task posteriori matchano quelli definiti nei task precedenti?
  - `TurnOrderMethod` (enum) ✓ definito in Task 1, usato in Task 6
  - `CreateSessionCommand` (con `GameNightEventId`, `GuestNames`) ✓ esteso in Task 4, usato in Task 5, 9, 10
  - `GameNightStatus.InProgress` ✓ aggiunto in Task 1, usato in Task 2
  - `PauseSessionCommand` ✓ creato in Task 5, usato in Task 9, 10
- [ ] Ogni task ha: test → implementazione → test pass → commit
- [ ] Ogni commit ha messaggio conforme a CLAUDE.md (`feat(scope): description`)

---

## Out of Scope / Deferred

I seguenti elementi della spec v2.1 sono **esplicitamente fuori scope** di questo plan e saranno affrontati in plan successivi:

### Plan 1bis (backend completion, ~4 task)

| Spec ref | Item | Ragione differimento |
|---|---|---|
| §6.1 | `GET /api/v1/sessions/current` (orphan recovery NFR-9) | Non critico per happy path backend, può essere aggiunto dopo |
| §6.6 | `POST /api/v1/game-nights/{id}/complete` (cascade finalize) | Richiede `FinalizeSessionCommand` extension + new `CompleteGameNightCommand` |
| §6.4 | Cursor-based pagination nelle diary queries | Per ora `.Take(limit)` offset-based; cursor in v2.2 |
| §4.5 / Domain | `AdvanceTurnCommand` + `Session.AdvanceTurn()` domain method | G9 — richiede endpoint e integrazione UI |
| §7 | Scenario "Chiusura serata" Gherkin | Dipende da `CompleteGameNightCommand` |
| §6.3 | `POST /sessions/{id}/notes` dedicated endpoint | `SessionNote` esiste già come entity, manca solo wiring |

### Plan 2 (frontend, separato)

- Gap G11-G15: Zustand store, ContextualHand components, Session/GameNight pages
- E2E Playwright (Gherkin §7 completo)

### Nota sulla concurrency della 1-Active invariant

Il plan implementa l'invariante sia a **livello handler** (read-check) sia a **livello DB** (unique partial index aggiunto in Task 1 step 8bis). In produzione sotto carico, il DB index è il safety net definitivo: se due request simultanee passano il check handler, una delle due fallirà sul `SaveChangesAsync` con `DbUpdateException` (violazione unique index) e va convertita in `ConflictException` nel catch block del handler Task 4. Istruzione aggiunta al Task 4 step 8bis.
