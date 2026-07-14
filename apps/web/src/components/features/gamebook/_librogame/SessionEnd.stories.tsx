/**
 * @mockup admin-mockups/design_files/librogame-runthrough-session-end.html
 *
 * SessionEnd story — DS-17 Phase D-2 Task 12 (sub-issue #2174).
 *
 * There is NO real standalone component for the session-end outcome modal
 * (confirmed during planning). The modal lives inside an overlay store and
 * is not exposed independently. This story uses a Template K presentational
 * mock `SessionEndMock` that renders the full modal UI from the mockup,
 * driven by a single `outcome` prop.
 *
 * The modal overlays the play session screen and presents the session result
 * with a hero outcome card, stats tiles, ManaPips pip row (kb · chat · player
 * · event counts), and outcome-specific CTAs.
 * Layout: `centered` (the mock renders the modal dialog within a dimmed
 * backdrop, faithfully replicating the overlay pattern).
 *
 * 4 mockup states → 4 story frames (all Template K forward-refactor):
 *
 *   Frame01_Paused    — save & exit, session paused, resume-card with pulse indicator
 *   Frame02_Victory   — campaign complete, highlights timeline, share + new-campaign CTAs
 *   Frame03_Defeat    — party wipe, checkpoint warning, retry CTA
 *   Frame04_Cancelled — abandon confirmation, destructive warning, archive impact ManaPips
 *
 * State-driving strategy (STATIC — `outcome` prop, no play functions):
 *   Each frame passes a different `outcome` value to `SessionEndMock`.
 *   The mock renders the correct modal content statically on first paint.
 *   The snapshot harness (`apps/web/e2e/storybook/librogame.snapshot.spec.ts`)
 *   does `goto + waitForTimeout(2000) + screenshot` — it never runs play.
 *
 * Canonical states mapping note:
 *   Mockup "Stato 01 · Paused"    → canonical entity "session" (active, paused state)
 *   Mockup "Stato 02 · Victory"   → canonical entity "session" (completed, success outcome)
 *   Mockup "Stato 03 · Defeat"    → canonical entity "session" (ended, failure outcome)
 *   Mockup "Stato 04 · Cancelled" → canonical entity "session" (abandoned, destructive action)
 *   fidelity.json `states` uses semantic outcome names from the mockup.
 *
 * BGG guard: no BGG refs in this story or mock (#2123). Internal campaign
 * framing only (campaign name "Campagna con i ragazzi", game "Eldoria").
 *
 * Refs: spec docs/superpowers/specs/2026-06-22-ds-17-phase-d-2-librogame-storybook-migration-design.md,
 *       umbrella #2063, sub-issue #2174 (Phase D-2).
 */

import {
  SessionEndMock,
  type SessionEndMockOutcome,
} from '@/__tests__/fixtures/mockup-pilots/librogame/_mocks/SessionEndMock';

import type { Meta, StoryObj } from '@storybook/react';

// ---------------------------------------------------------------------------
// Meta — component IS the mock (Template K: no real component exists)
// ---------------------------------------------------------------------------

const meta: Meta<typeof SessionEndMock> = {
  title: 'Pages/Librogame/Session End',
  component: SessionEndMock,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Pixel-faithful matrix di librogame-runthrough-session-end.html 4 stati. ' +
          'Template K forward-refactor: nessun componente reale per il session-end modal. ' +
          'SessionEndMock renderizza il modal overlay con hero outcome card, stats tiles, ' +
          'ManaPips pip row (kb · chat · player · event) e CTA outcome-specific. ' +
          'Sostituire con il vero SessionEndModal quando estratto dallo overlay store.',
      },
    },
    // fidelity.json `_state_mapping_note` documents the intentional canonical mapping:
    // default = Frame01_Paused + Frame02_Victory (happy-path outcomes), error =
    // Frame03_Defeat + Frame04_Cancelled (failure/destructive outcomes). Driven via
    // `outcome` prop values (paused/victory/defeat/cancelled), not canonical string
    // literals — lint:storybook-states heuristic can't see them (#2342 Task 4 bonifica).
    canonicalStates: ['default', 'error'],
  },
  args: {
    outcome: 'paused' as SessionEndMockOutcome,
    campaignName: 'Campagna con i ragazzi',
    onPrimary: undefined,
    onSecondary: undefined,
  },
  argTypes: {
    outcome: {
      control: { type: 'select' },
      options: ['paused', 'victory', 'defeat', 'cancelled'] satisfies SessionEndMockOutcome[],
      description: 'Quale stato del mockup renderizzare. Drives the modal content statically.',
    },
    campaignName: {
      control: 'text',
      description: 'Nome della campagna mostrato nel hero subtitle.',
    },
    onPrimary: {
      action: 'onPrimary',
      description: 'Callback per il pulsante CTA primario (outcome-dipendente).',
    },
    onSecondary: {
      action: 'onSecondary',
      description: 'Callback per il pulsante CTA secondario.',
    },
  },
};
export default meta;

