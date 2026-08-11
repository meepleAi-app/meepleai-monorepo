# ADR-088 — Mechanic Cards as RAG Retrieval Source

**Status**: Proposed
**Date**: 2026-07-30
**Tracker**: [tracking issue](https://github.com/meepleAi-app/meepleai-monorepo/issues/3416)
**Decision Makers**: Product Lead, Engineering Lead
**Related**: ADR-051 (Mechanic Extractor — IP Policy & AI-first Pipeline), ADR-084 (Mechanic Validation Canonical Shape), ADR-062 (Config Environment semantics), ADR-060 (Live-session persistence / outbox). Spec di dettaglio: [`2026-07-30-mechanic-card-rag-integration-design.md`](../../../for-developers/specs/2026-07-30-mechanic-card-rag-integration-design.md). Coordina con l'epic RAG citation region grounding ([spec](../../../superpowers/specs/2026-07-30-rag-citation-region-grounding-design.md)).

---

## Context

Due bounded context oggi non comunicano pur condividendo lo stesso substrato (chunk / PDF / gioco):

- **Mechanic Extractor** (`SharedGameCatalog`, ADR-051): pipeline admin-only che produce claim **riformulati player-facing**, human-approved (`Draft → InReview → Published`), ancorati a citazioni verbatim (`PdfPage` + `Quote` ≤25 parole + `ChunkId`) e con guardrail di grounding.
- **Agente RAG** (`KnowledgeBase`): pipeline player-facing su testo grezzo indicizzato (hybrid search + rerank + LLM).

Il RAG grezzo è a **bassa precisione** proprio sulle sezioni più richieste (Setup, Victory, Resources, Phases): il caso misurato è la query "Setup per N giocatori" su Terraforming Mars, dove l'heading mis-detection fa recuperare la sezione sbagliata e nel caso peggiore il retrieval non supera nemmeno `MinScore`, uscendo con "not available". Il Mechanic Extractor produce **esattamente** conoscenza verificata su quelle sezioni.

Serve una decisione architetturale su **come** far confluire quei claim nell'agente player-facing senza violare i confini dei bounded context né degradare i giochi non coperti. Il substrato lo rende fattibile: `MechanicCitation.ChunkId → TextChunkEntity.Id`, `MechanicSection ≈ GameBookRole`, e il contratto pubblicato (`Published` → `MechanicCard` → `PublishedMechanicCardDto`) esiste già come anti-corruption boundary.

## Decision

### D1 — Gate su claim `Published`, non `Certified`

L'agente attinge dai claim di una `MechanicCard` **attiva e non soppressa** (`Published`). `Certified` (che vive solo su `MechanicAnalysis`) **non** è un filtro: sarebbe un trust-flag additivo, ma è **deferito** — quando servirà andrà esposto **producer-side** (estensione del contratto pubblicato o snapshot in `MechanicCardContent`), mai letto da `KnowledgeBase` sull'aggregate `MechanicAnalysis`.

**Motivazione**: `Published` massimizza la copertura (pochi giochi sono `Certified`) restando human-approved; leggere `CertificationStatus` da KB violerebbe il confine BC.

### D2 — Integrazione read-time + cache, non proiezione event-driven

`KnowledgeBase` legge la card a question-time via `GetPublishedMechanicCardByGameQuery` (dietro `IMechanicCardProvider` + `IHybridCacheService` + fallback best-effort), consumando **solo** il contratto pubblicato. La proiezione event-driven è un'evoluzione futura.

**Motivazione**: la card è una **riga attiva per gioco** (indice unico filtrato), il query esiste già e **applica la suppression gratis**. Il gap "`MechanicCard.Unsuppress` non alza evento" è auto-sanato da un TTL cache (staleness *bounded*), mentre una proiezione materializzata non si auto-sanerebbe (staleness *unbounded*). KISS/YAGNI.

### D3 — Iniezione a livello di prompt-assembly (Seam A), sopra gli early-exit

I claim selezionati per `MechanicSection` (mapping da `GameBookRole`) sono iniettati come blocco `[Verified Rules]` **riconosciuto come contesto** nel prompt, **non** come arm della fusione RRF (non devono poter essere "reranked away"). Il card-fetch avviene **sopra** gli early-exit `no-results`/response-cache degli handler. Precedenza: `house rule > claim approvato > RAG grezzo`. Fail-open verso RAG grezzo; fail-closed sulla suppression (bounded-staleness). Le citazioni claim sono emesse con provenienza `source=claim` sul canale citazione dell'epic RAG citation region grounding (coordinamento: shape DTO di quella epic; SP-A prima di R1).

**Motivazione**: precisione e autorevolezza controllate, retrieval intatto → zero regressione sui giochi scoperti; l'iniezione sopra l'early-exit è ciò che risolve il caso retrieval-miss (altrimenti R1 sarebbe un no-op sul suo caso motivante).

## Consequences

**Positive**
- Risolve un fallimento *misurato* (setup TM) con contenuto human-verified, ad alta precisione.
- Confini BC rispettati (solo contratto pubblicato); nessun accoppiamento al lifecycle `MechanicAnalysis`.
- Citazioni claim con `Quote` verbatim esatta → miglior ancora rispetto allo snippet RAG a 150 char, e input ideale per il text-highlight dell'epic citazioni.
- Zero regressione sui giochi senza card (fail-open).

**Costs / rischi**
- Rischio primario di **governance del dato**: servire contenuto autorevole ma *vecchio* (card non rigenerata dopo aggiornamento regolamento) o *soppresso oltre la finestra di cache*. Mitigazioni: fingerprint card nelle chiavi di response-cache, TTL, sorveglianza `Version`/`PublishedAt`.
- Precondizione hard: R1 è validabile solo con ≥1 card `Published` (dipende dalla pipeline admin-review).
- Superficie condivisa col canale citazione dell'epic RAG citation region grounding → coordinamento e sequencing richiesti (SP-A prima di R1).

**Sequence**
- R1 (questa decisione) dietro feature flag `KnowledgeBase:MechanicCardInjection:Enabled` (default off), enablement graduale.
- R2 (oracolo di verifica) e R3 (fast-path card) come fasi successive.

## Alternatives

- **Hard-gate `Certified`** — scartato (D1): copertura troppo bassa.
- **Proiezione event-driven subito** — scartato (D2): staleness unbounded per il gap `Unsuppress`, complessità non giustificata per una riga per gioco.
- **Seam B (arm nella fusione RRF)** — scartato (D3): i claim autorevoli verrebbero rerankati; richiederebbe un campo provenance su `SearchResultDto` e non controllerebbe la precedenza.
