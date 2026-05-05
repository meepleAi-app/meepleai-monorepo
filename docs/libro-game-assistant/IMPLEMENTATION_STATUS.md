# 📊 Implementation Status — Libro Game AI Assistant MVP Phase 1

> **Sprint corrente**: Sprint 0 ✅ COMPLETO (2026-05-04) — pending PR review + Aaron procurement
> **Plan riferimento**: `docs/superpowers/plans/2026-05-04-libro-game-assistant-mvp-phase1-v2.md`
> **Branch**: `feature/libro-game-mvp-phase1` (parent: `main-dev`)

## Legenda status

- ⚪ **Not started** — non iniziato
- 🟡 **In progress** — in lavorazione
- 🔵 **Blocked** — blocked su human/external dependency (vedi BLOCKERS.md)
- ✅ **Done** — task completato (commit su feature branch); PR/merge separato
- ⏸️ **Deferred** — fuori scope sprint corrente

## Sprint 0 — Foundation code-only ✅ COMPLETO

**Commit history** (12 commit, 51 files, 19068 insertions):

| Commit | Task | Description |
|--------|------|-------------|
| `2cac3bf5a` | Kickoff | Feature tracking docs + prompt cleanup |
| `1506e25e2` | 0.7 | SharedGameCatalog audit doc |
| `9c962b43b` | 0.7 fix | Scope correction + new game flow + drift docs |
| `2cb547ae5` | 0.4 | Hetzner CAX31 + observability stack |
| `7aa52cce0` | 0.4 fix | Secrets block + age preflight + volume paths + provisioner |
| `0edb90ae8` | 0.1 step 3 | OCR validation Python script |
| `3ec3262ae` | 0.1 fix | JSON decode robustness + gitignore |
| `3667401ad` | 0.2 step 2-3 | Golden test set schemas + CSV-to-JSONL |
| `90cf5bd98` | 1.1 | PhotoBatchUpload aggregate + domain events (TDD 3/3) |
| `2bac5c617` | 1.2 | Repository + EF migration |
| `158e9e258` | 1.3 | UploadPhotoBatchCommand + Validator (TDD 7/7) |
| `32f5cadd5` | 1.4a | smoldocling /preprocess endpoint + IPhotoPreprocessor |
| `0c458eb3c` | Final fix | snake_case migration + state guards + validator order + 5 polish |

### Phase 0 (code subset)

| Task | Status | Commit | Note |
|------|--------|--------|------|
| 0.4 step 2-7 — Bootstrap script + observability + backup configs | ✅ | `2cb547ae5` + `7aa52cce0` | File artifacts only |
| 0.7 — SharedGameCatalog integration verification | ✅ | `1506e25e2` + `9c962b43b` | `docs/development/libro-game-architecture.md` |
| 0.1 step 3 — OCR validation Python script | ✅ | `0edb90ae8` + `3ec3262ae` | `tests/llm-eval/ocr-validation/` |
| 0.2 step 2-3 — Golden set JSONL schema + CSV-to-JSONL converter | ✅ | `3667401ad` | `tests/llm-eval/golden-set/` |

### Phase 1 (backend up to smoldocling endpoint)

| Task | Status | Commit | Tests | Note |
|------|--------|--------|-------|------|
| 1.1 — PhotoBatchUpload aggregate + VOs + events | ✅ | `90cf5bd98` + `0c458eb3c` | 4/4 pass | Plan errors fixed during spike |
| 1.2 — IPhotoBatchUploadRepository + EF migration | ✅ | `2bac5c617` + `0c458eb3c` | – | Schema: public + snake_case |
| 1.3 — UploadPhotoBatchCommand + FluentValidation | ✅ | `158e9e258` + `0c458eb3c` | 7/7 pass | Validator order DoS-safe |
| 1.4a — IPhotoPreprocessor + smoldocling /preprocess endpoint | ✅ | `32f5cadd5` + `0c458eb3c` | 4 pytest pass + 1 skip | Endpoint `/api/v1/preprocess` |

### Acceptance Sprint 0

