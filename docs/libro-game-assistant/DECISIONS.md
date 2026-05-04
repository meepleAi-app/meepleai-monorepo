# 🧭 Implementation Decisions Log — Libro Game AI Assistant

> Decisioni prese durante implementation, ortogonali alle decisioni vision (vision §7 Decisions Log).

## D-IMPL-01 — Workspace: working tree esistente

**Date**: 2026-05-04
**Decisione**: Implementation procede in `D:\Repositories\meepleai-monorepo-dev` (working tree esistente), NON in nuova directory `meepleai-libro-game` come suggerito dal prompt originale.
**Razionale**: 
- Working tree già synced con main-dev (post pull PR #698)
- Conventions MeepleAI già caricate (CLAUDE.md, secrets, dotnet/pnpm cache)
- Risk conflict con altri feature branch mitigato via branch isolation (`feature/libro-game-mvp-phase1`)
**Trade-off accettato**: Possibile contamination se altri working tree tocchino stessi file. Mitigation: branch dedicato + commit-only-on-feature-branch policy.

## D-IMPL-02 — Sprint strategy: Hybrid risk-aware (Opzione C)

**Date**: 2026-05-04
**Decisione**: Avvio Sprint 0 code-only in parallelo a Aaron procurement, senza aspettare PR-1/PR-2/PR-3 completion.
**Razionale**:
- Phase 0 letterale ha 1-3 weeks di idle time (procurement manuali)
- Code Sprint 0 può procedere autonomamente fino a Task 1.4a (smoldocling endpoint)
- Hard gate post-Sprint 0 (Task 0.1 step 5-7) protegge da committment a Phase 1 full prima di OCR validation
**Trade-off accettato**: Se OCR FAIL, perdiamo 16-22 days code Phase 1 backend (recoverable: domain entities + commands + handlers sono pattern indipendenti dal preprocessing). Mitigation: Task 1.4a è il punto naturale di pause.
**Alternative considerate e scartate**:
- A) Code-only sprint full Phase 1 → rischio 6 weeks code wasted se OCR FAIL
- B) Strict prompt order (wait procurement) → 1-3 weeks idle unacceptable

## D-IMPL-03 — Subagent driven development

**Date**: 2026-05-04
**Decisione**: Orchestrazione via `superpowers:subagent-driven-development` skill (raccomandata in plan v2 footer).
**Razionale**:
- Plan v2 designed per subagent execution (TDD red→green→refactor steps + acceptance gate per task)
- Main session protegge context window da output verbose subagent
- Review checkpoint tra task naturale fit con subagent boundaries
**Trade-off accettato**: Overhead di delegate vs main session. Mitigation: subagent solo per task code-producing, main session per coordination + decision points.

## D-IMPL-04 — Feature documentation in `docs/libro-game-assistant/`

**Date**: 2026-05-04
**Decisione**: Tracking implementation in nuova directory `docs/libro-game-assistant/` (4 file: README, IMPLEMENTATION_STATUS, DECISIONS, BLOCKERS).
**Razionale**:
- Vision + plan v2 sono documenti autoritativi committed (PR #698) — modifiche richiedono PR formale
- Tracking dinamico (status task, decisioni runtime, blockers) deve vivere separatamente
- Future maintainers trovano single source of truth per stato corrente
**Trade-off accettato**: Duplicazione minima riferimenti vision/plan. Mitigation: README contiene SOLO link, NON copia content.

## D-IMPL-05 — Cleanup `prompt.md` duplicates

**Date**: 2026-05-04
**Decisione**: Riscritto `prompt.md` rimuovendo le 2 ripetizioni delle sezioni Governance/Dependencies/Primo passo (originalmente 3x).
**Razionale**: Probabile errore copia-incolla durante stesura. File era 242 righe, ora ~100. Semantica preservata.
**Trade-off accettato**: Nessuno. File untracked, nessun rischio.

## D-IMPL-06 — Effort estimate consistency: 6-7 mesi (autoritativo)

**Date**: 2026-05-04
**Decisione**: Adottiamo **6-7 mesi calendar (27-28 weeks)** come stima ufficiale, allineata con Plan v2 self-review notes finali (riga 3433).
**Razionale**:
- Vision §6.4 dice 4-5 mesi MA Plan v2 esplicitamente dichiara "Honest re-estimate: 6-7 mesi, NOT 4-5 mesi. Vision doc §6.4 needs update."
- Plan v2 header dice 22-25 weeks (5-6 mesi) MA self-review finale dice 27-28 weeks (6-7 mesi)
- Prompt è già allineato con il valore finale (6-7 mesi)
**Conseguenza**: Vision §6.4 e Plan v2 header hanno valori obsoleti ma NON li modifichiamo in questa fase (richiederebbe PR mini-fix che attraversa code review). Annotare il discrepanza in IMPLEMENTATION_STATUS.md per contesto futuro.

## Decisioni differite

Decisioni che si presenteranno durante implementation (placeholder):

- **D-IMPL-XX** — Pattern naming PhotoBatch vs PhotoUpload (Task 1.1)
- **D-IMPL-XX** — Schema database: nuovo schema `libro_game` o riuso `document_processing`? (Task 1.2)
- **D-IMPL-XX** — Smoldocling preprocessing_mode flag: enum o string? (Task 1.4a)
- **D-IMPL-XX** — Frontend upload UX: drag-drop vs button-click (Task 1.8)

---

**Ultima modifica**: 2026-05-04 (kickoff Sprint 0)
