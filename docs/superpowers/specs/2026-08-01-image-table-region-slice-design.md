# Image-Table Region Grounding — Vertical Slice (Opzione E MVP)

**Data**: 2026-08-01
**Tipo**: design (slice) — brainstorming approvato
**Issue**: #3447 · **Epic**: #3435 · **Spec padre**: `2026-08-01-image-table-region-grounding-design.md` (§5bis Opzione E)
**Branch**: `feature/issue-3447-image-table-region-slice` (parent `main-dev`)

## Obiettivo

Provare **end-to-end su UN PDF** che una regione-tabella catturata da `hi_res` (Unstructured) può essere
**persistita** e **disegnata** sulla pagina PDF nel viewer, per **validare il meccanismo e giudicarne il valore**
prima di investire nella feature completa (router + job async + linkage).

**Non-goal (deferiti, tracciati in #3435)**: router "quali PDF", job async hi_res in-pipeline, linkage
citazione→regione (DC-F), estrazione contenuto (Metà C / VLM), scala corpus.

## Contesto (dall'investigazione #3419)

- `hi_res` emette elementi `Image`/`FigureCaption` **con bbox normalizzata [0,1]** (SP-B #3406) → è la *regione*
  della tabella-immagine. (`fast` non le emette — DC-A.)
- ⚠️ Il pipeline **oggi scarta** questi elementi: `UnstructuredPdfTextExtractor.MapStructuredElements` filtra
  `Where(e => !string.IsNullOrWhiteSpace(e.Text))`, e `Image`/`FigureCaption` hanno testo vuoto → droppati.
- `hi_res` è lento (~185-223s) > timeout API 120s → la **cattura via API sincrona non è fattibile**; per lo slice si
  usa una **cattura seed** (chiamata diretta a `unstructured`), il trigger produttizzato async è la feature vera.
- #3403 ha già `PdfBBoxOverlay` FE (rect %-based, child di `<Page>`) → riusabile per disegnare le regioni.

## Decisioni (dal brainstorming)

| # | Decisione | Motivazione |
|---|---|---|
| **S-1** | **Store dedicato** `pdf_image_regions` (non riuso di `text_chunks.bounding_boxes_json`) | Le regioni-immagine non hanno un chunk-testo; store separato disaccoppia dal linkage debole (DC-F) |
| **S-2** | **Mostra su apertura PDF** (non gated su citazione) | Dimostra il meccanismo direttamente (apri PDF → tabelle riquadrate); indipendente dal linkage |
| **S-3** | **Cattura via seed** (hi_res diretto, non endpoint sincrono) | hi_res > timeout API 120s; il trigger async produttizzato è fuori-slice |
| **S-4** | **Copyright gating notato ma deferito** | Dati seed, endpoint read-only; ma le regioni geometriche vanno gated `CopyrightTier=Full` (come #3403 DA-4) **prima** di qualsiasi rollout user-facing — prerequisito documentato, non nello slice |

## Architettura

```
[seed one-off]                                    [runtime]
unstructured hi_res(agricola)                     FE PdfViewer (open)
  elements[Image|FigureCaption].bbox [0,1]           │ GET /api/v1/pdf/{id}/image-regions
   └─ parse → PdfImageRegionEntity ──► pdf_image_regions ──► GetPdfImageRegionsQuery
        (pdf_id, page, x,y,w,h, element_type)                  └─ ImageRegionDto[]
                                                          └─ PdfImageRegionOverlay (%-based, child di <Page>)
```

### Componenti

1. **DB — `pdf_image_regions`** (migration EF, additiva)
   - `Id guid PK · PdfDocumentId guid FK→pdf_documents · PageNumber int · X,Y,Width,Height double (0,1 top-left) ·
     ElementType text (Image|FigureCaption) · CreatedAt timestamptz`
   - Index su `PdfDocumentId`. Nessun impatto sulle tabelle esistenti.

2. **Domain/Infra — capture**
   - Un helper riusa il parsing esistente ma **conserva** `Image`/`FigureCaption` con bbox (che oggi vengono droppati).
     Firma pura: `IReadOnlyList<PdfImageRegion> ExtractImageRegions(UnstructuredExtractionResponse)` (o equivalente sui
     raw elements) — testabile senza HTTP.
   - Seed one-off: comando/handler `SeedPdfImageRegionsCommand(PdfId, hiResJson)` che parsea e upsert (idempotente:
     replace-by-pdf). Invocato da un **admin endpoint** `POST /api/v1/admin/pdf/{id}/seed-image-regions` (gated admin,
     accetta il JSON hi_res nel body). Per lo slice, l'`hiResJson` di agricola si ottiene da una chiamata diretta a
     `unstructured` (`docker exec ... curl -F strategy=hi_res`, come nell'investigazione #3419) e si posta all'endpoint.
     *(Il trigger produttizzato che chiama hi_res async da solo è la feature vera, fuori-slice.)*

3. **Application — query (CQRS)**
   - `GetPdfImageRegionsQuery(PdfId) → IReadOnlyList<ImageRegionDto{Page,X,Y,Width,Height,ElementType}>`.
   - Endpoint `GET /api/v1/pdf/{id}/image-regions` via `IMediator.Send` (CQRS, zero service injection).

4. **FE — overlay**
   - Al mount del viewer per un PDF, fetch `GET /pdf/{id}/image-regions`; per ogni pagina, disegna le regioni della
     pagina come rect %-based (child di `<Page>`), stile distinto ("tabella": bordo tratteggiato) vs highlight-citazione.
   - Riusa il pattern `PdfBBoxOverlay` (#3403) o un `PdfImageRegionOverlay` gemello.

## Data flow

Seed → `pdf_image_regions` → `GET /image-regions` → FE overlay → **validazione visiva su agricola** (i box cadono
sulle tabelle? è utile?). Questo è il **gate decisionale** per la feature completa.

## Error handling / degradazione

- PDF senza regioni → `[]` → il viewer non disegna nulla (nessun errore).
- bbox fuori [0,1] → clamp (difensivo, coerente con #3403 DA-1).

## Testing

- **BE unit**: `ExtractImageRegions` cattura `Image`/`FigureCaption` con bbox da una risposta hi_res mockata, **scarta**
  gli altri tipi; il seed command upsert idempotente; la query ritorna le regioni per pdf_id.
- **BE integration**: migration applica; endpoint ritorna il DTO (Testcontainers).
- **FE unit**: l'overlay disegna N rect con left/top/width/height %-based corretti; `[]` → nessun rect.
- **Validazione manuale**: seed agricola su staging → apri il PDF → verifica box-su-tabella + valore.

## Criteri di accettazione

- **AC1** — Applicata la migration, `pdf_image_regions` esiste; seed di agricola popola ≥1 regione `Image`/`FigureCaption`.
- **AC2** — `GET /api/v1/pdf/{agricola}/image-regions` ritorna le regioni con bbox [0,1] e `elementType`.
- **AC3** — Nel viewer, aprendo agricola, le regioni sono disegnate come rect sulle pagine corrette.
- **AC4** — PDF senza regioni → `[]` → viewer senza rect, nessun errore.
- **AC5** — Baseline test invariata (0 fail); build BE/FE verdi.

## Riferimenti

- Spec padre `2026-08-01-image-table-region-grounding-design.md` (§5bis Opzione E, §2 retrievability, DC-A/DC-F).
- #3403 (`bounding_boxes_json`/`PdfBBoxOverlay`, DA-1 normalizzazione, DA-4 copyright).
- `UnstructuredPdfTextExtractor.MapStructuredElements` (filtro empty-text che oggi droppa Image/FigureCaption).
