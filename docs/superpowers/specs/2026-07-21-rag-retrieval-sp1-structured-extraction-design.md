# SP1 — Structured Extraction (RAG retrieval heading-aware epic)

**Data**: 2026-07-21
**Epic**: RAG answer-quality / retrieval — parte 3/3 "chunking heading-aware"
**Sub-progetto**: SP1 di 4 (fondante)
**Stato**: design v2 — corretto dopo review adversariale multi-esperto (2026-07-21)
**Branch previsto**: `feature/rag-retrieval-sp1-structured-extraction`

> **v2 changelog** — La v1 assumeva che i `chunks` della response Python portassero
> `element_type == "Title"`. **È falso**: la response espone l'output di `chunk_by_title`, i cui
> elementi hanno `category` = `"CompositeElement"`/`"Table"`, mai `"Title"` (il Title viene assorbito
> nel composite). I Title esistono solo nei **raw partition elements**, che oggi **non** sono
> serializzati. La v2 corregge la premessa e amplia lo scope di SP1 con un cambiamento lato Python
> + propagazione nell'orchestrator. Dettaglio in §3 e §12.

---

## 1. Contesto epic

SP1 è il primo di 4 sub-progetti che completano l'epic RAG retrieval, il cui obiettivo è la
answer-quality: il caso di riferimento è l'agente Terraforming Mars che, alla domanda
"Setup per N giocatori", restituiva la sezione sbagliata.

