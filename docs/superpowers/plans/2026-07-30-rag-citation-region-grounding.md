# RAG Citation Region Grounding — piano TDD

Epic "RAG citation region grounding". Spec: [`docs/superpowers/specs/2026-07-30-rag-citation-region-grounding-design.md`](../specs/2026-07-30-rag-citation-region-grounding-design.md).
Parent branch per ogni sub-progetto: `main-dev`.

## Obiettivo
Portare fino al FE `pdfDocumentId + pageNumber reale + chunkIndex + charStart/charEnd + regions[]`
(bbox normalizzate [0,1] top-left) e disegnare un overlay preciso sul PDF nel percorso chat, con
fallback graceful. Gate copyright: `regions` solo `CopyrightTier=Full`.

## Non-goal
Riscrittura estrazione tabelle in HTML/celle (Livello 2 separato) · grounding sub-riga
parola/carattere · denormalizzazione bbox su `pgvector_embeddings` · unificazione completa dei 4
wire (solo il minimo su Ask/legacy, DC-1).

## Precondizioni operative
- Container Docker `unstructured` DEVE essere ricostruito col codice coordinate-aware prima che
  emetta `bbox` (come SP3 #3269: container stale → bbox null silenzioso).
- bbox NON è recuperabile dal corpus esistente senza ri-estrazione (StructuredElementsJson salvato
  non ha coordinate). Char-offset SÌ (basta re-index).

---

## SP0 — Quick-win: cablare l'highlight testuale nel percorso chat  (branch `feature/issue-<n>-chat-quote-highlight`)

**Cosa**: il quote-highlight (`PdfQuoteViewer` + `makeQuoteTextRenderer`) esiste ma non è usato in
chat. Passare `citation.snippet`/`paraphrasedSnippet` come `highlightQuote` in `CitationPdfTab` e
sostituire/estendere `PdfPageModal` con `PdfQuoteViewer` (che già gestisce highlight + banner
fallback).

### TDD
- **RED** (vitest): test che `CitationPdfTab` monta il viewer con `highlightQuote` = snippet tier
  Full, e senza highlight per Protected (usa `paraphrasedSnippet`, che NON è verbatim → nessun
  match forzato).
- **RED**: test che quando il match fallisce compare `data-testid=pdf-quote-fallback`.
- **GREEN**: wire prop; riuso `PdfQuoteViewer`.
- **Regressione**: mechanic-card/admin che già usano `PdfQuoteViewer` restano invariati.

**Valore**: da subito «Vedi nel PDF» apre la pagina reale ed evidenzia (best-effort) la regione,
anche prima che le bbox esistano. Indipendente da tutto il resto.

---

## SP-A — Char-offset persistence + surfacing  (branch `feature/issue-<n>-chunk-char-offsets`)

**Cosa**: persistere `char_start/char_end` (già calcolati, oggi scartati) e portarli nel DTO
citazione + FE. Correggere `SearchResultDto.FromDomain` che scarta `PdfDocumentId/ChunkIndex`.

### Slice A1 — persistenza
- **RED** (unit `TextChunkEntity`/config): asserire nuove colonne `char_start/char_end` nullable.
- **RED** (unit, 5 siti): `PdfProcessingPipelineService.SaveTextChunksAsync`, `IndexPdfCommandHandler`,
  `UploadPdfCommandHandler.Processing`, `CompleteChunkedUploadCommandHandler`,
  `ImportRagDataCommandHandler` → asserire che `chunk.CharStart/CharEnd` finiscono su `TextChunkEntity`.
- **GREEN**: aggiungere proprietà + config EF (`.HasColumnName("char_start")…`) + 2 righe per sito.
- **Migration**: `dotnet ef migrations add AddChunkCharOffsets` (da `apps/api/src/Api`,
  solution `MeepleAI.Api.sln`). `AddColumn<int>` x2 nullable. Down = DropColumn x2.
- **Integration** (Testcontainers): re-index di un PDF con `StructuredElementsJson` popola
  `char_start/char_end`.

### Slice A2 — surfacing DTO + FE
- **RED**: `SearchResultDto.FromDomain` mantiene `PdfDocumentId`, `ChunkIndex`, `CharStart`, `CharEnd`.
- **RED**: `CitationDto` (4-arg e 7-arg) espone `chunkId/charStart/charEnd`; FE `Citation` type +
  zod `CitationSchema` accettano i campi (optional/nullable, backward-compat).
- **GREEN**: estensioni additive.
- **Gotcha**: fixare il cache-hit di `AskQuestionQueryHandler:222` che inventa `pageNumber` e svuota
  `documentId` (DC-1 minimo).

---

## SP-B — Coordinate pipeline (greenfield)  (branch `feature/issue-<n>-coordinate-extraction`)

**Cosa**: propagare le coordinate Unstructured normalizzate fino a `bounding_boxes_json`.

### Slice B1 — Python
- **RED** (pytest): `_normalized_bbox(el)` — element con `coordinates` PixelSpace → bbox atteso;
  element senza coordinates (PageBreak) → None; **indipendenza da layout dims** (stesso box
  relativo con W/H diversi).
- **GREEN**: helper in `main.py` + `BBoxSchema`/`bbox: Optional` in `schemas.py`.

### Slice B2 — C# plumbing
- **RED** (unit): `UnstructuredElement.Bbox` deserializzato; `MapStructuredElements` produce
  `ExtractedElement.BoundingBox`; `ExtractedElement` retro-compat (JSON senza bbox → null).
- **RED**: `ExtractedDocumentFactory` → `DocumentSection.BBox = union(elements start page)`;
  `AdvancedChunkingService.CreateChildChunks` → i child ereditano `section.BBox`;
  `HierarchicalChunkMapper` → `DocumentChunk.BBox` non più scartato;
  `EmbeddedTitleSplitter` → Title sintetico propaga la bbox del body.
- **GREEN**: record esteso (param opzionale), union helper, mapping.

### Slice B3 — persistenza bbox
- **RED**: 5 siti insert scrivono `BoundingBoxesJson`; entity/config nuova colonna jsonb.
- **GREEN** + **Migration** `AddChunkBoundingBoxes` (`AddColumn<string>("bounding_boxes_json",
  type:"jsonb", nullable:true)`). Config `HasColumnType("jsonb").IsRequired(false)`.
- **D**: bump `IndexerVersionRegistry` → `v1.2 — coordinate-aware` (`Current → v1.2`).
- **Integration** (Testcontainers): ingest di un PDF fixture con coordinate → `bounding_boxes_json`
  popolato, valori ∈ [0,1].

**Precondizione**: rebuild container `unstructured` prima dell'integration.

---

## SP-C — Region DTO surfacing + copyright gating  (branch `feature/issue-<n>-citation-regions-dto`)

**Cosa**: portare `regions[]` fino al FE su tutti i wire, gate `CopyrightTier=Full`, persistere in
`citationsJson`.

### TDD
- **RED**: LEFT JOIN `text_chunks` in `PgVectorStoreAdapter` restituisce `bounding_boxes_json`
  (pattern heading #3270); `SearchResult` porta `Regions`.
- **RED** (gating, critico): per `CopyrightTier=Full` `CitationDto.Regions` valorizzato; per
  `Protected` `Regions == null` (nessun highlight verbatim).
- **RED**: `ChunkCitation.Regions` **senza `[JsonIgnore]`** → presente in `citationsJson`;
  round-trip serialize/deserialize.
- **RED**: `Snippet`/`InlineCitationMatch` estesi; `StreamQaQueryHandler` e
  `ChatWithSessionAgentCommandHandler` popolano le regions.
- **GREEN**: estensioni + `_copyrightTierResolver` gate.
- **Baseline**: `ChunkPayloadTests`/`PgVectorEmbeddingEntityTests`/equality VO → aggiornare gli
  `GetEqualityComponents` senza rompere i test esistenti.

---

## SP-D — FE overlay + wire chat  (branch `feature/issue-<n>-pdf-bbox-overlay`)

**Cosa**: `PdfBBoxOverlay` + `PdfInlineViewer.highlightRects` + wire percorso chat.

### TDD (vitest + Testing Library)
- **RED**: `PdfBBoxOverlay` renderizza N `data-testid=pdf-bbox-rect` con `left/top/width/height` %
  corretti da `regions[]`.
- **RED**: `PdfInlineViewer` con `highlightRects` monta l'overlay e NON attiva il text layer;
  senza `highlightRects` ma con `highlightQuote` mantiene Pattern A (regressione
  `pdf-quote-fallback`/`onQuoteMatch`).
- **RED**: `CitationModal`/`CitationPdfTab` passano `citation.regions` + `pageNumber` al viewer;
  `regions` assenti → fallback Pattern A (SP0).
- **GREEN**: componente + prop additive + CSS `.pdf-bbox-highlight` (token `--c-warning`,
  `prefers-reduced-motion`).
- **Test convenzione Y** (anti-mirroring): fixture con rect noto in alto pagina → `top` piccolo.

---

## SP-E — Re-extract/re-index corpus + rollout  (branch `feature/issue-<n>-corpus-reextract`)

**Cosa**: attivare il grounding sui dati reali. bbox richiede ri-estrazione (non basta re-index).

### TDD / deliverable
- **D1**: comando/endpoint di re-extract big-bang su PDF Ready con `IndexerVersion != v1.2`
  (riuso pattern `BulkReindexReadyCommand` #3269, ma con **ri-estrazione**, non solo re-chunk).
  - **Gotcha null-trap SQL** (Npgsql): selettore `IndexerVersion == null || != target`
    (test integrazione Gruppo C come SP3).
  - Pacing su capacità coda; `ConflictException` per-PDF → skipped, mai abort.
- **D2**: suite non-regressione retrieval EN+IT (rag-smoke estesa) su staging prima del big-bang.
- **D3**: runbook per-env + rebuild container `unstructured` documentato; orchestrazione
  `make reindex-corpus ENV=` (se allineato a SP3).
- **AC**: post-reindex, i chunk dei PDF Ready hanno `bounding_boxes_json` non-null (ramo
  Unstructured); citazioni nel FE mostrano l'overlay.

---

## Rischi/gotcha trasversali
- **bbox non backfillabile** → limite noto; SP0+Pattern A coprono l'interim.
- **Copyright** (DA-4) → gate tier Full, test dedicato.
- **pgvector doppio-binario** → non toccare (DA-3), LEFT JOIN.
- **5 siti insert** → helper centralizzato di serializzazione bbox.
- **StructuredElementsJson retro-compat** → param opzionale su `ExtractedElement`.
- **Convenzione Y** top-left → test anti-mirroring.
- **Baseline test = 0 fail** (policy CLAUDE.md) + gate a11y/token verdi.

## Decisioni (dal design)
DA-1 normalizza in Python top-left [0,1] · DA-2 jsonb array su text_chunks · DA-3 no pgvector,
LEFT JOIN · DA-4 regions solo tier Full · DA-5 overlay %-based child di `<Page>` · DA-6 char-offset
subito · DA-7 char via re-index, bbox via re-extract · DA-8 union su start page.
Da confermare: DC-1 (scope unificazione wire), DC-2 (fast vs hi_res), DC-3 (big-bang vs lazy).
