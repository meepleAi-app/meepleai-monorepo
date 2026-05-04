# 🎲 Libro Game AI Assistant — Feature Tracker

> **Stato**: 🔄 Implementation Sprint 0 in avvio (2026-05-04)
> **Branch**: `feature/libro-game-mvp-phase1` (parent: `main-dev`)
> **Effort**: 6-7 mesi calendar (27-28 weeks) | 3 fullstack + 1 ML + 1 designer + part-time legal + part-time UX

App companion per giocatori casual italiani che vogliono giocare gamebook in lingua estera (Tainted Grail, ISS Vanguard, Stuffed Fables, Andor Chronicles, 7th Continent).

## Documenti autoritativi

| Documento | Path | Linee | Purpose |
|-----------|------|-------|---------|
| **Vision** | `docs/superpowers/specs/2026-05-04-libro-game-assistant-vision.md` | 967 | 12-month vision, persona, NFR, risk register |
| **Plan v2** | `docs/superpowers/plans/2026-05-04-libro-game-assistant-mvp-phase1-v2.md` | 3441 | Phase 0+1 detailed (TDD steps), Phase 2-4 compressed reference |
| **Plan v1** | `docs/superpowers/plans/2026-05-04-libro-game-assistant-mvp-phase1.md` | 2601 | DEPRECATED — kept for audit history |

## Tracking files in questa directory

| File | Purpose |
|------|---------|
| [IMPLEMENTATION_STATUS.md](./IMPLEMENTATION_STATUS.md) | Task-by-task status (Phase 0-4) |
| [DECISIONS.md](./DECISIONS.md) | Implementation decisions log (workspace, sprint strategy, scope adjustments) |
| [BLOCKERS.md](./BLOCKERS.md) | External/human blockers (procurement, legal, accounts) — Aaron-track |

## Strategia esecuzione

**Hybrid risk-aware sprint** (decisione kickoff 2026-05-04):

```
Sprint 0 (2 weeks) — Foundation code-only
   ├─ Task 0.4 (config) — bootstrap.sh + observability + backup
   ├─ Task 0.7 — SharedGameCatalog audit
   ├─ Task 0.1 step 3 — OCR validation script artifact
   ├─ Task 0.2 step 2-3 — golden set schema + converter
   ├─ Task 1.1 — PhotoBatchUpload domain
   ├─ Task 1.2 — Repository + EF migration
   ├─ Task 1.3 — UploadPhotoBatchCommand + Validator
   └─ Task 1.4a — IPhotoPreprocessor + smoldocling /preprocess endpoint
        ║
        ║ ⏸️  Aaron parallel track (weeks 1-4):
        ║   - Procurement 5 manuali fisici
        ║   - Legal advisor identification
        ║   - Contractor IT native speaker brief
        ║   - Hetzner + OpenRouter + Stripe accounts
        ║
        ▼
Sprint 1 (1 week) — Risk gate
   └─ Task 0.1 step 5-7 — Run OCR validation appena manuali arrivano
        ║
        ║ 🚦 Hard gate:
        ║   - PASS (3+ giochi avg ≥ 0.85, angled ≥ 0.7) → continua
        ║   - MARGINAL (2+ giochi 0.7-0.85) → continua con UI confidence stronger
        ║   - FAIL (1+ giochi < 0.7) → STOP, scope review
        ▼
Sprint 2-3 (3-4 weeks) — Phase 1 completa
   └─ Task 1.4b → 1.5 → 1.6 → 1.7 → 1.8 → 1.9
```

## Decision points già risolti (2026-05-04 kickoff)

1. ✅ **Workspace**: working tree esistente `D:\Repositories\meepleai-monorepo-dev` (NON nuova directory)
2. ✅ **Scope iniziale**: Hybrid risk-aware (Opzione C dello spec-panel review)
3. ✅ **Branch**: `feature/libro-game-mvp-phase1` da `main-dev`
4. ✅ **Subagent strategy**: `superpowers:subagent-driven-development`
5. ✅ **Documentazione**: feature tracking dedicato in `docs/libro-game-assistant/`
6. ✅ **Cleanup**: `prompt.md` deduplicato

## Riferimenti rapidi

- **Persona Sara**: casual italian boardgamer, designer freelance, gioca 1-2 volte/mese, pain "manuale non in italiano"
- **MVP Phase 1 IN scope**: G1 acquire manuale + G3 Q&A regole + G4 translation on-demand single paragraph
- **MVP Phase 1 OUT scope** (deferred MVP-1+): Save/resume, Setup wizard interattivo, Pre-translate chapter, Multi-device QR, AI Narrator audio, LLM preset selector, BYOK
- **Pricing**: 2-tier solo Free 50 pag/mese + Credits €5/100 pag (NO subscription premium MVP)
- **Infra**: Hetzner CAX31 day 1 (16GB ARM, €14/mese)
- **LLM**: OpenRouter abstraction + Anthropic Claude Sonnet 4.5/Haiku 4.5 + DeepSeek V3

---

**Ultima modifica**: 2026-05-04 (kickoff)
