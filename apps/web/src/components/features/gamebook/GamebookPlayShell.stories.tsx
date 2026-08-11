/**
 * @mockup admin-mockups/design_files/librogame-runthrough-play-session.html
 *
 * GamebookPlayShell argTypes matrix story — DS-17 Phase D-2 (sub-issue #2174).
 *
 * State-driving strategy (STATIC — MSW + Zustand decorator, no play functions):
 *   The real GamebookPlayShell is a form-based shell (paragraph progress tracker +
 *   "Traduci pagina" link + "Apri chat con agente" button). It has NO tabs, NO chat
 *   overlay, NO glossary sheet — those are aspirational mockup states not yet
 *   implemented in the real component.
 *
 *   MSW is REQUIRED: `useGamebookCampaign` calls GET /api/v1/gamebook/campaigns/{id}
 *   and without a handler the component renders the loading skeleton on every frame.
 *
 *   Four frames cover the distinct renderable states of the real component:
 *
 *   Frame01_StoryTab    — campaign §289 loaded, single narrative book auto-selected
 *                          (BookPicker hidden), submit button enabled.
 *                          Covers mockup state-01 (story-tab default runtime).
 *
 *   Frame02_EncounterTab — same campaign + 2 narrative books → BookPicker selector
 *                           visible. Maps to mockup state-02 (encounter-tab with
 *                           multi-book context visible in the sidebar).
 *
 *   Frame03_ChatOverlay  — campaign §289 + Zustand decorator pre-seeds
 *                           useChatPanelStore with isOpen=true so the chat button
 *                           reflects the active-chat state.
 *                           Maps to mockup state-03 (chat-overlay active).
 *
 *   Frame04_GlossaryInline — campaign with 10-entry history list rendered below the
 *                             form (data.history.length > 0). Maps to mockup state-04
 *                             (rich context panel / glossary-inline context).
 *
 * Snapshot harness (`apps/web/e2e/storybook/librogame.snapshot.spec.ts`) does
 * `goto + waitForTimeout(2000) + screenshot` — it does NOT run Storybook play
 * functions. MSW + decorator-based state ensures each frame is correct on first paint.
 *
 * BGG guard: no BGG refs in this file (#2123).
 *
 * Refs: spec docs/superpowers/specs/2026-06-22-ds-17-phase-d-2-librogame-storybook-migration-design.md,
 *       umbrella #2063, sub-issue #2174 (Phase D-2).
 */

import {
  mswForLibrogamePlaySessionMultiBook,
  mswForLibrogamePlaySessionRichHistory,
  mswForLibrogamePlaySessionState,
  MOCK_CAMPAIGN_ID,
  MOCK_GAME_ID,
} from '@/__tests__/fixtures/mockup-pilots/librogame/librogame-play-session';
import { useChatPanelStore } from '@/lib/stores/chat-panel-store';

import { GamebookPlayShell } from './GamebookPlayShell';

import type { Decorator } from '@storybook/react';
import type { Meta, StoryObj } from '@storybook/react';

// ---------------------------------------------------------------------------
// Decorator — seeds Zustand useChatPanelStore.isOpen before each story renders.
// Used only by Frame03_ChatOverlay to simulate the chat panel being active.
// ---------------------------------------------------------------------------
const chatOpenDecorator: Decorator = Story => {
  useChatPanelStore.setState({
    isOpen: true,
    gameContext: {
      id: MOCK_GAME_ID,
      name: 'Con i ragazzi — Eldoria',
      pdfCount: 0,
      kbStatus: 'ready',
    },
  });
  return <Story />;
};

// ---------------------------------------------------------------------------
// Default decorator — ensures chat panel is closed for non-chat frames.
// ---------------------------------------------------------------------------
const chatClosedDecorator: Decorator = Story => {
  useChatPanelStore.setState({ isOpen: false, gameContext: null });
  return <Story />;
};

// ---------------------------------------------------------------------------
// Meta
// ---------------------------------------------------------------------------

