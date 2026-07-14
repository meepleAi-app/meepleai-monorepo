/**
 * @mockup admin-mockups/design_files/librogame-runthrough-encounter-cheatsheet.html
 *
 * EncounterCheatsheetView argTypes matrix story — DS-17 Phase D-2 (sub-issue #2174).
 *
 * State-driving strategy (STATIC — props-driven, no play functions):
 *   EncounterCheatsheetView is a pure component: every FSM branch (idle / parsing /
 *   rendered / error) is reached by setting the `status` prop directly.
 *   No internal useState, no hooks, no network calls — no test-seam or MSW needed.
 *
 *   The snapshot harness (`apps/web/e2e/storybook/librogame.snapshot.spec.ts`) does
 *   `goto + wait + screenshot` only — it does NOT run Storybook play functions.
 *   Driving all states via props guarantees each frame renders its correct FSM cell
 *   on first paint without any interaction.
 *
 *   Frame01_Idle      — status='idle'     + storyContext (§147 → encounter §218)
 *   Frame02_Parsing   — status='parsing'  + onCancel provided (cancel button visible)
 *   Frame03_Rendered  — status='rendered' + full cheatsheet fixture (2 enemies, 3 options)
 *   Frame04_Error     — status='error'    + errorKind='parse-failed' (409 branch)
 *
 * Mockup route: /library/{gameId}/play/{campaignId}/encounter
 * Stati mockup: A (entry-from-story) · B (photo-segmenting) · C (cheatsheet-rendered) · D (resolved-back, out-of-scope)
 * D is intentionally omitted: `onResolve` navigates away — no cheatsheet UI to capture.
 *
 * BGG guard: no BGG refs in this file (#2123).
 *
 * Refs: spec docs/superpowers/specs/2026-06-22-ds-17-phase-d-2-librogame-storybook-migration-design.md,
 *       umbrella #2063, sub-issue #2174 (Phase D-2).
 */

import { fn } from 'storybook/test';

import {
  MOCK_ENCOUNTER_CHEATSHEET,
  MOCK_ENCOUNTER_LABELS,
  MOCK_ENCOUNTER_STORY_CONTEXT,
} from '@/__tests__/fixtures/mockup-pilots/librogame/librogame-encounter-cheatsheet';

import { EncounterCheatsheetView } from './EncounterCheatsheetView';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof EncounterCheatsheetView> = {
  title: 'Pages/Librogame/Encounter Cheatsheet',
  component: EncounterCheatsheetView,
  parameters: {
    layout: 'centered',
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: '/library/game-nanolith-001/play/campaign-001/encounter',
        query: {},
      },
    },
    docs: {
      description: {
        component:
          'Pixel-faithful matrix di librogame-runthrough-encounter-cheatsheet.html 4 stati. ' +
          'Tutti i frame usano la vera EncounterCheatsheetView props-driven — nessun mock, ' +
          'nessun MSW, nessun test-seam. `status` è una prop diretta che seleziona il ramo FSM.',
      },
    },
    // Frame03_Rendered (status='rendered') covers canonical `default`, Frame02_Parsing
    // (status='parsing') covers canonical `loading` — neither literal appears as a
    // quoted string, only as FSM-specific status values — lint:storybook-states
    // heuristic can't see them (#2342 Task 4 bonifica). Frame04_Error (status='error')
    // already detected via the literal `'error'` string.
    canonicalStates: ['default', 'loading', 'error'],
  },
  argTypes: {
    status: {
      control: { type: 'select' },
      options: ['idle', 'parsing', 'rendered', 'error'],
      description: 'FSM state: selects which branch renders (idle/parsing/rendered/error).',
    },
    errorKind: {
      control: { type: 'select' },
      options: ['parse-failed', 'not-found', 'generic'],
      description: 'Error variant shown in the error state.',
    },
    cheatsheet: {
      control: 'object',
      description: 'Full EncounterCheatsheet shape — required when status=rendered.',
    },
  },
  args: {
    status: 'idle',
    cheatsheet: null,
    labels: MOCK_ENCOUNTER_LABELS,
    onParse: fn(),
    onResolve: fn(),
    onOpenGlossary: fn(),
    onCancel: fn(),
  },
};
export default meta;

