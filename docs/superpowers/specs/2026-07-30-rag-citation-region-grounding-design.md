# RAG Citation Region Grounding — mostrare la regione PDF sorgente della risposta

**Data**: 2026-07-30
**Tipo**: design (spec-panel + audit codice)
**Epic proposta**: "RAG citation region grounding"
**Stato**: design v1 — prodotto da spec-panel multi-esperto + 2 audit codice (8 finder)
**Branch previsto epic**: feature branch per sub-progetto (parent `main-dev`)

> **Origine**: segnalazione utente — «dopo la risposta di un agent, i riferimenti al PDF
> sono tagliati (mancano lettere iniziali) e/o sono una sequenza di lettere/numeri,
> probabilmente perché nel PDF sono in tabella. La cosa importante è permettere all'utente
> di visualizzare la parte del PDF da cui l'agente ha dedotto la risposta.»

---

## 1. Diagnosi (verificata contro il codice)

Il fenomeno ha **due cause distinte che si sommano**. Vanno trattate separatamente perché
hanno costo e rimedio diversi.

### 1.1 Causa A — il testo della citazione è brutto (troncato / garbled)

Il layer di citazione **non introduce** il difetto. Ogni slice dello snippet taglia la *coda*,
è char-based UTF-16, parte da indice 0 — nessun offset byte-vs-char, nessun taglio dell'inizio:

- `AskQuestionQueryHandler.cs:574` → `Substring(0, Min(150, len))`
- `RagPromptAssemblyService.cs:443` → `AsSpan(0,117)+"..."`
- `StreamQaQueryHandler.cs:369` → testo **completo** (nessun taglio)

Il testo "tagliato/garbled" **arriva già così dal DB** (`text_chunks.Content` /
`pgvector_embeddings.text_content`), prodotto a monte da tre sorgenti:

