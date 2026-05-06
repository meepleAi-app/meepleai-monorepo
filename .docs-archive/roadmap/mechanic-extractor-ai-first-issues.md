# Mechanic Extractor — AI-First Pipeline: Issue Decomposition

**Parent ADR**: [ADR-051](../architecture/adr/adr-051-mechanic-extractor-ip-policy.md)
**Date**: 2026-04-23
**Epic tracking issue**: meepleAi-app/meepleai-monorepo#539
**Target Games M1**: Catan (simple, `6b76bdb3-424e-44bc-b02f-cb939ee9538b`), Mage Knight Board Game (complex, `8a9f8a90-594a-4316-a32e-daa43a4356fb`)

## Issue map (created on GitHub)

| ID | Issue | URL |
|----|-------|-----|
| ME-M1.1 | Schema + migration | #523 |
| ME-M1.2 | Command + prompt + DeepSeek | #524 |
| ME-M1.3 | Guardrails | #525 |
| ME-M1.4 | Admin review UI | #526 |
| ME-M1.5 | Publish action | #527 |
| ME-M1.6 | Public card page | #528 |
| ME-M1.7 | Footer + takedown | #529 |
| ME-M2.1 | Inline citation UX | #530 |
| ME-M2.2 | Landing demo | #531 |
| ME-M2.3 | Admin metrics | #532 |
| ME-M3.1 | Player feedback | #533 |
| ME-M3.2 | Auto-suppression | #534 |
| ME-M3.3 | Admin notify | #535 |
| ME-M4.1 | Deprecation banner | #536 |
| ME-M4.2 | Hide legacy menu | #537 |
| ME-M4.3 | Code removal | #538 |
| EPIC | Tracking | #539 |

## Milestone Overview

| Milestone | Focus | # Issues | Gate |
|-----------|-------|----------|------|
| **M1** | Core pipeline (schema → extraction → review → publish → public card) | 7 | Catan card pubblicata + Mage Knight card pubblicata |
| **M2** | Citation UX + trust chain | 3 | Citation UI live, admin metrics dashboard |
| **M3** | Feedback loop | 3 | Auto-suppression funzionante su flag utenti |
| **M4** | Variant C deprecation | 3 | Banner + redirect; rimozione codice tracked separatamente |

---

## M1 — Core Pipeline

### M1.1 — Schema: `mechanic_analyses`, `mechanic_cards`, `mechanic_card_feedback` + migrazione idempotente

**Labels**: `area:backend`, `bc:shared-game-catalog`, `type:schema`, `milestone:mechanic-extractor-m1`

**Contesto**: ADR-051 richiede 3 nuove tabelle in `SharedGameCatalog`. Pattern migrazione idempotente come PR #517.

**AC**:
- [ ] Entity classes + EF configurations in `BoundedContexts/SharedGameCatalog/Domain` + `Infrastructure`
- [ ] Tabella `mechanic_analyses`: `id, shared_game_id, pdf_document_id, model_used, prompt_version, generated_at, sections jsonb, citations jsonb, cost_usd, tokens_used, status, reviewed_by, reviewed_at, published_card_id, row_version`
- [ ] Tabella `mechanic_cards`: `id, shared_game_id, origin_analysis_id, origin, content jsonb, version, published_at, published_by, is_suppressed, suppressed_reason, error_reports_count, feedback_score, row_version`
- [ ] Tabella `mechanic_card_feedback`: `id, card_id, user_id, rating, error_claim_id, comment, created_at`
- [ ] UNIQUE `(shared_game_id, pdf_document_id, prompt_version)` su `mechanic_analyses` (ADR T7)
- [ ] Partial index `WHERE is_suppressed = false` su `mechanic_cards` (ADR T5)
- [ ] Migrazione idempotente: raw SQL con `IF NOT EXISTS` / `DO $$ IF EXISTS` come in PR #517
- [ ] FK con `ON DELETE CASCADE` verso `shared_games`
- [ ] Test: migration applicabile 2x senza errori

**Dependencies**: none

---

### M1.2 — Command `GenerateMechanicAnalysisCommand` + prompt v1 + integrazione DeepSeek

**Labels**: `area:backend`, `bc:shared-game-catalog`, `type:feature`, `milestone:mechanic-extractor-m1`, `ai`

**Contesto**: Endpoint admin che prende `(sharedGameId, pdfDocumentId)`, estrae chunks dal PDF, invia al LLM con schema JSON stretto, salva draft in `mechanic_analyses` status=`pending_review`.

