# Image-Region Area Filter + Unstructured Refresh — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Aggiungere un filtro area-minima anti-rumore alla cattura delle regioni immagine-tabella (#3456) e ricostruire/documentare l'immagine `unstructured-service` che emette i bbox (#3455).

**Architecture:** Filtro puro lato BE in `ImageRegionExtractor.FromHiResJson` (soglia `double`, default 3% via const), propagato come parametro opzionale attraverso il command/request/endpoint del seed. Per #3455 nessuna modifica al sorgente Python (già ha #3406): rebuild dell'immagine Docker + nota nel runbook di deploy.

**Tech Stack:** .NET 9 (xUnit + FluentAssertions), CQRS/MediatR, Docker Compose, Python 3.11 (unstructured-service).

## Global Constraints

- Solution BE: `apps/api/MeepleAI.Api.sln`. Build dopo un cambio di ctor/signature: build della **solution**, non del solo progetto.
- CQRS: endpoint usano solo `IMediator.Send` (nessuna service injection).
- Eccezioni dominio: `NotFoundException` (404), mai `InvalidOperationException` (500) — invariante esistente, non toccata qui.
- Naming C#: PascalCase pubblico, `_camelCase` privato.
- Baseline unit test: 0 fail. Una PR non deve aumentare il fail count.
- Git hooks eseguono build completa → `git commit`/`push` vanno in **timeout foreground**: eseguirli con `run_in_background` e verificare l'esito con `git log`/`git status` (exit 0 ≠ commit garantito).
- Soglia area = frazione di pagina `Width * Height` ∈ [0,1]; default **`0.03`** (3%); confronto **`>=`** (inclusivo); area calcolata sui valori **già clampati** a [0,1].

---

### Task 1: Filtro area-minima in `ImageRegionExtractor` (#3456)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/ImageRegionExtractor.cs`
- Test: `apps/api/tests/Api.Tests/Unit/DocumentProcessing/ImageRegionExtractorTests.cs`

**Interfaces:**
- Produces: `ImageRegionExtractor.FromHiResJson(string? hiResJson, double minAreaFraction = ImageRegionExtractor.DefaultMinAreaFraction)` → `IReadOnlyList<ExtractedImageRegion>`; `public const double ImageRegionExtractor.DefaultMinAreaFraction = 0.03`.
- Consumes: nulla (Task 1 è la base).

- [ ] **Step 1: Aggiorna il fixture e aggiungi i test del filtro**

In `ImageRegionExtractorTests.cs`, sostituisci il campo `HiResJson` (la `FigureCaption` passa da area 2.4% → >3% così resta "tenuta"; aggiungi una `Image` piccola sotto soglia) e aggiungi i test del filtro. Il test esistente `FromHiResJson_KeepsImageAndFigureCaption_WithBbox_DropsOthers` va aggiornato per la nuova FigureCaption e per la piccola Image droppata.

