# Cover Stack Live Audit — 2026-06-02

> **Scope**: verifica post-deploy della "cover stack" — umbrella **[#1821](https://github.com/meepleAi-app/meepleai-monorepo/issues/1821)** "Game cover images — eliminate BGG runtime dependency, legal-clean stack".
>
> **Timeline merge** (tutti su `main-dev`, 2026-06-02):
> - 17:25 UTC — PR [#1825](https://github.com/meepleAi-app/meepleai-monorepo/pull/1825) — L1 placeholder deterministico (#1822 ✅ CLOSED)
> - 18:12 UTC — PR [#1829](https://github.com/meepleAi-app/meepleai-monorepo/pull/1829) — L1 follow-up `HybridHubItem.id` → `MeepleCard`
> - 19:37 UTC — PR [#1839](https://github.com/meepleAi-app/meepleai-monorepo/pull/1839) — L4 `IPdfCoverExtractor` service + L2/L3 DB columns (#1823 / #1824 stub)
> - 19:48 UTC — PR [#1843](https://github.com/meepleAi-app/meepleai-monorepo/pull/1843) — L4 wiring in `PdfProcessingPipelineService`
>
> **Methodology**: static code analysis (acceptance criteria #1821 sono verificabili strutturalmente dal codice) + UI snapshot live (mobile 390×844).
>
> **Live snapshot environment**: presumibilmente staging `https://meepleai.app` (timestamp screenshot `2026-06-02 20:46` ≈ T+58min dal merge #1843; pattern Catan + admin coerente con [`2026-06-02-mobile-golden-path-audit.md`](./2026-06-02-mobile-golden-path-audit.md)). Da confermare nel prossimo refresh dell'audit.

## Top-level outcome

- **L1 #1822 — VERIFIED ✅** (static + UI). Codice strutturalmente impedisce qualsiasi richiesta HTTP runtime verso CDN BGG; placeholder deterministico visibile live (Catan → gradient verde + "C").
- **L2 #1823 — DB stub merged, implementation OPEN ⏳**. Colonne pronte per future enrichment Wikidata; nessun rendering live.
- **L3 #1824 — DB stub merged, implementation OPEN ⏳**. Colonne pronte per future user-uploaded cover; nessuna UI di upload ancora.
- **L4 #1831 — Service+pipeline shipped MA wiring end-to-end INCOMPLETO ❌**. Vedi sezione [End-to-end wiring gap](#-end-to-end-wiring-gap-l4) qui sotto: pipeline genera+salva webp in R2 e persiste `CoverR2Key` su `PdfDocumentEntity`, ma **nessun glue** propaga la cover alla view library + **nessun resolver endpoint** + **FE non consuma `CoverR2Key`**.
- **1 cleanup-debt** documentato: `next.config.js` ha ancora `cf.geekdo-images.com` nei `remotePatterns` Next/Image — whitelist morta, da rimuovere.

## 🚨 End-to-end wiring gap (L4)

Static analysis post-PR #1839 / #1843 rivela che la "spina dorsale" L4 è completa fino al DB ma il **last mile verso la library card è assente**. L'umbrella #1821 considera L4 "shipped" solo quando la cover PDF è effettivamente visibile sulle game card; allo stato corrente NON lo è.

### Gap A — Propagation + Resolver mancanti (MAJOR)

**Topologia colonne cover post-merge**:

| Layer | Entity | Column(s) | Popolato da |
|---|---|---|---|
| L4 | `PdfDocumentEntity` | `CoverR2Key`, `CoverGenerationStatus`, `CoverPageIndex`, `CoverGenerationError` | ✅ `PdfProcessingPipelineService.ExtractCoverImageAsync` (linee 498-574) |
| L2 | `SharedGameEntity` | `WikidataCoverR2Key`, `WikidataCoverSourceUrl`, `WikidataCoverLicense`, `WikidataCoverAttribution` | ⏳ stub, nessun job |
| L3 | `UserLibraryEntryEntity` | `CustomCoverR2Key` | ⏳ stub, nessun endpoint upload |
| L1 | (in-FE) | — | ✅ `Cover.tsx` + `GameCoverPlaceholder.tsx` |

**Cosa manca**:
1. **Nessun campo `SharedGameEntity.PdfCoverR2Key`** (o equivalente). La cover L4 estratta resta su `PdfDocumentEntity`, non si riflette mai sulla SharedGame.
2. **Nessun resolver endpoint** che, dato un `gameId`, decida la cover URL secondo priorità L3 → L4 → L2 → L1 fallback. Es. `GET /api/v1/games/{id}/cover` o `GET /api/v1/shared-games/{id}/cover/{size}.webp`.
3. **FE non consume `CoverR2Key`**: `grep -r "CoverR2Key" apps/web` → **0 matches**. La FE legge solo `SharedGameEntity.ImageUrl` (campo legacy BGG ora filtrato da `shouldUsePlaceholder` → placeholder L1).

**Effetto runtime**: anche dopo upload PDF + estrazione cover riuscita, la library card continua a mostrare il placeholder L1 — perché `MeepleCard` non sa nulla del PDF cover.

**Repro logico**:
1. User carica `rulebook-catan.pdf` su `/library/<catan-id>/kb`.
2. Pipeline `PdfProcessingPipelineService.ProcessAsync` esegue `ExtractCoverImageAsync` (linea 148) — assume `Outcome=Generated`.
3. Webp salvati in R2 path `GameImage/pdf-cover-<pdf-id>/{thumb,preview}.webp` (linee 523-534).
4. `pdfDoc.CoverR2Key = "pdf-cover-<pdf-id>"` (linea 536).
5. SaveChanges → DB ok.
6. FE refresh `/library` → query restituisce `SharedGameEntity` → `ImageUrl` invariato (BGG legacy) → `shouldUsePlaceholder` → **placeholder L1**.
7. Conclusione: la cover PDF è dormiente in R2, mai mostrata.

**Issue suggerite per chiudere il gap**:
- Aggiungere `SharedGameEntity.PdfCoverR2Key` (denormalizzato) + projector handler che, su `PdfDocumentReady` event, copia `PdfDocumentEntity.CoverR2Key` sulla `SharedGameEntity` corrispondente (one-to-one via `SharedGameId`).
- Aggiungere resolver endpoint `GET /api/v1/games/{id}/cover-url` (o computed field in `SharedGameDto.CoverUrl`) con priorità L3 → L4 → L2 → null.
- FE: `MeepleCard` consuma `CoverUrl` invece di `ImageUrl`; `shouldUsePlaceholder` agisce solo su URL legacy BGG come safety net residua.

### Gap B — `ExtractCoverImageAsync` legge filesystem invece di R2 (POTENTIAL)

In `PdfProcessingPipelineService.cs:511`:

```csharp
var pdfBytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
```

Confronto con `ExtractTextAsync` (linee 437-451) che invece usa correttamente il blob storage con fallback filesystem:

```csharp
var fileStream = await _blobStorageService.RetrieveAsync(fileId, BlobCategory.Pdf, fileId, cancellationToken);
if (fileStream == null) {
    // Fallback to local filesystem for backward compatibility (dev without S3)
    ...
}
```

**Rischio**: in staging/prod (storage S3/R2 attivo, vedi `STORAGE_PROVIDER=s3`), il PDF NON è su `filePath` (è in bucket R2). `File.ReadAllBytesAsync(filePath)` solleva `FileNotFoundException` → catch generic (linea 566) → `CoverGenerationStatus = "Failed"` + `CoverGenerationError = "Could not find file …"`.

**Mitigation**: la pipeline non crasha (best-effort), ma L4 è permanentemente broken in prod fino al fix. Sintomo visibile via:

```sql
SELECT cover_generation_status, COUNT(*)
FROM pdf_documents
WHERE uploaded_at >= '2026-06-02 19:48 UTC'  -- post merge #1843
GROUP BY cover_generation_status;
```

Se vediamo `Failed` >> `Generated` per i PDF post-merge, Gap B è confermato.

**Fix proposto**: replicare il pattern `_blobStorageService.RetrieveAsync` di `ExtractTextAsync`, oppure rifattorizzare un helper `LoadPdfBytesAsync(pdfDoc, filePath)` condiviso.

### Implicazioni per il L4 acceptance criteria

L'AC originale di #1821 dice:
> L1 ships e `/library` shows zero `cf.geekdo-images.com` requests …

Il subset L4 NON ha AC esplicito in #1821; la spec di #1831 (issue body) suggerisce "PDF-derived cover from first-page render". Operativamente questo richiede:
- [ ] Cover PDF visibile su `/library` per giochi con PDF caricato (oggi: NO).
- [ ] Cover preview 600×900 visibile su `/library/<id>` detail (oggi: NO).
- [ ] Fallback graceful a L1 quando `CoverGenerationStatus ∈ {Skipped, Failed}` (oggi: SÌ, ma per il motivo sbagliato — *tutto* fallisce graceful perché niente è connesso).

## Acceptance criteria check ([#1821](https://github.com/meepleAi-app/meepleai-monorepo/issues/1821))

| AC | Status | Evidence |
|---|---|---|
| L1 ships e `/library` shows zero `cf.geekdo-images.com` requests in browser network panel | ✅ **VERIFIED via codice** | `apps/web/src/lib/games/cover-utils.ts:21-25` (`BLOCKED_IMAGE_HOSTS`) + `apps/web/src/components/ui/data-display/meeple-card/parts/Cover.tsx:42-43` (gate `shouldUsePlaceholder` PRIMA del `<img src>`). UI live: `audit-01-library-l1-placeholder-mobile-390.png` mostra Catan con placeholder L1 (no `<img>` BGG renderato). |
| DB field `SharedGameCatalogEntry.ImageUrl` non più rendered as-is sul client quando punta a BGG (treated as missing) | ✅ **VERIFIED via codice** | `shouldUsePlaceholder` in `cover-utils.ts:38-55` parsea URL, normalizza hostname, restituisce `true` per host in `BLOCKED_IMAGE_HOSTS` o relativi subdomain. Test esaustivi in `cover-utils.test.ts:16-28` (BGG host + subdomain). |
| L2+L3 ship come follow-up; placeholder resta universal fallback | ⏳ **DB stub only** | Issue [#1823](https://github.com/meepleAi-app/meepleai-monorepo/issues/1823) (Wikidata) e [#1824](https://github.com/meepleAi-app/meepleai-monorepo/issues/1824) (user upload) restano **OPEN**. Le colonne DB sono presenti come stub (PR #1839) ma nessuna logica di rendering / upload. Placeholder L1 resta universal fallback come previsto. |
| Footer attribution Wikidata/Commons quando L2 lands | ⏳ **N/A** | Diventerà attivo con #1823. |

## Findings dettagliati per layer

### L1 — Placeholder deterministico ✅

**Componenti principali**:
- `apps/web/src/lib/games/cover-utils.ts` — gate `shouldUsePlaceholder`, hash determ., extract initials (i18n-aware con stop-words IT/EN)
- `apps/web/src/components/ui/data-display/meeple-card/parts/GameCoverPlaceholder.tsx` — render gradient HSL + initials + meeple silhouette SVG, SSR-safe, `data-testid="game-cover-placeholder"`
- `apps/web/src/components/ui/data-display/meeple-card/parts/Cover.tsx:42-72` — gate runtime, fallback `onError` se l'`<img>` legittimo fallisce, doppio placeholder (rich `GameCoverPlaceholder` per game-entity con `gameId`, fallback entity-icon altrove)

**Evidence UI** (`audit-01-library-l1-placeholder-mobile-390.png`):
- Mobile 390×844, route `/library`, user con 1 gioco
- Catan card mostra:
  - Gradient verde scuro 135° (hash di `gameId="cc1678e8-…"` → hue determ.)
  - Initials "C" centrato, font Quicksand bold, white-on-bg WCAG AA
  - Meeple silhouette decorativa opacità 0.12
  - Badge "GAME" arancione (entity color)
  - Title "Catan" + rating 7.1 ★★★★

**Test coverage** (✅ comprehensive):
- `cover-utils.test.ts` — BGG host + subdomain + R2 (`covers.meepleai.app`) + data URLs + relative paths + invalid input
- `GameCoverPlaceholder.test.tsx` — render + a11y `aria-hidden`

**Note residue** (non blocking):
- `apps/web/next.config.js:131-133` ha `cf.geekdo-images.com` nei `remotePatterns` — dead config (Next/Image non viene mai chiamato per BGG perché `Cover.tsx` usa `<img>` raw + gate). Da rimuovere come cleanup-debt.
- Seed manifests (`apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/{dev,staging,prod}.yml`) e snapshot SQL (`infra/data/snapshots/…sql`) contengono BGG URLs nel campo `ImageUrl`. Comportamento corretto a runtime (bloccati da `shouldUsePlaceholder`), ma valore di colonna inquinato → futura migrazione a `ImageUrl = NULL` per i record affetti (issue cleanup separata, low priority).

### L2 — Wikidata enrichment ⏳ DB stub only

- Issue [#1823](https://github.com/meepleAi-app/meepleai-monorepo/issues/1823) — **OPEN P2** (`area/backend`, `area/frontend`).
- PR #1839 ha aggiunto colonne DB stub al catalog (per future enrichment job).
- Nessuna job/rendering attivo. Nessun footer Wikidata/Commons attribution.
- **Next step**: implementare enrichment job (server-side, scheduled) + footer attribution.

### L3 — User-uploaded custom cover ⏳ DB stub only

- Issue [#1824](https://github.com/meepleAi-app/meepleai-monorepo/issues/1824) — **OPEN P2** (`area/frontend`).
- PR #1839 ha aggiunto colonne DB stub.
- Nessuna UI di upload, nessun endpoint dedicato.
- **Next step**: design upload flow (drawer su `/library/[gameId]` settings? o standalone `/upload`?) + endpoint + storage R2.

### L4 — PDF-derived cover ❌ Service shipped, wiring INCOMPLETE

> Vedi [End-to-end wiring gap (L4)](#-end-to-end-wiring-gap-l4) qui sopra per i 2 gap (A: propagation/resolver, B: filesystem-vs-R2). Questa sezione documenta solo i **componenti merged** e le verifiche tecniche di basso livello.

**Componenti merged** (PR #1839 + #1843):
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/IPdfCoverExtractor.cs` — interface, outcome discriminated (`Generated` / `Skipped` / `Failed`).
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfCoverExtractor.cs` — impl con Docnet.Core (PDFium) + SkiaSharp; heuristica `nonWhitePixelRatio - textLength/2000` su prime 3 pagine, render webp 200×300 thumbnail + 600×900 preview. Decisione documentata in-source.
- `PdfProcessingPipelineService.cs:51, 70-88` — `IPdfCoverExtractor?` iniettato opzionale.
- `PdfProcessingPipelineService.cs:498-574` — `ExtractCoverImageAsync` invocato a linea 148 dopo language detection.
- DI registration: `DocumentProcessingServiceExtensions.cs:164` — `services.AddScoped<IPdfCoverExtractor, PdfCoverExtractor>()`.
- DB columns (migration `20260602185637_AddPdfCoverExtractionColumns`):
  - `pdf_documents.cover_r2_key` (varchar 512 NULL)
  - `pdf_documents.cover_generation_status` (varchar 32 NOT NULL DEFAULT 'Pending')
  - `pdf_documents.cover_page_index` (int NULL)
  - `pdf_documents.cover_generation_error` (varchar 512 NULL)
  - index `ix_pdf_documents_cover_generation_status`
- Storage path: `BlobCategory.GameImage` / resource key `pdf-cover-{pdf-id}` / files `thumb.webp` + `preview.webp` (linee 523-534).
- Test unit: `apps/api/tests/Api.Tests/Unit/DocumentProcessing/PdfCoverExtractorTests.cs`.

**Cosa è verificato (static)**: pipeline invoke + outcome switch + storage + DB persistence + best-effort error handling (linea 566 catch generic).

**Cosa NON è verificato (live)**:
- Estrazione real-world: outcome `Generated` vs `Skipped` vs `Failed` su PDF reali (es. rulebook Catan 244 KB già su staging).
- Performance dell'estrazione (timeout, memory, retry).
- **Gap A**: anche con `Generated`, cover non visibile su library card (nessun propagator/resolver/FE consumer).
- **Gap B**: in prod con S3/R2, `File.ReadAllBytesAsync(filePath)` può fallire → `Failed` permanente.

**Repro per live verification (follow-up, dopo che Gap A+B sono fixati)**:
1. Login staging come `admin@meepleai.app`.
2. `/library/<gameId>/kb` → "Carica PDF" con rulebook nuovo (es. Wingspan).
3. Attendere completamento pipeline (smoke: 60s; vero target: <30s per PDF medio).
4. Query DB: `SELECT cover_generation_status, cover_r2_key, cover_page_index FROM pdf_documents WHERE id = '<new-pdf-id>'` → atteso `Generated`.
5. Tornare a `/library` → atteso: card del gioco mostra cover PDF estratto.
6. DevTools Network panel su quel card → `<img src>` deve puntare al resolver/R2 URL (non al placeholder inline SVG).

**Diagnostica Gap B in prod** (può essere fatto ORA, senza fix):

```sql
-- Verifica rate of failed PDF cover extractions post-merge #1843
SELECT
  cover_generation_status,
  COUNT(*) AS docs,
  MIN(uploaded_at) AS first,
  MAX(uploaded_at) AS last
FROM pdf_documents
WHERE uploaded_at >= '2026-06-02 19:48:00 UTC'
GROUP BY cover_generation_status
ORDER BY docs DESC;
```

Se `Failed >> Generated` → Gap B confermato (filesystem read non funziona con bucket attivo).

## Static-analysis residui geekdo (audit completo)

`grep -ri "geekdo" --type-not snapshot` totale **45 file**. Classificazione:

| Categoria | File count | Runtime impact | Action |
|---|---:|---|---|
| FE runtime gate (intenzionale) | 2 | Block-list per `shouldUsePlaceholder` | ✅ Keep |
| Test coverage gate | 2 | Verify gate works | ✅ Keep |
| Next/Image whitelist morta | 1 | Nessuno (gate intercetta prima) | 🧹 Remove |
| BE BGG API client (server-side enrichment, future #1823) | ~10 | Nessuno — non chiamato per cover display | ✅ Keep (server-side) |
| Mockup HTML statici (admin-mockups) | ~15 | Nessuno (design files, non shipped) | ✅ Keep |
| Seed manifests + snapshot SQL (`ImageUrl` legacy) | ~6 | DB pollution; runtime safe | 🧹 Migration follow-up (low priority) |
| Storybook + visual-test fixtures | ~7 | Nessuno (story files) | ✅ Keep |
| Tools script (`tools/fix-chess-game.ps1`) | 1 | One-shot ops script, BGG URL hardcoded; gate intercetta runtime | 🧹 Update se rieseguito |

## Pending verifiche (skipped per time budget / scope)

**Sbloccate ORA**:
- [ ] **Diagnostica Gap B in prod** — eseguire la query SQL nel paragrafo L4 sopra contro DB staging per misurare `Failed` vs `Generated` rate post-merge #1843. Non richiede fix; conferma o smentisce il filesystem-vs-R2 finding.
- [ ] **DevTools Network panel** live `/library` su staging: zero richieste `cf.geekdo-images.com` (richiesto da AC L1; static analysis fornisce evidenza equivalente strutturale, ma live conferma definitiva).
- [ ] **Desktop 1280×720** parity check: lo screenshot copre solo mobile 390. Verificare desktop in particolare per `HeroCard` variant (aspect-video).
- [ ] **Conferma environment screenshot** (staging vs local dev — coerenza con mobile-golden-path-audit suggerisce staging ma non confermato).

**Bloccate fino al fix di Gap A+B**:
- [ ] L4 end-to-end live: upload PDF nuovo + verifica cover estratto renderizzato su `/library`.
- [ ] L4 outcome `Skipped` su PDF text-only: comportamento atteso = fallback su L1 placeholder.

## Cleanup follow-up (separate issues)

- [ ] **Cleanup `next.config.js` `remotePatterns`** — rimuovere entry `cf.geekdo-images.com` + `**.boardgamegeek.com`. Risk: nessuno (gate FE intercetta prima); Benefit: chiarezza config + audit clean.
- [ ] **DB hygiene migration** — porre `ImageUrl = NULL` per i record `SharedGameCatalogEntry` con host in `BLOCKED_IMAGE_HOSTS` (seed + esistenti). Effetto: rimuove rumore in DB; comportamento UI invariato. Considerare come parte di #1823 enrichment job.
- [ ] **Ops script `tools/fix-chess-game.ps1`** — aggiornare con URL placeholder o R2 path se rieseguito.

## Screenshots index

| File | Layer | Route | Viewport | Notes |
|---|---|---|---|---|
| `2026-06-02-cover-stack-screenshots/audit-01-library-l1-placeholder-mobile-390.png` | L1 | `/library` | 390×844 | Catan con placeholder verde + "C" deterministic. Stats: Giochi 1 / Agenti 0 / Documenti 0 / Chat 0 (no PDF → L4 non testabile in questa run). |

## Related issues / PRs

**Issue tree**:
- 📌 [#1821](https://github.com/meepleAi-app/meepleai-monorepo/issues/1821) — Umbrella **OPEN P1**
- ✅ [#1822](https://github.com/meepleAi-app/meepleai-monorepo/issues/1822) — L1 placeholder **CLOSED 2026-06-02 17:25 UTC**
- 🟡 [#1823](https://github.com/meepleAi-app/meepleai-monorepo/issues/1823) — L2 Wikidata **OPEN P2** (DB stub merged)
- 🟡 [#1824](https://github.com/meepleAi-app/meepleai-monorepo/issues/1824) — L3 user upload **OPEN P2** (DB stub merged)
- 🟡 [#1831](https://github.com/meepleAi-app/meepleai-monorepo/issues/1831) — L4 PDF cover **OPEN P1** (service+pipeline merged, live verify pending)

**Pull requests** (all merged to `main-dev` on 2026-06-02):
- [#1825](https://github.com/meepleAi-app/meepleai-monorepo/pull/1825) — L1 placeholder (17:25 UTC)
- [#1829](https://github.com/meepleAi-app/meepleai-monorepo/pull/1829) — L1 follow-up `HybridHubItem.id` (18:12 UTC)
- [#1839](https://github.com/meepleAi-app/meepleai-monorepo/pull/1839) — L4 service + L2/L3 DB columns (19:37 UTC)
- [#1843](https://github.com/meepleAi-app/meepleai-monorepo/pull/1843) — L4 pipeline wiring (19:48 UTC)

**Cross-reference**:
- [`docs/for-developers/audits/2026-06-02-mobile-golden-path-audit.md`](./2026-06-02-mobile-golden-path-audit.md) — audit antecedente (mobile UX, pre-cover-stack); usa lo stesso Catan + admin sessione staging.
