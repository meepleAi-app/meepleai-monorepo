# Vista cover-gap admin (#3590 Slice A) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dare agli admin un modo per **trovare** i giochi senza cover, raggruppati per causa, e chiudere la deriva di deploy che li ha resi non recuperabili.

**Architecture:** Una query CQRS di sola lettura incrocia `shared_games` (le 4 chiavi cover tutte nulle) con i PDF collegati via la tabella ponte `shared_game_documents`, e deriva una causa per gioco da `cover_generation_status` + `error_category`. Un endpoint admin la espone; una pagina admin la consuma e porta all'editor cover già esistente. In coda, `unstructured-service` entra nella pipeline di deploy.

**Tech Stack:** .NET 9 + MediatR + EF Core, xUnit/FluentAssertions/Moq, Next.js 16 + React Query + Vitest, GitHub Actions.

## Global Constraints

- **Branch impilata** su `feature/issue-3383-single-pod-enforcement` (PR #3597). La PR va aperta con `--base feature/issue-3383-single-pod-enforcement`. Se le PR sotto mergiano prima, ribasare su `main-dev` e correggere la base.
- **CQRS**: gli endpoint usano SOLO `IMediator.Send()`. Mai iniettare un servizio in un endpoint.
- `SharedGameEntityConfiguration` ha `HasQueryFilter(e => !e.IsDeleted)`: il filtro soft-delete è **globale**, non riaggiungerlo.
- `PagedResult<T>` è quello di **`Api.Models`** (`Contracts.cs:586`). Ne esiste un secondo omonimo in `UserLibrary/.../GetUserGamesQuery.cs:69` — non usare quello.
- `cover_generation_status` e `error_category` sono persistiti come **stringa**, non come enum: confronta con `nameof(...)` o letterali.
- Policy degli endpoint admin di lettura: `AdminOrEditorPolicy`.
- MediatR e FluentValidation si auto-registrano per assembly scan (`Program.cs:344-361`): **nessuna registrazione manuale**.
- Working dir: `D:/Repositories/meepleai-monorepo-main/.claude/worktrees/i3583`. Branch: `feature/issue-3590-cover-gap-and-bgg-carveout`.

## Contesto verificato su staging (2026-08-07)

Numeri reali contro cui validare, dalla verifica documentata in [#3590](https://github.com/meepleAi-app/meepleai-monorepo/issues/3590#issuecomment-5214497502): **160 giochi, 136 con cover, 24 senza**. Il gruppo "PDF oltre il limite" è stato risolto (rebuild dell'immagine `unstructured` stale) e vale 2, non 4, perché due di quei giochi avevano già una cover Wikidata.

## File Structure

| File | Responsabilità | Azione |
|---|---|---|
| `.../SharedGameCatalog/Application/Queries/GetCoverGap/GetCoverGapQuery.cs` | Query + DTO monouso | **Crea** |
| `.../Queries/GetCoverGap/GetCoverGapQueryHandler.cs` | Join + classificazione causa | **Crea** |
| `.../Queries/GetCoverGap/GetCoverGapQueryValidator.cs` | Bound su paginazione | **Crea** |
| `apps/api/src/Api/Routing/SharedGameCatalog/SharedGameCatalogAdminEndpoints.cs` | Endpoint admin | Modifica: `GET /admin/shared-games/cover-gap` |
| `apps/api/tests/.../Queries/GetCoverGap/GetCoverGapQueryHandlerTests.cs` | Test classificazione | **Crea** |
| `apps/web/src/lib/api/clients/admin/adminCoverClient.ts` | Client admin cover | Modifica: `getCoverGap` |
| `apps/web/src/lib/api/schemas/admin/admin-cover.schemas.ts` | Schemi Zod | Modifica: schema cover-gap |
| `apps/web/src/hooks/admin/useCoverGap.ts` | Hook React Query | **Crea** |
| `apps/web/src/app/admin/(dashboard)/shared-games/cover-gap/page.tsx` | Pagina admin | **Crea** |
| `.github/workflows/deploy-staging.yml` | Pipeline deploy | Modifica: include `unstructured-service` |

---

### Task 1: query cover-gap con classificazione della causa

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetCoverGap/GetCoverGapQuery.cs`
- Create: `.../GetCoverGap/GetCoverGapQueryHandler.cs`
- Create: `.../GetCoverGap/GetCoverGapQueryValidator.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Queries/GetCoverGap/GetCoverGapQueryHandlerTests.cs`

**Interfaces:**
- Consumes: `MeepleAiDbContext.SharedGames` (`DbSet<SharedGameEntity>`), `.SharedGameDocuments` (`DbSet<SharedGameDocumentEntity>`), `.PdfDocuments` (`DbSet<PdfDocumentEntity>`); `Api.Models.PagedResult<T>`; `IQuery<T>` / `IQueryHandler<,>` da `Api.SharedKernel.Application.Interfaces`.
- Produces: `GetCoverGapQuery(int PageNumber = 1, int PageSize = 20, string? Cause = null) : IQuery<PagedResult<CoverGapGameDto>>` e `CoverGapGameDto(Guid GameId, string Title, int? BggId, string Cause, string? PdfFileName, long? PdfSizeBytes, string? ErrorCategory)`. Consumati dal Task 2 (endpoint) e dal Task 3 (FE).

**Le quattro cause** (stringhe stabili, sono un contratto con il FE e con il filtro `Cause`):

| Valore | Significato | Come si riconosce |
|---|---|---|
| `pdf_too_large` | Il PDF eccede il limite del servizio di estrazione | PDF collegato con `ErrorCategory == "PayloadTooLarge"` **oppure** `ProcessingState == "Failed"` con `FileSizeBytes` sopra soglia |
| `heuristic_rejected` | Nessuna pagina del rulebook è una cover accettabile — **esito corretto** | PDF collegato con `CoverGenerationStatus == "Skipped"` |
| `no_source` | Nessun PDF collegato e nessuna immagine libera | nessun PDF collegato |
| `other` | Tutto il resto (PDF in lavorazione, fallimenti non classificati) | fallback |

**Gotcha da rispettare** (verificati):
- Il gruppo "euristica" si riconosce da `CoverGenerationStatus == "Skipped"` scritto **direttamente sul campo** da `BackfillPdfCoversJob`, non via `PdfDocument.MarkCoverSkipped()` (metodo morto).
- La relazione PDF→gioco affidabile è la tabella ponte `SharedGameDocuments` (`SharedGameId` + `PdfDocumentId`). `PdfDocumentEntity.SharedGameId` è nullable e popolata solo su alcuni percorsi: **non** usarla come join primario.
- `ErrorCategory` è `"PayloadTooLarge"` solo dal fix #3589 in poi; i fallimenti precedenti hanno `"Service"` con un messaggio fuorviante. Per questo la causa `pdf_too_large` guarda **anche** la dimensione, non solo la categoria.

- [ ] **Step 1: scrivere il test che fallisce**

Crea il file di test. Usa una `DbContextOptionsBuilder` InMemory come fanno gli altri handler test del contesto (controlla `GetFilteredSharedGamesQueryHandlerTests.cs` per il fixture esatto e riusalo).

```csharp
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class GetCoverGapQueryHandlerTests
{
    [Fact]
    public async Task Handle_GameWithNoCoverAndNoPdf_ClassifiedAsNoSource()
    {
        // Arrange: un gioco con tutte e 4 le chiavi cover null e nessun documento collegato.
        // Assert: Items contiene il gioco con Cause == "no_source".
    }

    [Fact]
    public async Task Handle_GameWithSkippedCover_ClassifiedAsHeuristicRejected()
    {
        // PDF collegato via SharedGameDocuments con CoverGenerationStatus = "Skipped".
        // Assert: Cause == "heuristic_rejected".
    }

    [Fact]
    public async Task Handle_GameWithPayloadTooLargePdf_ClassifiedAsPdfTooLarge()
    {
        // PDF collegato con ErrorCategory = "PayloadTooLarge".
        // Assert: Cause == "pdf_too_large".
    }

    [Fact]
    public async Task Handle_GameWithAnyCoverKey_IsExcluded()
    {
        // Quattro giochi, ciascuno con UNA sola chiave valorizzata (pdf/bgg/wikidata/manual).
        // Assert: Items vuoto — avere una qualsiasi cover esclude dal gap.
        // Questo test protegge il contratto centrale: la vista elenca SOLO chi non ha NULLA.
    }

    [Fact]
    public async Task Handle_FiltersByCause_WhenCauseProvided()
    {
        // Due giochi con cause diverse; query con Cause = "no_source".
        // Assert: torna solo quello.
    }
}
```

Scrivi i corpi per intero seguendo il fixture reale del file di riferimento — non lasciare i commenti come segnaposto.

- [ ] **Step 2: eseguire i test per verificare che falliscano**

```bash
cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --nologo -v q --filter "FullyQualifiedName~GetCoverGapQueryHandlerTests"
```

Atteso: FALLISCE in compilazione (i tipi non esistono).

- [ ] **Step 3: creare query, DTO e validator**

`GetCoverGapQuery.cs` — DTO monouso nello stesso file (pattern recente del contesto, es. `GetSeedingStatus`):

```csharp
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetCoverGap;

/// <summary>
/// #3590 — elenca i giochi del catalogo SENZA alcuna cover (tutte e quattro le chiavi nulle),
/// con la CAUSA per cui la pipeline cover-da-PDF non li copre. Il collo di bottiglia non era
/// risolvere questi casi — il picker manuale esiste da #3545 — ma TROVARLI: non esisteva alcuna
/// vista dei giochi senza cover.
/// </summary>
/// <param name="Cause">Filtro opzionale su una delle cause note. Null = tutte.</param>
internal record GetCoverGapQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? Cause = null) : IQuery<PagedResult<CoverGapGameDto>>;

/// <summary>Un gioco senza cover, con la causa derivata dallo stato dei suoi PDF.</summary>
internal record CoverGapGameDto(
    Guid GameId,
    string Title,
    int? BggId,
    string Cause,
    string? PdfFileName,
    long? PdfSizeBytes,
    string? ErrorCategory);

/// <summary>Cause bounded — contratto con il front-end e con il filtro della query.</summary>
internal static class CoverGapCauses
{
    public const string PdfTooLarge = "pdf_too_large";
    public const string HeuristicRejected = "heuristic_rejected";
    public const string NoSource = "no_source";
    public const string Other = "other";

    public static readonly IReadOnlyList<string> All =
        new[] { PdfTooLarge, HeuristicRejected, NoSource, Other };
}
```

`GetCoverGapQueryValidator.cs`:

```csharp
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetCoverGap;

internal sealed class GetCoverGapQueryValidator : AbstractValidator<GetCoverGapQuery>
{
    public GetCoverGapQueryValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Cause)
            .Must(c => c is null || CoverGapCauses.All.Contains(c))
            .WithMessage($"Cause must be one of: {string.Join(", ", CoverGapCauses.All)}");
    }
}
```

- [ ] **Step 4: implementare l'handler**

`GetCoverGapQueryHandler.cs`. Struttura: parti dai giochi con tutte e 4 le chiavi nulle, porta i PDF collegati via la tabella ponte, classifica in memoria dopo la proiezione (la classificazione ha rami che EF non traduce bene, ed è su un insieme piccolo — decine di righe, non migliaia).

```csharp
public async Task<PagedResult<CoverGapGameDto>> Handle(GetCoverGapQuery request, CancellationToken ct)
{
    // Il filtro soft-delete è globale (HasQueryFilter): non va riaggiunto qui.
    var gapGames = _context.SharedGames
        .AsNoTracking()
        .Where(g => string.IsNullOrWhiteSpace(g.PdfCoverR2Key)
                 && string.IsNullOrWhiteSpace(g.BggCoverR2Key)
                 && string.IsNullOrWhiteSpace(g.WikidataCoverR2Key)
                 && string.IsNullOrWhiteSpace(g.ManualCoverR2Key));

    // PDF collegati via la tabella ponte (relazione canonica; PdfDocumentEntity.SharedGameId è
    // nullable e popolata solo su alcuni percorsi, quindi non è affidabile come join primario).
    var rows = await (
        from g in gapGames
        join sgd in _context.SharedGameDocuments on g.Id equals sgd.SharedGameId into docs
        from sgd in docs.DefaultIfEmpty()
        join p in _context.PdfDocuments on sgd.PdfDocumentId equals p.Id into pdfs
        from p in pdfs.DefaultIfEmpty()
        select new
        {
            g.Id,
            g.Title,
            g.BggId,
            PdfFileName = p != null ? p.FileName : null,
            PdfSize = p != null ? (long?)p.FileSizeBytes : null,
            CoverStatus = p != null ? p.CoverGenerationStatus : null,
            ErrorCategory = p != null ? p.ErrorCategory : null,
            ProcessingState = p != null ? p.ProcessingState : null,
        })
        .ToListAsync(ct)
        .ConfigureAwait(false);

    // Un gioco può avere più PDF: tieni la riga più informativa per gioco.
    var classified = rows
        .GroupBy(r => new { r.Id, r.Title, r.BggId })
        .Select(grp =>
        {
            var best = grp
                .OrderByDescending(r => Rank(Classify(r.CoverStatus, r.ErrorCategory, r.ProcessingState, r.PdfSize)))
                .First();
            var cause = Classify(best.CoverStatus, best.ErrorCategory, best.ProcessingState, best.PdfSize);
            return new CoverGapGameDto(
                grp.Key.Id, grp.Key.Title, grp.Key.BggId, cause,
                best.PdfFileName, best.PdfSize, best.ErrorCategory);
        })
        .Where(d => request.Cause is null || d.Cause == request.Cause)
        .OrderBy(d => d.Cause).ThenBy(d => d.Title)
        .ToList();

    var total = classified.Count;
    var items = classified
        .Skip((request.PageNumber - 1) * request.PageSize)
        .Take(request.PageSize)
        .ToList();

    return new PagedResult<CoverGapGameDto>(items, total, request.PageNumber, request.PageSize);
}
```

I due helper privati, con le soglie documentate:

```csharp
/// <summary>
/// Soglia oltre la quale un fallimento di estrazione si spiega con la dimensione. Allineata al
/// limite storico del servizio Unstructured (50MB): i fallimenti precedenti al fix #3589 hanno
/// ErrorCategory "Service" con un messaggio fuorviante ("Failed to connect"), quindi la sola
/// categoria non basta a riconoscerli.
/// </summary>
private const long LargePdfThresholdBytes = 52_428_800;