```csharp
    private const string HiResJson = """
    {"elements":[
      {"text":"Preparazione","page_number":1,"category":"Title","bbox":{"x":0.08,"y":0.10,"width":0.24,"height":0.05}},
      {"text":"","page_number":4,"category":"Image","bbox":{"x":0.10,"y":0.55,"width":0.80,"height":0.30}},
      {"text":"","page_number":5,"category":"FigureCaption","bbox":{"x":0.12,"y":0.20,"width":0.40,"height":0.10}},
      {"text":"","page_number":6,"category":"Image","bbox":null},
      {"text":"","page_number":7,"category":"Image","bbox":{"x":0.10,"y":0.10,"width":0.05,"height":0.05}}
    ]}
    """;

    [Fact]
    public void FromHiResJson_KeepsLargeImageAndFigureCaption_DropsOtherTypesNullBboxAndTinyRegions()
    {
        var regions = ImageRegionExtractor.FromHiResJson(HiResJson);

        // Image p4 (0.80*0.30=0.24) + FigureCaption p5 (0.40*0.10=0.04) kept;
        // Title dropped (type), bbox-null Image p6 dropped, tiny Image p7 (0.05*0.05=0.0025) dropped (area < 3%).
        regions.Should().HaveCount(2);
        regions.Should().ContainSingle(r => r.ElementType == "Image" && r.Page == 4 && r.Width == 0.80);
        regions.Should().ContainSingle(r => r.ElementType == "FigureCaption" && r.Page == 5);
        regions.Should().NotContain(r => r.Page == 7);
    }

    [Fact]
    public void FromHiResJson_DefaultThreshold_IsThreePercent()
    {
        ImageRegionExtractor.DefaultMinAreaFraction.Should().Be(0.03);
    }

    [Fact]
    public void FromHiResJson_DropsRegionsBelowMinArea()
    {
        // 0.10*0.20 = 0.02 (2%) < default 3% → dropped
        var json = """{"elements":[{"text":"","page_number":1,"category":"Image","bbox":{"x":0.1,"y":0.1,"width":0.10,"height":0.20}}]}""";
        ImageRegionExtractor.FromHiResJson(json).Should().BeEmpty();
    }

    [Fact]
    public void FromHiResJson_RespectsCustomMinArea()
    {
        // Image area = 0.20*0.20 = 0.04 (4%). Kept at default 3%, dropped at custom 10%.
        var json = """{"elements":[{"text":"","page_number":1,"category":"Image","bbox":{"x":0.1,"y":0.1,"width":0.20,"height":0.20}}]}""";
        ImageRegionExtractor.FromHiResJson(json).Should().HaveCount(1);
        ImageRegionExtractor.FromHiResJson(json, minAreaFraction: 0.10).Should().BeEmpty();
    }

    [Fact]
    public void FromHiResJson_AreaExactlyAtThreshold_IsKept()
    {
        // area = 0.30*0.10 = 0.03 == default threshold → kept (>=)
        var json = """{"elements":[{"text":"","page_number":1,"category":"Image","bbox":{"x":0.0,"y":0.0,"width":0.30,"height":0.10}}]}""";
        ImageRegionExtractor.FromHiResJson(json).Should().HaveCount(1);
    }

    [Fact]
    public void FromHiResJson_ZeroThreshold_DisablesFilter()
    {
        // tiny region kept when threshold is 0
        var json = """{"elements":[{"text":"","page_number":1,"category":"Image","bbox":{"x":0.1,"y":0.1,"width":0.01,"height":0.01}}]}""";
        ImageRegionExtractor.FromHiResJson(json, minAreaFraction: 0.0).Should().HaveCount(1);
    }
```

Il test `FromHiResJson_ClampsBboxToUnitRange` esistente usa area `1.0*0.2 = 0.2` (dopo clamp) → sopra soglia, resta verde. I test `FromHiResJson_NullEmptyInvalidOrNoElements_ReturnsEmpty` restano invariati.

- [ ] **Step 2: Esegui i test e verifica che falliscano**

Run: `cd apps/api && dotnet test MeepleAI.Api.sln --filter "FullyQualifiedName~ImageRegionExtractorTests" --nologo`
Expected: FAIL (compilazione: `DefaultMinAreaFraction`/parametro `minAreaFraction` non esistono; `HaveCount(2)` vs vecchio comportamento).

- [ ] **Step 3: Implementa il filtro area in `ImageRegionExtractor`**

In `ImageRegionExtractor.cs`, aggiungi la const e il parametro, e inserisci il filtro area dopo la `Select` (così opera sui valori clampati). Sostituisci il corpo di `FromHiResJson`:

```csharp
public static class ImageRegionExtractor
{
    /// <summary>Default anti-noise threshold (#3456): keep regions whose normalized area
    /// (Width*Height, fraction of page) is at least 3%. Filters out icons/glyphs.</summary>
    public const double DefaultMinAreaFraction = 0.03;

    private static readonly HashSet<string> RegionCategories =
        new(StringComparer.Ordinal) { "Image", "FigureCaption" };

    public static IReadOnlyList<ExtractedImageRegion> FromHiResJson(
        string? hiResJson,
        double minAreaFraction = DefaultMinAreaFraction)
    {
        if (string.IsNullOrWhiteSpace(hiResJson))
        {
            return Array.Empty<ExtractedImageRegion>();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<HiResEnvelope>(hiResJson);
            if (parsed?.Elements is null)
            {
                return Array.Empty<ExtractedImageRegion>();
            }

            return parsed.Elements
                .Where(e => e.Category is not null && RegionCategories.Contains(e.Category) && e.Bbox is not null)
                .Select(e => new ExtractedImageRegion(
                    Page: e.PageNumber > 0 ? e.PageNumber : 1,
                    X: Clamp01(e.Bbox!.X),
                    Y: Clamp01(e.Bbox!.Y),
                    Width: Clamp01(e.Bbox!.Width),
                    Height: Clamp01(e.Bbox!.Height),
                    ElementType: e.Category!))
                // #3456 anti-noise: drop tiny regions (icons/glyphs) below the area threshold.
                // Area is on already-clamped values; >= keeps a region exactly at the threshold.
                .Where(r => r.Width * r.Height >= minAreaFraction)
                .ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<ExtractedImageRegion>();
        }
    }
```

(Il resto della classe — `Clamp01`, i record `HiResEnvelope`/`HiResElement`/`HiResBbox`, e `ExtractedImageRegion` — resta invariato.)

- [ ] **Step 4: Esegui i test e verifica che passino**

Run: `cd apps/api && dotnet test MeepleAI.Api.sln --filter "FullyQualifiedName~ImageRegionExtractorTests" --nologo`
Expected: PASS (tutti).

- [ ] **Step 5: Commit** (background per via degli hook — poi verifica con `git log`)

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/ImageRegionExtractor.cs \
        apps/api/tests/Api.Tests/Unit/DocumentProcessing/ImageRegionExtractorTests.cs
git commit -m "feat(rag): area-minima anti-rumore in ImageRegionExtractor (#3456)"
```

---

### Task 2: Propaga la soglia configurabile attraverso il seed (#3456)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/SeedPdfImageRegionsCommand.cs`
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/SeedPdfImageRegionsCommandHandler.cs:36`
- Modify: `apps/api/src/Api/Routing/AdminPdfManagementEndpoints.cs:76-86,127`
- Test: `apps/api/tests/Api.Tests/Unit/DocumentProcessing/SeedPdfImageRegionsCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `ImageRegionExtractor.FromHiResJson(hiResJson, minAreaFraction)` e `ImageRegionExtractor.DefaultMinAreaFraction` (Task 1).
- Produces: `SeedPdfImageRegionsCommand(Guid PdfId, string HiResJson, double? MinAreaFraction = null)`; `SeedImageRegionsRequest(string HiResJson, double? MinAreaFraction = null)`.

- [ ] **Step 1: Aggiungi i test dell'handler per la soglia**

In `SeedPdfImageRegionsCommandHandlerTests.cs` aggiungi due test. Usano una regione piccola (area 2% < 3%) che il default scarta ma una soglia custom `0.0` conserva. Nota: la costruzione `new SeedPdfImageRegionsCommand(pdfId, json)` resta valida (il nuovo parametro è opzionale), quindi i test esistenti non cambiano.

```csharp
    private const string TinyRegionJson = """
    {"elements":[
      {"text":"","page_number":2,"category":"Image","bbox":{"x":0.1,"y":0.1,"width":0.10,"height":0.20}}
    ]}
    """;

    [Fact]
    public async Task Handle_DefaultThreshold_DropsTinyRegion()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"seedimg_{Guid.NewGuid():N}");
        var pdfId = Guid.NewGuid();
        SeedPdf(db, pdfId);
        await db.SaveChangesAsync();
        var handler = new SeedPdfImageRegionsCommandHandler(db, NullLogger<SeedPdfImageRegionsCommandHandler>.Instance);

        // 0.10*0.20 = 0.02 (2%) < default 3% → nothing seeded
        var count = await handler.Handle(new SeedPdfImageRegionsCommand(pdfId, TinyRegionJson), CancellationToken.None);
        count.Should().Be(0);
    }

    [Fact]
    public async Task Handle_CustomThresholdZero_KeepsTinyRegion()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"seedimg_{Guid.NewGuid():N}");
        var pdfId = Guid.NewGuid();
        SeedPdf(db, pdfId);
        await db.SaveChangesAsync();
        var handler = new SeedPdfImageRegionsCommandHandler(db, NullLogger<SeedPdfImageRegionsCommandHandler>.Instance);

        var count = await handler.Handle(new SeedPdfImageRegionsCommand(pdfId, TinyRegionJson, MinAreaFraction: 0.0), CancellationToken.None);
        count.Should().Be(1);
    }
```

