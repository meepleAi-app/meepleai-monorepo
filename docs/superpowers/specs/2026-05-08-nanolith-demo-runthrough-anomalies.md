# Nanolith Demo Runthrough — Anomalies Live Log

> Live MD compilato durante la pipeline runthrough Fase A. Tag classification per ogni entry:
> - `#BLOCKER` — failure reproducible 3× consecutivi che impedisce stadio
> - `#ANOMALY` — comportamento sub-target ma non bloccante (latenza, UX rough)
> - `#OBSERVATION` — note libere, comportamento atteso ma vale memorizzare

**Spec di riferimento:** `docs/superpowers/specs/2026-05-08-nanolith-demo-runthrough-phase-a.md`
**Plan di riferimento:** `docs/superpowers/plans/2026-05-08-nanolith-demo-runthrough-phase-a.md`

---

## §0 — T-10d Checkpoint Go/No-Go (data: 2026-05-08)

**Domanda:** PR #828 + #837 mergiati su main-dev?

[x] **YES** — PR #828 merge commit: `1df39e840` (2026-05-08 fast-forward) | PR #837 merge commit: `cbec2383f` (2026-05-08 fast-forward)
       → **Path A primary continues**

[ ] NO  — Mancano: N/A
       → Path B fallback: N/A

**Decisione finale:** **Path A primary** (Iter 1.A + Iter 1.B mergiati su `main-dev` PRIMA di T-10d effettivo).

**Contesto aggiuntivo discovered durante Task 0**:

Durante l'esecuzione di Task 0 (workspace setup) il `git pull --ff-only` su `main-dev` ha rivelato un fast-forward `71eab98e5..ed4f26830` di 144 file changes. Questo include i seguenti merge realizzati da team review fuori dalla nostra sessione brainstorming:

| Hash | PR | Descrizione |
|---|---|---|
| `1df39e840` | #828 | feat(gamebook): Iter 1.A — campaign sessions + chat shell for Nanolith dogfood |
| `cbec2383f` | #837 | feat(gamebook): Iter 1.B — photo translate + glossary + history for Nanolith dogfood |
| `3957b3c0b` | #838 | feat(gamebook): resume picker 4 stati + legacy routing cleanup |
| `ed4f26830` | #839 | docs/nanolith-staging-setup-design (merge commit) |

**Implicazione per pipeline plan**:

- ✅ **Task 1 e Task 2 SKIP** — entrambe PR mergiate fuori sessione, no review/merge ops da fare
- ✅ **Task 4 SKIP** — PR #838 ha shipped R5 ResumeCard 4 stati (mockup G state-01/02/03/04 + GamebookResumeShell orchestrator). `ResumeHero.tsx` (78 LOC) copre 1:1 lo scope state-02 originalmente plannato in Task 4
- ✅ **Task 1.A.9 legacy cleanup** anche shipped via PR #838: `apps/web/src/app/(authenticated)/library/games/[gameId]/translate/page.tsx` deleted (era il DEMO Path 5a workaround)
- 📦 **Bonus**: `2026-05-07-nanolith-staging-setup-design.md` + plan `2026-05-07-nanolith-staging-setup.md` aggiunti via PR #839 — riferimento utile per Task 5 seed staging

**Outcome T-10d**: pipeline pre-T-2d è già stata completata. Possiamo procedere immediatamente con:
- Task 3 (questo file, doc log) ✅
- Task 6-7 (smoke script + Makefile)
- Task 9 (Playwright @demo-runthrough)
- Poi G1-G5 runthrough operational

**Timeline reale collapsata**: 14gg → ~3-4gg.

---

## §1 — Stadio 1 (locale endpoint smoke)

(in attesa T-2d eseguibile dopo Task 6 + Task 7)

**Data esecuzione:** TBD
**Comando:** `make demo-smoke-local`
**Exit code:** TBD
**Endpoint OK:** TBD/7

**Anomalies/Observations:**
- TBD

**G1 status:** ⏳ pending

---

## §2 — Stadio 2 (locale browser flow)

(in attesa T-1d, eseguibile dopo G1 pass + Task 9)

**Data esecuzione:** TBD
**Browser:** Chrome ≥ 120 desktop su localhost:3000
**Auto Playwright:** TBD
**Manual flow time:** TBD

**Anomalies/Observations:**
- TBD

**G2 status:** ⏳ pending

---

## §3 — Stadio 3 (staging browser flow)

(in attesa T-1d, eseguibile dopo G2 pass + Task 5 seed staging)

**Data esecuzione:** TBD
**URL:** https://meepleai.app
**Auto smoke:** TBD
**Manual flow time:** TBD
**Diff funzionali vs G2:** TBD

**Anomalies/Observations:**
- TBD

**G3 status:** ⏳ pending

---

## §4 — Stadio 4 (smartphone Android Chrome)

(in attesa T-0d, eseguibile dopo G3 pass + device acquisition + page Storybook prep)

**Data esecuzione:** TBD
**Device:** TBD (Android ≥ 11)
**Browser:** Chrome [version]
**Manual flow time:** TBD
**Camera permission flow:** TBD
**Foto upload time:** TBD
**SSE translate first-token:** TBD

**Anomalies/Observations:**
- TBD

**G4 status:** ⏳ pending

---

## §5 — Pipeline closure (T-0d EOD)

(in attesa post-G4)

**Pipeline status:** ⏳ pending
**Branch:** feature/nanolith-demo-runthrough-phase-a
**PR:** TBD (open via gh pr create dopo G5)
**Total time:** TBD

**Lessons learned:**
- TBD