private static string Classify(string? coverStatus, string? errorCategory, string? processingState, long? sizeBytes)
{
    if (coverStatus is null && processingState is null)
    {
        return CoverGapCauses.NoSource;
    }

    if (string.Equals(errorCategory, "PayloadTooLarge", StringComparison.Ordinal)
        || (string.Equals(processingState, "Failed", StringComparison.Ordinal)
            && sizeBytes > LargePdfThresholdBytes))
    {
        return CoverGapCauses.PdfTooLarge;
    }

    if (string.Equals(coverStatus, "Skipped", StringComparison.Ordinal))
    {
        return CoverGapCauses.HeuristicRejected;
    }

    return CoverGapCauses.Other;
}

/// <summary>Precedenza quando un gioco ha più PDF: la causa più azionabile vince.</summary>
private static int Rank(string cause) => cause switch
{
    CoverGapCauses.PdfTooLarge => 3,
    CoverGapCauses.HeuristicRejected => 2,
    CoverGapCauses.Other => 1,
    _ => 0,
};
```

Il ctor prende `MeepleAiDbContext` e `ILogger<GetCoverGapQueryHandler>` con le guardie `?? throw new ArgumentNullException(...)`, come gli altri handler del contesto.

- [ ] **Step 5: eseguire i test per verificare che passino**

```bash
cd apps/api && dotnet test tests/Api.Tests/Api.Tests.csproj --nologo -v q --filter "FullyQualifiedName~GetCoverGapQueryHandlerTests"
```

Atteso: PASS, 5 test.

- [ ] **Step 6: commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetCoverGap/ \
        apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Queries/GetCoverGap/
git commit -m "feat(catalog): query cover-gap con classificazione per causa (#3590)"
```

