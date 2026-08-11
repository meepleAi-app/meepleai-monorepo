# Image-Region Anti-Noise Filter + Unstructured Image Refresh

**Data**: 2026-08-02
**Tipo**: design — brainstorming approvato
**Issue**: #3456 (F2, filtro) · #3455 (F1, immagine unstructured) · **Epic**: #3435 · **Origine**: validazione slice #3447
**Branch**: `feature/issue-3456-image-region-area-filter` (parent `main-dev`)

## Obiettivo

Ridurre il rumore nella cattura delle regioni immagine-tabella (#3447) e garantire che l'immagine
`unstructured-service` emetta i bbox necessari:

- **#3456** — le 613 regioni catturate su agricola sono dominate da icone/glifi minuscoli (mediana area
  **0.03%** pagina); solo **~32** hanno area >3% e cadono sulle vere figure/tessere/board. Introdurre un
  **filtro area minima** lato estrazione.
- **#3455** — `/extract` restituiva 0 bbox perché l'immagine Docker deployata **precede** SP-B #3406
  (`bbox=normalized_bbox(el)`). Il sorgente è già a posto; serve **rebuild** dell'immagine + nota nel
  runbook di deploy.

## Contesto

- `ImageRegionExtractor.FromHiResJson` (`apps/api/.../DocumentProcessing/Application/Services/`) oggi tiene
  **tutti** gli `Image`/`FigureCaption` con bbox. L'area normalizzata di una regione è `Width * Height` ∈ [0,1]
  (frazione di pagina). Consumatore: `SeedPdfImageRegionsCommandHandler` → `pdf_image_regions`.
- `apps/unstructured-service/src/api/coordinates.py` contiene già `normalized_bbox` (#3406). Il difetto #3455
  è puramente sull'**immagine Docker** buildata, non sul sorgente.

## Decisioni (dal brainstorming)

| # | Decisione | Motivazione |
|---|-----------|-------------|
| **D-1** | Filtro **lato BE estrazione** (in `FromHiResJson`), non FE resa | DB `pdf_image_regions` pulito (~32 vs 613), query/overlay semplici; ritaratura = re-seed (già idempotente replace-by-pdf) |
| **D-2** | Soglia **configurabile per-seed**, default **3%** (`0.03`) | #3456 chiede soglia "tarata su più rulebook"; esporla nel command/request permette taratura per-rulebook senza redeploy |
| **D-3** | **Solo area**, niente filtro per-tipo | YAGNI: l'obiettivo sono figure/tessere/board di area grande; un ramo per-tipo aggiunge complessità senza guadagno dimostrato. `FigureCaption` legittime di area >soglia restano incluse |
| **D-4** | Area calcolata sui valori **già clampati** [0,1] | Coerente con ciò che l'overlay disegna; evita che un bbox degenere fuori-range superi la soglia |
| **D-5** | Confronto **`>=`** (soglia inclusiva) | Determinismo su valori-limite; una regione esattamente alla soglia è "abbastanza grande" |
| **D-6** | #3455: **rebuild immagine + doc**, nessuna modifica Python | Il sorgente ha già #3406; il valore è ricostruire l'immagine e documentare la dipendenza |
| **D-7** | **Un branch/PR** per le due issue | Accoppiate: il filtro #3456 si valida end-to-end solo con un'immagine unstructured che emette bbox (#3455) |

## Architettura / modifiche

### #3456 — filtro area (BE)

```
FromHiResJson(hiResJson, minAreaFraction = 0.03)
  parse → Where(category ∈ {Image,FigureCaption} && bbox != null)
        → Select(clamp01 su X,Y,W,H)
        → Where(W * H >= minAreaFraction)      ← NUOVO filtro anti-rumore
        → ExtractedImageRegion[]
```

- **`ImageRegionExtractor.FromHiResJson`**: nuovo parametro `double minAreaFraction = 0.03`. Il filtro area
  si applica **dopo** il clamp (D-4), con `>=` (D-5). Firma pura, testabile senza HTTP.
- **`SeedImageRegionsRequest`** (record del body admin): nuovo campo `double? MinAreaFraction` opzionale.
- **`SeedPdfImageRegionsCommand`**: nuovo campo `double? MinAreaFraction`.
- **`SeedPdfImageRegionsCommandHandler`**: chiama `FromHiResJson(cmd.HiResJson, cmd.MinAreaFraction ?? 0.03)`.
- **`AdminPdfManagementEndpoints.SeedImageRegions`**: propaga `request.MinAreaFraction` al command.

### #3455 — immagine unstructured

- **Rebuild**: `docker compose build unstructured-service` + `up -d`; verifica `grep normalized_bbox` dentro il
  container conferma la presenza di #3406 nell'immagine ricostruita. Test `/extract` su agricola solo se il PDF
  è disponibile localmente.
- **Doc**: nota in `docs/for-developers/operations/deploy-staging-runbook.md` — l'immagine `unstructured-service`
  va ri-buildata quando cambia la pipeline coordinate (`coordinates.py`/`schemas.py`), con comando di verifica.

## Error handling / degradazione

- Input null/vuoto/JSON invalido → `[]` (invariato).
- Nessuna regione sopra soglia → `[]` → viewer senza rect, nessun errore (coerente con AC4 di #3447).
- `minAreaFraction` fuori range non è validato lato dominio: 0 = nessun filtro (retro-compat effettiva),
  >1 = tutto droppato; la responsabilità della taratura è del chiamante seed.

## Testing

- **BE unit** (`ImageRegionExtractorTests`):
  - fixture aggiornato: `FigureCaption` area >3% resta "tenuta"; aggiunta una `Image` piccola (<3%) droppata.
  - `FromHiResJson_DropsRegionsBelowMinArea` — regione sotto soglia scartata, sopra soglia tenuta.
  - `FromHiResJson_RespectsCustomMinArea` — soglia custom (es. 0.10) cambia l'esito.
  - `FromHiResJson_AreaExactlyAtThreshold_IsKept` — edge `>=`.
  - `FromHiResJson_DefaultThreshold_IsThreePercent` — verifica default.
  - i test null/empty/invalid e clamp restano verdi.
- **BE unit** (handler, se necessario): il default 3% è applicato quando `MinAreaFraction` è null.
- **Gate**: build BE verde; baseline unit invariata (0 fail).

## Criteri di accettazione

- **AC1** (#3456) — `FromHiResJson` con default scarta le regioni con `W*H < 0.03`, tiene le altre.
- **AC2** (#3456) — la soglia è sovrascrivibile per-seed via `SeedImageRegionsRequest.MinAreaFraction`.
- **AC3** (#3455) — immagine `unstructured-service` ricostruita contiene `normalized_bbox` (verifica in-container).
- **AC4** (#3455) — `deploy-staging-runbook.md` documenta la dipendenza rebuild↔coordinates.
- **AC5** — build BE verde; baseline test invariata.

## Non-goal (deferiti a #3435)

- Trigger async hi_res in-pipeline; linkage citazione→regione; estrazione contenuto; scala corpus.
- Filtro per-tipo / privilegiare `FigureCaption` (D-3): rivalutabile se la taratura su più rulebook lo richiede.
- Verifica immagine staging/prod via SSH: resta checklist ops documentata (non eseguibile da questa sessione).

## Riferimenti

- Slice #3447: `docs/superpowers/specs/2026-08-01-image-table-region-slice-design.md`.
- SP-B #3406 (`coordinates.py`/`normalized_bbox`), #3403 (`PdfBBoxOverlay`, normalizzazione/clamp).
