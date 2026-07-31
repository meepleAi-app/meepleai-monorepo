# DC-2 — `hi_res` extraction strategy for table-heavy PDFs (design)

**Issue**: [#3419](https://github.com/meepleAi-app/meepleai-monorepo/issues/3419) · **Epic**: #3403 (RAG citation region grounding) · **Follow-up di**: SP-E #3409

## Problema

Il region grounding (epic #3403) è attivo con `strategy=fast`, che emette già coordinate bbox
(strategy-independent). Ma per le **tabelle**, `hi_res` produce coordinate/struttura più precise.
DC-2 (deciso 2026-07-30): usare `hi_res` **solo per i PDF con tabelle**, non globalmente
(`hi_res` è pesante: CPU/memoria, rischio OOM/timeout sul box staging 8GB).

Oggi la strategy è **hardcoded `"fast"`** in `UnstructuredPdfTextExtractor.cs:123`
(`PrepareMultipartContent`), e `IPdfTextExtractor` non ha un parametro strategy. Il servizio
Python **accetta già** `strategy: Literal["fast","hi_res"]` per-request (`schemas.py:11`) → il
lavoro è tutto lato C#.

## Non-goal

- Non serve al grounding di base (`fast` fornisce già coordinate). Puro miglioramento qualità
  sulle regioni tabellari.
- Nessun cambiamento al servizio Python (già pronto).
- Nessun threading di strategy attraverso `ProcessingJob`/`EnqueuePdfCommand`/migration (vedi DA-2).

## Discovery (fatti verificati)

| Fatto | Riferimento |
|---|---|
| `IPdfTextExtractor` **internal**, 2 metodi `ExtractTextAsync`/`ExtractPagedTextAsync(Stream, bool enableOcrFallback=true, CancellationToken=default)` | `Infrastructure/External/IPdfTextExtractor.cs:19-34` |
| **6 implementer**: Unstructured, SmolDocling, Docnet, **Orchestrated** (default via provider "Orchestrator"), + 2 DevTools mock | discovery |
| Default DI = `OrchestratedPdfTextExtractor` → `EnhancedPdfProcessingOrchestrator` → keyed Unstructured. Solo Unstructured consuma la strategy | `OrchestratedPdfTextExtractor.cs:28-65`, `EnhancedPdfProcessingOrchestrator.cs:121,467,231-255,517-538` |
| Hardcode `"fast"` in `PrepareMultipartContent` (usato da entrambi i metodi, righe 48 e 234) | `UnstructuredPdfTextExtractor.cs:123,129` |
| 7 call-site esterni (tutti passano `cancellationToken` **posizionale** come 3° arg) | discovery |
| `ExtractedElement.ElementType` porta la categoria Unstructured incl. **`"Table"`** | `Domain/Services/ExtractedElement.cs:9-13` |
| `PdfDocumentEntity.StructuredElementsJson` persiste la lista di `ExtractedElement` | `Infrastructure/Entities/.../PdfDocumentEntity.cs` |
| `detected_tables`/`detected_structures` **NON** persistiti (solo nel DTO response transiente) | `UnstructuredPdfTextExtractor.cs:389-390` |
| Il reindex processa via `EnqueuePdfCommand → ProcessingJob → PdfProcessingQuartzJob → PdfProcessingPipelineService` | `Application/Jobs/PdfProcessingQuartzJob.cs` |
| `PdfProcessingPipelineService:528` **sovrascrive** `StructuredElementsJson` DOPO l'estrazione → prima della chiamata (`:514`) il valore è ancora quello **precedente** | `PdfProcessingPipelineService.cs:514,528` |
| `appsettings.json:217` ha `"Strategy": "fast"` — config **esistente ma ignorata** | — |

## Decisioni

| ID | Decisione | Perché |
|---|---|---|
| **DA-1** | Enum dominio `ExtractionStrategy { Fast, HiRes }`; `UnstructuredPdfTextExtractor` mappa enum→`"fast"`/`"hi_res"` | Type-safe; il resto degli implementer lo ignora |
| **DA-2** | La **decisione** strategy sta nella **pipeline** (`PdfProcessingPipelineService`), NON threadata da `ReindexDocumentCommand`/`ProcessingJob` | La pipeline ha già `pdfDoc.StructuredElementsJson`; evita colonna/migration/command changes. Blast radius = solo interface + orchestrator + il decision-site |
| **DA-3** | Table detection = `StructuredElementsJson` (precedente) contiene un `ElementType=="Table"` → `HiRes`, altrimenti `Fast` | Unico segnale persistito. Euristica: `fast` rileva già `Table`; se ne trova → serve precisione hi_res |
| **DA-4** | Param `strategy` aggiunto come **ultimo** (4°, dopo `CancellationToken`) con default `Fast` | I 7 call-site passano CT posizionale come 3° arg → un 4° param con default li lascia compilare invariati (solo la pipeline lo passa esplicito, named) |
| **DA-5** | Fresh ingest (nessun `StructuredElementsJson` precedente) → `Fast` | Non si possono conoscere le tabelle prima della 1ª estrazione; hi_res parte solo al **re-extract** (caso d'uso SP-E) |
| **DA-6** | Threading via `OrchestratedPdfTextExtractor` + `EnhancedPdfProcessingOrchestrator` (`Extract*WithFallbackAsync` → `TryExtract*WithStage`) | È la catena reale del default DI; senza, la strategy non arriva a Unstructured |

## Contratto (estensioni additive)

```csharp
public enum ExtractionStrategy { Fast, HiRes }   // Domain/Services

// IPdfTextExtractor (entrambi i metodi): + ExtractionStrategy strategy = ExtractionStrategy.Fast (4° param)
// EnhancedPdfProcessingOrchestrator.Extract*WithFallbackAsync: + strategy, threadato a TryExtract*WithStage → extractor
// UnstructuredPdfTextExtractor.PrepareMultipartContent(Stream, ExtractionStrategy): "fast"|"hi_res"
// Table detection: static helper su ExtractedElement[]/StructuredElementsJson → bool HasTables
```

Decision-site (`PdfProcessingPipelineService`, prima di `ExtractPagedTextAsync`):
```csharp
var strategy = ExtractionStrategyDecider.FromStructuredElements(pdfDoc.StructuredElementsJson);
// ... ExtractPagedTextAsync(stream, enableOcrFallback: true, cancellationToken, strategy);
```

## Rischi

| Rischio | Mitigazione |
|---|---|
| Blast radius interface (6 implementer + orchestrator) | Param additivo con default; 5/6 implementer accept-and-ignore; TDD per ognuno |
| Regressione pipeline PDF (alto valore) | TDD + non-regressione; feature attiva solo su re-extract con tabelle rilevate |
| `hi_res` più lento/pesante (OOM/timeout box 8GB) | Solo per PDF con tabelle (minoranza); il fix #3424 (timeout 120s + file-size 100MB) già alza i limiti; `mem_limit 3g` + concurrency=1 |
| Euristica table-detection imperfetta (fast può mancare tabelle) | Accettata: se `fast` non trova tabelle, `hi_res` non aggiungerebbe regioni tabellari comunque; nessun peggioramento |
| `Strategy` config in appsettings ignorata | Fuori scope: la decisione è table-based, non config-based (nota per non confondere) |

## Criteri di accettazione

- **AC1**: `IPdfTextExtractor` accetta `strategy`; `UnstructuredPdfTextExtractor` invia il campo
  `strategy` corretto (`fast`/`hi_res`) nel multipart (test unit).
- **AC2**: `PdfProcessingPipelineService` sceglie `HiRes` quando `StructuredElementsJson` contiene
  un `ElementType=="Table"`, altrimenti `Fast` (test unit/integration).
- **AC3**: I 5 implementer non-Unstructured + i 7 call-site esistenti restano invariati/compilano
  (nessuna regressione).
- **AC4**: Il ramo Orchestrated threada la strategy fino a Unstructured (test).
