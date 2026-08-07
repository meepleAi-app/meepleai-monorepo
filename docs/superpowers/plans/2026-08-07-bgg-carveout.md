# Carve-out BGG re-upload (#3590 Slice B) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dare agli admin un trigger per ri-ospitare la cover BGG di un gioco già a catalogo, e chiudere il bug latente che oggi cancella quella cover a ogni update.

**Architecture:** Un comando CQRS dedicato riusa il downloader server-to-server già sanzionato da ADR-059 §2 (`IBggCoverDownloader` + `BggCoverUploadPipeline`), oggi cablato solo alla creazione da PDF. **Non** passa da `SetManualCover`: `BggHostDenyList` resta intatta sul path a URL arbitrario, che è esattamente il suo scopo. Prerequisito: l'aggregato acquisisce `BggCoverR2Key` (oggi assente) e il mapper di persistenza smette di azzerarlo.

**Tech Stack:** .NET 9 + MediatR + EF Core, xUnit/FluentAssertions/Moq.

## Global Constraints

- Branch da `main-dev` aggiornato (le tre PR del lotto sono mergiate).
- **CQRS**: endpoint con solo `IMediator.Send()`.
- **ADR-059 §2**: il path server-to-server admin verso BGG è legittimo per *facts* e per il re-hosting di asset lato admin. Il freeze #2123 riguarda le richieste **browser** verso host geekdo e non è toccato.
- **`BggHostDenyList` NON va allentata.** Se un test o un handler la aggira, il design è sbagliato.
- Eccezioni: `NotFoundException` (404), `ConflictException` (409) — mai `InvalidOperationException` (500).
- Working dir: `D:/Repositories/meepleai-monorepo-main/.claude/worktrees/i3583`.

## Scoperta che cambia il piano (verificata in sessione)

`SharedGameRepository.Update()` fa `MapToEntity(aggregato)` + `DbContext.Update(entity)` su **grafo detached**: marca tutte le colonne come Modified. Il mapper **non scrive** `BggCoverR2Key` (e `MapToDomain` non lo legge), quindi:

```
UPDATE shared_games SET bgg_cover_r2_key = NULL ...
```

**Provato empiricamente**: un load → `Update()` → `SaveChanges()` azzera una `BggCoverR2Key` preesistente (`Expected "bgg-covers/13/cover", but found <null>`).

Due conseguenze:
1. È un **bug di perdita dati pre-esistente**, indipendente da questa issue: ogni update di un SharedGame distrugge la cover BGG re-uploadata.
2. Spiega perché `CreateSharedGameFromPdfCommandHandler:175` scrive direttamente sull'entità EF tracciata scavalcando l'aggregato — era l'unico modo per far sopravvivere il valore.

Senza il Task 1, il carve-out scriverebbe una cover destinata a sparire al primo update.

---

### Task 1: `BggCoverR2Key` nell'aggregato e nel mapper (chiude il bug latente)

**Files:**
- Modify: `.../SharedGameCatalog/Domain/Aggregates/SharedGame.cs`
- Modify: `.../SharedGameCatalog/Infrastructure/Repositories/SharedGameRepository.cs` (`MapToDomain` ~riga 382, `MapToEntity` ~riga 452)
- Create: `apps/api/tests/.../Infrastructure/Repositories/SharedGameRepositoryBggCoverTests.cs`

**Interfaces:**
- Produces: `SharedGame.BggCoverR2Key` (get) e `SharedGame.SetBggCover(string coverR2Key)`, consumati dal Task 2.

- [ ] **Step 1: test di regressione che fallisce**

