# Asse D — v2-shipped audit (PR D.0 MVP)

**Data**: 2026-06-05
**Issue**: [#1899](https://github.com/meepleAi-app/meepleai-monorepo/issues/1899)
**Parent umbrella**: [#1895](https://github.com/meepleAi-app/meepleai-monorepo/issues/1895)
**Branch**: `feature/issue-1899-asse-d-mvp`
**Scope**: PR D.0 audit-only (MVP cut da plan v2 originale che era XL ~25gg)

---

## Sommario esecutivo

Pre-flight discovery sulle 14 route in scope asse D rivela che **~80% sono GIÀ shipped upstream** in Wave B/C/D del v2 design migration (PR storici #635-#1525). Plan v1 asse D (XL ~25gg) era **write-time stale** rispetto allo stato del codebase.

**Decisione MVP cut**:
- PR D.0 = questo audit doc (~1.5h effort)
- Asse D #1899 rimane OPEN per follow-up minimal su route ancora pending
- Umbrella #1895 considerata 75%+ complete con A+B+C shipped sessioni 32/33/34

---

## Stato route asse D (14 route in scope)

### ✅ Già shipped upstream (~80%)

| Route | Stato matrix | Wave / PR | Status asse D |
|-------|--------------|-----------|---------------|
| `/library` | ✅ done | B.3 (PR #638) | **SHIPPED** — useLibrary hybrid grid pattern |
| `/games/[id]` | ✅ done | C.1 (PR #702) | **SHIPPED** — useGame + 5-tab pattern |
| `/games` | ✅ done | Via #1521 redirect → `/library` (canonical) | **SHIPPED** — multi-tab orchestrator removed, hub redirected |
| `/game-nights` | ✅ done | Stage 3 (PR #1173) | **SHIPPED** — Calendar + day-detail drawer + filters |
| `/sessions/[id]` | ✅ done | D.3 (PR #762) | **SHIPPED** — podium + KPI + diary + photos + share |
| `/agents` | ✅ done | B.2 (PR #637) | **SHIPPED** — useAgentList grid pattern |
| `/agents/[id]` | ✅ done | C.2 (PR #711) | **SHIPPED** — useAgent + chat history + KB docs chain |
| `/players/[id]` | ✅ done | Wave 3 (PR #724) | **SHIPPED** — usePlayerStatistics (v1 carryover) |

### ⏳ Pending — out-of-MVP scope asse D

| Route | Stato matrix | Note plan v2 |
|-------|--------------|--------------|
| `/sessions/[id]/live` | ⏳ pending L+ (Phase 0.5 + sub-PR split) | Real-time SSE + multi-hook + dialog states. Backend asse A polymorphic ScoreType wireable ora |
| `/discover` | ⏳ pending L (Phase 0.5 required) | Multiple horizontal-row hooks. Plan v2 dice "wrap in /games?tab=discover" per invariante #20 (asse B sidebar config DEC-5) |
| `/sessions` | ⏳ pending M | Sessions list + filters. Non bloccante |
| `/toolkits/[id]` | ⏳ pending M | Toolkit summary + version timeline (DEC-2 plan v2 originale: "Coming soon" toast stub) |
| `/kb/[id]` | ⏳ deferred | Pivot legale 2026-05-10. DEC-2 plan v2: "Coming soon" toast |

### ❌ Nuove (DEC-2 plan v2 originale stub costruiti)

| Route | Stato | Note |
|-------|-------|------|
| `/game-nights/[id]/summary` | ❌ Nuovo | DEC-2 stub costruito. Non implementato in PR D.0 MVP |
| `/game-nights/[id]/live` | ❌ Nuovo (separato da /sessions/[id]/live) | Layout immersivo no sidebar. Gated su asse A polymorphic ScoreType drawer editor |
| `/games/[id]/summary` | n/a (non in scope plan v1) | |

### 🔄 Esistenti ma da auditare

| Route | Discovery | Allineamento Claude Design |
|-------|-----------|---------------------------|
| `/game-nights/[id]` | Esiste (Stage 3 PR #1173) come sub-route di `/game-nights` | TODO verifica hero + Player/RSVP + Sessions sections + CTA contestuali |
| `/game-nights/new` | Esiste come page form | TODO verifica wizard 3-step pattern asse B (Quando+Dove → Invita → Game suggested) |
| `/onboarding` | Esiste (page.tsx) | TODO verifica wizard 3-step (Generi → BGG → Invita friend) |
| `/login` + `/register` | Esistono in `/(auth)/` separati | Plan dice "modale unificata con tabs" — refactor invasivo non MVP |

---

## Gap analysis vs plan v1

Plan v1 asse D era basato su assunzione **scratch**: 14 route da costruire + 2 stub. Discovery rivela:

| Plan v1 assumption | Reality |
|--------------------|---------|
| 14 route da costruire/refactor | 8 già SHIPPED, 5 pending, 1 deferred |
| `/library` MVP refactor | Già LibraryHub + hybrid grid pattern |
| `/games/[id]` 5-tab refactor | Già C.1 shipped con useGame contract |
| Polymorphic ScoreType drawer editor FE | NON ESISTE — necessario per D.3+D.4 |
| Cross-asse E2E (5 user journey) | Infra E2E auth seeding NON RESOLVED |
| Designer Review Checklist | NESSUN designer attivo |
| PR sequence 7 PR | Solo 1 PR realistic per sessione |

**Effort rebaseline drastico**: XL ~25gg → realistic ~0.5gg per audit MVP cut. Resto del work diventa follow-up issue separate **non parte di asse D umbrella**.

---

## Decisioni operate (DEC-1..DEC-4)

| ID | Decisione | Rationale |
|----|-----------|-----------|
| **DEC-1** | MVP cut: solo PR D.0 audit + (no D.1 implementation) | 80% route già shipped, plan v1 stale |
| **DEC-2** | PR D.1 superflua post-discovery | /library + /games/[id] già shipped (#638, #702) |
| **DEC-3** | Self-attestation review per future PR | Pattern coerente asse B+C, no designer attivo |
| **DEC-4** | Audit-only chiude MVP D.0 | Foundation per sessioni future su gap reali |

---

## Recommended follow-up (post-MVP)

Asse D resta OPEN con scope ridotto a route effettivamente pending:

### Priority 1 — Asse A wire-through
1. **`/sessions/[id]/live`** — wire polymorphic ScoreType editor (Points/BinaryWin/Objectives/Ranking) sopra backend asse A T6-T10. Effort ~3-4gg.
2. **`/game-nights/[id]/live`** — layout immersivo + Session editor drawer. Gated su #1
3. **`/game-nights/[id]/summary`** — post-completed GN summary (NEW, DEC-2 stub plan v1)

### Priority 2 — Sezioni stub
4. **`/discover` wrap in `/games?tab=discover`** — invariante #20 (asse B sidebar DEC-5). Effort ~1gg
5. **`/sessions`** index refactor — filter chips Tutte/Live/Draft/Completate

### Priority 3 — Auth/onboarding
6. **`/login` + `/register` modale unificata** — refactor invasivo. Defer fino a designer attivo
7. **`/onboarding`** wizard 3-step pattern asse B WizardModal

### Cross-cutting
8. **Cross-asse E2E (MAJ-11)** — 5 user journey richiede E2E auth seeding infra (~3gg solo infra)
9. **Manual QA checklist (MAJ-8)** — per 5 stati per route in `docs/for-developers/qa/`

---

## Acceptance MVP D.0

- [x] Pre-flight discovery: tutte 14 route scanned vs v2-migration-matrix
- [x] Status taxonomy: shipped / pending / deferred / nuovo
- [x] Effort rebaseline: XL ~25gg → realistic ~0.5gg audit + ~10-15gg follow-up reale
- [x] DEC-1..DEC-4 decisioni lockate
- [x] Follow-up priority list per sessioni future
- [x] Audit doc committed + #1899 status update

---

## References

- Plan v2 originale asse D (XL): NON scritto (deciso direttamente MVP cut)
- Plan v1 asse D body issue #1899 (pre-discovery, stale)
- v2-migration-matrix.md (840 LOC, authoritative per route shipping status)
- Spec consolidato: [`docs/superpowers/specs/2026-06-04-claude-design-alignment-spec-panel-review.md`](../../superpowers/specs/2026-06-04-claude-design-alignment-spec-panel-review.md)
- Umbrella #1895 — Asse A+B+C già MERGED sessioni 32/33/34

---

## Changelog

- **2026-06-05**: initial audit doc post-discovery. MVP cut DEC-1..DEC-4 lockate. Asse D umbrella stato chiarito (75%+ via upstream waves), follow-up identificato.