const meta: Meta<typeof GamebookPlayShell> = {
  title: 'Pages/Librogame/Play Session',
  component: GamebookPlayShell,
  parameters: {
    layout: 'fullscreen',
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: `/library/${MOCK_GAME_ID}/play/${MOCK_CAMPAIGN_ID}`,
        query: {},
      },
    },
    docs: {
      description: {
        component:
          'Pixel-faithful matrix di librogame-runthrough-play-session.html 4 stati. ' +
          'Tutti i frame usano la vera GamebookPlayShell MSW-driven — ' +
          'GET /gamebook/campaigns/{id} è obbligatorio o il componente mostra lo skeleton. ' +
          'Frame02 mostra il BookPicker (2 libri narrativi). ' +
          'Frame03 usa un Zustand decorator per simulare il chat panel aperto. ' +
          'Frame04 mostra la history dei paragrafi visitati (10 voci).',
      },
    },
    // All 4 frames cover the canonical `default` (steady-state, loaded) state via
    // `mswForLibrogamePlaySession*` handlers, not the literal `mswForState('default')`
    // — lint:storybook-states heuristic can't see it (#2342 Task 4 bonifica). Only
    // `default` is declared in fidelity.json for this mockup.
    canonicalStates: ['default'],
  },
  decorators: [chatClosedDecorator],
  argTypes: {
    campaignId: {
      control: 'text',
      description: 'Campaign UUID — used in GET /gamebook/campaigns/{id}.',
    },
    gameId: {
      control: 'text',
      description: 'Game UUID — passed to "Apri chat" context + "Traduci pagina" link.',
    },
    gameRef: {
      control: 'object',
      description: 'GameRef {id, kind} for useGameBooks. kind=0 = Shared.',
    },
  },
  args: {
    campaignId: MOCK_CAMPAIGN_ID,
    gameId: MOCK_GAME_ID,
    gameRef: { id: MOCK_GAME_ID, kind: 0 },
  },
};
export default meta;

type Story = StoryObj<typeof GamebookPlayShell>;

// ── Frame 01 · Story Tab — §289 loaded, single book auto-selected ─────────────

export const Frame01_StoryTab: Story = {
  name: '01 · Story Tab — §289 caricato, libro singolo auto-selezionato',
  parameters: {
    msw: {
      handlers: mswForLibrogamePlaySessionState,
    },
    docs: {
      description: {
        story:
          'Stato 01 mockup. Aaron al §289 Cap 4 "La grotta dei Goblin". ' +
          'Campagna "Con i ragazzi — Eldoria" caricata via MSW. ' +
          'Un solo libro narrativo → BookPicker nascosto, submit abilitato. ' +
          'History degli ultimi 7 paragrafi visitati visibile sotto il form. ' +
          'Bottone "Apri chat con agente" + link "Traduci pagina" presenti nell\'header.',
      },
    },
  },
};

// ── Frame 02 · Encounter Tab — 2 narrative books → BookPicker visible ─────────

export const Frame02_EncounterTab: Story = {
  name: '02 · Encounter Tab — 2 libri narrativi, BookPicker visibile',
  parameters: {
    msw: {
      handlers: mswForLibrogamePlaySessionMultiBook,
    },
    docs: {
      description: {
        story:
          'Stato 02 mockup. Stessa campagna §289 con 2 libri narrativi (Storybook + Encounter Book). ' +
          'Il BookPicker mostra il selettore di libri perché narrativeBooks.length > 1. ' +
          'Utente deve scegliere il libro prima di poter salvare il paragrafo. ' +
          'Maps to mockup encounter-tab state dove il contesto Encounter Book è attivo.',
      },
    },
  },
};

// ── Frame 03 · Chat Overlay — Zustand decorator: chat panel isOpen=true ───────

export const Frame03_ChatOverlay: Story = {
  name: '03 · Chat Overlay — chat panel Zustand pre-seeded: isOpen=true',
  decorators: [chatOpenDecorator],
  parameters: {
    msw: {
      handlers: mswForLibrogamePlaySessionState,
    },
    docs: {
      description: {
        story:
          'Stato 03 mockup. Chat panel pre-aperto via Zustand decorator ' +
          '(useChatPanelStore.setState({isOpen:true, gameContext:{...}})). ' +
          'Il pulsante "Apri chat con agente" nell\'header riflette il contesto gioco attivo. ' +
          'Il game context è pre-seeded con gameId e kbStatus=ready. ' +
          'Maps to mockup chat-overlay state dove il tutor è attivo.',
      },
    },
  },
};

// ── Frame 04 · Glossary Inline — 10-entry history list ───────────────────────

export const Frame04_GlossaryInline: Story = {
  name: '04 · Glossary Inline — 10 paragrafi visitati nella history list',
  parameters: {
    msw: {
      handlers: mswForLibrogamePlaySessionRichHistory,
    },
    docs: {
      description: {
        story:
          'Stato 04 mockup. Stessa campagna con 10 paragrafi visitati nella history ' +
          '(§100 → §112 → … → §289). Il testo "Storia: 100 -> 112 -> … -> 289" ' +
          'è visibile sotto il form grazie a data.history.length > 0. ' +
          'Maps to mockup glossary-inline state dove il rich context panel è espanso.',
      },
    },
  },
};