```csharp
[Fact]
public async Task Update_PreservesBggCoverR2Key()
{
    // Regressione: MapToEntity non scriveva BggCoverR2Key e Update() fa un update di grafo
    // detached (tutte le colonne Modified) → ogni load-modify-save azzerava la cover BGG.
    await using var db = TestDbContextFactory.CreateInMemoryDbContext();
    var id = Guid.NewGuid();
    db.SharedGames.Add(new SharedGameEntity
    {
        Id = id, Title = "Probe", Description = "d", Status = 2,
        CreatedBy = Guid.NewGuid(), CreatedAt = DateTime.UtcNow,
        ImageUrl = "", ThumbnailUrl = "",
        BggCoverR2Key = "bgg-covers/13/cover",
    });
    await db.SaveChangesAsync();
    db.ChangeTracker.Clear();

    var repo = new SharedGameRepository(db, Mock.Of<IDomainEventCollector>());
    var game = await repo.GetByIdAsync(id, CancellationToken.None);
    repo.Update(game!);
    await db.SaveChangesAsync();
    db.ChangeTracker.Clear();

    var after = await db.SharedGames.AsNoTracking().FirstAsync(g => g.Id == id);
    after.BggCoverR2Key.Should().Be("bgg-covers/13/cover");
}
```

`IDomainEventCollector` è in `Api.SharedKernel.Application.Services`.

Aggiungi anche un test su `SetBggCover`: rifiuta stringa vuota (`ArgumentException`), è idempotente sullo stesso valore, aggiorna su valore diverso.

- [ ] **Step 2: eseguire — deve fallire con `found <null>`**

```bash
cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --nologo -v q --filter "FullyQualifiedName~SharedGameRepositoryBggCoverTests"
```

- [ ] **Step 3: campo + metodo sull'aggregato**

In `SharedGame.cs`, accanto a `_pdfCoverR2Key`, aggiungi il campo privato, la proprietà pubblica e il metodo modellato **esattamente** su `SetPdfCoverR2Key` (riga ~738):

```csharp
/// <summary>
/// Chiave R2 della cover BGG ri-ospitata (layer L2.5). Scritta dal re-upload
/// server-to-server admin (ADR-059 §2), MAI da un URL arbitrario: quel path è
/// sbarrato da <c>BggHostDenyList</c> per il ban #2123.
/// </summary>
public void SetBggCover(string coverR2Key)
{
    if (string.IsNullOrWhiteSpace(coverR2Key))
        throw new ArgumentException("Cover R2 key cannot be empty", nameof(coverR2Key));

    if (string.Equals(_bggCoverR2Key, coverR2Key, StringComparison.Ordinal))
        return;

    _bggCoverR2Key = coverR2Key;
}
```

Il costruttore/factory di ricostituzione deve accettare `bggCoverR2Key` come gli altri campi cover (parametro opzionale in coda, come `manualCoverR2Key`).

- [ ] **Step 4: mapper bidirezionale**

- `MapToDomain` (~382): aggiungi `bggCoverR2Key: entity.BggCoverR2Key` accanto a `pdfCoverR2Key`.
- `MapToEntity` (~452): aggiungi `BggCoverR2Key = game.BggCoverR2Key,` accanto a `PdfCoverR2Key`, con il commento che spiega perché ometterlo azzerava la colonna (stesso motivo già annotato per le colonne manual).

- [ ] **Step 5: eseguire — deve passare**

