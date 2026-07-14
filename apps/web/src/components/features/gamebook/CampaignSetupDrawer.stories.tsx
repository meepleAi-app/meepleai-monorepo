/**
 * @mockup admin-mockups/design_files/librogame-runthrough-setup-wizard.html
 *
 * CampaignSetupDrawer argTypes matrix story — DS-17 Phase D-2 (sub-issue #2174).
 *
 * State-driving strategy (STATIC — no play functions):
 *   All 4 frames render interaction-driven states via test-seam props
 *   (`initialStep`, `initialTitle`, `initialPresetId`) so the snapshot harness
 *   (`apps/web/e2e/storybook/librogame.snapshot.spec.ts`) captures the correct
 *   wizard step on first render without needing to run play functions.
 *
 *   Frame01_Step1Name    — initialStep:1 (default), default title, preset group-a.
 *   Frame02_Step2Players — initialStep:2, stepper shows step 1 done + step 2 active.
 *   Frame03_Step3Confirm — initialStep:3, MSW handler mocks POST /api/v1/gamebook/campaigns.
 *   Frame04_ValidationErr — initialStep:1, initialTitle:'Ca' (2 chars < 3-char min)
 *                           → titleError renders statically without interaction.
 *
 * Why static over play:
 *   The snapshot harness does `goto(iframe) + waitForTimeout(2000) + screenshot`;
 *   it does NOT execute Storybook play functions. Frames 02/03/04 with play-only
 *   state advancement captured step-1 in every screenshot, making them useless.
 *   The test-seam props (backward-compatible optional props) solve this at the
 *   component level with maximum fidelity — the real component renders, no mocks.
 *
 * BGG guard: no BGG refs in this file (#2123).
 *
 * Refs: spec docs/superpowers/specs/2026-06-22-ds-17-phase-d-2-librogame-storybook-migration-design.md,
 *       umbrella #2063, sub-issue #2174 (Phase D-2).
 */

import { fn } from 'storybook/test';

import { mswForLibrogameSetupWizardState } from '@/__tests__/fixtures/mockup-pilots/librogame/librogame-setup-wizard';

import { CampaignSetupDrawer } from './CampaignSetupDrawer';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof CampaignSetupDrawer> = {
  title: 'Pages/Librogame/Setup Wizard',
  component: CampaignSetupDrawer,
  parameters: {
    layout: 'centered',
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: '/library/game-nanolith-001',
        query: {},
      },
    },
    docs: {
      description: {
        component:
          'Pixel-faithful matrix di librogame-runthrough-setup-wizard.html 4 stati. ' +
          '3-step wizard drawer per "Nuova campagna libro game" (Nome → Giocatori → Conferma). ' +
          'Tutti i frame usano test-seam props (initialStep/initialTitle/initialPresetId) ' +
          'per render statico senza play functions — compatibile col snapshot harness ' +
          '(goto+waitForTimeout+screenshot, no play execution).',
      },
    },
    // Frame01-03 cover canonical `default` (steady-state wizard steps) via
    // `initialStep` test-seam props, not string literals. Frame04_ValidationErr
    // covers canonical `error`: the mockup's own state-04 is labeled
    // "Validation Error" / `SetupWizardDrawer · ErrorState` (see
    // librogame-runthrough-setup-wizard.html:496-547) and is rendered via
    // `initialTitle:'Ca'` (2-char title triggers the inline field error), not
    // a quoted `'error'` literal — lint:storybook-states heuristic can't see
    // either (#2342 Task 4 bonifica).
    canonicalStates: ['default', 'error'],
  },
  argTypes: {
    gameId: { control: 'text', description: 'Game reference ID passed to POST payload.' },
    gameTitle: { control: 'text', description: 'Game title shown in drawer header + review card.' },
    open: { control: 'boolean', description: 'Controlled open state.' },
    onOpenChange: { action: 'openChange' },
    initialStep: {
      control: { type: 'select' },
      options: [1, 2, 3],
      description: 'Storybook/test seam: initial wizard step (default 1).',
    },
    initialTitle: {
      control: 'text',
      description: 'Storybook/test seam: initial campaign title.',
    },
    initialPresetId: {
      control: { type: 'select' },
      options: ['group-a', 'group-b', 'custom'],
      description: 'Storybook/test seam: initial preset selection.',
    },
  },
  args: {
    gameId: 'game-nanolith-001',
    gameTitle: 'Eldoria',
    open: true,
    onOpenChange: fn(),
  },
};
export default meta;

