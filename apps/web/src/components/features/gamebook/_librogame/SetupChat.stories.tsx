/**
 * @mockup admin-mockups/design_files/librogame-runthrough-setup-chat.html
 *
 * SetupChat story — DS-17 Phase D-2 Task 11 (sub-issue #2174).
 *
 * There is NO real standalone component for the setup-chat panel. The chat
 * panel lives inside `GamebookPlayShell` via `useChatPanelStore` (Zustand)
 * and is not exposed independently. This story uses a Template K
 * presentational mock `SetupChatMock` that renders the full chat UI from
 * the mockup, driven by a single `state` prop.
 *
 * The chat panel allows Aaron to query the "Game Tutor" agent about the
 * game rules and setup, backed by the Press Start + Rules KB. Key features:
 *   - Query input + send button
 *   - Agent reply with actionable numbered steps
 *   - Citation chips linking to KB page references
 *   - Confidence badge (alta / media / bassa)
 *   - Low-confidence disclaimer with hallucination guard
 *   - Out-of-context decline + switch-game action pills
 *   - Typing indicator during SSE streaming (P95 ≤ 8s)
 *
 * 4 mockup states → 4 story frames (all Template K forward-refactor):
 *
 *   Frame01_Default         — 4-player setup query, 5-step reply, confidence alta (0.92)
 *   Frame02_LowConfidence   — 5-player variant, disclaimer, confidence bassa (0.42)
 *   Frame03_OutOfContext    — query for another game, decline + switch actions
 *   Frame04_Loading         — typing indicator, 1.8s elapsed, 8s budget
 *
 * State-driving strategy (STATIC — `state` prop, no play functions):
 *   Each frame passes a different `state` value to `SetupChatMock`.
 *   The mock renders the correct chat content statically on first paint.
 *   The snapshot harness (`apps/web/e2e/storybook/librogame.snapshot.spec.ts`)
 *   does `goto + waitForTimeout(2000) + screenshot` — it never runs play.
 *
 * BGG guard: no BGG refs in this story or mock (#2123). Internal catalog
 * framing only (game title "Eldoria", KB "Press Start + Rules").
 *
 * Refs: spec docs/superpowers/specs/2026-06-22-ds-17-phase-d-2-librogame-storybook-migration-design.md,
 *       umbrella #2063, sub-issue #2174 (Phase D-2).
 */

import {
  SetupChatMock,
  type SetupChatMockState,
} from '@/__tests__/fixtures/mockup-pilots/librogame/_mocks/SetupChatMock';

import type { Meta, StoryObj } from '@storybook/react';

// ---------------------------------------------------------------------------
// Meta — component IS the mock (Template K: no real component exists)
// ---------------------------------------------------------------------------

const meta: Meta<typeof SetupChatMock> = {
  title: 'Pages/Librogame/Setup Chat',
  component: SetupChatMock,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Pixel-faithful matrix di librogame-runthrough-setup-chat.html 4 stati. ' +
          'Template K forward-refactor: nessun componente reale per il setup-chat panel. ' +
          'SetupChatMock renderizza la UI di chat con query input, risposta agente, ' +
          'citation chips, confidence badge e typing indicator. ' +
          'Sostituire con il vero ChatPanel quando estratto da GamebookPlayShell.',
      },
    },
  },
  args: {
    state: 'default' as SetupChatMockState,
    gameTitle: 'Eldoria',
    sessionName: 'Sera con i ragazzi',
    onSend: undefined,
  },
  argTypes: {
    state: {
      control: { type: 'select' },
      options: [
        'default',
        'low-confidence',
        'out-of-context',
        'loading',
      ] satisfies SetupChatMockState[],
      description: 'Quale stato del mockup renderizzare. Drives the chat panel content statically.',
    },
    gameTitle: {
      control: 'text',
      description: 'Titolo del gioco mostrato nel session header.',
    },
    sessionName: {
      control: 'text',
      description: 'Nome della sessione mostrato nel session header.',
    },
    onSend: {
      action: 'onSend',
      description: 'Callback per il pulsante Invia (no-op in snapshot statico).',
    },
  },
};
export default meta;

type Story = StoryObj<typeof SetupChatMock>;

// ── Frame 01 · Default — setup 4 giocatori, confidence alta (mockup Stato 1) ──

export const Frame01_Default: Story = {
  name: '01 · Default — setup 4 giocatori, risposta IT 5 step, confidence alta (mockup Stato 1)',
  args: {
    state: 'default',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Mockup Stato 1 (default): Aaron 30 minuti pre-arrivo amici chiede ' +
          '"come si setupa Eldoria per 4 giocatori?". ' +
          'Risposta IT 5 step actionable (tabellone → carte personaggio → dadi → mazzo → iniziativa). ' +
          'Citation chips: pag. 4 Setup base + pag. 7 Iniziativa. ' +
          'Confidence badge alta · 0.92 (verde). ' +
          'Suggested prompts: "Componenti necessari?" + "Chi inizia?". ' +
          'DoD N1: ≥ 4/5 step actionable, citation accuracy ≥ 90%.',
      },
    },
  },
};

// ── Frame 02 · Low Confidence — variante 5 giocatori (mockup Stato 2) ──────

export const Frame02_LowConfidence: Story = {
  name: '02 · Low Confidence — variante 5 giocatori, disclaimer, confidence bassa (mockup Stato 2)',
  args: {
    state: 'low-confidence',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Mockup Stato 2 (low-confidence): Aaron chiede una variante non coperta da Press Start ' +
          '(5 giocatori). Confidence retrieval < 0.7. ' +
          'Risposta esplicita di non-conoscenza + disclaimer ⚠️ "Non sono certo". ' +
          'Suggerimento consultare Rules KB pag. 18 variante "Avventura epica" (5-6 giocatori). ' +
          'Citation chip: Rules pag. 18 (suggerito). ' +
          'Confidence badge bassa · 0.42 (rosso). Hallucination guard attivo.',
      },
    },
  },
};

// ── Frame 03 · Out of Context — query su altro gioco (mockup Stato 3) ───────

export const Frame03_OutOfContext: Story = {
  name: '03 · Out of Context — query altro gioco, decline + switch actions (mockup Stato 3)',
  args: {
    state: 'out-of-context',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Mockup Stato 3 (out-of-context): Aaron per errore chiede setup di un gioco diverso ' +
          "(KB non collegato). L'agente declina esplicitamente, NON inventa. " +
          'Scope dichiarato: gioco Eldoria (Press Start + Rules). ' +
          'Action pills: "Cambia gioco" + "Cerca agente" + "Resta su Eldoria". ' +
          'Confidence badge bassa · Out of scope · 0.0.',
      },
    },
  },
};

// ── Frame 04 · Loading — typing indicator durante streaming (mockup Stato 4) ──

export const Frame04_Loading: Story = {
  name: '04 · Loading — typing indicator, 1.8s elapsed, budget 8s (mockup Stato 4)',
  args: {
    state: 'loading',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Mockup Stato 4 (loading): Aaron ha appena premuto Invia. ' +
          "L'agente sta cercando in KB + generando risposta (P95 budget 8s). " +
          'Typing indicator (3 dot animati) + meta "⏱️ Cerco in 24 pag · 1.8s elapsed (budget 8s)". ' +
          'Input bar disabilitata (valore preservato). Send button disabilitato (⏳). ' +
          'aria-busy=true sul conversation log.',
      },
    },
  },
};