- [ ] **Step 2: Esegui i test e verifica che falliscano**

Run: `cd apps/api && dotnet test MeepleAI.Api.sln --filter "FullyQualifiedName~SeedPdfImageRegionsCommandHandlerTests" --nologo`
Expected: FAIL (compilazione: `SeedPdfImageRegionsCommand` non ha `MinAreaFraction`).

- [ ] **Step 3: Aggiungi il parametro al command, request e propagalo nell'handler + endpoint**

`SeedPdfImageRegionsCommand.cs` — aggiungi il parametro opzionale al record:

```csharp
internal sealed record SeedPdfImageRegionsCommand(Guid PdfId, string HiResJson, double? MinAreaFraction = null) : ICommand<int>;
```

`SeedPdfImageRegionsCommandHandler.cs` riga 36 — passa la soglia (fallback al default della slice):

```csharp
        var regions = ImageRegionExtractor.FromHiResJson(
            command.HiResJson,
            command.MinAreaFraction ?? ImageRegionExtractor.DefaultMinAreaFraction);
```

`AdminPdfManagementEndpoints.cs` riga 127 — aggiungi il campo opzionale al request record:

```csharp
internal record SeedImageRegionsRequest(string HiResJson, double? MinAreaFraction = null);
```

`AdminPdfManagementEndpoints.cs` righe 82-84 — propaga la soglia al command:

```csharp
        var count = await mediator.Send(
            new SeedPdfImageRegionsCommand(pdfId, request.HiResJson, request.MinAreaFraction),
            cancellationToken).ConfigureAwait(false);
```

- [ ] **Step 4: Esegui i test dell'handler e la build della solution**

Run: `cd apps/api && dotnet test MeepleAI.Api.sln --filter "FullyQualifiedName~SeedPdfImageRegionsCommandHandlerTests" --nologo`
Expected: PASS (vecchi + 2 nuovi).
Run (verifica ctor call-site endpoint): `cd apps/api && dotnet build MeepleAI.Api.sln --nologo`
Expected: Build succeeded, 0 error.

- [ ] **Step 5: Commit** (background — poi verifica con `git log`)

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/SeedPdfImageRegionsCommand.cs \
        apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/SeedPdfImageRegionsCommandHandler.cs \
        apps/api/src/Api/Routing/AdminPdfManagementEndpoints.cs \
        apps/api/tests/Api.Tests/Unit/DocumentProcessing/SeedPdfImageRegionsCommandHandlerTests.cs
git commit -m "feat(rag): soglia area configurabile per-seed nell'endpoint image-regions (#3456)"
```

---

### Task 3: Rebuild + verifica immagine `unstructured-service` (#3455)

**Files:** nessuna modifica sorgente (il sorgente ha già #3406). Task ops, nessun commit.

**Interfaces:** verifica AC3 (immagine ricostruita contiene `normalized_bbox`).

- [ ] **Step 1: Build dell'immagine unstructured-service dal sorgente attuale**

Il build è lungo (unstructured + torch + NLTK) → eseguilo in background e attendi la notifica.

Run (background): `cd infra && docker compose build unstructured-service`
Expected: `Successfully built` / servizio buildato senza errori.

- [ ] **Step 2: Avvia il servizio e verifica `#3406` dentro l'immagine ricostruita**

```bash
cd infra && docker compose up -d unstructured-service
docker exec meepleai-unstructured grep -rn "normalized_bbox" /app/src/api/coordinates.py
```
Expected: la `grep` trova `bbox=normalized_bbox(el)` / `def normalized_bbox` → conferma che l'immagine ricostruita include SP-B #3406.

Se il container non parte per dipendenze mancanti (rete/env `[ai]`), ripiega sulla verifica statica dal builder:
```bash
cd infra && docker compose run --rm --no-deps unstructured-service grep -rn "normalized_bbox" /app/src/api/coordinates.py
```