---

### Task 2: endpoint admin

**Files:**
- Modify: `apps/api/src/Api/Routing/SharedGameCatalog/SharedGameCatalogAdminEndpoints.cs`

**Interfaces:**
- Consumes: `GetCoverGapQuery` / `CoverGapGameDto` (Task 1).
- Produces: `GET /api/v1/admin/shared-games/cover-gap`, consumato dal Task 3.

- [ ] **Step 1: registrare la route**

Accanto alle altre `MapGet` admin (modello: `/admin/shared-games/seeding-status`, righe ~443-447):

```csharp
group.MapGet("/admin/shared-games/cover-gap", HandleGetCoverGap)
    .RequireAuthorization("AdminOrEditorPolicy")
    .WithName("GetCoverGap")
    .WithSummary("Get catalog games with no cover, grouped by cause (Admin/Editor)")
    .WithDescription("#3590 — i giochi senza alcuna cover, con la causa per cui la pipeline cover-da-PDF non li copre: pdf_too_large, heuristic_rejected, no_source, other.")
    .Produces<PagedResult<CoverGapGameDto>>();
```

La route costante non confligge con `{id:guid}`: il constraint disambigua.

- [ ] **Step 2: aggiungere l'handler statico**

```csharp
private static async Task<IResult> HandleGetCoverGap(
    IMediator mediator,
    [FromQuery] string? cause = null,
    [FromQuery] int pageNumber = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken ct = default)
{
    var result = await mediator
        .Send(new GetCoverGapQuery(pageNumber, pageSize, cause), ct)
        .ConfigureAwait(false);
    return Results.Ok(result);
}
```