**AC**:
- [ ] `POST /api/v1/admin/mechanic-analyses` (admin-only) con body `{ sharedGameId, pdfDocumentId, promptVersion? }`
- [ ] `GenerateMechanicAnalysisCommand` + `Handler` + `Validator` (FluentValidation)
- [ ] Retrieval chunks da `pdf_documents` via pgvector (riuso `IRetrievalService` esistente)
- [ ] Prompt template v1 committato in `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/Prompts/MechanicAnalysisPromptV1.md`
- [ ] JSON schema enforcement: claim/citation structure con `pdf_page` + `quote` (ADR T3)
- [ ] LLM: DeepSeek primary, OpenRouter fallback (riuso `ILlmRoutingService`)
- [ ] Pre-flight cost estimate + hard cap default €2.00 (ADR T8)
- [ ] Salvataggio: `status='pending_review'`, token/cost tracked
- [ ] Test integration: estrazione end-to-end su Catan PDF fixture

**Dependencies**: M1.1

---

### M1.3 — Guardrails: quote length, rejection sampling, citation verification, cost cap

**Labels**: `area:backend`, `bc:shared-game-catalog`, `type:feature`, `milestone:mechanic-extractor-m1`, `compliance:ip`

**Contesto**: Vincoli tecnici T1-T4 dell'ADR-051. Post-processing dell'output LLM prima del salvataggio. Reject + re-prompt su violazione.

**AC**:
- [ ] Validator T1: ogni `citation.quote` ≤ 25 parole (conteggio word-tokenizer) → reject + re-prompt (max 2 retry)
- [ ] Validator T2: rejection sampling letterale — se >10 parole consecutive matchano il chunk sorgente → reject + re-prompt
- [ ] Validator T3: ogni `claim` deve avere almeno 1 `citation` con `pdf_page` valido
- [ ] Validator T4: `citation.pdf_page` deve esistere nel PDF; `quote` deve essere substring del chunk associato
- [ ] Validator T8: stop se cost estimate > cap (override admin solo con reason)
- [ ] Logging strutturato: ogni rejection loggata con motivo + retry count per audit
- [ ] Test unitari per ogni validator
- [ ] Test integration: prompt intenzionalmente "buggy" viene bloccato

**Dependencies**: M1.2

---

### M1.4 — Admin Review UI: per-claim approve/reject

**Labels**: `area:frontend`, `type:feature`, `milestone:mechanic-extractor-m1`, `admin`

**Contesto**: Sostituisce l'attuale review read-only. Admin vede ogni claim con citation highlight, può approvare/rifiutare per-claim, aggiungere note.

**AC**:
- [ ] Refactor `/admin/knowledge-base/mechanic-extractor/review` per consumare `mechanic_analyses` (non più `mechanic_drafts`)
- [ ] Route: `/admin/knowledge-base/mechanic-analyses/[analysisId]/review`
- [ ] Per ogni claim: checkbox approve/reject + campo note
- [ ] Citation viewer: apre PDF viewer alla `pdf_page` evidenziando `quote`
- [ ] Bulk actions: "Approve all", "Reject all con quote >20 parole"
- [ ] Indicatori guardrail: badge verdi/rossi sui claim passati/falliti T1-T4
- [ ] Submit → `POST /api/v1/admin/mechanic-analyses/{id}/review` con `{ claimApprovals: [...], reviewNotes }`
- [ ] Status transition: `pending_review` → `approved` o `rejected`
- [ ] Footer attribution aggiornato (rimossa dicitura Variant C, nuova da ADR-051)
- [ ] Test E2E Playwright: review flow admin

**Dependencies**: M1.2

---

### M1.5 — Publication action: promote approved analysis → `mechanic_card`

**Labels**: `area:backend`, `bc:shared-game-catalog`, `type:feature`, `milestone:mechanic-extractor-m1`

**Contesto**: Dopo review approvata, admin pubblica come `mechanic_card` visibile agli utenti loggati.

**AC**:
- [ ] `POST /api/v1/admin/mechanic-analyses/{id}/publish` con body `{ title? }`
- [ ] `PublishMechanicCardCommand` + `Handler` + `Validator`
- [ ] Solo analisi con `status='approved'` possono essere pubblicate
- [ ] Crea riga `mechanic_cards` con `origin='ai_reviewed'`, `version=1`
- [ ] Update `mechanic_analyses.published_card_id`
- [ ] Audit log: `mechanic_card_audit_log(card_id, action='published', actor, timestamp, metadata)`
- [ ] Riutilizza `SharedGameId` per unicità (una sola card attiva `WHERE !is_suppressed` per gioco)
- [ ] Test integration: publish flow + verifica card visibile da endpoint player

**Dependencies**: M1.4

---

### M1.6 — Public card page su `/games/{slug}/card` (login-gated)

**Labels**: `area:frontend`, `type:feature`, `milestone:mechanic-extractor-m1`, `user-facing`

**Contesto**: Specchietto pubblico dimostra comprensione AI. Login-gated in M1; rilassare ad alpha/public in fase successiva.

