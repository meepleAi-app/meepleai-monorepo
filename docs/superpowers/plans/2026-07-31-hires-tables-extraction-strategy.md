# DC-2 hi_res-per-tabelle — piano TDD (#3419)

Spec: [`2026-07-31-hires-tables-extraction-strategy-design.md`](../specs/2026-07-31-hires-tables-extraction-strategy-design.md).
Parent branch: `main-dev`. Solution BE: `apps/api/MeepleAI.Api.sln`.

> ## ⚠️ Stato + revisione approccio (2026-07-31)
> **Groundwork shippato** (Slice 1): `ExtractionStrategy` enum + `ExtractionStrategyDecider.FromStructuredElements`
> (Domain/Services) con test 9/9 verdi. NON tocca `IPdfTextExtractor` → zero blast radius.
>
> **Il wiring via param sull'interface (Slice 2 originale) è stato TENTATO e RIGETTATO**: aggiungere
> `strategy` a `IPdfTextExtractor.ExtractText/PagedTextAsync` rompe **~89 call-site** nel test project
> (chiamate posizionali con `CancellationToken` come 3° arg + i Moq setup). Mettere `strategy` PRIMA di CT
> (per non rompere i posizionali) viola **CA1068** (CT deve essere l'ultimo param, escalato a error) →
> reorder che rompe comunque decine di call-site. Blast radius inaccettabile per un follow-up di qualità.
>
> **Approccio raccomandato per la sessione dedicata — scoped context (nessun cambio interface):**
> - Nuovo servizio **scoped** `IExtractionStrategySelector { ExtractionStrategy Current { get; set; } }`
>   (default `Fast`), registrato scoped.
> - `UnstructuredPdfTextExtractor` inietta il selector e legge `Current` in `PrepareMultipartContent`
>   (al posto dell'hardcoded/param). Gli altri extractor lo ignorano (non lo iniettano).
> - `PdfProcessingPipelineService` inietta il selector e fa `selector.Current = ExtractionStrategyDecider.FromStructuredElements(pdfDoc.StructuredElementsJson)`
>   PRIMA di chiamare `ExtractPagedTextAsync`. Scope per-request → pipeline + extractor condividono lo scope.
> - **Zero** modifiche a `IPdfTextExtractor`/orchestrator/call-site/test → nessun blast radius.
> - TDD: selector default Fast; Unstructured legge il selector nel multipart; pipeline setta il selector da StructuredElements.
> - Nota concorrenza: scoped = per-request; non usare singleton/`AsyncLocal` cross-request.

## Obiettivo
Instradare l'estrazione Unstructured a `hi_res` **solo per i PDF con tabelle** (rilevate dal
`StructuredElementsJson` precedente), altrimenti `fast`. Additivo, zero regressione sui path fresh-ingest.

## Slice 1 — `ExtractionStrategy` + decider (dominio, no dipendenze esterne)
- **RED** (unit): `ExtractionStrategyDecider.FromStructuredElements(json)` →
  - `null`/vuoto/JSON malformato → `Fast` (default sicuro).
  - JSON con un elemento `ElementType=="Table"` → `HiRes`.
  - JSON senza `Table` (solo Title/NarrativeText) → `Fast`.
  - case-insensitive su "table"? (decidi: match esatto "Table" come emesso da Unstructured).
- **GREEN**: `enum ExtractionStrategy { Fast, HiRes }` + decider che deserializza `List<ExtractedElement>`
  (riusa il tipo già persistito) e cerca `Any(e => e.ElementType == "Table")`. Robusto a JSON invalido (try/catch → Fast).

## Slice 2 — `IPdfTextExtractor` + Unstructured usa la strategy
- **RED** (unit `UnstructuredPdfTextExtractor`): con `strategy: HiRes` il multipart contiene il campo
  `strategy="hi_res"`; con `Fast` (o default) → `"fast"`. (Testare `PrepareMultipartContent` o via mock HTTP handler
  ispezionando il body inviato.)
- **GREEN**: aggiungere `ExtractionStrategy strategy = ExtractionStrategy.Fast` come **ultimo** param a
  `ExtractTextAsync`/`ExtractPagedTextAsync` (interface + Unstructured). `PrepareMultipartContent` prende la strategy →
  `strategy.ToWireString()` (`Fast→"fast"`, `HiRes→"hi_res"`). Rimuovere l'hardcoded `"fast"`.
- **GREEN (mechanical)**: aggiungere il param (accept-and-ignore) a SmolDocling, Docnet, i 2 DevTools mock.
- **Regressione**: i 7 call-site esterni compilano invariati (param con default).

## Slice 3 — Orchestrated + Orchestrator threading
- **RED** (unit): `OrchestratedPdfTextExtractor.ExtractPagedTextAsync(..., HiRes)` → l'orchestrator riceve `HiRes` →
  l'extractor keyed Unstructured riceve `HiRes`. (Mock dell'orchestrator/extractor keyed, assert sul param.)
- **GREEN**: `EnhancedPdfProcessingOrchestrator.Extract*WithFallbackAsync` + `TryExtract*WithStage` + i punti
  `extractor.Extract*Async(...)` (righe 252, 538; stage-3 Docnet 339/364 opzionale, ignora) threadano `strategy`.
  `OrchestratedPdfTextExtractor` passa il param ai metodi pubblici dell'orchestrator.

## Slice 4 — decision-site nella pipeline
- **RED** (unit/integration `PdfProcessingPipelineService`): con `pdfDoc.StructuredElementsJson` contenente un
  `Table` → l'estrazione è invocata con `HiRes`; senza tabelle/null → `Fast`. (Mock `IPdfTextExtractor`, assert sul
  param strategy.)
- **GREEN**: prima di `ExtractPagedTextAsync` (`:514`), `var strategy = ExtractionStrategyDecider.FromStructuredElements(pdfDoc.StructuredElementsJson);`
  e passarlo. **Ordine**: leggere PRIMA della riga `:528` che sovrascrive `StructuredElementsJson`.
- **Nota**: gli altri 3 call-site (fresh ingest) NON passano strategy → default `Fast` (corretto: nessun elemento precedente).

## Slice 5 — verifica + PR
- Build solution 0 err; unit test nuovi verdi; non-regressione DocumentProcessing (extractor/orchestrator/pipeline).
- `/code-review` xhigh + review avversariale (Workflow find→verify) sul diff.
- PR → `main-dev` (chiude #3419). Commit-msg subject ≤90 char.

## Rischi/gotcha
- Param dopo `CancellationToken` (non-convenzionale ma necessario per i call-site posizionali) — commentare.
- 6 implementer + 2 mock: dimenticarne uno = build error (lo cattura il compilatore).
- `ExtractedElement` deserializzazione: riusare lo stesso `JsonSerializerOptions` con cui è serializzato
  `StructuredElementsJson` (verificare naming policy).
- Baseline test = 0 fail; il ramo hi_res è dark finché un PDF con tabelle non viene re-estratto.