Aggiungi il `using` del namespace `...Application.Queries.GetCoverGap`. **Solo `IMediator`**: nessun servizio iniettato, è la regola CQRS del progetto.

- [ ] **Step 3: build**

```bash
cd apps/api/src/Api && dotnet build --nologo -v q
```

Atteso: `Avvisi: 0`, `Errori: 0`.

- [ ] **Step 4: commit**

```bash
git add apps/api/src/Api/Routing/SharedGameCatalog/SharedGameCatalogAdminEndpoints.cs
git commit -m "feat(catalog): endpoint admin GET /admin/shared-games/cover-gap (#3590)"
```

---

### Task 3: pagina admin cover-gap

**Files:**
- Modify: `apps/web/src/lib/api/schemas/admin/admin-cover.schemas.ts`
- Modify: `apps/web/src/lib/api/clients/admin/adminCoverClient.ts`
- Create: `apps/web/src/hooks/admin/useCoverGap.ts`
- Create: `apps/web/src/app/admin/(dashboard)/shared-games/cover-gap/page.tsx`

**Interfaces:**
- Consumes: `GET /api/v1/admin/shared-games/cover-gap` (Task 2).
- Produces: la pagina admin; nessun consumatore a valle.

**Prima di scrivere**: leggi `useCoverCandidates.ts` e `adminCoverClient.ts` per riusarne esattamente lo stile (naming delle query key, forma del client, gestione errori). Non introdurre un secondo stile nella stessa cartella.

