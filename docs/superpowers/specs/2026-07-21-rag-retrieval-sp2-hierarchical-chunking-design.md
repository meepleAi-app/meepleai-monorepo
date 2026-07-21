# SP2 — Hierarchical Chunking + Persistence (RAG retrieval heading-aware epic)

**Data**: 2026-07-21
**Epic**: RAG retrieval heading-aware (#3266) — SP2 di 4, issue #3268
**Dipende da**: SP1 (#3267, merged `a6bd3df91`)
**Stato**: design v2 — corretto dopo review adversariale multi-esperto (23 finding)
**Branch previsto**: `feature/rag-retrieval-sp2-hierarchical-chunking`

> **v2 changelog** — la review ha invalidato quattro assunzioni: (1) il null-path NON è
> byte-identico al flat attuale (passa per `AdvancedChunkingService` con config diversa); (2) la
> strategia Sparse (2000 char) supera `MaxEmbeddingChars` (1800) → truncation silenziosa; (3) la
> branch dual-language ricostruisce i chunk tradotti scartando l'heading; (4) le 4 pipeline non
> condividono la shape `DocumentChunkInput` (IndexPdf usa `DocumentChunk`). La v2 corregge questi
> punti + robustezza persistenza + un servizio di chunking condiviso. Dettaglio in §10.

---

## 1. Contesto epic

SP2 è il secondo di 4 sub-progetti (#3266). SP1 ha reso disponibili `StructuredElements` +
`ExtractedDocumentFactory.FromExtraction`, ma senza consumer. SP2 wire il chunking heading-aware
nelle **4** pipeline di ingestion e persiste gli heading.

### 1.1 Decisioni cardine (brainstorming 2026-07-21)

| Decisione | Scelta |
|---|---|
| Cosa si embedda/persiste | **Solo i child (sentence), Heading ereditato dal parent** — parent non persistito, `ParentChunkId = null` |
| Ampiezza pipeline | **Tutte e 4** (IndexPdf via StructuredElementsJson; Complete via cambio signature) |

### 1.2 Obiettivo (preciso, corretto post-review)

Popolare `TextChunkEntity.Heading` con il **valore reale per-sezione** dai raw elements. Questo
**riduce** (non elimina) l'uso del fallback LLM nella role-classification: un chunk il cui `Heading`
matcha una `HeadingRule` (es. "Setup"/"Preparazione"/"Combat") viene classificato deterministicamente
e **non** invoca l'LLM; un heading non-vuoto che non matcha alcuna regola resta `None` → fallback
(comportamento corretto). 🔸 **Level** dei child persistiti è costante `2` e **ElementType** costante
`"text"` (i child sono corpo; la normalizzazione heading/table vive sul parent, non persistito):
solo `Heading` porta valori reali per-sezione. #730 `ElementType` resta uniformemente `"text"` dopo
SP2 (per-chunk ElementType è un **non-goal**, eventuale follow-up).

---

## 2. Problema: la catena attuale (verificata)

Le 4 pipeline chunkano flat, ma **non in modo uniforme**:
- Upload / PdfProcessingPipeline / Complete: `fullText` → `ITextChunkingService.PrepareForEmbedding(fullText, …)` → `List<DocumentChunkInput>`.
- **IndexPdf**: `pdf.ExtractedText` → `ITextChunkingService.ChunkText(extractedText)` → `List<DocumentChunk>` (tipo diverso, porta `Embedding`).

Poi: embeddings (`IEmbeddingService`) → `TextChunkEntity` + pgvector.

- `AdvancedChunkingService`/`ExtractedDocumentFactory`/`StructuredElements`: costruiti da SP1, **zero
  consumer** in produzione.
- `TextChunkEntity` ha già le colonne #730 ma sono default → `TextChunkRoleClassifier` mappa
  `HeadingPath=""` → `RoleClassifierService.ApplyRules("")` = `None` → fallback LLM per **ogni** chunk.

### 2.1 Asimmetria delle 4 pipeline (verificata)

| Pipeline | Result al chunking? | Chunk type | pgvector | Chunk size attuale |
|---|---|---|---|---|
| `UploadPdfCommandHandler.Processing.cs` | ✅ `extractResult` in mano | `DocumentChunkInput` | `PgVectorEmbeddingEntity` diretto | 512/50 |
| `PdfProcessingPipelineService.cs` (Quartz/recovery/re-index) | ✅ result in mano | `DocumentChunkInput` | `IVectorStoreAdapter` (`KbEntities.Embedding`) | 1024/150 + **dual-language** |
| `IndexPdfCommandHandler.cs` | ❌ solo `pdf.ExtractedText`, no blob | **`DocumentChunk`** | `PgVectorEmbeddingEntity` diretto | ChunkText default |
| `CompleteChunkedUploadCommandHandler.cs` | 🟡 estrae (riga 577) ma scarta (riga 624) | `DocumentChunkInput` | **nessuna** (asimmetria pre-esistente) | 512/50 |

---

## 3. Scope & confine

### In scope (SP2)
- **`IHeadingAwareChunker`** (nuovo application service, `DocumentProcessing`) che incapsula l'intero
  chain: `FromExtraction` → `AdvancedChunkingService.ChunkDocumentAsync` → mapper → child con Heading
  + **clamp a `MaxEmbeddingChars`**. Iniettato nelle 4 pipeline (una sola call, un solo seam testabile).
- **`HierarchicalChunkMapper`** (helper puro interno al servizio): `HierarchicalChunk[]` → child.
- **Persistenza StructuredElements** robusta: nuovo campo `PdfDocumentEntity.StructuredElementsJson`
  (`string?`, versionata) + migration; popolato all'estrazione; invariante con `ExtractedText`.
- **Complete signature**: `ExtractPdfTextAsync` restituisce anche `StructuredElements`.
- **Dual-language**: la branch traduzione propaga `Heading` (risolve il TODO
  `PdfProcessingPipelineService.cs:225-227`).
- Test unit + per-pipeline + round-trip versionato + regression role-fast-path + non-regressione
  content-preservation.

### Fuori scope (SP3+/follow-up)
- Big-bang re-index del corpus (SP3); ranking/heading-boost/reranker/query-expansion (SP4).
- Asimmetria pgvector di Complete (pre-esistente — documentata, non corretta).
- Persistenza gerarchica `HierarchicalChunk` DB (`ChunkPayload`/`IChunkRepository` — orfani, non usati).
- Popolamento `ParentChunkId` (solo-child → `null`) e per-chunk `ElementType`/`Level` reali (non-goal).

---

## 4. Architettura & componenti

### 4.1 `IHeadingAwareChunker` (application service condiviso) [M14/M17]
Vive in `DocumentProcessing.Application.Services` (dipende **inward** su `KnowledgeBase.IAdvancedChunkingService` — direzione DocumentProcessing → KB, coerente con la convenzione):

```csharp
internal interface IHeadingAwareChunker
{
    Task<List<DocumentChunkInput>> ChunkAsync(
        Guid documentId, Guid? gameId,
        IReadOnlyList<ExtractedElement>? structuredElements,
        string fullText, CancellationToken ct);
}
```

Implementazione:
1. `ExtractedDocumentFactory.FromExtraction(documentId, gameId, structuredElements, fullText)`.
2. `AdvancedChunkingService.ChunkDocumentAsync(doc, config: cappedConfig, ct)` — 🔸 **config con
   `ChunkSizeChars` clampato a `MaxEmbeddingChars` (1800)** per evitare il truncation di embedding
   (§4.4). Auto-select della strategia (Baseline/Dense/Sparse) ma con size cap.
3. `HierarchicalChunkMapper.ToChildDocumentChunks(hchunks)` (solo child).
4. **Post-split di sicurezza**: se un child mappato ha `Text.Length > MaxEmbeddingChars`, splittarlo
   con `ITextChunkingService.ChunkText(text, MaxEmbeddingChars, DefaultOverlap)` preservando Heading
   (stesso pattern di `SplitOversizedPageChunks`).

`IndexPdf` consuma `DocumentChunk` (non `DocumentChunkInput`): il servizio ritorna
`DocumentChunkInput` (che porta `Heading`/`Level`/`ElementType`); IndexPdf costruisce i suoi
`DocumentChunk` da quella lista (aggiungendo l'`Embedding`), riusando i campi hierarchy. 🔸 [M15]

### 4.2 `HierarchicalChunkMapper` (helper puro)
Solo child (Level 2, non-root). Per ogni child: `Text=child.Content`, `Page=` per-child (§4.5),
`CharStart/End`, `Heading=child.Metadata.Heading`, `Level=(short)child.Level`,
`ElementType=child.Metadata.ElementType` (`"text"`), `ParentChunkId=null`. Sezione con solo parent
(nessun child) → nessun output.

### 4.3 DI [M10/M19]
`IHeadingAwareChunker` iniettato come **parametro ctor opzionale nullable con default `null`** (come
il precedente `IRoleClassifierService? = null`), così i ~13 construction-site dei test esistenti
continuano a compilare; `null` → il handler usa il flat path esistente (fallback). Nei 2
background-task path (Upload `ProcessPdfAsync`, Complete `TriggerPdfProcessingAsync`) il servizio va
**risolto dallo scope** (`scope.ServiceProvider.GetRequiredService<…>()`), non da un field del
handler (evita captive-scope). IndexPdf + PdfProcessingPipeline girano in-scope → ctor injection ok.

### 4.4 Clamp embedding-size [M2]
La strategia Sparse produce `ChunkSizeChars = 2000 > MaxEmbeddingChars (1800)` → truncation
silenziosa in tutte le pipeline (nessun cap prima di `GenerateEmbeddings`). Il servizio clampa
`ChunkSizeChars` a `MaxEmbeddingChars` nella config passata a `ChunkDocumentAsync`, **e** applica il
post-split di sicurezza (§4.1.4). Test: nessun child persistito/embeddato supera `MaxEmbeddingChars`.

### 4.5 Page attribution per-child [M9]
`AdvancedChunkingService` assegna a ogni child la pagina del **primo** elemento della sezione → per
sezioni multi-pagina tutti i child citano la prima pagina (regressione citation). 🔸 Il mapper
ricalcola la pagina per-child da `CharStart` (stima `charsPerPage`, come `EstimatePageNumber`),
oppure — se non ricostruibile — documenta esplicitamente l'attribuzione a livello-sezione. Test
multi-pagina.

### 4.6 Persistenza StructuredElements robusta [M7/M8/M12/M13]
- Migration: `pdf_documents.StructuredElementsJson` (`text`, nullable). Backfill `null`. 🔸 La colonna
  **non** è inclusa nel trigger `tsvector` FTS. Nota: seconda copia del testo su una hot row
  (optimistic concurrency `xmin`) — accettato, documentato; alternativa side-table valutata e
  rimandata.
- **DTO versionato**: serializzo `{ schemaVersion: 1, elements: [...] }` con
  `JsonSerializerOptions` **pinnate e condivise** tra serialize e read; reader tollerante (ignora
  membri sconosciuti, default sui mancanti).
- **Invariante `ExtractedText ↔ StructuredElementsJson`** [M7]: ogni writer di `ExtractedText`
  co-scrive lo StructuredElementsJson **oppure** lo azzera nello stesso `SaveChanges` (incl.
  `ExtractPdfTextCommandHandler` che ri-estrae). Così IndexPdf non indicizza mai da StructuredElements
  stale (azzerato → null-path).
- **Robustezza read** [M8]: IndexPdf avvolge la deserializzazione in `try/catch(JsonException)` → log
  warning + tratta come `null` (null-path), **mai** hard-fail (no PDF marcato Failed per JSON corrotto).

### 4.7 Dual-language propaga Heading [M3/M6]
`PdfProcessingPipelineService` aggiunge chunk tradotti (EN) ricostruendo `DocumentChunkInput` senza
i campi #730 (TODO a `:225-227`). SP2 propaga `origChunk.Heading/Level/ElementType` sui chunk
tradotti → anche i rulebook non-EN attivano la role-fast-path.

---

## 5. Data flow & degradazione

```
StructuredElements (Upload/Pipeline/Complete)  |  StructuredElementsJson (IndexPdf, try/catch→null)
  ▼
IHeadingAwareChunker.ChunkAsync
  → FromExtraction → ExtractedDocument.Sections
  → AdvancedChunkingService.ChunkDocumentAsync (config size-capped) → parent L0 + child L2
  → mapper (solo child, Heading, per-child page) → post-split > MaxEmbeddingChars
  → List<DocumentChunkInput> {Text, Page, Heading, Level=2, ElementType="text"}
  ▼
[flusso esistente] embeddings + TextChunkEntity (Heading!) + pgvector  (+ dual-language con Heading)
  ▼
TextChunkRoleClassifier: HeadingPath rule-matching → role deterministico (LLM ridotto) ✅
```

- **null-path** [M1/M11/M18]: `StructuredElements` null/vuoto o JSON corrotto → `FromExtraction(null)`
  → 1 sezione preambolo → child con `Heading=null`. 🔸 I boundary **cambiano** (config auto vs 512/50
  o 1024/150 attuali) → l'output **non** è byte-identico al flat attuale. Criterio di non-regressione
  corretto: (a) i child null-path hanno `Heading=null` e `role_tags=None`; (b) **content-preservation**
  — la concatenazione del testo dei child ricostruisce il contenuto della sezione modulo overlap.
  Nessuna asserzione di byte-identità.
- Il resto (embeddings, `SaveTextChunks*`, `SaveEmbeddings*`, role-classifier call) invariato.

---

## 6. Testing

- **Unit `HierarchicalChunkMapper`**: solo child; Heading ereditato; `ParentChunkId=null`; `Level=2`;
  per-child page (multi-pagina); sezione solo-parent → vuota.
- **Unit `IHeadingAwareChunker`**: nessun child > `MaxEmbeddingChars` (documento narrativo → Sparse
  cap); null → null-path; content-preservation.
- **Round-trip versionato**: DTO `{schemaVersion,elements}` serialize→deserialize; **deserializza un
  blob legacy congelato** (stringa fissa) → tollerante; JSON malformato → null (non eccezione al
  chiamante).
- **Per-pipeline** (4 predicati distinti [M5], Testcontainers dove serve): dato `StructuredElements`
  con Title rule-matching → la pipeline X persiste `TextChunkEntity` con `Heading != null` **e**
  `role_tags != None`; per Complete il predicato **esclude** righe pgvector (asimmetria). Ogni writer
  (Upload/Pipeline/Complete) scrive `StructuredElementsJson != null` dopo estrazione (incl. il
  ramo early-return per conflitto in Complete) [M20].
- **Dual-language**: documento non-EN → i chunk tradotti portano l'Heading ereditato.
- **Regression role-fast-path** [M2-tech/M4]: heading che matcha una `HeadingRule` (es. "Setup") →
  classificazione deterministica, `ILlmService` mai invocato; **companion**: heading non-vuoto
  non-matching → resta fallback (la fast-path riduce, non elimina).
- **DI/construction** [M19]: i ~13 test construction-site compilano (param opzionale null → flat);
  `IndexPdfIntegrationTests.RegisterMockServices` registra `IHeadingAwareChunker` reale + deps.

---

## 7. Definition of Done (SP2)

- [ ] `IHeadingAwareChunker` + `HierarchicalChunkMapper` (helper) + unit test (incl. size-cap, page).
- [ ] Migration `pdf_documents.StructuredElementsJson` + `PdfDocumentEntity` field (fuori dal tsvector).
- [ ] Persistenza versionata + `JsonSerializerOptions` pinnate; invariante `ExtractedText↔JSON` in
      **tutti** i writer (co-write o azzera, incl. `ExtractPdfTextCommandHandler`).
- [ ] `CompleteChunkedUploadCommandHandler.ExtractPdfTextAsync` surface `StructuredElements`.
- [ ] **Upload** usa il chunker (predicato: TextChunkEntity Heading≠null + role_tags≠None).
- [ ] **PdfProcessingPipeline** usa il chunker + dual-language propaga Heading.
- [ ] **IndexPdf** legge `StructuredElementsJson` (try/catch→null), costruisce `DocumentChunk` dai
      `DocumentChunkInput` del chunker.
- [ ] **Complete** usa il chunker (predicato esclude pgvector).
- [ ] Clamp `MaxEmbeddingChars` + post-split: nessun chunk embeddato eccede 1800.
- [ ] Test: mapper, chunker (size/page/null), round-trip versionato (blob legacy + malformato),
      per-pipeline ×4, dual-language, role-fast-path (match + non-match), StructuredElementsJson-written.
- [ ] Nessuna regressione: null-path content-preservation + Heading=null (non byte-identità); build +
      suite verdi; ~13 construction-site compilano.
- [ ] PR mergiata in `main-dev`.

---

## 8. Rischi

| Rischio | Mitigazione |
|---|---|
| Sparse > MaxEmbeddingChars → truncation | §4.4 clamp + post-split + test |
| StructuredElements stale vs ExtractedText | §4.6 invariante co-write/azzera in ogni writer |
| JSON corrotto → hard-fail | §4.6 try/catch → null-path |
| Captive-scope su background-task | §4.3 resolve da scope + ctor opzionale |
| Boundary diversi → embeddings diversi | intenzionale (SP3 re-index rende coerente); content-preservation, non byte-identità |
| Dual-language heading-less | §4.7 propaga Heading |
| Hot-row bloat (StructuredElementsJson) | fuori tsvector; alternativa side-table documentata/rimandata |

---

## 9. Riferimenti

- Epic #3266; SP1 `docs/superpowers/{specs,plans}/2026-07-21-rag-retrieval-sp1-*`.
- ADR-016; #730 (hierarchy columns); #1391 (RoleTags denormalizzato).
- File chiave: le 4 pipeline; `AdvancedChunkingService`/`ExtractedDocumentFactory`/`ChunkingConfiguration`/`ChunkingStrategySelector`; `ChunkingConstants.MaxEmbeddingChars`; `PdfDocumentEntity`/`TextChunkEntity`; `TextChunkRoleClassifier`/`RoleClassifierService`; `EnhancedPdfProcessingOrchestrator.SplitOversizedPageChunks` (pattern post-split); `ExtractPdfTextCommandHandler` (invariante).

---

## 10. Appendice — review adversariale (2026-07-21)

Spec v1 sottoposta a review multi-esperto (Wiegers/Nygard/Newman/Fowler/Crispin + technical-verifier),
23 finding confermati (0 critical, 3 major, 20 minor) su 28 grezzi. Risoluzione in v2:

| ID | Sintesi | Risoluzione |
|----|---------|-------------|
| M1/M11/M18 | null-path "identico a oggi" falso/contraddittorio | §5 content-preservation, non byte-identità |
| M2 | Sparse 2000 > MaxEmbeddingChars 1800 → truncation | §4.4 clamp + post-split |
| M3/M6 (tech/nygard) | dual-language scarta Heading | §4.7 propaga Heading |
| M14/M17 | chain copy-pasted in 4 handler | §4.1 `IHeadingAwareChunker` condiviso (DocumentProcessing→KB) |
| M15 | IndexPdf usa `DocumentChunk` non `DocumentChunkInput` | §4.1 IndexPdf costruisce DocumentChunk dai DocumentChunkInput |
| M10/M19 | DI captive-scope + rompe ~13 test | §4.3 ctor opzionale + resolve da scope |
| M7 | StructuredElements stale vs ExtractedText | §4.6 invariante in ogni writer |
| M8 | JSON corrotto → hard-fail | §4.6 try/catch → null-path |
| M12 | no versioning/serializer pinned | §4.6 DTO versionato + options pinnate + blob-legacy test |
| M9 | page attribution first-page per sezione multi-pagina | §4.5 per-child page |
| M2-tech/M4 | role-fast-path "elimina" LLM falso | §1.2 "riduce"; test match + non-match |
| M3-tech | Level/ElementType "valori reali" | §1.2 costanti (2/"text"), solo Heading reale |
| M5 | DoD non per-pipeline; §2 impreciso | §7 4 predicati; §2 corretto (IndexPdf ChunkText/DocumentChunk) |
| M13/M16 | hot-row bloat; alternativa re-embed | §4.6 fuori tsvector; alternativa documentata/rimandata |
| M20 | no test StructuredElementsJson-written | §6 per-writer + early-return |
