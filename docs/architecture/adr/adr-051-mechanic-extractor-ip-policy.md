# ADR-051: Mechanic Extractor — IP Policy & AI-first Pipeline

**Status**: Proposed
**Date**: 2026-04-23
**Issue**: Spec Panel Mechanic Extractor (2026-04-23) — evoluzione Variant C → AI-first
**Decision Makers**: Product Lead, Engineering Lead
**Related**: ADR-043 (LLM NFR), ADR-024 (PDF embedding pipeline), PR #517 (idempotent migrations pattern)

---

## Context

Il Mechanic Extractor fu introdotto con il workflow **Variant C** ("human-authored, AI-assisted"): l'operatore scrive note sul regolamento, l'AI assiste trasformandole in draft, l'AI **non legge mai il testo originale del PDF**. Questa scelta era un **scudo preventivo di over-compliance** in assenza di consulenza IP formale: se l'AI non ha mai letto il testo, l'output non può essere derivato del testo originale.

Al 2026-04-23 lo stato è:
- Variant C implementato in `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/MechanicExtractor/*`
- Tabella `mechanic_drafts` in produzione con **0 righe** (nessuna estrazione mai finalizzata)
- Footer UI dichiara esplicitamente *"L'AI non ha mai letto il testo del PDF originale"*
- Frontend editor in `apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/`

Due nuovi obiettivi di prodotto emergono dal Spec Panel 2026-04-23:

1. **Specchietti di consultazione** — reference cards accurate per il giocatore al tavolo
2. **Dimostrazione di comprensione AI** — artefatto che prova che l'AI comprende le regole con grounding visibile (citazioni al manuale)

**Tensione con lo status quo**: entrambi gli obiettivi richiedono che l'**AI legga il PDF**, invertendo la premessa Variant C. Il footer attuale diventa tecnicamente falso e controproducente per il posizionamento "trust chain visibile".

Driver della decisione:
- L'utente carica un PDF che possiede legalmente (responsabilità downstream)
- Le **regole funzionali** di un gioco (fatti) non sono tutelate da copyright: solo la *espressione testuale* lo è
- Il diritto di citazione (art. 70 L.633/41 in Italia, fair use negli US) consente quote brevi con attribution
- Senza AI-on-PDF, il purpose 2 (comprehension demo con citations) è irrealizzabile

---

## Decision

**Sostituire il modello Variant C (AI-no-read)** con un **modello AI-first con citation enforcement e review gate umano**, formalizzando la IP policy tramite vincoli tecnici auto-applicati.

### Policy di uso dei contenuti del manuale

1. **Riformulazione obbligatoria**: ogni claim prodotto dall'AI deve essere **non-letterale**. Trascrizione o traduzione fedele del testo originale è **vietata**.
2. **Quote cap**: ogni citation può contenere una `quote` testuale del manuale di **massimo 25 parole** (soglia fair use conservativa).
3. **Citation obbligatoria**: ogni claim pubblicato deve avere almeno **una citation con `pdf_page`** e `quote ≤ 25 parole` a scopo di attribution.
4. **Attribution visibile**: l'UI player-facing mostra copyright dell'editore originale e link al claim → citation.
5. **Responsabilità di upload sull'utente**: il Terms of Service stabilisce che l'utente dichiara di possedere una copia legittima del PDF caricato (contratto esistente da rivedere).
6. **Takedown policy**: un editore può richiedere la soppressione di `mechanic_cards` per i propri giochi; effetto immediato senza deploy via flag `is_suppressed`.
7. **Review gate umano obbligatorio**: nessuna `mechanic_card` viene pubblicata senza approvazione esplicita di un admin (per-claim review); questo è anche un controllo di qualità, non solo IP.

### Vincoli tecnici enforced dal sistema