| Sintomo | Causa | Evidenza |
|---|---|---|
| «mancano lettere iniziali» | Chunk che iniziano a **metà parola** | `TextChunkingService.cs:122-124` (~71% chunk con frammento iniziale); mitigato da `SnapToWordStart` (commit #3241) |
| «sequenza di lettere/numeri» (testo) | Marcatori **U+FFFE** al posto del trattino di sillabazione + parole spezzate su newline | `PdfTextProcessingDomainService.cs:62-79` (`NormalizeText`, riparato in #3241) |
| «sequenza di lettere/numeri» (tabelle) | Tabelle **linearizzate**: `strategy='fast'` hardcoded, `text_as_html` mai serializzato, regola di merge che fonde i token | `UnstructuredPdfTextExtractor.cs:123`; `pdf_extraction_service.py:95`; merge `([a-z])\n([a-z])→$1$2` a `PdfTextProcessingDomainService.cs:79` |

**Conseguenza operativa**: i fix #3241/#3282 sono già nel codice → i *nuovi* documenti sono a
posto. I documenti indicizzati **prima** conservano chunk corrotti nel DB → la citazione li
riproduce fedelmente. **Il codice è a posto, il corpus no.** Le tabelle **non** sono coperte da
quei fix.

### 1.2 Causa B — non si può vedere la sorgente PDF

Il frontend **ha già** un viewer PDF completo (`react-pdf`/`pdfjs-dist`): `PdfInlineViewer`,
`PdfQuoteViewer` (highlight + banner fallback), `PdfPageModal`, `CitationModal`. Il deep-link a
**pagina** (`#page=N`, `initialPage`) esiste ovunque. L'highlight della **regione**
(`highlightQuote` + `makeQuoteTextRenderer`) esiste come capacità **ma non è cablato nel percorso
chat** — è usato solo in mechanic-card e admin. Nel percorso chat `CitationPdfTab.tsx:75` monta il
viewer senza `highlightQuote`; `PdfPageModal.tsx` usa `renderTextLayer=false`.

Metadati di grounding persistiti oggi: **solo `page_number`** (a volte *stimato*
`charPos/2000+1` nel percorso flat → può puntare a pagina sbagliata), `chunk_index`, `heading`.
`CharStart/CharEnd` **calcolati ma scartati** in tutti i siti di insert. Bounding box: il VO
`ChunkMetadata.BBox` **esiste (ADR-016) ma non è mai popolato** — le coordinate di Unstructured
muoiono al confine del DTO `UnstructuredElement`.

### 1.3 Insight strategico

**Risolvere B rende A molto meno grave**: anche con snippet testuale imperfetto, se l'utente vede
la porzione reale del PDF evidenziata, la risposta è verificabile. Il requisito centrale
dell'utente è B (grounding visivo), non lo snippet perfetto.

---

## 2. Obiettivo e requisiti

**Obiettivo**: dato un messaggio dell'agente con citazione, un clic apre il PDF alla **pagina
reale** con la **regione sorgente evidenziata** (rettangoli precisi), con fallback esplicito
quando la regione non è disponibile.

Requisiti (SMART):

- **R1** — Ogni citazione porta fino al FE: `pdfDocumentId` risolto, `pageNumber` reale,
  `chunkIndex`, `charStart/charEnd`, `regions[]` (bbox normalizzate [0,1] top-left).
- **R2** — Il viewer disegna le `regions[]` come overlay preciso; se assenti, ripiega
  sull'highlight testuale esistente; se anche quello fallisce, mostra il banner fallback.
- **R3** — Le `regions[]` (highlight verbatim) sono esposte **solo per `CopyrightTier=Full`**;
  per `Protected` nessun highlight geometrico verbatim (coerenza con il copyright leak guard #447 /
  ADR-059).
- **R4** — Il grounding è **best-effort e degradabile**: PDF via SmolDocling/Docnet o senza
  text-layer (OCR) hanno `regions=null` → fallback a pagina + testo, senza errori.
- **R5** — Nessuna regressione della baseline test (fail count = 0) né del gate a11y/token.

Non-goal:

- Riscrivere l'estrazione tabelle in HTML/celle (è il Livello 2 separato; qui le tabelle
  beneficiano comunque di regione+pagina).
- Grounding a livello di parola/carattere sub-riga (MVP = regione a livello di sezione/riga).
- Denormalizzare bbox su `pgvector_embeddings` (si usa LEFT JOIN `text_chunks`).

---

## 3. Decisioni cardine

| # | Decisione | Scelta | Motivazione |
|---|---|---|---|
| **DA-1** | Normalizzazione coordinate | **In Python, top-left [0,1]** dividendo per `layout_width/height` | Chiude il problema unità (punti fast vs pixel hi_res vs DPI) al boundary; il FE è zero-math |
| **DA-2** | Persistenza bbox | Colonna **`bounding_boxes_json jsonb`** su `text_chunks` (array page-aware) | Un chunk parent copre più box/righe → serve un array, non 4 colonne float |
| **DA-3** | Denormalizzazione pgvector | **No** — LEFT JOIN `text_chunks` su `source_chunk_id` | Evita il doppio-binario raw-SQL di `pgvector_embeddings`; pattern già usato per heading #3270 |
| **DA-4** | Copyright gating | `regions[]` esposte **solo tier Full** | bbox = highlight verbatim → rischio leak su Protected (ADR-059/#447) |
| **DA-5** | Overlay FE | **Pattern B** (rects %-based, child di `<Page>`) primario; **Pattern A** (quote text-match) fallback | %-based è scale/DPR-independent, zero conversione se il wire è già normalizzato |
| **DA-6** | Char-offset | Persistere `char_start/char_end` (**già calcolati**) subito | Costo minimo (colonne + mapping), backfillabile via re-index, migliora il fallback text-highlight |
| **DA-7** | Backfill | char-offset via **re-index** (basta `StructuredElementsJson`); bbox via **ri-estrazione** (nuovo `IndexerVersion` coordinate-aware) | Le coordinate non sono mai state salvate → il corpus va ri-estratto col servizio Python coordinate-enabled |
| **DA-8** | Aggregazione box | MVP = **unione (min/max)** degli element della sezione sulla start page | Semplice; multi-pagina limitato alla start page (estensione futura: regione per pagina) |

**Da confermare (DC)**:

- **DC-1** — Unificare i 4 wire di citazione (risolvere `VectorDocumentId` vs `PdfDocumentId`,
  uniformare snippet length) è **in scope** di questa epic o cleanup separato? (Raccomandato:
  prerequisito minimo su Ask/legacy per keyare le regions su `PdfDocumentId+ChunkIndex`.)
- **DC-2** — `strategy='fast'` (box a granularità pdfminer per-riga/word) vs `hi_res` (box
  layout-block, più lento) per l'estrazione coordinate. Raccomandato: restare su `fast` (coordinate
  già disponibili), `hi_res` solo on-demand per scansioni.
- **DC-3** — Big-bang re-extract dell'intero corpus vs lazy on-next-reindex. Raccomandato:
  big-bang per-env (pattern SP3 #3269), staging prima di prod.

---

## 4. Architettura della soluzione (data-flow proposto)

```
Unstructured (Python)
  element.metadata.coordinates (points[4], system.width/height, PixelSpace top-left)
   └─(NUOVO) _normalized_bbox → bbox {x,y,width,height} ∈ [0,1]      ← DROP #1 chiuso in main.py
      ElementSchema.bbox: Optional[BBoxSchema]                        ← schema Python
        │  HTTP JSON
        ▼
UnstructuredPdfTextExtractor.cs
  UnstructuredElement.Bbox (NUOVO)  → MapStructuredElements
   └─ ExtractedElement(Text, PageNumber, ElementType, ElementBoundingBox?)  ← record esteso (param opzionale)
        │  (serializzato in PdfDocumentEntity.StructuredElementsJson — retro-compat: assente = null)
        ▼
ExtractedDocumentFactory.FromExtraction
  DocumentSection.BBox = UnionNormalized(group.Elements su start page)  ← DROP #3 chiuso
        ▼
AdvancedChunkingService
  parent.BBox = section.BBox (già copiato); child.BBox = section.BBox (NUOVO inherit)  ← DROP child chiuso
        ▼
HierarchicalChunkMapper → DocumentChunk.BBox (NUOVO campo)             ← DROP #4 chiuso
        ▼
5 siti insert  →  TextChunkEntity.CharStart/CharEnd (già calcolati) + BoundingBoxesJson (NUOVO)
        ▼  [migration EF: char_start int, char_end int, bounding_boxes_json jsonb — tutti NULL]
Retrieval  →  SearchResult (già ha PdfDocumentId+ChunkIndex+Heading)
   └─ SearchResultDto (+ PdfDocumentId, ChunkIndex, CharStart, CharEnd, Regions)  ← PUNTO DI PERDITA #1 chiuso
        ▼
CitationDto (4-arg + 7-arg) / Snippet / InlineCitationMatch  (+ regions[], gated CopyrightTier=Full)
        │  SSE / citationsJson
        ▼
FE Citation.regions[]  →  PdfBBoxOverlay (child di <Page>, rect %-based)  + fallback Pattern A
```

Le coordinate `regions` sopravvivono **solo** per il ramo di estrazione Unstructured (Stage 1).
SmolDocling/Docnet → `regions=null` (degradazione R4).

---

## 5. Decomposizione in sub-progetti

| SP | Cosa | Costo | Dipende da | Backfill |
|----|------|-------|-----------|----------|
| **SP0 — Quick-win chat highlight** | Cablare `PdfQuoteViewer` (highlight testuale già esistente) nel percorso chat (`CitationPdfTab`/`PdfPageModal`); passare `citation.snippet` come `highlightQuote` | XS | — | n/a (usa dato esistente) |
| **SP-A — Char-offset persistence** | Colonne `char_start/char_end` + mapping 5 siti + `SearchResultDto`/`CitationDto` surfacing + FE consuma | S | — | re-index (StructuredElementsJson) |
| **SP-B — Coordinate pipeline** | Python coordinates normalizzate → `ExtractedElement.BoundingBox` → union in factory → child inherit → `DocumentChunk.BBox` → `bounding_boxes_json`; nuovo `IndexerVersion v1.2` | L | — (greenfield) | ri-estrazione |
| **SP-C — Region DTO surfacing + copyright gating** | `regions[]` in tutti i wire citazione, gate `CopyrightTier=Full`, persistenza `citationsJson`; FE types + zod | M | SP-B (contratto mockabile) | — |
| **SP-D — FE overlay + wire** | `PdfBBoxOverlay` + `PdfInlineViewer.highlightRects` + wire `CitationModal`/`CitationPdfTab`/`PdfPageModal` + fallback Pattern A | M | SP-C | — |
| **SP-E — Re-extract/re-index corpus + rollout** | Big-bang re-extract per-env + runbook + non-regression EN+IT | M | SP-B deployato | — |

SP0 e SP-A sono spedibili subito e indipendenti; danno valore mentre SP-B (il grosso) matura.
SP-C/SP-D sviluppabili in parallelo con contratto mockato.

---

## 6. Contratti proposti (shape concrete)

### 6.1 Python — `ElementSchema` (unstructured-service)

```jsonc
// elemento attuale
{ "text": "PREPARAZIONE", "page_number": 1, "category": "Title" }
// proposto
{ "text": "PREPARAZIONE", "page_number": 1, "category": "Title",
  "bbox": { "x": 0.081, "y": 0.103, "width": 0.239, "height": 0.076 } // null se coords assenti
}
```

Normalizzazione (Python, unico punto con `system.width/height`):
```
xs=[p[0] for p in coords.points]; ys=[p[1] for p in coords.points]
x=min(xs)/W; y=min(ys)/H; w=(max(xs)-min(xs))/W; h=(max(ys)-min(ys))/H   # W,H = system.width/height; clamp [0,1]
```
`min/max` → robusto all'ordine dei points; divisione per W/H → zoom/DPI-independent. PixelSpace è
**top-left, y-down** (fast e hi_res) → **nessun flip Y**.

### 6.2 C# — record

```csharp
// DocumentProcessing (NON dipendere da KB)
public record ExtractedElement(string Text, int PageNumber, string ElementType,
                               ElementBoundingBox? BoundingBox = null); // param opzionale = retro-compat StructuredElementsJson
public sealed record ElementBoundingBox(float X, float Y, float Width, float Height); // [0,1] top-left

// DocumentChunk / DocumentChunkInput: CharStart/CharEnd già presenti; aggiungere:
public IReadOnlyList<BoundingBox>? BoundingBoxes { get; init; } // KB.BoundingBox esteso con int Page
```

### 6.3 DB — `text_chunks` (migration `AddChunkCharOffsetsAndBoundingBoxes`)

```sql
ALTER TABLE public.text_chunks
  ADD COLUMN char_start integer NULL,
  ADD COLUMN char_end   integer NULL,
  ADD COLUMN bounding_boxes_json jsonb NULL;
```
`bounding_boxes_json` = `[{ "page":3, "x":0.10, "y":0.16, "width":0.55, "height":0.02 }, …]`.
`pgvector_embeddings` **non toccata** (DA-3). xmin non usato su `text_chunks` → nessun concurrency
token. Colonne nullable → nessun backfill al deploy (no lock lunghi).

### 6.4 DTO citazione (estensioni additive)

```csharp
record CitationRegionDto(float X, float Y, float W, float H); // [0,1] top-left
// CitationDto 7-arg (+): int? ChunkIndex, int? CharStart, int? CharEnd, IReadOnlyList<CitationRegionDto>? Regions
// CitationDto 4-arg (+): string? ChunkId, int? CharStart, int? CharEnd, IReadOnlyList<CitationRegionDto>? Regions
// Snippet (+): int? charStart, int? charEnd, IReadOnlyList<CitationRegionDto>? boundingBoxes
// InlineCitationMatch (+): string? ChunkId, int? PdfCharStart, int? PdfCharEnd, regions  (StartOffset/EndOffset restano answer-relative)
// ChunkCitation (+, NO [JsonIgnore]): CharStart, CharEnd, Regions  → finiscono in citationsJson persistito
```

`SearchResultDto.FromDomain` deve smettere di scartare `PdfDocumentId`/`ChunkIndex` (già sul
dominio `SearchResult`).

### 6.5 FE

```ts
interface CitationRegion { x: number; y: number; w: number; h: number; } // [0,1] top-left
interface Citation { /* … */ chunkId?: string; charStart?: number; charEnd?: number; regions?: CitationRegion[]; }
```

```tsx
// nuovo componente, child di <Page> (che è position:relative) → % ancorate al wrapper
function PdfBBoxOverlay({ rects }: { rects: readonly CitationRegion[] }) {
  return <div aria-hidden className="pointer-events-none absolute inset-0">
    {rects.map((r,i)=><div key={i} data-testid="pdf-bbox-rect" className="pdf-bbox-highlight absolute"
       style={{left:`${r.x*100}%`,top:`${r.y*100}%`,width:`${r.w*100}%`,height:`${r.h*100}%`}}/>)}
  </div>;
}
// PdfInlineViewer: renderTextLayer solo se (quote && !hasRects); Pattern A resta il fallback.
```

---

## 7. Rischi e mitigazioni

| Rischio | Mitigazione |
|---|---|
| **bbox non backfillabile** sul corpus esistente (StructuredElementsJson non ha mai avuto coords) | Comunicare come limite noto; big-bang re-extract per-env (SP-E); fallback Pattern A intanto |
| **Copyright leak** (highlight verbatim su Protected) | DA-4: regions solo tier Full; test che asserisce `regions=null` per Protected |
| **Convenzione Y errata** (bottom-left vs top-left) → rettangoli specchiati | Contratto esplicito "top-left, y-down, [0,1]"; test con pagina asimmetrica nota |
| **Divergenza documentId** (VectorDocumentId vs PdfDocumentId su Ask/legacy) | DC-1: risolvere prima di keyare le regions; SP-A include il fix `SearchResultDto` |
| **pgvector doppio-binario** (EF + raw SQL) → drift se si aggiunge colonna solo EF | DA-3: non toccare pgvector; LEFT JOIN `text_chunks` |
| **5 siti di insert** (non 4: +`ImportRagDataCommandHandler`) → dimenticarne uno | Centralizzare la serializzazione bbox in un helper (come `HeadingAwareChunkAdapter`) |
| **StructuredElementsJson retro-compat** | Solo parametro **opzionale** su `ExtractedElement` (assente = null in System.Text.Json) |
| **Reference frame CharStart/CharEnd** = content ricostruito, non ExtractedText/PDF | Documentare; per l'highlight geometrico contano le `regions`, non il char-range |
| **SmolDocling/Docnet no coords** | R4 degradazione: regions=null, fallback pagina+testo |
| **Overlay disallineato** da `max-w-full`/overflow-hidden del viewer | Ancorare % al wrapper `.react-pdf__Page` (l'overlay è child del Page); verificare wrapper==canvas width |

---

## 8. Criteri di accettazione

- **AC1** — Caricato un PDF (ramo Unstructured) e posta una domanda, la citazione nel FE espone
  `regions[]` non vuoto e il viewer disegna il rettangolo sulla pagina reale.
- **AC2** — Per un chunk di tabella, la regione evidenziata copre la porzione tabellare citata
  (anche se lo snippet testuale resta linearizzato).
- **AC3** — Per una citazione tier `Protected`, `regions=null` e nessun highlight verbatim.
- **AC4** — Per un PDF estratto via SmolDocling/Docnet (o senza text-layer), il viewer apre la
  pagina e mostra il banner fallback senza errori.
- **AC5** — Nessuno snippet di citazione inizia a metà parola (test di regressione anti-mid-word).
- **AC6** — Baseline unit-test fail = 0; gate a11y/token verdi; nessuna regressione.

---

## 9. Riferimenti

- Audit pipeline citazione (workflow 4-finder) — estrazione, chunking, citation-gen, FE rendering.
- Deep-dive L3 (workflow 4-finder) — coordinate Unstructured, contratto DTO, overlay react-pdf,
  migration EF.
- Epic RAG heading-aware #3266 (SP1-SP4), `IndexerVersionRegistry`, `BulkReindexReadyCommand` /
  `ReindexDocumentCommand` (pattern re-index big-bang SP3 #3269).
- ADR-016 (ChunkMetadata/BBox spina dorsale), ADR-059 (copyright posture), ADR-060 (persistence).
- Fix a monte: #3241 (`SnapToWordStart` + de-sillabazione), #3282 (heading-aware re-index).
