# DS-17 Phase D-2 — Librogame Storybook Migration (design)

**Issue**: [#2174](https://github.com/meepleAi-app/meepleai-monorepo/issues/2174)
**Umbrella**: [#2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063) DS-17 Mockup-to-App Fidelity
**Date**: 2026-06-22
**Status**: APPROVED (Approccio A — full coverage real-first)

---

## 1. Context & finding

Il body della issue #2174 presuppone un **greenfield**: creare la route `/libro-game/*`, un "nuovo bounded context", e migrare 4 stem mockup `sp6-libro-game-{index,resume-state,photo-upload,quota-credits}`.

**La realtà verificata sul codebase (2026-06-22) è diversa:**

| Presupposto del body | Realtà verificata |
|---|---|
| 4 mockup `sp6-libro-game-*` | ❌ Non esistono come file. Esistono **14 mockup `librogame-*`** (13 `librogame-runthrough-*` + 1 `librogame-game-night-storyboard`) in `admin-mockups/design_files/` |
| Route `/libro-game/*` "MISSING completamente" | ❌ Esiste già la route **`/gamebook`** (`apps/web/src/app/(authenticated)/gamebook/page.tsx` + `_components/` + `upload/`) |
| "nuovo bounded context, requires architecture decision" | ❌ Ecosistema **già implementato**: **41 file** in `apps/web/src/components/features/gamebook/` + backend `GameManagement` + `GamebookPhotoStorageService` |

**Causa del drift**: lo spec DS-17-11 (`docs/superpowers/specs/2026-06-11-ds-17-11-sp6-7-nano-cluster-design.md:38-43`) catalogò i 4 stem `sp6-libro-game-*` come "route mancante → defer Phase D" e aprì #2174. Da allora il dominio è stato implementato direttamente come codice (`/gamebook`) e i mockup ridisegnati come serie `librogame-*`, ma il body di #2174 non è mai stato aggiornato.

**Gap reale (allineato all'obiettivo DS-17 = eliminare drift mockup↔app via Storybook)**: i 14 mockup `librogame-*` sono tutti `design_intent: current` ma con `story_path` **vuoto** → nessuna Storybook story. Dei 41 componenti `/gamebook`, solo 1 ha una story (`gamebook/upload/page.stories.tsx`).

Quindi "Phase D-2 librogame ecosystem" **non è** creare route/componenti (esistono), **è** migrare i mockup `librogame-*` in Storybook stories sui componenti `/gamebook` esistenti, col pattern di Phase D-1 (PR #2468 / commit `414a9b7b0`).

---

## 2. Goal & scope

Migrare **tutti i 13 mockup `librogame-*` funzionali** in Storybook stories (Approccio A — full coverage real-first), chiudendo #2174. Il 14° mockup (`librogame-game-night-storyboard`) è meta-documentazione (iframe che aggrega altri mockup) → escluso dalle story, documentato nella designer queue.

**In scope:**
- ~73 Story export su 13 mockup, organizzate in commit per-mockup.
- Story sui **componenti reali** dove esistono; component-mock presentational (dal JSX/HTML) per gli stati-gap dei mockup PARTIAL.
- Fixture + MSW per cluster `librogame`.
- Aggiornamento `story_path` + `fixtures_path` su 13 `.fidelity.json`.
- Estensione/creazione snapshot spec con le entry `FRAMES`.
- Correzione del body della issue #2174 (rimozione del drift).
- Aggiornamento designer queue + riga cluster `librogame` su umbrella #2063.

**Out of scope:**
- Creazione di route/componenti di prodotto nuovi (l'ecosistema `/gamebook` esiste già).
- `librogame-game-night-storyboard` story (meta-documentazione).
- Cattura baseline PNG (deferita — vedi §7).
- Implementazione degli stati-gap a livello di prodotto (i component-mock `forward-*` rappresentano il mockup, non aggiungono feature reali).
- CI snapshot gate flip a blocking (resta `continue-on-error`, condizione di chiusura Phase C/D).
- Mobile viewport snapshots (desktop default; mobile opt-in via `fidelity.json: viewports`).

---

## 3. Mappatura mockup → componente

Verificata leggendo i componenti `apps/web/src/components/features/gamebook/` e la route `/gamebook`.

| # | Mockup | Componente reale | Match | Story stimati |
|---|---|---|---|---|
| 1 | librogame-runthrough-library-search | `/gamebook` page → `GamebookIndexView` + `GameSearchBar` + `GamebookCard` | STRONG | 4 |
| 2 | librogame-runthrough-game-detail | `LibroGameDetailView.tsx` | STRONG | 4 |
| 3 | librogame-runthrough-game-onboarding | nessun componente prereq-stepper → component-mock | PARTIAL | 4 |
| 4 | librogame-runthrough-setup-wizard | `CampaignSetupDrawer.tsx` (fallback `NewCampaignDialog.tsx`) | STRONG | 4 |
| 5 | librogame-runthrough-setup-chat | chat panel dentro `GamebookPlayShell` (store) → wrapper mock | PARTIAL | 4 |
| 6 | librogame-runthrough-play-session | `GamebookPlayShell.tsx` | STRONG | 4 |
| 7 | librogame-runthrough-resume-picker | `ResumeBooksList.tsx` (copre 3/5 stati) | PARTIAL | 5 |
| 8 | librogame-runthrough-encounter-cheatsheet | `EncounterCheatsheetView.tsx` | STRONG | 4 |
| 9 | librogame-runthrough-glossary-editor | `GlossaryEditorModal.tsx` (base; collision/bulk/variants forward) | PARTIAL | 6 |
| 10 | librogame-runthrough-translate-viewer | `TranslateViewer.tsx` | STRONG | 13 |
| 11 | librogame-runthrough-quota-credits | `checkout/` (5 step) + `QuotaWidget` + `SoftWarningCredits` | PARTIAL | 7 |
| 12 | librogame-runthrough-session-end | outcome modal in overlay store → component-mock | PARTIAL | 4 |
| 13 | librogame-runthrough-error-states | stati distribuiti (ErrorBoundary + `TranslateViewer.steps`) → component-mock | PARTIAL | 10 |
| 14 | librogame-game-night-storyboard | — (meta, iframe) | REFERENCE | 0 (escluso) |

**Totale stimato: ~73 Story export** (somma colonna, escluso #14). Il numero è una stima; il count effettivo è fissato durante l'implementazione per-mockup. Eventuali scostamenti vengono annotati nel plan (no silent cap).

**BGG**: 0 riferimenti user-facing (`cf.geekdo-images.com`, `boardgamegeek.com`, `images.geekdo.com`, `geekdo-images.com`, `useSearchBggGames`) in tutti i 14 mockup → nessuno Stage 0 cleanup necessario. Se durante l'implementazione emerge un riferimento BGG non rilevato, PAUSE + notify (regola DP-5 / vincolo #2123).

---

## 4. Architettura

Cluster nuovo: **`librogame`**. Pattern file-by-file identico a Phase D-1.

```
admin-mockups/design_files/librogame-*.fidelity.json     ← story_path + fixtures_path aggiornati (13 file)
apps/web/src/__tests__/fixtures/mockup-pilots/librogame/  ← 1 fixture .ts per mockup (MOCK_* + mswFor*State())
apps/web/src/<co-located>/<name>.stories.tsx             ← 1 story file per mockup (vedi §5 per la sede)
apps/web/e2e/storybook/librogame.snapshot.spec.ts        ← FRAMES array (1 entry per Story export)
docs/for-developers/frontend/c-librogame-review-queue.md ← designer queue (13 shipped + 1 reference)
```

- **Namespace Storybook**: title `Pages/Librogame/<Name>`, slug `pages-librogame-<name>--<frame>`.
- **Co-locazione story**: la story vive accanto a ciò che renderizza (component-mock co-locato col componente reale, o `page.stories.tsx` accanto a `page.tsx` per la route). Coerente con il pattern D-1 (Option 1 co-located).

---

## 5. Story policy (Approccio A)

- **STRONG (6 mockup)**: la story **importa e renderizza il componente reale** (es. `LibroGameDetailView`, `TranslateViewer`, `GamebookPlayShell`). Un export `Story` per stato/frame del mockup, defaults dal primo frame, axis via `argTypes`.
- **PARTIAL (7 mockup)**: la story renderizza il **componente reale per gli stati che già copre**; per gli **stati-gap** (non implementati nel prodotto — es. glossary collision/bulk/variants, session-end outcome modal, error-states pool) usa un **component-mock presentational** ricavato dal JSX/HTML del mockup, con l'export marcato `forward-*` nel nome + nota JSDoc. Questo rende esplicito che lo stato esiste nel mockup ma non ancora nel codice.
- **REFERENCE (1 mockup)**: `librogame-game-night-storyboard` → nessuna story; riga "Reference" nella designer queue.
- Ogni story file ha il blocco JSDoc `@mockup` (come D-1), `meta.title = 'Pages/Librogame/<Name>'`, `argTypes` matrix che rispecchia gli assi del mockup, callback come `fn()` spy (Storybook 8).

---

## 6. Fixtures & MSW

- Una fixture `.ts` per mockup in `apps/web/src/__tests__/fixtures/mockup-pilots/librogame/<stem>.ts`.
- Export `MOCK_*` (dati puri) per i component-mock e i componenti presentational.
- `mswForLibrogame<Stem>State(state)` per i mockup le cui story renderizzano componenti reali che fanno fetch (es. `/gamebook` index, game-detail). Endpoint GameManagement mockati a livello story via `parameters.msw.handlers`.

---

## 7. Snapshot & baseline policy

- Genero le entry `FRAMES` in `apps/web/e2e/storybook/librogame.snapshot.spec.ts` (1 entry per Story export, slug + file PNG atteso).
- **Cattura baseline PNG deferita**: il gate snapshot resta `continue-on-error: true` (non-blocking), coerente con le fasi DS-17 precedenti (Phase C: P247/P252). La cattura baseline avverrà nel batch di chiusura phase, non in questa PR. La PR dichiara esplicitamente questa deferral (no silent cap).
- Gate attivi in CI per questa PR: `pnpm typecheck`, `pnpm lint`, `pnpm lint:fidelity`, `pnpm lint:bgg` (+ `lint:bgg-mockups`), e i test unit/story esistenti (nessuna regressione).

---

## 8. Correzione body issue #2174

Il body verrà riscritto per riflettere la realtà:
- Rimuovere i 4 stem `sp6-libro-game-*` inesistenti.
- Sostituire con i 13 mockup `librogame-*` reali + nota che `/gamebook` (route + 41 componenti) è già implementato.
- Riformulare il goal come "Storybook stories migration (DS-17 fidelity)", non "creazione route/bounded context".
- Linkare questo design doc.

---

## 9. Acceptance criteria

- [ ] 13 mockup `librogame-*` migrati in Storybook stories (~73 export), 1 story file co-locato per mockup.
- [ ] 13 fixture files in `apps/web/src/__tests__/fixtures/mockup-pilots/librogame/`.
- [ ] STRONG → render del componente reale; PARTIAL → reale + component-mock `forward-*` per gli stati-gap.
- [ ] 13 `.fidelity.json` con `story_path` + `fixtures_path` popolati.
- [ ] `librogame.snapshot.spec.ts` con le entry `FRAMES` (baseline deferito, gate non-blocking).
- [ ] `librogame-game-night-storyboard` documentato come Reference (no story).
- [ ] Designer queue `c-librogame-review-queue.md` pubblicata.
- [ ] Body #2174 corretto + riga cluster `librogame` su umbrella #2063.
- [ ] `pnpm typecheck` + `pnpm lint` + `pnpm lint:fidelity` + `pnpm lint:bgg` clean; 0 regressioni unit/story.

---

## 10. Effort & risks

**Effort stimato**: ~4-6 giorni. Acceleratore: il pattern D-1 è un template file-by-file diretto e i 6 STRONG hanno componenti reali pronti. Freno: i 7 PARTIAL richiedono component-mock per gli stati-gap (analisi assi + markup dal mockup), e `translate-viewer` (13 stati) + `error-states` (10 stati) sono i più onerosi.

| Rischio | Mitigazione |
|---|---|
| PR voluminosa (~73 story) → review pesante | Commit per-mockup, review incrementale; designer queue dettagliata |
| Stati-gap PARTIAL ambigui (mockup mostra stato non implementato) | Component-mock marcato `forward-*` + nota JSDoc; design_intent resta `current` per il mockup, ma la story segnala lo stato come forward |
| Drift count ~73 → reale diverso | Conteggio fissato per-mockup nel plan; scostamenti annotati (no silent cap) |
| BGG ref non rilevato in un mockup | PAUSE + notify (DP-5 / #2123) |
| `GamebookPlayShell`/`TranslateViewer` richiedono store/provider in Storybook | Decorator/provider mock nello story file (verificare `.storybook/preview.tsx` esistente) |

---

## 11. Refs

- Phase D-1 template: PR #2468, commit `414a9b7b0` — `apps/web/src/components/features/game-nights/transition/game-night-transition.stories.tsx`, `apps/web/src/app/(public)/join/event/[code]/page.stories.tsx`, fixtures `mockup-pilots/sp6-7-nano/`, `e2e/storybook/sp6-7-nano.snapshot.spec.ts`.
- DS-17-11 spec: `docs/superpowers/specs/2026-06-11-ds-17-11-sp6-7-nano-cluster-design.md`.
- Phase C pilot spec: `docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md`.
- Fidelity schema + validator: `apps/web/scripts/mockup-annotations/validate-fidelity.mjs` (`pnpm lint:fidelity`).
- Existing reference story: `apps/web/src/app/(authenticated)/gamebook/upload/page.stories.tsx`.
- Scope distinction `/gamebook` vs `/library/[gameId]/play`: issue #836 / #835 (commento in `gamebook/page.tsx`).