Poi la suite del repository e dei command handler che salvano SharedGame:

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --nologo -v q --filter "FullyQualifiedName~SharedGameRepository|FullyQualifiedName~CreateSharedGameFromPdf|FullyQualifiedName~SetManualCover"
```

Attenzione: `CreateSharedGameFromPdfCommandHandler` scrive `entity.BggCoverR2Key` **dopo** `AddAsync` (che ora mappa il campo dall'aggregato, null per un gioco nuovo). L'ordine regge, ma i suoi test devono restare verdi — se uno diventa rosso, è un segnale reale, non da aggirare.

- [ ] **Step 6: commit**

```bash
git commit -m "fix(catalog): l'aggregato possiede BggCoverR2Key e il mapper non lo azzera (#3590)"
```

---

### Task 2: comando di re-upload BGG

**Files:**
- Create: `.../Application/Commands/ReuploadBggCover/ReuploadBggCoverCommand.cs`
- Create: `.../ReuploadBggCover/ReuploadBggCoverCommandHandler.cs`
- Create: `.../ReuploadBggCover/ReuploadBggCoverCommandValidator.cs`
- Create: test dell'handler

**Interfaces:**
- Consumes: `SharedGame.SetBggCover` (Task 1); `IBggCoverDownloader.DownloadAndUploadAsync(int bggId, string remoteImageUrl, CancellationToken) -> Task<string?>`; `IBggApiService.GetGameDetailsAsync(int bggId, CancellationToken) -> Task<BggGameDetailsDto?>` (ha `ImageUrl`); `ISharedGameRepository`, `IUnitOfWork`.
- Produces: `ReuploadBggCoverCommand(Guid GameId, Guid AdminId) : ICommand<BggCoverResult>` e `BggCoverResult(string R2Key)`.

**Flusso**: carica il gioco (404 se assente) → 409 se `BggId` è null (non c'è nulla da scaricare) → `GetGameDetailsAsync` per l'`ImageUrl` → 409 se BGG non espone immagine → `DownloadAndUploadAsync` → 409 se ritorna null (il downloader logga e non lancia) → `game.SetBggCover(key)` → `Update` + `SaveChangesAsync` → evict cache come fa `SetManualCoverCommandHandler`.

**Da NON fare**: passare da `SetManualCoverCommand`, allentare `BggHostDenyList`, o reimplementare il download.

Test richiesti:
- percorso felice → chiave persistita sull'aggregato, `SaveChangesAsync` chiamato una volta;
- gioco senza `BggId` → `ConflictException`, nessuna chiamata al downloader;
- gioco inesistente → `NotFoundException`;
- downloader ritorna null → `ConflictException`, nessuna scrittura sull'aggregato;
- **non-regressione**: `SetManualCoverCommandValidator` continua a rifiutare un URL geekdo (la deny-list non è stata toccata).

---

### Task 3: endpoint admin

**Files:** `apps/api/src/Api/Routing/SharedGameCatalog/SharedGameCatalogAdminEndpoints.cs`

```csharp
group.MapPost("/admin/shared-games/{id:guid}/bgg-cover", HandleReuploadBggCover)
    .RequireAuthorization("AdminOrEditorPolicy")
    .WithName("ReuploadBggCover")
    .WithSummary("Re-host the BGG cover for a catalog game (Admin/Editor)")
    .WithDescription("#3590 — path server-to-server legittimo per ADR-059 §2. NON accetta un URL: la sorgente è l'immagine che BGG espone per il BggId del gioco. Il campo cover manuale a URL libero resta sbarrato verso geekdo dalla deny-list.")
    .Produces<BggCoverResult>();
```

L'handler statico prende `Guid id`, `IMediator`, e l'admin id dal contesto utente come fanno gli altri endpoint di mutazione (controlla come `HandleSetManualCover` lo ricava — riusare lo stesso meccanismo, non inventarne uno).

---

### Task 4: aggiornare il doc obsoleto + PR

- `docs/for-developers/specs/2026-08-02-admin-cover-editor-design.md:88` afferma «host BGG non bloccati nel fetch server-side (ADR-059 §2)»: **superato da #3495**, che ha introdotto `BggHostDenyList` sul path manuale. Correggere distinguendo i due path: manuale a URL libero = geekdo **bandito**; re-upload admin dedicato = **consentito**.
- Corpo PR: la distinzione fra i due path; il bug latente chiuso dal Task 1 (con l'evidenza del test); che la deny-list non è stata allentata.

Verifica finale: build 0/0 · categoria `Unit` senza regressioni · `dotnet format` con `--include` dalla root.

## Self-Review

**Rischio principale**: il Task 1 tocca il mapper di un aggregato centrale. Se un test esistente si rompe, va capito — non aggirato: significherebbe che qualcosa dipendeva dall'azzeramento.

**Fuori scope deliberato**: portare `CreateSharedGameFromPdfCommandHandler` sul nuovo metodo di dominio (l'utente ha scelto di non allargare la PR). Dopo il Task 1 quel path resta corretto: scrive sull'entità tracciata **dopo** che `AddAsync` ha mappato l'aggregato.