type Story = StoryObj<typeof SessionEndMock>;

// ── Frame 01 · Paused — sessione in pausa, save & exit (mockup Stato 01) ─────

export const Frame01_Paused: Story = {
  name: '01 · Paused — sessione salvata, resume disponibile (mockup Stato 01)',
  args: {
    outcome: 'paused',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Mockup Stato 01 (paused): Aaron preme "esci" durante la sessione. ' +
          'Hero session-color con icona ⏸️ e tag "In pausa". ' +
          'Resume card con pulse-dot session-color: "Pronto a riprendere · Capitolo 4 in corso". ' +
          'Statistiche sessione 2×2 grid: 12 Q&A regole · 8 Paragrafi · 3 Encounter · 2 Foto traduzione. ' +
          'ManaPips: kb 142 · chat 12 · player 4 · event 3. ' +
          'Footer: "Riepilogo" (secondary) + "Riprendi più tardi →" (primary session-color).',
      },
    },
  },
};

// ── Frame 02 · Victory — campagna completata, highlights + stats finali ───────

export const Frame02_Victory: Story = {
  name: '02 · Victory — campagna completata, highlights timeline, CTAs (mockup Stato 02)',
  args: {
    outcome: 'victory',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Mockup Stato 02 (victory): Aaron e party completano la storyline. ' +
          'Hero success-color con icona 🏆 e tag "Vittoria". ' +
          'Highlights timeline 4 righe: Cap 2 Patto Goblin · Cap 4 Boss Architetto · Cap 6 Glossario · Epil. Finale buono. ' +
          'Stats finali 2×2 grid: 7 Sessioni · 612 Paragrafi · 89 Q&A · 22 Encounter. ' +
          'ManaPips: kb 142 · chat 89 · session 7 · event 22. ' +
          'Footer: "📤 Condividi" (secondary) + "📖 Nuova campagna →" (primary success-color).',
      },
    },
  },
};

// ── Frame 03 · Defeat — party wipe, checkpoint warning, retry ─────────────────

export const Frame03_Defeat: Story = {
  name: '03 · Defeat — party sconfitto, checkpoint disponibile, retry CTA (mockup Stato 03)',
  args: {
    outcome: 'defeat',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Mockup Stato 03 (defeat): boss letale, party sconfitto. ' +
          'Hero danger-color con icona 💀 e tag "Sconfitta". ' +
          'Checkpoint warning box warning-color: ultima safe area §412 · Accampamento (12 min fa). ' +
          'Sessione corrente 2×2 grid: 33 Paragrafi · 5 Encounter · 8 Q&A · 1 Foto trad. ' +
          'Nessun ManaPips (sessione interrotta prima della fine). ' +
          'Footer: "Termina campagna" (secondary) + "⟲ Riprendi dal checkpoint" (primary warning-color).',
      },
    },
  },
};

// ── Frame 04 · Cancelled — abbandono campagna, conferma destructive ────────────

export const Frame04_Cancelled: Story = {
  name: '04 · Cancelled — abbandono campagna, conferma destructive + archive impact (mockup Stato 04)',
  args: {
    outcome: 'cancelled',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Mockup Stato 04 (cancelled): Aaron sceglie "Termina campagna" → conferma esplicita. ' +
          'Hero neutral con icona ⚠️ e tag "Conferma" (border outline). ' +
          'Destructive warning danger-color: 🚨 "Azione irreversibile" + archivio read-only. ' +
          'Due ManaPips row: (1) Persistenti: kb 142 · glossario 34; ' +
          '(2) Archiviati danger-tint: progresso 445 · sessioni 4. ' +
          'Footer: "Annulla" (secondary) + "Conferma e archivia" (primary danger-color).',
      },
    },
  },
};
