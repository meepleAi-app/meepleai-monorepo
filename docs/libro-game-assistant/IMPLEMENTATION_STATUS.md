# 📊 Implementation Status — Libro Game AI Assistant MVP Phase 1

> **Sprint corrente**: Sprint 0 — Foundation code-only (avviato 2026-05-04)
> **Plan riferimento**: `docs/superpowers/plans/2026-05-04-libro-game-assistant-mvp-phase1-v2.md`

## Legenda status

- ⚪ **Not started** — non iniziato
- 🟡 **In progress** — in lavorazione
- 🔵 **Blocked** — blocked su human/external dependency (vedi BLOCKERS.md)
- ✅ **Done** — task completato + PR mergiato
- ⏸️ **Deferred** — fuori scope sprint corrente

## Sprint 0 — Foundation code-only (weeks 1-2)

### Phase 0 (code subset)

| Task | Status | Issue | PR | Note |
|------|--------|-------|-----|------|
| 0.4 step 2-7 — Bootstrap script + observability + backup configs | ⚪ Not started | – | – | File artifacts only, NO actual provisioning |
| 0.7 — SharedGameCatalog integration verification | ⚪ Not started | – | – | Read-only audit, output report |
| 0.1 step 3 — OCR validation Python script | ⚪ Not started | – | – | Artifact prepared, run blocked su manuali Aaron |
| 0.2 step 2-3 — Golden set JSONL schema + CSV-to-JSONL converter | ⚪ Not started | – | – | Schema + utility, data creation contractor-blocked |

### Phase 1 (backend up to smoldocling endpoint)

| Task | Status | Issue | PR | Note |
|------|--------|-------|-----|------|
| 1.1 — PhotoBatchUpload aggregate + VOs (PageOrientation, ConfidenceLevel) + events | ⚪ Not started | – | – | TDD: domain logic first |
| 1.2 — IPhotoBatchUploadRepository + EF migration | ⚪ Not started | – | – | Schema: `document_processing` |
| 1.3 — UploadPhotoBatchCommand + FluentValidation Validator | ⚪ Not started | – | – | – |
| 1.4a — IPhotoPreprocessor interface + smoldocling `/preprocess` endpoint (Python) | ⚪ Not started | – | – | smoldocling-service extension |

### Acceptance Sprint 0

- [ ] Tutti i task code Sprint 0 mergiati su `feature/libro-game-mvp-phase1`
- [ ] Test pass (unit + integration)
- [ ] Pattern compliance verificata (audit checklist plan v2)
- [ ] OCR validation script eseguibile (anche se non runnable senza manuali)

## Sprint 1 — Risk gate (week 3, blocked Aaron procurement)

| Task | Status | Issue | PR | Blocker |
|------|--------|-------|-----|---------|
| 0.1 step 1 — Procurare 5 manuali gamebook reali | 🔵 Blocked | – | – | Aaron procurement (€50-300, 1-3 weeks shipping) |
| 0.1 step 2 — Fotografare 150 foto in 3 condizioni | 🔵 Blocked | – | – | Manuali fisici |
| 0.1 step 4 — Verify Task 1.4 deployed in dev | ⚪ Not started | – | – | Dipende da 1.4a Sprint 0 |
| 0.1 step 5 — Run validation script | 🔵 Blocked | – | – | Foto manuali |
| 0.1 step 6 — Decision matrix gate | 🔵 Blocked | – | – | Validation results |
| 0.1 step 7 — Document findings + commit | 🔵 Blocked | – | – | Decision matrix |

### 🚦 Hard gate criteria (Sprint 1 exit)

- ✅ **PASS** se: 3+ giochi con `avg good-light ≥ 0.85` AND `avg angled ≥ 0.7` AND `high_conf_pct(good) ≥ 90%`
- ⚠️ **MARGINAL** se: 2+ giochi `avg good-light 0.7-0.85` → procedi con UI confidence indicators stronger (Task 1.6)
- 🔴 **FAIL** se: 1+ giochi `avg good-light < 0.7` OR > 30% pages low-conf in good-light → STOP, scope review

## Sprint 2-3 — Phase 1 completa (weeks 4-7, post gate)

| Task | Status | Issue | PR | Dipende da |
|------|--------|-------|-----|------------|
| 1.4b — SmoldoclingPhotoPreprocessor HTTP client implementation | ⚪ Not started | – | – | 1.4a, OCR PASS |
| 1.5 — UploadPhotoBatchCommandHandler | ⚪ Not started | – | – | 1.1, 1.2, 1.3, 1.4b |
| 1.6 — IPhotoBatchProcessor (parallel processing service) | ⚪ Not started | – | – | 1.4b |
| 1.7 — GetPhotoBatchStatusQuery + handler + endpoint | ⚪ Not started | – | – | 1.2 |
| 1.8 — Frontend upload page (using existing HttpClient + Zod) | ⚪ Not started | – | – | 1.3, 1.7 |
| 1.9 — Phase 1 acceptance gate (E2E test full flow) | ⚪ Not started | – | – | 1.1-1.8 |