| # | Vincolo | Implementazione |
|---|---------|-----------------|
| T1 | Quote length ≤ 25 parole | Validator post-LLM: reject + re-prompt se violato |
| T2 | Rejection sampling su copia letterale | Post-check: se una sequenza di output matcha > 10 parole consecutive del PDF source → reject + re-prompt |
| T3 | Citation obbligatoria per claim | JSON schema enforcement nel prompt + validator applicativo |
| T4 | Page reference verificabile | `citation.pdf_page` deve puntare a una pagina esistente nel PDF; `quote` deve essere substring del chunk associato |
| T5 | Kill switch `is_suppressed` | Bool su `mechanic_cards`, indice parziale `WHERE is_suppressed = false` |
| T6 | Audit log review | Ogni `status` transition su `mechanic_analyses` produce una riga in audit log con `reviewed_by`, `reviewed_at` |
| T7 | Prompt versioning | `prompt_version` colonna + UNIQUE `(shared_game_id, pdf_document_id, prompt_version)` → riproducibilità |
| T8 | Cost cap | Pre-flight estimate + hard cap default €2.00/analisi; override esplicito admin con reason |

### Messaggio di attribution (UI)

**Rimosso** (footer Variant C):
> *"L'AI non ha mai letto il testo del PDF originale."*

**Sostituito con** (footer unificato):
> *"Analisi elaborata dall'AI sul manuale del gioco. Ogni affermazione è riformulata in parole originali e cita la pagina del regolamento. Copyright © degli editori per il testo originale del manuale."*

### Deprecazione Variant C

- `mechanic_drafts` e rotta editor esistente diventano **legacy/deprecated**
- Banner deprecation sull'editor + redirect suggerito al nuovo flow AI-first
- **Nessuna migrazione dati** (0 righe esistenti)
- Codice rimosso post ≥ 6 mesi di sunset (tracked separatamente)

---

## Consequences

### Positive

- ✅ Sblocca purpose 1 (specchietti) e purpose 2 (comprehension demo) tecnicamente e legalmente
- ✅ Pipeline unica (no dual-path Variant C + AI-first) → meno complessità, più coerenza
- ✅ Citation visibile = differenziante competitivo vs LLM generici
- ✅ Review gate = controllo qualità + IP safety in un unico step
- ✅ Policy scritta → protegge in caso di dispute (ADR come evidenza di due diligence)
- ✅ Pipeline condivisibile con chat RAG (stesso grounding, stessa policy quote cap)

### Negative

- ❌ Footer attuale va sostituito in UI esistente (breaking visuale)
- ❌ Variant C effort speso diventa solo legacy (0 righe → zero valore prodotto perso, ma code to deprecate)
- ❌ Review gate è un collo di bottiglia scalabilità (1 admin review ≈ 5-15 min/gioco)
- ❌ T2 (rejection sampling letterale) può aumentare latency (retry) e cost (token ri-generati)

### Rischi residui mitigati ma non eliminati

- 🟡 **Takedown request da editore**: mitigazione via `is_suppressed` immediato; impossibile prevenire del tutto (serve diplomazia/partnership)
- 🟡 **Quote cap 25 parole** è uno standard editoriale conservativo, non una soglia legale hard; un giudice potrebbe valutare diversamente in dispute
- 🟡 **Player feedback come loop anti-hallucination**: dipende da volume utenti (cold start problem)
- 🟡 **Responsabilità upload utente** copre solo dal lato civile; se l'utente carica un PDF piratato consapevolmente, MeepleAI potrebbe essere co-obbligata a vigilanza (richiede ToS review legale)

### Follow-up obbligatori

- **ToS review** con consulenza legale IP entro M2 (prima del lancio card pubbliche login-gated)
- **Template takedown request** pubblicato su sito + email `takedown@meepleai.app`
- **Periodico audit** (ogni 3 mesi) di campione `mechanic_cards` per verificare compliance T1-T4

---

## Alternatives Considered

### A. Mantenere Variant C come unico path

**Pro**: Zero rischio IP aggiuntivo
**Contro**: Purpose 2 (comprehension demo con citation) impossibile senza AI-on-PDF. Purpose 1 (specchietto) limitato dalla velocità di input manuale umano. Effort di scrivere le note manualmente frena adozione.
**Reject**: non soddisfa i goal del spec panel 2026-04-23.

