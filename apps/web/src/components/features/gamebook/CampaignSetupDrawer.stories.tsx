/**
 * @mockup admin-mockups/design_files/librogame-runthrough-setup-wizard.html
 *
 * CampaignSetupDrawer argTypes matrix story — DS-17 Phase D-2 (sub-issue #2174).
 *
 * State-driving strategy:
 *   The drawer's step state (StepId 1|2|3) is fully INTERNAL — `useState<StepId>(1)`.
 *   Only `open`, `onOpenChange`, and `trigger` are external props.
 *   All 4 frames use controlled mode (`open={true}` via render:) so the drawer is
 *   visible on first render without requiring a trigger click.
 *
 *   Frame01_Step1Name    — real component, open=true on mount, step 1 (default).
 *   Frame02_Step2Players — play: click "Avanti →" once → step advances to 2.
 *   Frame03_Step3Confirm — play: click "Avanti →" twice → step advances to 3.
 *                          MSW handler mocks POST /api/v1/gamebook/campaigns so
 *                          the confirm button renders without a network error.
 *   Frame04_ValidationErr — play: clear the title input → titleError renders
 *                           inline, "Avanti →" becomes disabled.
 *
 * Portal note (vaul):
 *   DrawerContent renders into a vaul portal (outside the canvasElement root).
 *   All play functions query via `document.body` — NOT `within(canvasElement)`.
 *
 * BGG guard: no BGG refs in this file (#2123).
 *
 * Refs: spec docs/superpowers/specs/2026-06-22-ds-17-phase-d-2-librogame-storybook-migration-design.md,
 *       umbrella #2063, sub-issue #2174 (Phase D-2).
 */

import { fn, userEvent, within } from 'storybook/test';

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
          'Step state è interno; frames 2-4 avanzano/modificano lo stato via play functions. ' +
          'DrawerContent usa vaul portal → play queries usano document.body.',
      },
    },
  },
  argTypes: {
    gameId: { control: 'text', description: 'Game reference ID passed to POST payload.' },
    gameTitle: { control: 'text', description: 'Game title shown in drawer header + review card.' },
    open: { control: 'boolean', description: 'Controlled open state.' },
    onOpenChange: { action: 'openChange' },
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
    gameId: 'game-nanolith-001',
    gameTitle: 'Eldoria',
    open: true,
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

// ── Frame 02 · Step 2 Players — after first "Avanti →" ────────────────────

export const Frame02_Step2Players: Story = {
  name: '02 · Step 2 Giocatori — 4 player chip + add custom',
  args: {
    gameId: 'game-nanolith-001',
    gameTitle: 'Eldoria',
    open: true,
    onOpenChange: fn(),
  },
  parameters: {
    docs: {
      description: {
        story:
          'Stato 02 mockup — step 2/3. Aaron (host) + Marco, Giulia, Luca (guest) come player chip. ' +
          'Add custom button (dashed, disabled). Suggerimento agente Nanolith Tutor. ' +
          'Stepper: [✓ Nome done | 2 Giocatori active | 3 pending]. ' +
          'Avanzato via play: click "Avanti →" dal step 1.',
      },
    },
  },
  play: async () => {
    // DrawerContent renders in a vaul portal → must query document.body, not canvasElement.
    const body = within(document.body);

    // Wait for the drawer to be rendered and "Avanti →" button to appear
    const nextBtn = await body.findByTestId('campaign-setup-next');
    await userEvent.click(nextBtn);
  },
};

// ── Frame 03 · Step 3 Confirm — after second "Avanti →", with MSW ─────────

export const Frame03_Step3Confirm: Story = {
  name: '03 · Step 3 Conferma — review card ManaPips + CTA "Inizia sessione"',
  args: {
    gameId: 'game-nanolith-001',
    gameTitle: 'Eldoria',
    open: true,
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
          'CTA "📖 Inizia sessione" abilitato. Stepper: [✓ Nome | ✓ Giocatori | 3 Conferma active]. ' +
          'Avanzato via play: click "Avanti →" x2 dal step 1. ' +
          'MSW handler mocks POST /api/v1/gamebook/campaigns → 201 (no real network error).',
      },
    },
  },
  play: async () => {
    const body = within(document.body);

    // Step 1 → Step 2
    const nextBtn1 = await body.findByTestId('campaign-setup-next');
    await userEvent.click(nextBtn1);

    // Step 2 → Step 3 (button still has data-testid="campaign-setup-next" in step 2)
    const nextBtn2 = await body.findByTestId('campaign-setup-next');
    await userEvent.click(nextBtn2);
  },
};

// ── Frame 04 · Validation Error — title cleared, error inline ─────────────

export const Frame04_ValidationErr: Story = {
  name: '04 · Validation Error — nome vuoto, bottone disabilitato, hint danger',
  args: {
    gameId: 'game-nanolith-001',
    gameTitle: 'Eldoria',
    open: true,
    onOpenChange: fn(),
  },
  parameters: {
    docs: {
      description: {
        story:
          'Stato 04 mockup — step 1, campo nome svuotato. ' +
          'Inline error "⚠ Il nome deve essere almeno 3 caratteri." con bordo danger. ' +
          '"Avanti →" disabilitato (titleValid=false). Recovery immediato digitando. ' +
          'Avanzato via play: select-all + delete sul campo "Nome campagna".',
      },
    },
  },
  play: async () => {
    const body = within(document.body);

    // Find the title input and clear it to trigger the validation error
    const titleInput = await body.findByTestId('campaign-setup-title');
    await userEvent.clear(titleInput);
    // Type a single character then clear to ensure titleError branch is reached
    // (error only shows when title.length > 0 fails MIN_TITLE_LENGTH check)
    await userEvent.type(titleInput, 'A');
    await userEvent.clear(titleInput);
    // Type 2 chars: triggers "almeno 3 caratteri" error (length 2 > 0 but < 3)
    await userEvent.type(titleInput, 'Ca');
  },
};