**AC**:
- [ ] Route: `/games/[slug]/card` — render `mechanic_card.content`
- [ ] Middleware: redirect a login se non autenticato
- [ ] Layout mobile-first, print-friendly (per player al tavolo)
- [ ] Sezioni: Summary, Mechanics, Victory, Resources, Phases, FAQ (come review UI ma read-only)
- [ ] Inline citations: ogni claim ha badge `[p.N]` cliccabile → opens modal/sidebar con `quote` + link al PDF
- [ ] Footer attribution ADR-051 (editore + MeepleAI)
- [ ] 404 se `is_suppressed=true` o nessuna card pubblicata
- [ ] Badge "AI-generated, human-reviewed" con link a "come funziona" (landing M2.2)
- [ ] Test E2E: apri card Catan, verifica citations

**Dependencies**: M1.5

---

### M1.7 — Refactor footer Variant C + pagina takedown policy

**Labels**: `area:frontend`, `type:chore`, `milestone:mechanic-extractor-m1`, `compliance:ip`

**Contesto**: Footer attuale dichiara "AI non ha mai letto il testo" — tecnicamente falso con nuova pipeline. ADR-051 definisce nuovo testo.

**AC**:
- [ ] Rimosso testo Variant C in `review/page.tsx:262-265`
- [ ] Nuovo footer (da ADR-051): *"Analisi elaborata dall'AI sul manuale del gioco. Ogni affermazione è riformulata in parole originali e cita la pagina del regolamento. Copyright © degli editori per il testo originale del manuale."*
- [ ] Pagina `/legal/takedown` pubblica con template form
- [ ] Email alias `takedown@meepleai.app` setup (infra task, tracked separatamente)
- [ ] Link a `/legal/takedown` da footer card pubblica (M1.6)
- [ ] Documento `docs/legal/takedown-policy.md` con SLA response + processo interno

**Dependencies**: M1.6 (per link dalla card)

---

## M2 — Trust Chain

### M2.1 — Inline citation UI su public card

**Labels**: `area:frontend`, `type:enhancement`, `milestone:mechanic-extractor-m2`

**Contesto**: M1.6 implementa citation base. M2 migliora UX con preview inline, highlight pagina PDF, copy-as-quote.

**AC**:
- [ ] Hover su badge `[p.N]` mostra tooltip con `quote` (primi 60 char)
- [ ] Click apre side-panel con PDF viewer pre-scrollato alla pagina + quote evidenziata
- [ ] Azione "Copia citazione" → clipboard con format APA-style
- [ ] A11y: focus trap, ESC chiude panel, keyboard nav claim↔citation
- [ ] Performance: lazy-load PDF.js solo on-demand

**Dependencies**: M1.6

---

### M2.2 — Landing "Come funziona" con demo Catan

**Labels**: `area:frontend`, `type:feature`, `milestone:mechanic-extractor-m2`, `marketing`

**Contesto**: Asset pubblico per dimostrare comprensione AI + trust chain. Catan come esempio hero (scelto ADR).

**AC**:
- [ ] Route pubblica `/how-it-works/game-comprehension`
- [ ] Sezione "Trust chain" in 4 step: PDF → AI reads → Review admin → Card con citations
- [ ] Embed live della card Catan con citation interattiva
- [ ] CTA: "Prova con il tuo gioco" → login
- [ ] SEO: OG tags, structured data `LearningResource`
- [ ] A11y: WCAG AA

**Dependencies**: M1.6 (Catan pubblicato)

---

### M2.3 — Admin metrics dashboard: cost, review time, approval rate

**Labels**: `area:frontend`, `area:backend`, `type:feature`, `milestone:mechanic-extractor-m2`, `admin`

**Contesto**: Strumento operativo per monitorare qualità + costi pipeline AI.

**AC**:
- [ ] Route `/admin/mechanic-extractor/metrics`
- [ ] KPI: avg cost/analisi, avg review time, approval rate, rejection reasons breakdown
- [ ] Time series: costo giornaliero (7/30/90 giorni)
- [ ] Tabella: analisi recenti con status + reviewer + cost
- [ ] Filtri: per gioco, per reviewer, per status
- [ ] Export CSV

**Dependencies**: M1.5

---

## M3 — Feedback Loop

### M3.1 — Player feedback UI: 👍/👎 + error report

**Labels**: `area:frontend`, `type:feature`, `milestone:mechanic-extractor-m3`, `user-facing`

**Contesto**: Utenti loggati possono segnalare errori su singoli claim. Feedback in `mechanic_card_feedback`.