- [x] Tutti i task code Sprint 0 mergiati su `feature/libro-game-mvp-phase1` (13 commit)
- [x] Test pass (4 + 7 unit C# + 4 pytest Python)
- [x] Pattern compliance verificata (audit fixed 4 plan v2 errors)
- [x] OCR validation script eseguibile (run blocked su manuali Aaron)
- [x] PR #701 mergiato in `main-dev` (commit `555001410`)

## Sprint 1 — Phase 1 Backend completion ✅ COMPLETO

**Commit history** (5 commit, 28 files, 2363 insertions):

| Commit | Task | Description |
|--------|------|-------------|
| `1e8853066` | 1.4b | SmoldoclingPhotoPreprocessor HTTP client + DI (3/3 tests) |
| `ac624cb6c` | 1.5 | UploadPhotoBatchCommandHandler + Enqueue + IPhotoBatchProcessor stub (2/2 tests) |
| `edf588b10` | 1.6 | PhotoBatchProcessor parallel processing — KB indexing deferred Phase 2 (6/6 tests) |
| `ff055c786` | 1.7 | GetPhotoBatchStatus query + photo-batches endpoints (5/5 tests) |
| `1b690b02f` | 1.8 | Frontend gamebook upload UI — TanStack Query + Zod + react-intl (11/11 tests) |

### Phase 1 backend (Task 1.4b → 1.8)

| Task | Status | Commit | Tests |
|------|--------|--------|-------|
| 1.4b — SmoldoclingPhotoPreprocessor HTTP client | ✅ | `1e8853066` | 3/3 unit |
| 1.5 — UploadPhotoBatchCommandHandler + Enqueue | ✅ | `ac624cb6c` | 2/2 unit |
| 1.6 — IPhotoBatchProcessor parallel + bounded mutex | ✅ | `edf588b10` | 6/6 unit |
| 1.7 — GetPhotoBatchStatusQuery + endpoints (CQRS) | ✅ | `ff055c786` | 5/5 unit |
| 1.8 — Frontend upload page + ConfidenceBadge | ✅ | `1b690b02f` | 11/11 component |

### Acceptance Sprint 1

- [x] Tutti i task backend Sprint 1 committati su `feature/libro-game-sprint1-phase1`
- [x] Test pass (16 backend C# + 11 frontend = 27 unit tests)
- [x] Build clean (0 warnings 0 errors backend, typecheck OK frontend)
- [x] Pattern compliance verificata (5 plan v2 spike errors corrected)
- [ ] PR aperto + code review + merge in `main-dev` ← **NEXT**

### Sprint 1 plan v2 spike findings

5 plan v2 errors caught during spike-and-adapt:
1. Task 1.4b: `/preprocess` → `/api/v1/preprocess` (already known from Sprint 0)
2. Task 1.5: `IBlobStorageService.StoreAsync(Stream, string, string gameId, ...)` — `gameId` is **string** NOT `Guid`
3. Task 1.5: `BlobStorageResult` shape — `(Success, FileId?, FilePath?, FileSizeBytes, ErrorMessage?)` — NO `ContentHash` field
4. Task 1.6: KB services (`IDocumentChunker`, `IEmbeddingService`, `IKnowledgeBaseIndexer`, `KnowledgeChunk`) **NOT FOUND** → KB indexing deferred to Phase 2 Task 2.3 (TODO comment in PhotoBatchProcessor)
5. Task 1.7: `GetPresignedDownloadUrlAsync` no `CancellationToken` param; auth pattern uses `httpContext.TryGetAuthenticatedUser()` (session-based, NOT JWT claims)

### Task 1.9 — Phase 1 acceptance gate (DEFERRED)

🔵 **Blocked on B-1** procurement (manuali fisici). Avvio Sprint 2 (post-B-1):
- E2E Playwright test su 1 manuale Tainted Grail completo (50 pagine)
- Validation criteria: throughput ≥ 10 pag/min, confidence ≥ 0.7 su ≥ 95% pages, KB query funzionante post-indexing

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
