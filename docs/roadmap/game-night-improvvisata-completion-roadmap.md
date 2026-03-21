# Game Night Improvvisata — Completion Roadmap

**Date**: 2026-03-16 | **Epic**: #379 (CLOSED — all 26 issues) | **Implementation**: 100%

---

## Executive Summary

La user story "Game Night Improvvisata" è **100% implementata** a livello di codice. Tutte le 26 issue dell'Epic #379 sono chiuse. La verifica end-to-end del codice (2026-03-16) conferma che ogni componente del journey è wired e funzionante.

**Cosa rimane**: validazione E2E, edge case coverage, aggiornamento documentazione.

---

## Verification Results (2026-03-16)

### Journey Step-by-Step Status

| # | Step | Backend | Frontend | Tests | Overall |
|---|------|---------|----------|-------|---------|
| 1 | Cerco gioco nell'app | ✅ | ✅ | ✅ E2E | ✅ |
| 2 | Cerco in BGG | ✅ `GET /bgg/search` | ✅ `BggSearchPanel` | ✅ E2E | ✅ |
| 3 | Aggiungo privato + posseduto | ✅ `POST /import-bgg` | ✅ Import UI | ⚠️ Mock only | ✅ |
| 4 | Carico PDF + disclaimer | ✅ Chunked upload | ✅ Upload UI | ⚠️ Mock only | ✅ |
| 5 | Notifica PDF pronto | ✅ SSE + deep link | ✅ `NotificationPanel` | ❌ No E2E | ✅ |
| 6 | Agent auto-creato | ✅ Dual-path handler | N/A (automatic) | ✅ 40+ unit | ✅ |
| 7 | Agente aiuta preparare | ✅ RAG + chat | ✅ Chat UI | ⚠️ Mock only | ✅ |
| 8 | ScoreAssistant NLP | ✅ Parse + auto-record | ✅ `ScoreAssistant` | ❌ No E2E | ✅ |
| 9 | Arbitro dispute | ✅ `SubmitRuleDispute` | ✅ Arbitro UI | ❌ No E2E | ✅ |
| 10 | Salva stato + foto | ✅ `PauseSnapshot` + async AI | ✅ `ResumeSessionPanel` | ⚠️ Mock only | ✅ |
| 11 | Riprendi con recap | ✅ Resume + recap + invite refresh | ✅ 3-component UX | ❌ No E2E | ✅ |

### Key Verified Components

| Component | Implementation | Evidence |
|-----------|---------------|----------|
| `AutoCreateAgentOnPdfReadyHandler` | ✅ Dual-path (KnowledgeBase + GameManagement) | 200+ lines, 40+ tests |
| `PauseSnapshot` entity | ✅ Full schema, JSONB, 3 indexes | 30+ tests, migration presente |
| `GenerateAgentSummaryHandler` | ✅ Async AI summary in italiano | Fire-and-forget with fallback |
| `NotifyAgentReadyHandler` | ✅ Event → SSE → deep link | Full chain verified |
| `ParseAndRecordScoreCommandHandler` | ✅ NLP + auto-record at 80%+ | Bidirectional agent↔scores |
| `ResumeSessionFromSnapshotCommandHandler` | ✅ Recap + fresh invite + auto-save cleanup | 3 frontend components |

---

## Roadmap: Validation & Quality

### Phase 1: Mock E2E Journey (2-3 giorni) — COMPLETED 2026-03-21

Espandere i test E2E mock-based per coprire il journey completo UI.

| Task | File | Status |
|------|------|--------|
| Complete UI journey test | `e2e/game-night-improvvisata-full-journey.spec.ts` (151 lines) | ✅ |
| ScoreAssistant confidence flows | `e2e/sessions/score-assistant.spec.ts` (147 lines) | ✅ |
| Arbitro verdict display | `e2e/sessions/rule-arbitration.spec.ts` (66 lines) | ✅ |
| Resume + recap + photo review | `e2e/sessions/session-resume.spec.ts` (121 lines) | ✅ |
| SSE notification | `e2e/notifications/in-app-notifications.spec.ts` (113 lines) | ✅ General |

**Deliverable**: Copertura UI del journey con mock — ✅ Complete

### Phase 2: Backend Integration E2E (3-4 giorni) — COMPLETED 2026-03-21

Test con backend reale, servizi AI reali (OpenRouter, embedding service).