Parti già mergiate dell'epic:
- **1/3 keyword FTS** — PR [#3242](https://github.com/meepleAi-app/meepleai-monorepo/pull/3242)
  (Italian FTS + OR tolerance in `KeywordSearchService`).
- **2/3 ranking** — PR [#3243](https://github.com/meepleAi-app/meepleai-monorepo/pull/3243)
  (demote legend chunks). Ha lasciato follow-up ranking espliciti, raccolti in SP4.

### 1.1 Decisioni cardine (brainstorming 2026-07-21)

| Decisione | Scelta | Implicazione |
|---|---|---|
| Scope del 3/3 | **End-to-end**: chunking heading-aware + tutti i follow-up ranking del 2/3 | 4 sub-progetti |
| Producer heading | **Wire `AdvancedChunkingService` (ADR-016 puro)** | Re-chunk gerarchico + prerequisito estrazione strutturata |
| Transizione corpus | **Big-bang re-index al deploy** (per-ambiente; staging prima di prod) | Nessun feature flag; safety-net = suite non-regressione su staging |

### 1.2 Decomposizione in sub-progetti

| SP | Cosa | Dipende da |
|----|------|-----------|
| **SP1 — Structured Extraction** *(questo doc)* | Espone i raw elements tipizzati dal servizio Python e li propaga fino a un `ExtractedDocument.Sections` popolato con heading | — |
| SP2 — Hierarchical Chunking + Persistence | Wire `AdvancedChunkingService` nelle pipeline; mapping `HierarchicalChunk`→`TextChunkEntity`+pgvector; bridge tipi; parent/child | SP1 |
| SP3 — Big-bang Re-index/Re-embed | Job bulk re-estrai+re-chunk+re-embed dei PDF Ready; bump `IndexerVersion`; suite non-regressione EN+IT | SP2 |
| SP4 — Ranking end-to-end | `SearchResultItem` porta `RoleTags`+`Heading` → vector-arm boost; reranker nel playground + MinScore reconcile; query-expansion IT | SP2 (heading-boost); reranker+query-expansion indipendenti |

---

## 2. Obiettivo di SP1

Rendere disponibili gli elementi tipizzati (con heading) dal servizio Python `unstructured` e
costruire da essi un `ExtractedDocument` con `Sections` popolate — **verificabile in isolamento**,
senza toccare ancora chunking di ingestion né persistenza.

SP1 valida esplicitamente l'incognita principale dell'epic: **i rulebook reali producono heading
utili?** Se la risposta fosse negativa, lo scopriamo qui, con la spesa minore, prima di investire
in SP2/SP3.

---

## 3. Problema: la catena rotta (verificata contro il codice)

1. Il servizio Python `unstructured-service` **rileva** la struttura tipizzata. `partition_pdf`
   (`unstructured_adapter.py:41-47`) produce raw elements con `element.category` ∈
   {`"Title"`, `"NarrativeText"`, `"Table"`, `"ListItem"`, …}, `element.text`,
   `element.metadata.page_number`.

2. **La struttura viene però appiattita prima di uscire dal servizio.** In
   `pdf_extraction_service.py`:
   - `:88` chiama `chunk_by_title(elements, …)` (`unstructured_adapter.py:78`), che **aggrega** i
     raw elements in `CompositeElement` (category `"CompositeElement"`) e `Table`. Un Title di
     sezione viene **assorbito** nel `CompositeElement` e non sopravvive come elemento standalone.
   - `:113-125` costruisce i `TextChunk` di dominio dai **chunk** (post-`chunk_by_title`), con
     `element_type = getattr(chunk, "category", None)` (`:120`) → `"CompositeElement"`/`"Table"`,
     **mai `"Title"`**. `element_type` è `Optional[str]` (default `None`) — `schemas.py:23`,
     `models.py:13`.
   - I raw elements con i Title vivono in `ExtractionResult.elements` (`:152`) ma **non sono
     serializzati** nella `PdfExtractionResponse` (`main.py:207-215` serializza solo `chunks` +
     `metadata.detected_structures`, quest'ultima una lista aggregata di tipi distinti, senza
     testo/pagina per-elemento).

3. **Il lato C# eredita l'appiattimento.** `UnstructuredPdfTextExtractor`
   (`.../DocumentProcessing/Infrastructure/External/UnstructuredPdfTextExtractor.cs`):
   - `UnstructuredExtractionResponse.Chunks` (`:311-315`, `ElementType` = `string?`) è deserializzato
     ma il suo `element_type` è `"CompositeElement"`/`"Table"`.
   - `ExtractPagedTextAsync` (`:223`) → `CreatePageChunksFromText` (`:268`) ri-splitta il blob per
     `length/pageCount` in `PageTextChunk` sintetici. `PageTextChunk`
     (`IPdfTextExtractor.cs:120-124`) porta solo `PageNumber, Text, CharStartIndex, CharEndIndex`.

4. **Nessun caller produttivo** costruisce `ExtractedDocument`/`Sections`, e
   `AdvancedChunkingService.ProcessPlainText` (`:143-216`) forza `Heading = null` (`:180, 200`).

**Conclusione**: gli heading sono rilevati ma scartati al confine `chunk_by_title` (Python) e mai
ricostruiti in C#. La v1 di questa spec assumeva erroneamente che i `chunks` portassero `"Title"`;
è la premessa che la review ha invalidato.

---

## 4. Scope & confine

### In scope (SP1)
- **Python**: aggiungere alla `PdfExtractionResponse` un array `elements` (i raw partition elements
  con `text`, `page_number`, `category`), serializzato accanto a `chunks`.
- **C#**: introdurre `ExtractedElement` come **contract di dominio** (`DocumentProcessing.Domain`);
  farlo attraversare `PagedTextExtractionResult` **e** l'orchestrator (`EnhancedPagedExtractionResult`,
  `OrchestratedPdfTextExtractor`).
- **C#**: `ExtractedDocumentFactory.FromExtraction(...)` costruisce `ExtractedDocument.Sections`
  raggruppando gli elementi per `Title` (§6), con degradazione flat-text-safe (§7).
- Test unit + contract/integration sul path reale + eval riproducibile su fixture reale (§8).

### Fuori scope (rinviato a SP2+)
- Wire di `AdvancedChunkingService` nell'ingestion; persistenza hierarchy su `TextChunkEntity`/pgvector.
- Modifiche al ranking; re-index del corpus.
- Miglioramento di SmolDocling/Docnet (restano senza struttura → degradazione, §7).
- Table-handling speciale (le tabelle restano contenuto della sezione, con `ElementType` preservato).

### Confine netto
SP1 termina quando `ExtractedDocument` con `Sections`-con-heading è **prodotto dal path di
estrazione reale in produzione** (l'orchestrator, non solo `UnstructuredPdfTextExtractor` in
isolamento) ed è coperto da test. Nessun consumer nuovo a valle (quello è SP2).

---

## 5. Architettura & componenti

### 5.1 Python — esporre i raw elements
`apps/unstructured-service/src/api/schemas.py`: nuovo `ElementSchema { text, page_number, category }`
e campo `elements: List[ElementSchema]` su `PdfExtractionResponse`. Popolato in `main.py` dai raw
`ExtractionResult.elements` (che già esistono, `pdf_extraction_service.py:152`), non dai `chunks`.
`chunks` resta invariato (retro-compatibilità con i consumer esistenti). Nessuna nuova elaborazione:
si serializza ciò che è già in memoria.

### 5.2 C# — `ExtractedElement` come contract di dominio
Per non far dipendere `KnowledgeBase` da `DocumentProcessing.Infrastructure.External` (convenzione
del repo: KB → solo `DocumentProcessing.Domain`, [M5/M6]), il tipo vive nel **published language**:

```csharp
// Api.BoundedContexts.DocumentProcessing.Domain.Services
public record ExtractedElement(
    string Text,
    int PageNumber,       // 1-indexed
    string ElementType);  // category normalizzata, MAI null (vedi §6.4)
```

`PagedTextExtractionResult` (`IPdfTextExtractor.cs:82`) riceve un campo opzionale
`IReadOnlyList<ExtractedElement>? StructuredElements = null` (`null` = stage senza struttura).

### 5.3 C# — deserializzazione + propagazione end-to-end
- `UnstructuredPdfTextExtractor`: deserializza il nuovo `elements[]` della response Python
  (nuovo record `UnstructuredElement`); `ExtractPagedTextAsync` mappa `elements` → `ExtractedElement`
  (con normalizzazione `category`, §6.4) e popola `StructuredElements`. `PageChunks` e
  `ExtractTextAsync` **invariati** (regression pin in §8).
- **Orchestrator [M2/M11]**: `EnhancedPagedExtractionResult`
  (`EnhancedPdfProcessingOrchestrator.cs:877-887`) riceve `StructuredElements`;
  `CreateEnhancedPagedResult` (`:680-690`) lo copia; `OrchestratedPdfTextExtractor.ExtractPagedTextAsync`
  (`:57-63`) lo ripropaga nel `PagedTextExtractionResult` finale. Senza questi 3 passaggi il campo
  arriva `null` in produzione (il provider di default è `"Orchestrator"`,
  `DocumentProcessingServiceExtensions.cs:97,120`).

### 5.4 C# — `ExtractedDocumentFactory`
Builder puro in `KnowledgeBase.Application.Services.Chunking` (accanto a `ExtractedDocument`), che
dipende solo da `DocumentProcessing.Domain` (`ExtractedElement`) e non da Infrastructure:

```csharp
static ExtractedDocument FromExtraction(
    Guid documentId,
    Guid? gameId,
    IReadOnlyList<ExtractedElement>? structuredElements,
    string flatText);   // response.Text normalizzato, per il null-path (§7)
```

---

## 6. Data flow, grouping, offset

```
PDF → partition_pdf → raw elements {text, page_number, category=Title|NarrativeText|Table|…}
  → PdfExtractionResponse.elements[]        ← SP1 espone (Python)
  → UnstructuredElement[] (deserializzato)
  → ExtractedElement[] (category normalizzata, mai null)  ← StructuredElements
  → [orchestrator propaga senza droppare]
  → ExtractedDocumentFactory.FromExtraction()
  → ExtractedDocument { Content, Sections[] {Heading, Content, Page, ElementType, CharStart, CharEnd} }
  → [consumato da AdvancedChunkingService in SP2]
```

### 6.1 Grouping (Title → Section)
- Un elemento con `ElementType == "Title"` **apre** una nuova sezione; il suo testo diventa
  `Heading`. Il match è **esatto** su `"Title"` (case-sensitive); ogni altra category → corpo.
- Gli elementi dal Title (incluso) fino al **prossimo Title** = `Content` della sezione (§6.3).
- Elementi **prima del primo Title** → sezione "preambolo" con `Heading = null`.
- **Title consecutivi** [M9]: un Title immediatamente seguito da un altro Title produce una sezione
  con `Content` = solo il testo del Title (heading-only) — **la sezione è emessa**, l'heading non si
  perde.
- **Title come ultimo elemento** [M9]: produce una sezione heading-only finale, emessa.
- Nessun Title in tutto il documento → una sola sezione preambolo (`Heading = null`) con tutto il
  contenuto (equivale a valle al fallback plain-text).

### 6.2 CharStart / CharEnd & Content
`ExtractedDocument.Content` = concatenazione di **tutti** gli elementi in ordine (separatore
`"\n\n"`). `CharStart`/`CharEnd` di ogni sezione sono gli offset del suo span dentro `Content`.

### 6.3 Title text nel Content [M4]
Il testo del Title **è incluso** in `section.Content` (la sezione parte dal Title). `Heading` ne è
una copia (annotazione). Questo garantisce l'**invariante** verificabile:
`section.Content == doc.Content.Substring(section.CharStart, section.CharEnd - section.CharStart)`
per **ogni** sezione (preambolo, heading-only, multi-Title inclusi). L'invariante è un test (§8).

### 6.4 Vocabolario `ElementType` & normalizzazione [M1/M3/M8/M12]
- `category` da Python è `Optional[str]`: al boundary di mapping, `null`/whitespace →
  `"NarrativeText"` (canonico di corpo). `ExtractedElement.ElementType` è quindi **mai null**.
- `DocumentSection.ElementType` (`IAdvancedChunkingService.cs:92-94`, vocab documentato
  `{text, table, list, heading}`, default `"text"`) è popolato via mapping deterministico:
  `Title→"heading"`, `Table→"table"`, `ListItem→"list"`, tutto il resto→`"text"`. Il mapping è una
  funzione pura testata. (Nota: il grouping §6.1 usa la category **grezza** `"Title"`; la
  normalizzazione riguarda solo il valore scritto in `DocumentSection.ElementType`.)

---

## 7. Error handling & degradazione (canonico, no "or") [M3/minor]

Un **unico** comportamento per il null-path:

- `StructuredElements == null` **oppure** vuoto (SmolDocling/Docnet, response malformata, `elements`
  assente) → `FromExtraction` produce **esattamente una** sezione preambolo con:
  `Heading = null`, `Content = flatText`, `ElementType = "text"`, `Page = 1`, `CharStart = 0`,
  `CharEnd = flatText.Length`. `ExtractedDocument.Content = flatText`.
- **Il testo del documento non va mai perso**: nel null-path il flat text (da `response.Text`) è la
  sorgente della sezione preambolo, così SP2 può comunque chunkare (fallback plain-text) senza
  contenuto vuoto. (Bug evitato: una "sezione vuota" verrebbe scartata da
  `AdvancedChunkingService.cs:96-97` → perdita totale del documento.)
- **Copertura heading**: dipende dallo Stage 1 (Unstructured), default quando la qualità è ≥ soglia.
  SmolDocling/Docnet non forniscono struttura → degradano a preambolo. Documentato; il loro
  miglioramento è fuori scope.
- Nessun nuovo percorso di fallimento: `ExtractTextAsync`/`PageChunks` restano il comportamento
  odierno.

---

## 8. Testing

### 8.1 Unit `ExtractedDocumentFactory`
- Title singolo → 1 sezione, heading corretto.
- Multi-Title → N sezioni; offset corretti.
- Elementi prima del primo Title → sezione preambolo `Heading = null`.
- Title consecutivi → sezione heading-only emessa [M9].
- Title finale senza corpo → sezione heading-only finale emessa [M9].
- Nessun Title → 1 preambolo con tutto il contenuto.
- `category` null/`""` → elemento trattato come corpo, `ElementType` mai null, nessuna NRE [M3/M8].
- Match esatto: `"title"` minuscolo / `"SectionHeader"` → corpo, non aprono sezione [M8].
- Table nel corpo → contenuto incluso, mapping `ElementType → "table"` sull'elemento.
- **Invariante substring** [M4]: per ogni sezione
  `section.Content == doc.Content.Substring(CharStart, CharEnd - CharStart)`.
- **Null-path** [M3/M2-minor]: `structuredElements == null` con `flatText` non vuoto → una sezione
  preambolo con `Content == flatText`, `CharStart == 0`, `CharEnd == flatText.Length`; `[]` idem.

### 8.2 Contract/integration estrattore
- **Path reale** [M2]: `OrchestratedPdfTextExtractor` su una response Stage-1 (mock HTTP) →
  `StructuredElements` **sopravvive** fino al `PagedTextExtractionResult` finale (non solo il test
  diretto di `UnstructuredPdfTextExtractor`).
- **Degradazione** [M10]: response con `elements: []` e response senza `elements` →
  `StructuredElements == null` **e** `PageChunks` byte-per-byte invariato (regression pin del DoD
  "PageChunks invariati").
- Le fixture riflettono l'output **reale**: `elements` con `category` grezze
  (`"Title"`/`"NarrativeText"`/`"Table"`), **non** chunk `"Title"` sintetici [C1-C3].

### 8.3 De-risk eval riproducibile (gate di SP1) [M1-minor/M7]
- **Fixture**: catturare **una** response JSON reale del servizio `unstructured` per un rulebook di
  riferimento (Terraforming Mars IT se caricabile, altrimenti un rulebook IT già disponibile in
  staging), committarla come fixture di test. La cattura fissa una volta il giudizio umano
  "heading sensati".
- **Harness**: replay della fixture via `HttpMessageHandler` mock → estrattore → factory
  (CI-runnable). Target **Stage-1 diretto** per non confondere l'esito con il routing
  qualità→fallback.
- **Criterio di accettazione misurabile**: enumerare 4-6 sezioni-chiave attese con il loro heading
  (es. "Preparazione"/"Setup", "Punteggio", …); il gate passa se **≥ N di M** heading attesi sono
  recuperati non-null e corrispondenti. Esito (pass/fail + heading trovati) documentato nella PR;
  **fail → si rivaluta l'approccio prima di SP2**.

---

## 9. Rischi & incognite

| Rischio | Mitigazione |
|---|---|
| `partition_pdf` (strategy `fast`) rileva pochi/rumorosi Title sui rulebook | Il de-risk eval §8.3 è il **gate esplicito**; valutare `hi_res` come follow-up se `fast` è insufficiente |
| I raw `elements` gonfiano la response (payload/latenza) | Sono già in memoria; si serializza soltanto. Misurare la dimensione response sul rulebook di riferimento |
| `category` di `unstructured` instabile tra versioni | Il grouping dipende solo da `"Title"` vs resto; il mapping §6.4 manda ogni category sconosciuta a `"text"` |
| `response.Text` diverge da `join(elements)` (offset) | `Content` ricostruito per concatenazione è la sorgente canonica; invariante substring testata (§8.1) |
| Regressione dei consumer esistenti della response Python | `chunks` invariato; si **aggiunge** solo `elements` |

---

## 10. Definition of Done (SP1)

- [ ] Python: `PdfExtractionResponse.elements[]` (`ElementSchema {text, page_number, category}`) dai
      raw partition elements; `chunks` invariato; test del servizio aggiornato.
- [ ] `ExtractedElement` in `DocumentProcessing.Domain`; `StructuredElements` su
      `PagedTextExtractionResult`.
- [ ] `UnstructuredPdfTextExtractor` popola `StructuredElements` dai raw `elements` (mapping §6.4).
- [ ] Orchestrator propaga `StructuredElements` (`EnhancedPagedExtractionResult` +
      `CreateEnhancedPagedResult` + `OrchestratedPdfTextExtractor`) [M2/M11].
- [ ] `ExtractedDocumentFactory.FromExtraction` (grouping §6, null-path §7) — dipende solo da
      `DocumentProcessing.Domain` [M5/M6].
- [ ] Test unit factory (tutti i casi §8.1) verdi, incluso l'invariante substring e il null-path.
- [ ] Contract test sul path **orchestrato** reale + regression pin `PageChunks` invariato [M2/M10].
- [ ] De-risk eval §8.3 con fixture reale committata e criterio ≥N/M documentato + esito nella PR.
- [ ] Nessuna regressione sui path esistenti (`ExtractTextAsync`/`PageChunks` invariati; build +
      suite verdi).
- [ ] PR mergiata nel branch padre.

---

## 11. Riferimenti

- ADR-016 (hierarchical chunking) — `AdvancedChunkingService`, `HierarchicalChunk`, `ExtractedDocument`.
- Issue #730 — schema campi hierarchy su `TextChunkEntity`.
- #1391 / Phase D — RoleTags + `RoleMatchBoost` (SP2/SP4).
- File chiave:
  - `apps/unstructured-service/src/api/schemas.py`, `.../application/pdf_extraction_service.py`,
    `.../infrastructure/unstructured_adapter.py`, `.../api/main.py` (contract Python)
  - `.../DocumentProcessing/Infrastructure/External/UnstructuredPdfTextExtractor.cs`,
    `IPdfTextExtractor.cs`, `OrchestratedPdfTextExtractor.cs`,
    `.../Application/Services/EnhancedPdfProcessingOrchestrator.cs`
  - `.../KnowledgeBase/Application/Services/Chunking/IAdvancedChunkingService.cs`
    (`ExtractedDocument`, `DocumentSection`), `AdvancedChunkingService.cs` (consumer, SP2)

---

## 12. Appendice — review adversariale (2026-07-21)

Spec sottoposta a review multi-esperto (Wiegers/Nygard/Newman/Fowler/Crispin + verificatore
tecnico contro il codebase), 19 finding confermati (3 critical, 3 major, 13 minor) su 31 grezzi.

| ID | Sintesi | Risoluzione in v2 |
|----|---------|-------------------|
| C1-C3 | `chunks` non portano `"Title"` (post-`chunk_by_title`) → grouping a zero sezioni | §3 premessa corretta; §5.1 espone raw `elements`; §6.1 grouping su raw category |
| M1 | Vocabolario `ElementType` + mapping indefiniti | §6.4 mapping deterministico |
| M2/M11 | Orchestrator droppa `StructuredElements` | §5.3 propagazione a 3 hop; §8.2 test path reale |
| M3 | Null/empty specificato in 3 modi + silent text loss | §7 comportamento canonico flat-text-safe |
| min. Content/offset null-path | Content/offset del preambolo non specificati | §7 + §8.1 null-path test |
| min. null `element_type` | NRE/corruzione latente | §6.4 coalesce mai-null |
| min. Title-in-Content | Invariante substring non pinnata | §6.3 + §8.1 invariante |
| min. factory placement (M5/M6) | KB dipende da DP.Infrastructure | §5.2 `ExtractedElement` in DP.Domain |
| min. de-risk gate | Nessun criterio misurabile/harness | §8.3 fixture reale + ≥N/M |
| min. edge case Title (M9) | Title consecutivi/finali persi | §6.1 heading-only emesse |
| min. off-by-one | Citazione riga errata | corretto: `AdvancedChunkingService.cs:180,200` |