- [ ] **Step 3: (Opzionale) Smoke `/extract` se agricola è disponibile localmente**

Solo se il PDF agricola è raggiungibile in locale: `curl -F file=@<agricola.pdf> -F strategy=hi_res http://localhost:8001/extract` e verifica che gli `Image`/`FigureCaption` abbiano `bbox` popolato. Altrimenti salta (Step 2 è sufficiente per AC3); la verifica su staging resta checklist SSH documentata (Task 4).

---

### Task 4: Documenta la dipendenza rebuild↔coordinates nel runbook (#3455)

**Files:**
- Modify: `docs/for-developers/operations/deploy-staging-runbook.md`

**Interfaces:** verifica AC4.

- [ ] **Step 1: Aggiungi la nota di rebuild**

Trova nel runbook una sezione adatta (deploy dei servizi / build immagini). Aggiungi un blocco con questo contenuto (adatta i titoli alla struttura esistente del file):

```markdown
### ⚠️ Rebuild `unstructured-service` quando cambia la pipeline coordinate

Quando modifichi `apps/unstructured-service/src/api/coordinates.py` o `schemas.py`
(pipeline delle coordinate bbox, es. SP-B #3406), l'immagine Docker deployata **NON**
si aggiorna da sola: `partition_pdf` continua a girare sul codice bakeato nell'immagine.

Sintomo di immagine stantia: `POST /extract` (strategy=hi_res) restituisce elementi
`Image`/`FigureCaption` **senza** `bbox` (0 bbox), pur avendo la libreria le coordinate.

Rebuild + verifica:
```bash
cd infra && docker compose build unstructured-service && docker compose up -d unstructured-service
docker exec meepleai-unstructured grep -n normalized_bbox /app/src/api/coordinates.py   # deve trovare #3406
```

Ref: #3455 (epic #3435), slice #3447.
```

- [ ] **Step 2: Commit** (background — poi verifica con `git log`)

```bash
git add docs/for-developers/operations/deploy-staging-runbook.md
git commit -m "docs(ops): nota rebuild unstructured quando cambia coordinates.py (#3455)"
```

---

### Task 5: Gate finale + apertura PR

**Files:** nessuna modifica; verifica AC5 e apertura PR.

- [ ] **Step 1: Build + suite unit DocumentProcessing verde**

Run: `cd apps/api && dotnet test MeepleAI.Api.sln --filter "BoundedContext=DocumentProcessing&Category=Unit" --nologo`
Expected: PASS, 0 fail (baseline invariata).

- [ ] **Step 2: Push del branch** (background — verifica con `git status`/`git log origin/...`)

```bash
git push -u origin feature/issue-3456-image-region-area-filter
```

- [ ] **Step 3: Apri la PR verso il parent `main-dev`**

```bash
gh pr create --base main-dev --head feature/issue-3456-image-region-area-filter \
  --title "feat(rag): filtro area anti-rumore image-regions + rebuild unstructured (#3456, #3455)" \
  --body "Closes #3456\nCloses #3455\n\n- #3456: filtro area-minima (default 3%, configurabile per-seed) in ImageRegionExtractor.\n- #3455: rebuild immagine unstructured (sorgente già #3406) + nota runbook deploy.\n\nSpec: docs/superpowers/specs/2026-08-02-image-region-area-filter-design.md"
```

- [ ] **Step 4: Aggiorna le checkbox/DoD delle issue #3456 e #3455 su GitHub** (spuntare le azioni completate; annotare che verifica staging via SSH e smoke agricola restano ops post-merge). Chiudere via merge PR (auto-delete branch attivo).

---

## Note per l'esecutore

- **Ordine**: Task 1 → 2 (dipendenza: 2 usa la firma/const di 1). Task 3/4 (#3455) sono indipendenti da 1/2 e possono procedere in parallelo. Task 5 chiude.
- **Rischio Task 3**: il build dell'immagine unstructured può richiedere 10-30 min e dipendere dalla rete; se fallisce/eccede, il codice #3456 (Task 1-2) e la doc (Task 4) restano deliverable committabili — annotare l'esito del rebuild nella issue senza bloccare la PR.