- [ ] **Step 1: schema Zod**

In `admin-cover.schemas.ts`, accanto agli schemi esistenti:

```ts
export const coverGapCauseSchema = z.enum([
  'pdf_too_large',
  'heuristic_rejected',
  'no_source',
  'other',
]);

export const coverGapGameSchema = z.object({
  gameId: z.string().uuid(),
  title: z.string(),
  bggId: z.number().nullable(),
  cause: coverGapCauseSchema,
  pdfFileName: z.string().nullable(),
  pdfSizeBytes: z.number().nullable(),
  errorCategory: z.string().nullable(),
});

export const coverGapPageSchema = z.object({
  items: z.array(coverGapGameSchema),
  total: z.number(),
  page: z.number(),
  pageSize: z.number(),
});
```

⚠️ Prima di stringere qualunque campo a `required`, verifica la nullability effettiva lato C#: `bggId`, `pdfFileName`, `pdfSizeBytes`, `errorCategory` sono nullable nel DTO, quindi `.nullable()` è corretto e non va tolto.

- [ ] **Step 2: client**

In `adminCoverClient.ts`, seguendo la forma dei metodi già presenti:

```ts
async getCoverGap(params: { cause?: string; pageNumber?: number; pageSize?: number } = {}) {
  const search = new URLSearchParams();
  if (params.cause) search.set('cause', params.cause);
  if (params.pageNumber) search.set('pageNumber', String(params.pageNumber));
  if (params.pageSize) search.set('pageSize', String(params.pageSize));
  const qs = search.toString();
  return coverGapPageSchema.parse(
    await apiFetch(`/api/v1/admin/shared-games/cover-gap${qs ? `?${qs}` : ''}`)
  );
}
```