**AC**:
- [ ] Per ogni claim nella card: pulsante 👍/👎
- [ ] 👎 apre modal "Segnala errore" con: tipo (fattuale/ambiguo/contraddice regola), descrizione, citazione alla regola corretta (opzionale)
- [ ] `POST /api/v1/mechanic-cards/{id}/feedback`
- [ ] Rate limit: max 10 feedback/user/day
- [ ] Idempotenza: un user può cambiare feedback ma non duplicare
- [ ] UI conferma submit + "grazie" state

**Dependencies**: M1.6

---

### M3.2 — Auto-suppression logic + re-processing queue

**Labels**: `area:backend`, `bc:shared-game-catalog`, `type:feature`, `milestone:mechanic-extractor-m3`

**Contesto**: Quando N error reports su stessa card superano soglia, auto-suppress + enqueue rigenerazione.

**AC**:
- [ ] Background job (Hangfire/cron) evalua feedback counts
- [ ] Soglia default: `error_reports_count >= 5` AND `feedback_score < 0.5` → auto-suppress
- [ ] `is_suppressed=true, suppressed_reason='auto_feedback'`
- [ ] Enqueue nuova analisi con `promptVersion=current+1` (se disponibile), o alert admin per review manuale
- [ ] Audit log su suppression
- [ ] Config soglie via `SystemConfiguration` BC (admin tunable)

**Dependencies**: M3.1

---

### M3.3 — Admin notification on suppression event

**Labels**: `area:backend`, `bc:user-notifications`, `type:feature`, `milestone:mechanic-extractor-m3`

**Contesto**: Admin deve sapere immediatamente quando una card viene soppressa (auto o manuale).

**AC**:
- [ ] Event `MechanicCardSuppressedEvent` pubblicato su suppression
- [ ] Handler in `UserNotifications` BC crea notifica per tutti admin
- [ ] Email opzionale (se admin ha `notify_on_card_suppression=true`)
- [ ] Notifica include: gioco, motivo, link a metrics + re-process queue

**Dependencies**: M3.2

---

## M4 — Variant C Deprecation

### M4.1 — Deprecation banner + redirect sull'editor Variant C

**Labels**: `area:frontend`, `type:chore`, `milestone:mechanic-extractor-m4`

**AC**:
- [ ] Banner giallo in alto su `/admin/knowledge-base/mechanic-extractor` (editor): "Questo flow è deprecato, usa il nuovo [AI Analysis]"
- [ ] CTA primario → `/admin/knowledge-base/mechanic-analyses/new`
- [ ] Banner persiste 6 mesi pre-rimozione

**Dependencies**: M1.2

---

### M4.2 — Hide legacy menu items

**Labels**: `area:frontend`, `type:chore`, `milestone:mechanic-extractor-m4`

**AC**:
- [ ] Sidebar admin: "Mechanic Extractor (legacy)" dietro feature flag `show_legacy_mechanic_extractor=false` (default)
- [ ] Env override per staging/debug

**Dependencies**: M4.1

---

### M4.3 — Code removal Variant C (tracked separately, +6 months)

**Labels**: `area:backend`, `area:frontend`, `type:chore`, `milestone:mechanic-extractor-m4`, `deferred`

**Contesto**: Issue placeholder per sunset code. NON chiudere fino a rimozione effettiva.

**AC**:
- [ ] Gate: >= 6 mesi da M4.1 merge
- [ ] Rimossi: `MechanicExtractor` commands/queries legacy, `mechanic_drafts` table, route editor, componenti frontend Variant C
- [ ] Migration drop table `mechanic_drafts` (idempotente)
- [ ] Smoke test: nessun link rotto, no 500 su admin nav

**Dependencies**: M4.2

---

## Ordine Esecuzione M1 (critical path)

```
M1.1 (schema) → M1.2 (command/prompt) → M1.3 (guardrails) → M1.4 (review UI)
                                                               ↓
                                    M1.7 (footer+takedown) ← M1.6 (public card) ← M1.5 (publish)
```

**Parallelizable**: M1.4 + M1.5 in parallelo (UI ≠ command backend); M1.7 dopo M1.6.

## Gate criteria per M1 complete

1. ✅ Catan card pubblicata su `/games/catan/card` (login-gated)
2. ✅ Mage Knight PDF caricato su staging (prerequisito operatore, parte M1.2)
3. ✅ Mage Knight card pubblicata su `/games/mage-knight/card`
4. ✅ 0 citazioni con quote >25 parole (grep su `mechanic_cards.content`)
5. ✅ Footer attribution nuovo live
6. ✅ Takedown policy page pubblicata

## Prerequisiti operativi (NON issue tecnica)

- [ ] Upload PDF Mage Knight su staging (~100 pagine, ~15MB) — bloccante per M1.2 test
- [ ] Conferma copia legittima per testing (Catan già OK)
- [ ] ToS review con consulenza IP entro M2 (separato da questo roadmap)
- [ ] Setup email `takedown@meepleai.app` (separato, M1.7)
