# Designer Review Queue — DS-17 Phase D-2 `librogame` cluster

**Issue**: [#2174](https://github.com/meepleAi-app/meepleai-monorepo/issues/2174) · **Umbrella**: [#2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063)
**Spec**: `docs/superpowers/specs/2026-06-22-ds-17-phase-d-2-librogame-storybook-migration-design.md`
**Plan**: `docs/superpowers/plans/2026-06-22-ds-17-phase-d-2-librogame-storybook-migration.md`
**Date**: 2026-06-22

13 mockup `librogame-*` migrati in **76 Storybook story export** sui componenti `/gamebook` esistenti. Snapshot suite: `apps/web/e2e/storybook/librogame.snapshot.spec.ts` (baseline PNG **deferiti** al batch di chiusura Phase — gate `continue-on-error`).

> **Come revisionare**: apri Storybook (`pnpm -C apps/web storybook`) → naviga `Pages/Librogame/<Name>`. Confronta ogni Frame con il mockup `admin-mockups/design_files/<stem>.html`.

---

## ✅ Shipped — `design_intent: current` (5 mockup, 27 frame)

La story renderizza il **componente reale**; lo stato del mockup corrisponde al prodotto attuale.

| Mockup | Story | Frame | Note |
|---|---|---|---|
| `librogame-runthrough-game-detail` | `LibroGameDetailView.stories.tsx` | 6 | Default + KB indexing/error (prop `kbStatus`); loading/error/not-found sono mock del page-shell parent (`forward-*`) |
| `librogame-runthrough-library-search` | `gamebook/_components/GamebookIndexView.stories.tsx` | 6 | ⚠️ Il mockup HTML ritrae il hub `/library`; la story copre la FSM `/gamebook` (6 celle) come entry-point del persona flow. Vedi JSDoc story |
| `librogame-runthrough-setup-wizard` | `CampaignSetupDrawer.stories.tsx` | 4 | 3 step + validation-err, via test-seam `initialStep`/`initialTitle` |
| `librogame-runthrough-encounter-cheatsheet` | `EncounterCheatsheetView.stories.tsx` | 4 | Puro props-driven (`status`): idle/parsing/rendered/error |
| `librogame-runthrough-quota-credits` | `CheckoutModal.stories.tsx` | 7 | 4 step checkout (test-seam) + step3 loading/failed + soft-warning (`SoftWarningCredits`) |

---

## 🔮 Shipped — `design_intent: forward-refactor` (8 mockup, 49 frame) — **richiede attenzione designer**

Il mockup ritrae UI più ricca di quanto il componente reale implementi oggi. La story renderizza il componente reale per ciò che esiste + **component-mock presentational** (`forward-*`, in `__tests__/.../_mocks/`) per gli stati non ancora implementati. **Da revisionare come "target di design futuro"**, non come fedeltà 1:1 al codice attuale.

| Mockup | Story | Frame | Gap rispetto al componente reale |
|---|---|---|---|
| `librogame-runthrough-play-session` | `GamebookPlayShell.stories.tsx` | 4 | Componente reale è un **form-based progress tracker**; il mockup mostra una shell tabbed (Story/Encounter/Chat/Glossary) non implementata. I 4 frame mappano al closest intent via MSW + store decorator |
| `librogame-runthrough-translate-viewer` | `TranslateViewer.stories.tsx` | 12 | 6 Phase reali via test-seam; wake-lock = Template K (non implementato). 13 stati mockup → 12 frame (contrasto-AAA merged in `translated`) |
| `librogame-runthrough-resume-picker` | `ResumeBooksList.stories.tsx` | 5 | Componente reale è una lista semplice; first-time/single/multi via props, stale-warning + with-tutorial = Template K |
| `librogame-runthrough-glossary-editor` | `GlossaryEditorModal.stories.tsx` | 6 | pristine/edited/collision/save-error reali (seam + MSW); bulk-import + variants = Template K (non implementati) |
| `librogame-runthrough-game-onboarding` | `_librogame/GameOnboarding.stories.tsx` | 4 | Nessun componente reale (prereq-stepper) → tutto Template K |
| `librogame-runthrough-setup-chat` | `_librogame/SetupChat.stories.tsx` | 4 | Chat panel vive nello store di `GamebookPlayShell`, non esposto → Template K |
| `librogame-runthrough-session-end` | `_librogame/SessionEnd.stories.tsx` | 4 | Outcome modal in overlay store → Template K (paused/victory/defeat/cancelled) |
| `librogame-runthrough-error-states` | `_librogame/ErrorStates.stories.tsx` | 10 | 10 banner errore distribuiti su hook/boundary → Template K unificato |

---

## 📎 Reference — non migrato (1 mockup)

| Mockup | Motivo |
|---|---|
| `librogame-game-night-storyboard` | Meta-documentazione (iframe che aggrega altri mockup per validazione di flusso), non una UI component → nessuna story. `design_intent: current`, `story_path` vuoto. |

---

## Note tecniche

- **Test-seam props** aggiunti a 4 componenti reali (`CampaignSetupDrawer`, `TranslateViewer`, `GlossaryEditorModal`, `CheckoutModal`): props opzionali default-safe per montare stati interni staticamente. 544 test gamebook passano (0 regressioni).
- **Stati canonici fidelity**: l'enum `StateName` (`default|empty|loading|error|sse|offline|quota-soft|quota-hard`) non esprime gli stati di dominio (es. `segments_ready`, `collision`); i nomi precisi sono nei `name` delle story + `_state_mapping_note` nei fidelity.
- **0 riferimenti BGG** in tutto il cluster (vincolo #2123).
- **Scostamento conteggio**: 76 frame effettivi vs ~73 stimati nel piano (+3, alcuni mockup avevano più stati del previsto).