type Story = StoryObj<typeof EncounterCheatsheetView>;

// Stable spies at module scope — reset between story loads, not between re-renders.
const mockOnParse = fn();
const mockOnResolve = fn();
const mockOnGlossary = fn();
const mockOnCancel = fn();

// ── Frame 01 · Idle — entry CTA with story context (§147) ────────────────────

export const Frame01_Idle: Story = {
  name: '01 · Idle — entry CTA, contesto §147 → encounter §218',
  args: {
    status: 'idle',
    storyContext: MOCK_ENCOUNTER_STORY_CONTEXT,
    cheatsheet: null,
    onParse: mockOnParse,
    onResolve: mockOnResolve,
  },
  parameters: {
    docs: {
      description: {
        story:
          "Stato A mockup. Aaron al §147 di Nanolith: il paragrafo richiede l'Encounter Book. " +
          'Mostra il contesto narrativo (excerpt + paragrafo §147) e la CTA primaria ' +
          '"📷 Apri Encounter Book".',
      },
    },
  },
};

// ── Frame 02 · Parsing — busy spinner with cancel ─────────────────────────────

export const Frame02_Parsing: Story = {
  name: '02 · Parsing — spinner OCR in corso (budget 4.5 s), cancel visibile',
  args: {
    status: 'parsing',
    cheatsheet: null,
    onParse: mockOnParse,
    onResolve: mockOnResolve,
    onCancel: mockOnCancel,
  },
  parameters: {
    docs: {
      description: {
        story:
          'Stato B mockup. Spinner animato con aria-busy=true. ' +
          '"Analisi in corso…" + hint sul budget OCR (4.5 s P95 < 8 s). ' +
          'Bottone "Annulla" visibile perché onCancel è fornito.',
      },
    },
  },
};

// ── Frame 03 · Rendered — full cheatsheet (2 enemies, 3 options, win/loss) ────

export const Frame03_Rendered: Story = {
  name: '03 · Rendered — cheatsheet completa: 2 nemici, 3 opzioni, condizioni vittoria/sconfitta',
  args: {
    status: 'rendered',
    cheatsheet: MOCK_ENCOUNTER_CHEATSHEET,
    onParse: mockOnParse,
    onResolve: mockOnResolve,
    onOpenGlossary: mockOnGlossary,
  },
  parameters: {
    docs: {
      description: {
        story:
          'Stato C mockup. Card compatta con statistiche strutturate: 2 nemici (Guardiano Nanolith §218, ' +
          'Scudo Vivente), 3 opzioni di combattimento con dice roll, condizioni vittoria/sconfitta. ' +
          'Confidence alta (enemies 0.88, options 0.74, conditions 0.91) → nessun warning low-confidence. ' +
          'Toolbar con "Nuovo scatto", "Glossario" e "Risolvi →".',
      },
    },
  },
};

// ── Frame 04 · Error — parse-failed 409 branch ───────────────────────────────

export const Frame04_Error: Story = {
  name: '04 · Error — parse-failed (409), retry CTA',
  args: {
    status: 'error',
    errorKind: 'parse-failed',
    cheatsheet: null,
    onParse: mockOnParse,
    onResolve: mockOnResolve,
  },
  parameters: {
    docs: {
      description: {
        story:
          'Stato error mockup. Ramo parse-failed (HTTP 409): il modello non ha estratto ' +
          'statistiche valide. ⚠️ + messaggio + bottone "Riprova". ' +
          "Recoverable: l'utente può scattare di nuovo (onParse).",
      },
    },
  },
};
