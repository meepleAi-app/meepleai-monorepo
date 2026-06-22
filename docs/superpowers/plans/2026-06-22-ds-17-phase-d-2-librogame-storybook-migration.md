# DS-17 Phase D-2 — Librogame Storybook Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrare i 13 mockup `librogame-*` funzionali in ~73 Storybook story sui componenti `/gamebook` esistenti (Approccio A — full coverage real-first), chiudendo #2174.

**Architecture:** Pattern file-by-file di DS-17 Phase D-1 (PR #2468). Per ogni mockup: 1 fixture `.ts` + 1 story file co-locato + entry nel cluster snapshot spec + aggiornamento `.fidelity.json`. STRONG → render del componente reale; PARTIAL → componente reale per gli stati coperti + component-mock presentational per gli stati-gap. Niente nuovo codice di prodotto: l'ecosistema `/gamebook` (route + 41 componenti) esiste già.

**Tech Stack:** Next.js 16 App Router · React 19 · Storybook 8 (`storybook/test` `fn()`) · MSW (`msw`) · Playwright snapshot (`@playwright/test`) · Vitest · zod fidelity validator.

**Spec:** `docs/superpowers/specs/2026-06-22-ds-17-phase-d-2-librogame-storybook-migration-design.md`

---

## Reference templates (codice concreto)

Tre template, estratti dai deliverable Phase D-1. Ogni task-mockup ne istanzia uno.

### Template P — Story su componente reale *presentational* (props-driven)
Quando il componente accetta props che pilotano tutti gli stati (es. `LibroGameDetailView`, `EncounterCheatsheetView`, `CampaignSetupDrawer`). Modello: `apps/web/src/components/features/game-nights/transition/game-night-transition.stories.tsx`.

```tsx
/**
 * @mockup admin-mockups/design_files/librogame-runthrough-<stem>.html
 *
 * <Title> argTypes matrix story — DS-17 Phase D-2 (#2174).
 * HERO COMPONENT: `<Component>` (<component-path>) — presentational, props-driven.
 * Stage axis (<N> frames): <elenco stati>.
 * Refs: spec docs/superpowers/specs/2026-06-22-ds-17-phase-d-2-librogame-storybook-migration-design.md, umbrella #2063, #2174.
 */
import { fn } from 'storybook/test';
import { MOCK_LIBROGAME_<STEM> } from '@/__tests__/fixtures/mockup-pilots/librogame/librogame-<stem>';
import { <Component> } from '@/components/features/gamebook/<Component>';
import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof <Component>> = {
  title: 'Pages/Librogame/<Name>',
  component: <Component>,
  parameters: {
    layout: 'fullscreen', // o 'centered' per modali/drawer
    docs: { description: { component: '<descrizione pixel-faithful>' } },
  },
  argTypes: {
    // 1 entry per prop che varia tra gli stati del mockup — control 'select'/'boolean'/'text'
  },
  args: {
    // defaults dal primo frame + callback come fn()
  },
};
export default meta;
type Story = StoryObj<typeof <Component>>;

export const Frame01_<ShortName>: Story = {
  name: '01 · <descrizione stato>',
  args: { /* override per lo stato */ },
};
// ... 1 export per stato/frame
```

### Template M — Story su componente reale *fetch-driven* (MSW)
Quando il componente fa data-fetch interno (es. route `/gamebook` → `GamebookIndexView`, hook `useGamebook*`). Modello: `apps/web/src/app/(public)/join/event/[code]/page.stories.tsx`.

```tsx
/**
 * @mockup admin-mockups/design_files/librogame-runthrough-<stem>.html
 * <Title> — DS-17 Phase D-2 (#2174). HERO: `<Component>` (fetch-driven, story drives via MSW).
 * Stage axis (<N> states): <elenco>. Refs: spec …, #2063, #2174.
 */
import { mswForLibrogame<Stem>State } from '@/__tests__/fixtures/mockup-pilots/librogame/librogame-<stem>';
import { <Component> } from '<component-path>';
import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof <Component>> = {
  title: 'Pages/Librogame/<Name>',
  component: <Component>,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true }, // se è una route page
    docs: { description: { component: '<descrizione>' } },
  },
  argTypes: { /* eventuali props (es. id) */ },
  args: { /* defaults */ },
};
export default meta;
type Story = StoryObj<typeof <Component>>;

export const Frame01_<ShortName>: Story = {
  name: '01 · <stato>',
  parameters: { msw: { handlers: mswForLibrogame<Stem>State('<state>') } },
};
// ... 1 export per stato
```

### Template Mf — Story via URL fixture-override (per componenti con `?fixture=` built-in)
Quando il componente ha un override di stato via query string (verificato: `GamebookIndexView` ha `STATE_OVERRIDE_ENABLED` con `?fixture=loading|error|empty|default|quota-soft|quota-hard`, `apps/web/src/app/(authenticated)/gamebook/_components/GamebookIndexView.tsx:175-220`). Pilota lo stato via `parameters.nextjs.navigation.query` invece di MSW — più semplice e deterministico per gli stati non-network.

```tsx
export const Frame01_<ShortName>: Story = {
  name: '01 · <stato>',
  parameters: { nextjs: { appDirectory: true, navigation: { query: { fixture: '<state>' } } } },
};
// loading/error che NON passano dal fixture data-path usano comunque MSW (Template M).
```

### Template K — Component-mock presentational (stati-gap PARTIAL / mockup senza componente)
Quando il prodotto non implementa lo stato (es. session-end outcome modal, error-states pool, game-onboarding stepper). Si crea un componente presentational che riproduce il markup del mockup, in zona fixtures (NON codice di prodotto), e la story lo renderizza.

```tsx
// apps/web/src/__tests__/fixtures/mockup-pilots/librogame/_mocks/<Stem>Mock.tsx
/**
 * Component-mock presentational per librogame-runthrough-<stem> (DS-17 Phase D-2 #2174).
 * Stato non implementato nel prodotto → marcato forward-*. Riproduce il markup del mockup
 * usando i token canonici (var(--background), var(--foreground), …). NO logica di prodotto.
 */
export type <Stem>MockState = '<s1>' | '<s2>'; // union degli stati-gap
export function <Stem>Mock({ state }: { state: <Stem>MockState }) {
  return ( /* markup token-faithful dal mockup, switch su `state` */ );
}
```

### Template F — Fixture
Dati puri (Template P/K) o handler MSW (Template M). Modelli: `…/sp6-7-nano/sp7-game-night-transition.ts` (dati) e `…/sp7-game-night-join-public.ts` (MSW).

```ts
// apps/web/src/__tests__/fixtures/mockup-pilots/librogame/librogame-<stem>.ts
// Dati: export const MOCK_LIBROGAME_<STEM>: <Type> = { … };
// MSW:  export function mswForLibrogame<Stem>State(state: '<...>') { return [ http.get('*/api/...', …) ]; }
```

### FRAMES entry (cluster snapshot spec)
File: `apps/web/e2e/storybook/librogame.snapshot.spec.ts`. 1 entry per ogni Story export.

```ts
{ slug: 'pages-librogame-<name>--frame-01-<shortname>', file: 'librogame-<name>-01-<shortname>.png' },
```

Lo slug è derivato da `meta.title` (`Pages/Librogame/<Name>` → `pages-librogame-<name>`) + l'export (`Frame01_ShortName` → `frame-01-shortname`). Verificare lo slug effettivo aprendo `/iframe.html?id=<slug>` in Storybook.

### Comandi di verifica
```bash
# typecheck (dalla root apps/web)
cd apps/web && pnpm typecheck
# lint mirato sui file toccati
cd apps/web && pnpm lint
# fidelity schema
cd apps/web && pnpm lint:fidelity
# BGG guard
cd apps/web && pnpm lint:bgg && pnpm lint:bgg-mockups
# Storybook build (verifica che le story compilino e gli slug esistano)
cd apps/web && pnpm build-storybook
```

---

## Procedura standard per task-mockup (Task 1-13)

Ogni task-mockup esegue questi step (2-5 min ciascuno). I DATI specifici sono nella tabella del task.

1. **Leggi il mockup + il componente reale.** `admin-mockups/design_files/<mockup>.{html,jsx}` (stati/assi) e il componente reale al path indicato. **NON fidarti del template suggerito nel task — verificalo leggendo il componente.** Determina:
   - Usa hook di fetch (`useQuery`/`useMutation` di `@tanstack/react-query`, custom hook `useGamebook*`/`useGameBooks`/`usePhotoUpload`/`useTranslateSegment*`)? → **Template M** (MSW handlers). I componenti `/gamebook` sono **prevalentemente react-query + Zustand**: aspettati M/decorator, NON P.
   - Usa uno store Zustand (`useChatPanelStore`, `useReaderMode`, ecc.)? → aggiungi un **decorator** che seed-a lo store nello story file.
   - È davvero solo props-driven (riceve dati via props, nessun fetch)? → **Template P**. (Conferma leggendo la signature.)
   - Lo stato del mockup **non esiste nella FSM/`status`/`Phase` union del componente reale**? → quello stato è un **gap → Template K** (component-mock `forward-*`). **Riconcilia gli stati elencati nel task con la FSM reale del componente** (leggi la sua union di stati): scarta i nomi inventati, usa i nomi reali, e tratta come `forward-*` solo ciò che il prodotto non implementa.
   - Se il componente usa `useRouter()` (`next/navigation`), imposta `parameters.nextjs: { appDirectory: true }` nello story file (anche se `preview.tsx` lo fornisce globalmente, esplicitalo).
2. **Crea la fixture** `apps/web/src/__tests__/fixtures/mockup-pilots/librogame/librogame-<stem>.ts` (Template F). Dati o handler MSW secondo il template scelto.
3. **(solo se servono stati-gap)** Crea il component-mock `…/librogame/_mocks/<Stem>Mock.tsx` (Template K) per gli stati che il componente reale non copre.
4. **Crea la story** al path indicato (Template P/M). 1 export per stato; `meta.title = 'Pages/Librogame/<Name>'`; `argTypes` che rispecchiano le props del componente / l'asse `state`.
5. **Verifica render:** `cd apps/web && pnpm typecheck` (PASS) e build storybook locale o avvio storybook per controllare che gli export rendano senza errori.
6. **Aggiorna fidelity:** in `admin-mockups/design_files/<mockup>.fidelity.json` imposta `acceptance.story_path` (path story) e `acceptance.fixtures_path` (path fixture). Poi `cd apps/web && pnpm lint:fidelity` (PASS).
7. **Aggiungi le entry FRAMES** in `apps/web/e2e/storybook/librogame.snapshot.spec.ts` (1 per export).
8. **Commit:** `git add <fixture> <story> [<mock>] <fidelity> <snapshot-spec> && git commit -m "feat(stories): #2174 Phase D-2 librogame-<stem> story (<N> frames)"` (body ≤100 char/riga, trailer Co-Authored-By).

> Se in qualunque mockup emerge un riferimento BGG user-facing non rilevato (`cf.geekdo-images.com`, `boardgamegeek.com`, `images.geekdo.com`, `geekdo-images.com`, `useSearchBggGames`) → **PAUSE + notify user** (regola DP-5 / vincolo #2123). Non procedere con quel mockup.

---

## File structure

**Create:**
- `apps/web/src/__tests__/fixtures/mockup-pilots/librogame/librogame-<stem>.ts` × 13
- `apps/web/src/__tests__/fixtures/mockup-pilots/librogame/_mocks/<Stem>Mock.tsx` × (stati-gap PARTIAL, ~4-6 file)
- story file × 13 (co-locate — vedi tabella per-task)
- `apps/web/e2e/storybook/librogame.snapshot.spec.ts`
- `docs/for-developers/frontend/c-librogame-review-queue.md`

**Modify:**
- `admin-mockups/design_files/librogame-*.fidelity.json` × 13 (`story_path` + `fixtures_path`)
- `.storybook/main.ts` (solo se la stories-glob non copre i path scelti — verificato in Task 0)
- (GitHub) body issue #2174 + umbrella #2063 row

---

### Task 0: Cluster scaffolding + verifica Storybook glob

**Files:**
- Create: `apps/web/src/__tests__/fixtures/mockup-pilots/librogame/.gitkeep`
- Create: `apps/web/e2e/storybook/librogame.snapshot.spec.ts`
- Read: `apps/web/.storybook/main.ts`

- [ ] **Step 1: Conferma la stories-glob.** La glob è `'../src/**/*.stories.@(js|jsx|mjs|ts|tsx)'` (`apps/web/.storybook/main.ts:4`) — copre **tutto** `src/`, incluso `src/__tests__/`. I component-mock (Template K) vivono in `src/__tests__/fixtures/mockup-pilots/librogame/_mocks/` e sono solo **importati** dalle story (non sono `.stories.tsx`). Le story `.stories.tsx` stanno co-locate sotto `src/components/features/gamebook/` (o `src/components/features/gamebook/_librogame/` per i mockup senza componente reale) e `src/app/(authenticated)/gamebook/` — scelta di chiarezza, non vincolo di glob.

- [ ] **Step 2: Crea lo snapshot spec skeleton.** Crea `apps/web/e2e/storybook/librogame.snapshot.spec.ts` con header `@mockup DS-17 Phase D-2 cluster librogame (#2174)`, `const FRAMES = [];` (popolato dai task successivi) e il test-loop identico a `sp6-7-nano.snapshot.spec.ts:253-260` (goto `/iframe.html?id=${slug}&viewMode=story`, waitForTimeout 2000, toHaveScreenshot). Sostituisci la label test in `` `librogame ${file…}` ``.

- [ ] **Step 3: Crea la dir fixtures** con `.gitkeep`.

- [ ] **Step 4: Commit.**
```bash
git add apps/web/e2e/storybook/librogame.snapshot.spec.ts apps/web/src/__tests__/fixtures/mockup-pilots/librogame/.gitkeep
git commit -m "chore(stories): #2174 Phase D-2 librogame cluster scaffold"
```

---

## STRONG mockups (componente reale pronto)

### Task 1 (PILOTA): librogame-runthrough-game-detail

> Pilota: valida il pattern nel dominio gamebook prima di scalare. Dopo questo task, conferma il pattern prima di proseguire.

**Files:**
- Read: `admin-mockups/design_files/librogame-runthrough-game-detail.{html,fidelity.json}`, `apps/web/src/components/features/gamebook/LibroGameDetailView.tsx`
- Create: `apps/web/src/__tests__/fixtures/mockup-pilots/librogame/librogame-game-detail.ts`
- Create: `apps/web/src/components/features/gamebook/LibroGameDetailView.stories.tsx`
- Modify: `admin-mockups/design_files/librogame-runthrough-game-detail.fidelity.json`, `apps/web/e2e/storybook/librogame.snapshot.spec.ts`

**Dati:** Componente `LibroGameDetailView` (game detail hero + meta grid 4 info + CTA "Avvia libro game"). Data via props (Template **P**), MA usa `useRouter()` (`LibroGameDetailView.tsx:38`) → imposta `parameters.nextjs: { appDirectory: true }` nello story file. Importa anche `NanolithCampaignCTA` (`:25`) → in step 1 verifica che quel sottocomponente non abbia dipendenze di fetch/context aggiuntive. ~4 stati: `default`, `loading`, `error`, `not-found` — verifica in step 1 se loading/error/not-found sono pilotabili via prop o derivano da fetch (in tal caso quegli stati passano a Template M/K). Title `Pages/Librogame/Game Detail`.

- [ ] Esegui la **Procedura standard** (step 1-8). Nota pilota: dopo lo Step 5 (typecheck PASS), fermati e verifica con l'utente/review che il pattern sia corretto prima di Task 2+.

---

### Task 2: librogame-runthrough-library-search

**Files:**
- Read: `admin-mockups/design_files/librogame-runthrough-library-search.{html,fidelity.json}`, `apps/web/src/app/(authenticated)/gamebook/page.tsx`, `apps/web/src/app/(authenticated)/gamebook/_components/GamebookIndexView.tsx`
- Create: `apps/web/src/__tests__/fixtures/mockup-pilots/librogame/librogame-library-search.ts`
- Create: `apps/web/src/app/(authenticated)/gamebook/page.stories.tsx`
- Modify: fidelity + snapshot spec

**Dati:** Route `/gamebook` index → `GamebookIndexView`. FSM reale a 6 celle: `loading | error | empty | default | quota-soft | quota-hard` (NON esistono `search-filtered`/`empty-no-match` — non inventarli). Template **Mf** (URL fixture override `?fixture=…` via `parameters.nextjs.navigation.query`) per `default`/`empty`/`quota-soft`/`quota-hard` (`GamebookIndexView.tsx:175-220`); Template **M** (MSW) solo per `loading`/`error` se il fixture-path non li forza. ~5-6 stati. Title `Pages/Librogame/Library Search`. `nextjs: { appDirectory: true }`.

- [ ] Procedura standard (1-8).

---

### Task 3: librogame-runthrough-setup-wizard

**Files:**
- Read: mockup + `apps/web/src/components/features/gamebook/CampaignSetupDrawer.tsx` (fallback `NewCampaignDialog.tsx`)
- Create: `…/librogame/librogame-setup-wizard.ts`, `apps/web/src/components/features/gamebook/CampaignSetupDrawer.stories.tsx`
- Modify: fidelity + snapshot spec

**Dati:** `CampaignSetupDrawer` (3-step drawer). Display props-driven (`open`, step interno), MA usa `useMutation` (`@tanstack/react-query`, `:33`) + `useRouter()` (`:119`). `QueryClientProvider` è globale in `preview.tsx` → `useMutation` non crasha. Il submit dello Step 3 spara `POST /api/v1/gamebook/campaigns` reale → lo stato `step3-confirm` deve includere un **MSW handler** per quell'endpoint (Template **P** + MSW per il submit). Imposta `nextjs: { appDirectory: true }`. ~4 stati: `step1-name`, `step2-players`, `step3-confirm`, `validation-err` (client-side, props-driven). Title `Pages/Librogame/Setup Wizard`. `layout: 'centered'`.

- [ ] Procedura standard (1-8).

---

### Task 4: librogame-runthrough-encounter-cheatsheet

**Files:**
- Read: mockup + `apps/web/src/components/features/gamebook/EncounterCheatsheetView.tsx`
- Create: `…/librogame/librogame-encounter-cheatsheet.ts`, `apps/web/src/components/features/gamebook/EncounterCheatsheetView.stories.tsx`
- Modify: fidelity + snapshot spec

**Dati:** `EncounterCheatsheetView` (card encounter OCR→cheatsheet). Template **P**. ~4 stati: `idle`, `parsing`, `rendered`, `error`. Title `Pages/Librogame/Encounter Cheatsheet`.

- [ ] Procedura standard (1-8).

---

### Task 5: librogame-runthrough-play-session

**Files:**
- Read: `admin-mockups/design_files/librogame-runthrough-play-session.{html,jsx}` + `apps/web/src/components/features/gamebook/GamebookPlayShell.tsx`
- Create: `…/librogame/librogame-play-session.ts`, `apps/web/src/components/features/gamebook/GamebookPlayShell.stories.tsx`
- Modify: fidelity + snapshot spec

**Dati:** `GamebookPlayShell` (orchestrator). Template **M** (NON P). Usa `useGamebookCampaign(campaignId)` + `useUpdateGamebookProgress(campaignId)` + `useGameBooks(gameRef)` (react-query) + `useChatPanelStore` (Zustand). Renderizza uno **skeleton finché `isLoading || !data`** → senza MSW per `GET /api/v1/gamebook/campaigns/{id}` la story mostra solo lo skeleton. **NON ha tab-props**: gli stati del mockup (`story-tab`/`encounter-tab`/`chat-overlay`/`glossary-inline`) NON sono pilotabili via prop → in step 1 verifica se sono interazioni UI (tab click via `play` function) o vanno resi come Template **K** (component-mock forward-*). Aggiungi un decorator che seed-a `useChatPanelStore` se la chat overlay serve aperta. Riconcilia gli stati con ciò che il componente realmente espone. Title `Pages/Librogame/Play Session`.

- [ ] Procedura standard (1-8). Step 1 è critico: leggi `GamebookPlayShell.tsx` per intero, determina MSW handlers + decorator store, e quali stati richiedono component-mock K.

---

### Task 6: librogame-runthrough-translate-viewer

**Files:**
- Read: mockup + `apps/web/src/components/features/gamebook/TranslateViewer.tsx` (+ eventuale `TranslateViewer.steps.ts`)
- Create: `…/librogame/librogame-translate-viewer.ts`, `apps/web/src/components/features/gamebook/TranslateViewer.stories.tsx`
- Modify: fidelity + snapshot spec

**Dati:** `TranslateViewer` (foto→OCR→translate fullscreen). Template **M + K** (NON P). Props solo `{ campaignId, gameRef }` — **nessun `initialStep`/`initialPhase`**: tutto lo stato è interno (`useGameBooks`, `usePhotoUpload`, `useSegmentPhoto`, `useTranslateSegmentSSE`, `useReaderMode`). `Phase` union reale: `idle | uploading | segmenting | segments_ready | translating | translated`. Gli stati del mockup raggiungibili via la Phase si pilotano con **MSW** (handlers per upload/segment/translate-SSE). Gli stati `wake-lock`, `aaa-contrast`, `fullscreen` **non sono Phase** → resi come Template **K** (component-mock forward-*). `reader-mode`/`lang-badge`/`lang-override` sono combinazioni interne (`useReaderMode` toggle, `modalState`) → in step 1 valuta se raggiungibili via interazione o K. **`TranslateViewer.steps` è un file di helper** (`deriveUiStep`, `isAbortableStep`, `LABELS`), NON uno store: non "seedabile". ~13 export totali (mix MSW + K). Title `Pages/Librogame/Translate Viewer`. **Mockup più oneroso** — leggi a fondo `TranslateViewer.tsx` + `TranslateViewer.steps.ts` + gli hook in step 1.

- [ ] Procedura standard (1-8). Step 1 critico: mappa ogni stato del mockup → (Phase via MSW) | (combinazione interna via interazione) | (Template K forward-*).

---

## PARTIAL mockups (componente reale parziale + component-mock per stati-gap)

### Task 7: librogame-runthrough-resume-picker

**Files:**
- Read: mockup + `apps/web/src/components/features/gamebook/ResumeBooksList.tsx`
- Create: `…/librogame/librogame-resume-picker.ts`, `…/librogame/_mocks/ResumePickerMock.tsx` (per gli stati onboarding/tutorial non coperti), `apps/web/src/components/features/gamebook/ResumeBooksList.stories.tsx`
- Modify: fidelity + snapshot spec

**Dati:** `ResumeBooksList` è puro **props-driven** (`progress: BookProgress[]`, `onResume`), con solo 2 branch reali: lista non-vuota / empty. `single-resume` (array di 1) e `multi-campaign` (array di N) si pilotano variando la prop `progress` (Template **P**). Gli stati `stale-warning`, `first-time`, `with-tutorial` **non hanno branch** nel componente → Template **K** (`ResumePickerMock`, forward-*). 5 stati totali. Title `Pages/Librogame/Resume Picker`.

- [ ] Procedura standard (1-8) con step 3 (component-mock per 2 stati-gap).

---

### Task 8: librogame-runthrough-glossary-editor

**Files:**
- Read: mockup + `apps/web/src/components/features/gamebook/GlossaryEditorModal.tsx`
- Create: `…/librogame/librogame-glossary-editor.ts`, `…/librogame/_mocks/GlossaryEditorMock.tsx` (solo bulk-import/variants), `apps/web/src/components/features/gamebook/GlossaryEditorModal.stories.tsx`
- Modify: fidelity + snapshot spec

**Dati:** `GlossaryEditorModal` usa `useTranslation()` (→ `useIntl`; `ReactIntlProvider` è globale in `preview.tsx` → OK) + `useUpsertGlossary(campaignId)` (mutation). Copre nativamente `edit-pristine`, `edited`, **e `collision`** (implementato, `:268-283` ramo `state.status === 'collision'` + `CollisionBanner` — **NON è un gap**, pilotabile via Template P). Lo stato `save-error` richiede un **MSW handler** che fa fallire `PUT /api/v1/gamebook/glossary/{id}`. Stati-gap reali (non implementati) `bulk-import`, `variants` → Template **K** (`GlossaryEditorMock`, forward-*). ~6 stati. Title `Pages/Librogame/Glossary Editor`. `layout: 'centered'`.

- [ ] Procedura standard (1-8) con step 3.

---

### Task 9: librogame-runthrough-quota-credits

**Files:**
- Read: mockup + `apps/web/src/components/features/gamebook/CheckoutModal.tsx` (orchestratore) + `checkout/` (step `Step1QuotaReached`, `Step2PackPicker`, `Step3CheckoutForm`, `Step4Success`, `CheckoutStepIndicator`) + `SoftWarningCredits.tsx`
- Create: `…/librogame/librogame-quota-credits.ts`, story `apps/web/src/components/features/gamebook/CheckoutModal.stories.tsx`
- Modify: fidelity + snapshot spec

**Dati:** L'orchestratore è **`CheckoutModal.tsx`** (un livello sopra `checkout/`, NON `checkout/CheckoutFlow`). checkout flow 7 step: `step1-quota`, `step2-picker`, `step3-form`, `step3-loading`, `step3-failed`, `step4-success`, `soft-warning`. Template **P** sui componenti checkout reali; gli step con submit (`step3-loading`/`step3-failed`) potrebbero richiedere MSW se `CheckoutModal` usa una mutation (verifica in step 1). `soft-warning` usa `SoftWarningCredits` (componente separato — può essere una story export distinta o un secondo componente nello stesso file). Title `Pages/Librogame/Quota Credits`.

- [ ] Procedura standard (1-8). Step 1: leggi `CheckoutModal.tsx` per capire come pilotare i 7 step (prop step? mutation? state interno).

---

### Task 10: librogame-runthrough-game-onboarding

**Files:**
- Read: mockup (nessun componente reale prereq-stepper)
- Create: `…/librogame/librogame-game-onboarding.ts`, `…/librogame/_mocks/GameOnboardingMock.tsx`, story `apps/web/src/components/features/gamebook/_librogame/GameOnboarding.stories.tsx`
- Modify: fidelity + snapshot spec

**Dati:** Nessun componente → tutta component-mock (Template K). Prereq stepper: `prereq-missing`, `pdf-uploading`, `kb-indexing`, `ready`. 4 stati, tutti `forward-*`. Title `Pages/Librogame/Game Onboarding`. Crea la story sotto `src/components/features/gamebook/_librogame/` (dir glob-coperta; il mock vive in `__tests__/.../_mocks/`).

- [ ] Procedura standard (1-8) con step 3.

---

### Task 11: librogame-runthrough-setup-chat

**Files:**
- Read: mockup + `apps/web/src/components/features/gamebook/GamebookPlayShell.tsx` (chat panel) / eventuale `useChatPanelStore`
- Create: `…/librogame/librogame-setup-chat.ts`, `…/librogame/_mocks/SetupChatMock.tsx`, story `apps/web/src/components/features/gamebook/_librogame/SetupChat.stories.tsx`
- Modify: fidelity + snapshot spec

**Dati:** Chat panel vive dentro lo store, non esposto come componente → component-mock `SetupChatMock` (Template K). 4 stati: `default`, `low-confidence`, `out-of-context`, `loading`. Title `Pages/Librogame/Setup Chat`.

- [ ] Procedura standard (1-8) con step 3.

---

### Task 12: librogame-runthrough-session-end

**Files:**
- Read: mockup (outcome modal in overlay store)
- Create: `…/librogame/librogame-session-end.ts`, `…/librogame/_mocks/SessionEndMock.tsx`, story `apps/web/src/components/features/gamebook/_librogame/SessionEnd.stories.tsx`
- Modify: fidelity + snapshot spec

**Dati:** Outcome modal → component-mock `SessionEndMock` (Template K). 4 stati: `paused`, `victory`, `defeat`, `cancelled` (+ ManaPips stat tile). Title `Pages/Librogame/Session End`. `layout: 'centered'`.

- [ ] Procedura standard (1-8) con step 3.

---

### Task 13: librogame-runthrough-error-states

**Files:**
- Read: mockup (10 banner stati distribuiti)
- Create: `…/librogame/librogame-error-states.ts`, `…/librogame/_mocks/ErrorStatesMock.tsx`, story `apps/web/src/components/features/gamebook/_librogame/ErrorStates.stories.tsx`
- Modify: fidelity + snapshot spec

**Dati:** Pool di 10 error banner → component-mock `ErrorStatesMock` (Template K, 1 prop `variant`). Stati: `stream-timeout`, `ocr-fail`, `llm-503`, `segmentation-fail`, `ocr-low-conf`, `photo-blur`, `translation-timeout`, `source-lang-unknown`, `network-mid-ocr`, `quota-exhausted`. 10 stati. Title `Pages/Librogame/Error States`.

- [ ] Procedura standard (1-8) con step 3.

---

## Finalizzazione

### Task 14: Designer queue + reference + body issue + umbrella + gate finale

**Files:**
- Create: `docs/for-developers/frontend/c-librogame-review-queue.md`
- Modify: (GitHub) body #2174, umbrella #2063

- [ ] **Step 1: Designer review queue.** Crea `docs/for-developers/frontend/c-librogame-review-queue.md` con: sezione "Shipped" (13 mockup con story_path + N frame), sezione "Reference" (`librogame-game-night-storyboard` — meta-documentazione, no story, con motivazione), sezione "Forward states" (elenco degli stati `forward-*` resi via component-mock, raggruppati per mockup, per la review designer). Segui il formato di una queue esistente (es. `docs/for-developers/frontend/c1-sp6-7-nano-review-queue.md` se presente).

- [ ] **Step 2: Gate finale.**
```bash
cd apps/web && pnpm typecheck && pnpm lint && pnpm lint:fidelity && pnpm lint:bgg && pnpm lint:bgg-mockups
```
Expected: tutti PASS. Conferma 0 regressioni con i test unit/story esistenti dei componenti toccati: `cd apps/web && pnpm test src/components/features/gamebook --run`.

- [ ] **Step 3: Conteggio finale.** Conta gli export Story totali nei 13 story file e confronta con la stima ~73. Annota lo scostamento nel body PR (no silent cap).

- [ ] **Step 4: Commit designer queue.**
```bash
git add docs/for-developers/frontend/c-librogame-review-queue.md
git commit -m "docs(ds-17): #2174 Phase D-2 librogame designer review queue"
```

- [ ] **Step 5: Correggi body #2174 + umbrella #2063** (via `gh issue edit`): rimuovi i 4 stem `sp6-libro-game-*` inesistenti, sostituisci con i 13 mockup `librogame-*` + nota che `/gamebook` è già implementato, riformula goal come "Storybook stories migration", linka spec + plan. Aggiungi riga cluster `librogame` su #2063.

- [ ] **Step 6: PR** verso `main-dev` (parent branch). Body: riepilogo, conteggio story effettivo, nota baseline PNG deferiti (gate `continue-on-error`), 0 BGG, link spec + plan. Footer `🤖 Generated with [Claude Code]`.

---

## Self-Review

**1. Spec coverage:**
- §2 scope (13 mockup → ~73 story) → Task 1-13 ✓
- §3 mappatura (STRONG/PARTIAL/REFERENCE) → Task 1-6 STRONG, 7-13 PARTIAL, storyboard escluso in Task 14 ✓
- §4 architettura (cluster librogame, namespace, file layout) → Task 0 + template ✓
- §5 story policy (real-first / forward-* mock) → Template P/M/K + Procedura step 3 ✓
- §6 fixtures/MSW → Template F + step 2 ✓
- §7 snapshot + baseline deferral → Task 0 + step 7 + Task 14 step 6 (PR note) ✓
- §8 body correction → Task 14 step 5 ✓
- §9 acceptance → coperto da Task 0-14 ✓
- §10 rischi (BGG, store/provider) → nota BGG nella Procedura + Task 5/6 note provider ✓

**2. Placeholder scan:** I `<placeholder>` nei template sono segnaposto-template intenzionali (l'esecutore li sostituisce con i dati della tabella del task), non placeholder vaghi. Ogni task ha componente, stati, e file espliciti. Nessun "TODO/add appropriate X".

**3. Type consistency:** Naming coerente — `mswForLibrogame<Stem>State`, `MOCK_LIBROGAME_<STEM>`, `<Stem>Mock`, title `Pages/Librogame/<Name>`, slug `pages-librogame-<name>`, fixture path `…/mockup-pilots/librogame/librogame-<stem>.ts`. Snapshot spec `librogame.snapshot.spec.ts` referenziato in modo uniforme.

**Note aperte (risolte in esecuzione, non placeholder):** i nomi-prop esatti e gli handler MSW specifici dipendono dalla lettura del componente reale (step 1 di ogni task) — by-design del pattern scaffold DS-17.

### Post code-review (subagent, 2026-06-22)

Una review pre-esecuzione (`feature-dev:code-reviewer`) ha letto i componenti reali e corretto assunzioni sistematiche (i componenti `/gamebook` sono react-query + Zustand, non presentational). Correzioni applicate:
- **Procedura step 1** rafforzato: default M/decorator per gamebook, riconciliazione FSM, stati non-FSM → K, `nextjs.appDirectory` per `useRouter`.
- **Template Mf** aggiunto (URL fixture-override `?fixture=` per `GamebookIndexView`).
- **Task 2** (`GamebookIndexView`): FSM reale `loading|error|empty|default|quota-soft|quota-hard` (rimossi i nomi inventati), Template Mf.
- **Task 3** (`CampaignSetupDrawer`): MSW per il submit Step 3 (`useMutation`).
- **Task 5** (`GamebookPlayShell`): Template M + decorator store; nessuna tab-prop (skeleton se `!data`).
- **Task 6** (`TranslateViewer`): Template M + K; `Phase` union reale; `TranslateViewer.steps` è helper non store.
- **Task 7** (`ResumeBooksList`): puro props (2 branch); stati extra → K.
- **Task 8** (`GlossaryEditorModal`): `collision` è implementato (NON gap); `save-error` → MSW.
- **Task 9** (checkout): orchestratore reale = `CheckoutModal.tsx` (non `checkout/CheckoutFlow`).
- **Task 0**: corretto il commento sulla glob (`../src/**/*.stories.@(...)` copre anche `__tests__/`).

I 6 CRITICAL della review sono risolti. Il pilota (Task 1) valida il pattern corretto prima di scalare.