type Story = StoryObj<typeof CampaignSetupDrawer>;

// Stable spy — kept at module scope so Actions panel accumulates across re-renders.
const mockOnOpenChange = fn();

// ── Frame 01 · Step 1 Name — initial step on open ─────────────────────────

export const Frame01_Step1Name: Story = {
  name: '01 · Step 1 Nome — nome campagna + preset gruppo',
  args: {
    open: true,
    initialStep: 1,
    onOpenChange: mockOnOpenChange,
  },
  parameters: {
    docs: {
      description: {
        story:
          'Stato 01 mockup — drawer aperto, step 1/3 attivo. ' +
          'Campo "Nome campagna" con valore default "Campagna con i ragazzi". ' +
          'Preset gruppo A selezionato (4 giocatori). Stepper: [1 Nome active | 2 | 3].',
      },
    },
  },
};

// ── Frame 02 · Step 2 Players ──────────────────────────────────────────────

export const Frame02_Step2Players: Story = {
  name: '02 · Step 2 Giocatori — roster editabile (host + guests)',
  args: {
    open: true,
    initialStep: 2,
    onOpenChange: fn(),
  },
  parameters: {
    docs: {
      description: {
        story:
          'Stato 02 mockup — step 2/3 (#2917). Roster editabile via <PlayerSetup>: host = ' +
          'utente corrente (fallback "Tu" senza sessione) + guest del preset group-a ' +
          '(Marco, Giulia, Luca). Input "Nome giocatore" + bottone "Aggiungi" ABILITATI ' +
          '(add/remove/reorder). Suggerimento agente Nanolith Tutor riflette il roster live. ' +
          'Stepper: [✓ Nome done | 2 Giocatori active | 3 pending]. ' +
          'Reso statico via initialStep:2 (test-seam prop).',
      },
    },
  },
};

// ── Frame 03 · Step 3 Confirm — with MSW ──────────────────────────────────

export const Frame03_Step3Confirm: Story = {
  name: '03 · Step 3 Conferma — review card ManaPips + CTA "Inizia sessione"',
  args: {
    open: true,
    initialStep: 3,
    onOpenChange: fn(),
  },
  parameters: {
    msw: {
      handlers: mswForLibrogameSetupWizardState,
    },
    docs: {
      description: {
        story:
          'Stato 03 mockup — step 3/3. Review card con cover-mini, meta-rows ' +
          '(Preset / Giocatori / Lingua agente / Durata stimata) + info box "Cosa succede ora". ' +
          'La riga Giocatori mostra il roster REALE assemblato allo step 2 (#2917), non il preset. ' +
          'CTA "📖 Inizia sessione" abilitato. Stepper: [✓ Nome | ✓ Giocatori | 3 Conferma active]. ' +
          'Reso statico via initialStep:3 (test-seam prop). ' +
          'MSW handler mocks POST /api/v1/gamebook/campaigns → 201 (no real network error).',
      },
    },
  },
};

// ── Frame 04 · Validation Error — short title shows error statically ───────

export const Frame04_ValidationErr: Story = {
  name: '04 · Validation Error — nome vuoto, bottone disabilitato, hint danger',
  args: {
    open: true,
    initialStep: 1,
    initialTitle: 'Ca',
    onOpenChange: fn(),
  },
  parameters: {
    docs: {
      description: {
        story:
          'Stato 04 mockup — step 1, campo nome con 2 caratteri ("Ca" < 3 min). ' +
          'Inline error "⚠ Il nome deve essere almeno 3 caratteri." con bordo danger. ' +
          '"Avanti →" disabilitato (titleValid=false). ' +
          'titleError è derivato in render (title.length>0 && trim<3) — nessun touched flag. ' +
          'Reso statico via initialTitle:"Ca" (test-seam prop).',
      },
    },
  },
};