Adatta `apiFetch` al helper realmente usato nel file.

- [ ] **Step 3: hook**

`useCoverGap.ts`, sul modello di `useCoverCandidates.ts`:

```ts
export function useCoverGap(params: { cause?: string; pageNumber?: number } = {}) {
  return useQuery({
    queryKey: ['admin', 'cover-gap', params],
    queryFn: () => adminCoverClient.getCoverGap(params),
  });
}
```

- [ ] **Step 4: pagina**

`cover-gap/page.tsx`. Requisiti concreti:
- Tabella dei giochi senza cover: titolo, causa (etichetta leggibile), nome del PDF e dimensione in MB quando presenti.
- Filtro per causa (le quattro dell'enum + "tutte").
- Conteggio totale in testa — è il numero che si confronta con la copertura del catalogo.
- Per ogni riga, un link al gioco su `/shared-games` così l'admin raggiunge l'editor cover esistente (affordance a matita, `AdminCoverEditAffordance`). **Non** duplicare l'editor qui.
- **Token semantici obbligatori**: `bg-background`, `bg-card`, `text-foreground`, `text-muted-foreground`, `border-border`. Le utility hardcoded (`bg-white`, `text-gray-*`, `bg-slate-*`) sono **errore** ESLint (`local/no-hardcoded-color-utility`).
- Etichette in italiano, coerenti col resto dell'area admin.

- [ ] **Step 5: qualità FE**

```bash
cd apps/web && pnpm typecheck && pnpm lint
```

Atteso: nessun errore. Se `lint:tokens` segnala colori hardcoded, correggi i token — non aggiungere eccezioni.

- [ ] **Step 6: commit**

```bash
git add apps/web/src/lib/api/schemas/admin/admin-cover.schemas.ts \
        apps/web/src/lib/api/clients/admin/adminCoverClient.ts \
        apps/web/src/hooks/admin/useCoverGap.ts \
        apps/web/src/app/admin/\(dashboard\)/shared-games/cover-gap/page.tsx
git commit -m "feat(catalog): pagina admin cover-gap (#3590)"
```

---

### Task 4: unstructured-service nella pipeline di deploy

**Files:**
- Modify: `.github/workflows/deploy-staging.yml`

**Interfaces:** nessuna — CI.

**Perché è in questa PR:** è la causa per cui i 4 PDF grandi risultavano non recuperabili. Verificato il 2026-08-07: staging girava un'immagine del 31 luglio 13:50 UTC, costruita **un'ora prima** che il commit del limite 50→100MB atterrasse — e le mancavano anche i due commit di region-seeding RAG (#3435, #3565). Il servizio non compare in `deploy-staging.yml` né in alcun workflow di build/publish: appare solo come URL nei test. Si ricostruisce a mano, quindi va alla deriva in silenzio.

- [ ] **Step 1: capire come il deploy tratta i servizi buildati da sorgente**

`deploy-staging.yml` (blocco `SERVICES`, righe ~1092-1130) fa `docker pull` di immagini GHCR per `api`, `web`, `embedding-service`, `reranker-service`. `unstructured-service` nel compose è `build:` da sorgente, quindi **non** ha un'immagine GHCR da pullare: il pattern del pull non si applica tale e quale.

Leggi il blocco per intero prima di modificarlo e scegli l'innesto coerente: o si aggiunge il servizio al passo di build sul VPS, o lo si porta su GHCR come embedding/reranker. La seconda è più pulita ma più grande; la prima è locale a questo workflow.

- [ ] **Step 2: implementare il rilevamento del cambiamento e il rebuild**

Aggiungi un ramo che, quando cambiano i file sotto `apps/unstructured-service/`, ricostruisce e ricrea il servizio sul VPS. Vincoli **non negoziabili**, appresi sul campo:

- **Prune PRIMA del build, non dopo.** L'immagine pesa 10.2GB; il 2026-08-07 il build ha portato il disco dal 74% al **99% (873MB liberi)**. `docker builder prune -f` recupera ~6.5GB.
- Il compose richiede le variabili immagine (`EMBEDDING_IMAGE`, `RERANKER_IMAGE`, `ORCHESTRATION_IMAGE`, `API_IMAGE`), altrimenti fallisce con `required variable ... is missing a value`. I tag dell'ultimo deploy sono in `/opt/meepleai/repo/infra/DEPLOYMENT.json`.
- Riusa il gate di spazio disco già presente nel workflow per gli altri servizi AI (c'è un fail-safe con soglia: cercalo e applica lo stesso, non inventarne un altro).

- [ ] **Step 3: validare la sintassi del workflow**

```bash
python -c "import yaml; yaml.safe_load(open('.github/workflows/deploy-staging.yml',encoding='utf-8')); print('YAML valido')"
```

Atteso: `YAML valido`. Un errore di sintassi qui rompe **tutti** i deploy, non solo questo servizio.

- [ ] **Step 4: commit**

```bash
git add .github/workflows/deploy-staging.yml
git commit -m "fix(ci): unstructured-service entra nella pipeline di deploy (#3590)"
```

---

### Task 5: verifica finale e PR

- [ ] **Step 1: build backend**

Da `apps/api/src/Api`: `dotnet build --nologo -v q` → `Avvisi: 0`, `Errori: 0`.

- [ ] **Step 2: regressione backend**

Da `apps/api`:

```bash
dotnet test tests/Api.Tests/Api.Tests.csproj --nologo -v q --filter "Category=Unit"
```

Baseline su questa branch: **21364 passati, 0 falliti, 23 skipped**. Riporta il conteggio esatto; non deve peggiorare.

- [ ] **Step 3: qualità frontend**

```bash
cd apps/web && pnpm typecheck && pnpm lint
```

- [ ] **Step 4: formattazione backend**

Dalla root del worktree (non da `apps/api`: i path del git diff sono relativi alla root):

```bash
dotnet format apps/api/MeepleAI.Api.sln --include $(git diff --name-only feature/issue-3383-single-pod-enforcement...HEAD -- '*.cs' | tr '\n' ' ')
```

- [ ] **Step 5: push e PR**

```bash
git push -u origin feature/issue-3590-cover-gap-and-bgg-carveout
gh pr create --base feature/issue-3383-single-pod-enforcement \
  --title "feat(catalog): vista admin cover-gap + unstructured nel deploy (#3590 Slice A)" \
  --body "<vedi sotto>"
```

Il corpo deve contenere: le quattro cause e come si derivano; che il collo di bottiglia era **trovare** i giochi, non risolverli (il picker manuale esiste da #3545); l'esito della verifica staging (24 giochi senza cover, i 4 PDF grandi risolti); che `unstructured-service` non era in alcuna pipeline; e che **Slice B (carve-out BGG) segue in una PR separata**.

---

## Self-Review

**Copertura**: la vista cover-gap (query, endpoint, pagina) copre la parte «trovare i 24» decisa con l'utente; il fix pipeline copre la causa a monte emersa dalla verifica staging. Il **carve-out BGG è deliberatamente fuori** da questo piano: va in Slice B, come concordato, e richiede prima un `SetBggCover` sull'aggregato (oggi assente — l'unico path che scrive `BggCoverR2Key` mutila l'entità EF direttamente, `CreateSharedGameFromPdfCommandHandler:175`).

**Placeholder**: due punti restano volutamente adattivi e sono segnalati sul posto — il fixture InMemory del Task 1 Step 1 (da riusare dal file di riferimento invece di trascriverlo non verificato) e l'innesto del Task 4 Step 2 (il blocco `SERVICES` va letto per intero: `unstructured` non ha immagine GHCR, quindi il pattern del pull non si applica meccanicamente).

**Coerenza dei tipi**: `PagedResult<T>` è quello di `Api.Models`; `CoverGapGameDto` ha gli stessi campi in C#, nello schema Zod e nella pagina; le quattro stringhe di causa sono definite una volta in `CoverGapCauses` e replicate nell'enum Zod — se cambiano, vanno cambiate in entrambi i punti.

**Rischio noto non coperto**: la classificazione avviene in memoria dopo la proiezione. È deliberato (i rami non si traducono bene in SQL, l'insieme è di decine di righe), ma se il catalogo crescesse di ordini di grandezza andrebbe spinta in SQL. Non è un problema oggi con 160 giochi.