### B. AI-on-PDF senza vincoli di quote length o review gate

**Pro**: Massima libertà, meno complessità implementativa
**Contro**: Rischio IP alto (output può contenere copie letterali) + rischio reputazionale alto (hallucination pubblicate senza filtro).
**Reject**: asimmetria di rischio (1 errore = 10 win annullati) rende indifendibile.

### C. AI-on-PDF con guardrails tecnici + review gate ← **DECISIONE ADOTTATA**

**Pro**: Bilancia apertura e safety. Sblocca entrambi i purpose. Protezione a strati (prompt → post-check → review umano → feedback loop).
**Contro**: Più complessità (ma distribuita lungo il pipeline, non in un singolo layer).

### D. Outsource compliance a modelli open-source on-prem

**Pro**: Nessun terzo vede il PDF (DeepSeek/OpenRouter ricevono chunks)
**Contro**: Cost & latency molto peggiori. Non sposta la sostanza del rischio IP (è l'output pubblicato, non il processing, a generare esposizione).
**Reject**: trade-off non vale il complexity increase.

---

## References

- Spec Panel Mechanic Extractor 2026-04-23 (interno, conversazione Claude)
- ADR-043: LLM subsystem NFR (policy cost cap e fallback)
- ADR-024: Advanced PDF embedding pipeline (chunking + pgvector)
- [PR #517](https://github.com/meepleAi-app/meepleai-monorepo/pull/517) — Pattern idempotent migration
- Legge italiana 633/1941 art. 70 (diritto di citazione)
- 17 U.S.C. § 107 (Fair Use)
- [Feist Publications v. Rural Telephone Service (1991)](https://supreme.justia.com/cases/federal/us/499/340/) — fatti non sono copyrightable
- File attuale: `apps/api/src/Api/Routing/AdminMechanicExtractorEndpoints.cs`
- File attuale: `apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/review/page.tsx`

---

## Target Games per M1 Testing

| Gioco | SharedGameId | PDF status | Motivo |
|-------|--------------|-----------|--------|
| **Catan** | `6b76bdb3-424e-44bc-b02f-cb939ee9538b` | ✅ 1 PDF Ready | Gioco semplice (~20 pagine), noto globalmente, baseline |
| **Mage Knight Board Game** | `8a9f8a90-594a-4316-a32e-daa43a4356fb` | ⚠️ **0 PDF Ready** | Gioco complesso (~100 pagine), stress test prompt + chunking. **Richiede upload PDF prima di M1.2** |

**Gate M1**: per procedere al test su Mage Knight, occorre prima caricare un PDF Ready del manuale (responsabilità operatore, parte del setup M1.2).

---

## Addendum 2026-04-26 — Sprint 2: rilassamento dell'invariante di atomicità "abort = scarto totale"

**Status**: Accepted
**Driver**: T23/T24 calibration spike (Carcassonne + 7 Wonders, 2026-04-26)
**Implementing commits**: `42bf5b21f` (P1' partial-recovery checkpoint), `2969c773f` (P0 LlmCostCalculator alias)
**Sprint plan**: `docs/superpowers/plans/2026-04-25-mechanic-extractor-ai-validation-sprint-2.md`

### Contesto

Il design originale dell'esecutore della pipeline M1.2 (`MechanicAnalysisExecutor`) trattava ogni run come **all-or-nothing**:
- Se tutte e 6 le sezioni (Summary, Mechanics, Victory, Resources, Phases, Faq) producevano output validato, l'analisi transitava a `Draft` con i claim aggregati e poi a `InReview` via `SubmitForReview`.
- Se *una qualunque* sezione abortiva (cost cap superato, hard-failure LLM dopo fallback provider, validator reject oltre retry), `ApplyAbort` invocava direttamente `MechanicAnalysis.AutoRejectFromDraft(reason, …)` **scartando tutti i claim parsati dalle sezioni precedenti**.

Il calibration spike T23 (vedi `docs/research/mechanic-validation-calibration-spike-2026-04-26.md`) ha mostrato due problemi operativi causati da questa rigidità:

1. **Costo sprecato**: in T23 (Carcassonne) la sezione `Faq` ha sforato il cost cap dopo che 5 sezioni avevano già parsato correttamente — ~80k token già spesi sono stati buttati assieme ai ~30 claim validi.
2. **Triage impossibile**: l'admin riceve un'analisi `Rejected` con `RejectionReason="cost_cap_exceeded"` ma **nessun claim**, quindi non può valutare *cosa* è effettivamente sopravvissuto né decidere se rilanciare la pipeline (rischio: ri-pagare i token già spesi su sezioni già pronte) o accettare la salvezza parziale.

### Decisione

**Rilassare l'invariante di atomicità**: un abort della pipeline non scarta più automaticamente i claim parsati dalle sezioni precedenti. Se almeno un claim sopravvive al parser difensivo (`MechanicOutputParser.Parse`), l'analisi viene **checkpoint-ata** in un nuovo stato `PartiallyExtracted` invece di essere rigettata.

#### Nuovo stato

| Status | Valore | Origine | Significato |
|--------|--------|---------|-------------|
| `Draft` | 0 | Esistente | Pipeline completata con successo, in attesa di `SubmitForReview` |
| `InReview` | 1 | Esistente | Admin sta revisionando i claim |
| `Published` | 2 | Esistente | Tutti i claim approvati, card pubbliche |
| `Rejected` | 3 | Esistente | Pipeline abortita **e** zero claim salvabili, oppure admin ha rejectato dopo review |
| **`PartiallyExtracted`** | **4** | **Sprint 2** | **Pipeline abortita ma ≥ 1 claim parsato dalle sezioni successe — admin deve triage** |

`Suppressed` rimane ortogonale (`IsSuppressed: bool`), non è uno status.

#### Nuove transizioni di stato

Aggiunte (delta vs ADR-051 originale):

```
Draft (0) → PartiallyExtracted (4)     : MarkAsPartiallyExtracted (system, abort con salvage)
PartiallyExtracted (4) → InReview (1)  : SubmitForReview (admin promuove la salvezza al review flow)
PartiallyExtracted (4) → Rejected (3)  : Reject (admin scarta la salvezza — equivalente al pre-Sprint-2)
```

`SubmitForReview` da `PartiallyExtracted` cancella il `RejectionReason` (era un abort marker, non un giudizio umano) — i claim salvati erano già `Pending`, mai revisionati.

#### Invariante T23 preservata

Il telemetry di costo (`TotalTokensUsed`, `EstimatedCostUsd`) viene **comunque** registrato via `MechanicAnalysis.RecordUsage(...)` **prima** della transizione terminale, sia nel ramo "salvage" sia nel ramo "full reject". Questo garantisce che il fix P0 di `LlmCostCalculator` (alias `deepseek-chat` → pricing OpenRouter) non venga vanificato da un percorso che salta la registrazione.

#### Effetto sui vincoli T1-T8

Nessuno dei vincoli tecnici originari (T1 quote cap, T2 anti-letterale, T3 citation obbligatoria, T4 page reference, T5 kill switch, T6 audit log, T7 prompt versioning, T8 cost cap) cambia semantica:
- I claim sopravvissuti sono passati comunque attraverso il parser difensivo, che applica T1/T3/T4 prima dell'`AddClaim`.
- T2 e T8 sono enforced **dentro** la pipeline (pre-aggregate); il checkpoint avviene **dopo** l'enforcement, quindi i claim salvati hanno già superato i guardrail.
- T6 emette un `MechanicAnalysisStatusChangedEvent(Draft → PartiallyExtracted)` consumato dal `MediatorSaveChangesInterceptor` esattamente come per le altre transizioni.

### Conseguenze

#### Positive

- ✅ **Costo non sprecato**: i token già spesi su sezioni success producono valore tangibile (claim triagibili) anche in caso di abort tardivo.
- ✅ **Triage informato**: l'admin vede `Status=PartiallyExtracted`, `RejectionReason=cost_cap_exceeded` (o equivalente), `Claims.Count > 0` e decide consapevolmente: promuovere a review (`SubmitForReview`), scartare (`Reject`), o lanciare un nuovo run completo.
- ✅ **Audit trail invariato**: ogni transizione produce sempre un `MechanicAnalysisStatusChangedEvent`; nessun gap nell'audit log.
- ✅ **Backwards compatibility totale**: nessuna riga esistente `mechanic_analyses` cambia stato (la tabella ha 0 righe in produzione, ma anche se non lo fosse, lo schema `int` accetta il nuovo valore senza migration).

#### Negative

- ❌ **Stato in più da gestire in UI**: `PartiallyExtracted` richiede un'icona/colore distinto e un'azione "Promote to Review" oltre a "Reject". *Mitigazione*: copertura nel sprint UI M1.2 (PR #547).
- ❌ **Possibile confusione in metriche**: dashboard "% analisi pubblicate" deve distinguere `PartiallyExtracted` come una categoria intermedia, non confonderla con `Rejected`. *Mitigazione*: aggiornare le query delle metriche operative.
- ❌ **Cost cap "soft"**: un admin che promuove una `PartiallyExtracted` ottiene 5/6 sezioni ad un costo ≤ cap; se poi rilancia la pipeline per recuperare la sezione mancante, il costo totale supera il cap originale. *Mitigazione*: documentare esplicitamente che il cap è **per-run**, non **per-analisi-end-to-end**, e che la responsabilità di non rilanciare è dell'admin.

#### Rischi residui

- 🟡 **Aspettativa "tutto-o-niente"**: alcuni admin potrebbero leggere "abort" come segnale di "buttare via tutto" e rejectare meccanicamente le `PartiallyExtracted` senza valutare. *Mitigazione*: copy UI esplicito ("X sezioni hanno prodotto Y claim validi prima dell'abort — valuta se promuovere o scartare").

### Alternative considerate

#### A. Mantenere atomicità totale (status quo pre-Sprint-2)

**Pro**: Modello mentale semplice, nessuno stato nuovo.
**Contro**: Spreca i token già pagati e non offre triage.
**Reject**: il cost overhead documentato in T23 è sufficiente da solo a giustificare il refactor.

#### B. Salvare i claim ma mantenere `Status=Draft` (no nuovo status)

**Pro**: Riduce stati.
**Contro**: `Draft` significa "completata, in attesa di review umano del *full set*"; mischiarci dentro analisi parziali offusca il significato. L'admin non distingue più "ready to review" da "abort recovery".
**Reject**: confusione semantica > risparmio di un enum value.

#### C. Salvare i claim e auto-rilanciare le sezioni mancanti

**Pro**: Massima resilienza, no admin-in-the-loop.
**Contro**: Un cost cap exceeded *deve* fermare la spesa — un retry automatico viola la garanzia di cost ceiling. Inoltre, una validation failure dopo retry cap non ha motivo di succedere al secondo tentativo immediato.
**Reject**: viola l'intent del cost cap (T8) e non risolve i fallimenti hard-LLM.

### Codice di riferimento

- `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Enums/MechanicAnalysisStatus.cs:31` — enum value
- `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/MechanicAnalysis.cs` — `MarkAsPartiallyExtracted`, `ValidTransitions`, estensione `SubmitForReview`
- `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/MechanicExtractor/MechanicAnalysisExecutor.cs:263` — `ApplyAbort` con recovery summary
- Test domain: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/Aggregates/MechanicAnalysisTests.cs` (7 nuovi test)
- Test executor: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Services/MechanicExtractor/MechanicAnalysisExecutorApplyAbortTests.cs` (6 nuovi test)
- Suite Mechanic unit: 277 test, tutti passanti.