| Task | Scope | Status |
|------|-------|--------|
| Save/Resume with PauseSnapshot | Save → snapshot → resume with recap | ✅ 3 tests |
| Score parsing (NLP) | Parse → auto-record → confirm → fallback | ✅ 4 tests |
| Disputes (AI Arbitro + v2) | AI verdict + structured dispute flow | ✅ 6 tests |
| Data integrity checks | State transitions, score persistence, snapshots, soft-delete, invites | ✅ 6 tests |
| Concurrent access | Simultaneous scores, proposals, voting, race conditions, concurrency | ✅ 6 tests |

**Total**: 25 integration tests across 5 files + shared helper module
**Spec**: `docs/superpowers/specs/2026-03-21-game-night-improvvisata-phase2-design.md`
**Plan**: `docs/superpowers/plans/2026-03-21-game-night-improvvisata-phase2.md`
**Requires**: API + PostgreSQL + Redis + Embedding Service + OpenRouter
**Deliverable**: Integrità dati verificata end-to-end

### Phase 3: Edge Case Hardening (1-2 giorni) — VERIFIED 2026-03-16

Full analysis: [`docs/testing/game-night-edge-cases-analysis.md`](../testing/game-night-edge-cases-analysis.md)

| Edge Case | Status | Evidence |
|-----------|--------|----------|
| PDF retry on failure | ✅ Handled | `MaxRetries = 3`, `PdfFailedEvent` with notification |
| Agent summary LLM failure | ✅ Handled | Fire-and-forget + fallback "Sessione ripresa dal turno X" |
| Expired invite join | ✅ Handled | `CanJoin = false`, `ReasonCannotJoin`, host can regenerate |
| Score parse < 60% confidence | ✅ Handled | `RequiresConfirmation = true` + candidate list |
| Agent quota exceeded | ✅ Handled | Silent skip + warning log, user can create manually |
| PDF too large (> 100MB) | ✅ Handled | Tier limits + structure validation + user-friendly error |
| Concurrent score updates | ✅ Handled | `RowVersion` optimistic concurrency on LiveGameSession |
| Resume after long pause | ⚠️ Design choice | No max duration (OK), fresh 24h invite on resume |

**Result**: 7/8 fully handled, 1/8 is a design choice (no max pause duration). **No action required.**

### Phase 4: Documentation Update (0.5 giorni) — COMPLETED 2026-03-21

| Doc | Action | Status |
|-----|--------|--------|
| Vertical slice spec | N/A — no separate file exists | ✅ Skip |
| Roadmap | Phase 2 marked COMPLETED in PR #52 | ✅ Done |
| API contracts | Clarified scope: scheduled events only, linked Improvvisata docs | ✅ Done |

### Phase 5: Full Pipeline Test (5-7 giorni, opzionale)

Test con stack completo (embedding service, Qdrant) per validare:
- PDF upload → processing → embedding → vector indexing → agent auto-create
- RAG query con contenuto reale del PDF
- Agent recap generation con LLM reale

**Richiede**: Tutti i servizi attivi (API + PG + Qdrant + Redis + Embedding + LLM)

---

## Deferred Features (Spec'd But Not Implemented)

Funzionalità nel design spec classificate "Should Have" o "Nice to Have", intenzionalmente non implementate:

| Feature | Spec Section | Priority | Reason |
|---------|-------------|----------|--------|
| Auto-save ogni 10 minuti | Section 3 | 🟢 LOW | Background service, non critico per MVP |
| Offline score buffer | Section 2 | 🟢 LOW | Requires service worker, complex |
| Guest reconnection token | Section 2 | 🟢 LOW | localStorage persistence |
| QR code per invite | Section 2 | 🟢 LOW | Client-side `qrcode.react`, trivial |
| PDF processing progress bar | Section 1 | 🟢 LOW | Visual only, status polling exists |

**Raccomandazione**: Creare issue GitHub per ciascuna se si decide di implementarle in futuro.

---

## Timeline Summary

| Phase | Effort | Status | Deliverable |
|-------|--------|--------|-------------|
| Phase 1: Mock E2E | 2-3 giorni | ✅ COMPLETED 2026-03-21 | UI coverage 100% |
| Phase 2: Integration E2E | 3-4 giorni | ✅ COMPLETED 2026-03-21 | Data integrity verified |
| Phase 3: Edge cases | 1-2 giorni | ✅ VERIFIED 2026-03-16 | Edge cases hardened |
| Phase 4: Docs update | 0.5 giorni | ✅ COMPLETED 2026-03-21 | Docs current |
| Phase 5: Full pipeline | 5-7 giorni | ⏳ Optional | Complete validation |

**MVP validation (Phase 1-4)**: ✅ **COMPLETE**

---

**Last Updated**: 2026-03-16