## Phase 2 — G3 Q&A + TranslationService (weeks 8-13)

> Task list compressed in plan v2 §"Phases 2, 3, 4 — Reference". Steps dettagliati da rielaborare runtime.

| Task | Status | Note |
|------|--------|------|
| 2.1 — TranslationService skeleton + OpenRouter integration | ⏸️ Deferred Sprint 4+ | Richiede expansion da plan v1 reference |
| 2.2 — TranslationCache Redis | ⏸️ Deferred Sprint 4+ | – |
| 2.3 — KB Q&A extension (modify AskQuestionQueryHandler) | ⏸️ Deferred Sprint 4+ | – |
| 2.4 — IQAComplexityClassifier + heuristic impl | ⏸️ Deferred Sprint 4+ | – |
| 2.5 — HouseRule entity + repo + matcher + endpoints | ⏸️ Deferred Sprint 4+ | – |
| 2.6 — GameGlossaryEntry entity + auto-bootstrap NER | ⏸️ Deferred Sprint 4+ | – |
| 2.7 — Hallucination CI gate (LLM-as-judge GPT-4 vs Claude) | ⏸️ Deferred Sprint 4+ | – |
| 2.8 — docker-compose.test.yml E2E env | ⏸️ Deferred Sprint 4+ | – |
| 2.9 — Phase 2 acceptance gate | ⏸️ Deferred Sprint 4+ | – |

## Phase 3 — G4 + UI + Pricing (weeks 14-19)

| Task | Status | Note |
|------|--------|------|
| 3.1 — GetParagraphQuery (numbered + semantic fallback) | ⏸️ Deferred Sprint 5+ | – |
| 3.2 — IPricingEngine + CreditBasedPricingEngine + UserQuota | ⏸️ Deferred Sprint 5+ | – |
| 3.3a-c — Stripe integration (checkout + webhook + endpoints) | ⏸️ Deferred Sprint 5+ | – |
| 3.4 — Quartz MonthlyQuotaResetJob | ⏸️ Deferred Sprint 5+ | – |
| 3.5a-f — Frontend gameplay screen split | ⏸️ Deferred Sprint 5+ | – |
| 3.6 — IUserRateLimiter middleware | ⏸️ Deferred Sprint 5+ | – |
| 3.7 — Privacy policy UI | ⏸️ Deferred Sprint 5+ | – |
| 3.8 — GDPR DeleteUserDataCommand + ExportUserDataQuery | ⏸️ Deferred Sprint 5+ | – |
| 3.9 — Phase 3 acceptance E2E + payment | ⏸️ Deferred Sprint 5+ | – |

## Phase 4 — Launch prep (weeks 20-25)

| Task | Status | Note |
|------|--------|------|
| 4.1a-d — Chaos engineering tests | ⏸️ Deferred Sprint 6+ | – |
| 4.2 — Usability testing 5 sessions | ⏸️ Deferred Sprint 6+ | UX researcher, $250 budget |
| 4.3 — DR drill (restore from backup, RTO < 2h) | ⏸️ Deferred Sprint 6+ | – |
| 4.4 — Cost telemetry dashboard final review | ⏸️ Deferred Sprint 6+ | – |
| 4.5 — Final launch checklist (8 items vision §6.5) | ⏸️ Deferred Sprint 6+ | – |
| 4.6 — Production deploy + monitoring 1 week | ⏸️ Deferred Sprint 6+ | – |

---

## Pre-launch acceptance criteria (vision §6.3)

- [ ] 5 sessioni reali end-to-end completate con gruppi target
- [ ] ≥ 70% utenti completano almeno 1 sessione di 2h+
- [ ] Costo medio sessione ≤ €3.00
- [ ] Hallucination rate Q&A ≤ 3% su test set golden (PR-3)
- [ ] OCR validation su 5 manuali ≥ 85% pages confidence accettabile (PR-2)
- [ ] Legal review copyright completata + TOS aggiornato (PR-1)
- [ ] Latenza P95 Q&A < 5 sec, traduzione < 5 sec
- [ ] Pricing engine 2-tier funzionante con cap free 50 pag/mese

## Pre-launch prerequisites (vision §6.5)

- [ ] PR-1: Legal review + TOS + privacy policy GDPR-compliant
- [ ] PR-2: OCR validated su ≥ 5 manuali gamebook reali
- [ ] PR-3: Test set golden 100 Q&A + 50 paragrafi
- [ ] CAX31 deployment + monitoring + alerting + backup automated
- [ ] Pricing engine end-to-end tested (Free counter + Credits checkout + Stripe)
- [ ] Hallucination rate ≤ 3% in CI gate
- [ ] 5 sessioni usability testing completate
- [ ] DR drill eseguito (restore < 2h)

---

**Ultima modifica**: 2026-05-04 (kickoff Sprint 0)
