/**
 * @mockup admin-mockups/design_files/sp7-game-night-detail-rsvp.html
 *
 * Game Night Detail + RSVP argTypes matrix story — DS-17 Phase C-1 (sub-issue #2166).
 *
 * Multi-route mockup canonical pick (P245 — CRITICAL):
 *   The mockup covers a SINGLE Next.js route (`/game-nights/[id]`) with MANY
 *   internal state variants driven by:
 *     - `viewer`: 'host' | 'invitee'  (UI role from `actor` in useGameNightDetail)
 *     - `status`: GameNight status (Draft / Published / Cancelled / Completed)
 *     - `tab`:    'detail' | 'voting' | 'chat'
 *     - `rsvpKey`/`viewerRSVP`: invitee response state
 *
 *   HERO COMPONENT: `GameNightDetailView` (the page-client at
 *   apps/web/src/app/(authenticated)/game-nights/[id]/_components/GameNightDetailView.tsx)
 *   is the canonical render-mode hero. It wires `useGameNightDetail(id, viewer?.id)`
 *   internally, so the story drives it via MSW handlers per viewer/status combo.
 *
 *   DO NOT create separate story files per RSVP state variant; cover via
 *   argTypes axis (`'host' | 'guest-pending' | 'guest-confirmed' | 'guest-cancelled' | 'guest-reschedule'`).
 *   Mirrors DS-17-9 auth-flow pattern (LoginForm canonical w/ state variants for 8 routes).
 *
 * Stage axis (sp7-game-night-detail-rsvp.jsx STATES array lines 1346-1367 —
 * 10 Mobile frames + 2 Desktop frames = 12 frames total):
 *   state: 1..12 (host-pending, host-ready, invitee-pending, invitee-confirmed,
 *                 voting-active, voting-tied, cancelled, in-progress, completed,
 *                 mobile-tab-chat, desktop-split-detail, desktop-split-voting)
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-11-sp6-7-nano-cluster-design.md,
 *       umbrella #2063, sub-issue #2166.
 *
 * NOTE: this is a SCAFFOLD draft. Move to
 *   apps/web/src/app/(authenticated)/game-nights/[id]/page.stories.tsx
 * when wiring into the Storybook tree.
 */

import { mswForSp7DetailRsvpState } from '@/__tests__/fixtures/mockup-pilots/sp6-7-nano/sp7-game-night-detail-rsvp';
import { GameNightDetailView } from '@/app/(authenticated)/game-nights/[id]/_components/GameNightDetailView';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof GameNightDetailView> = {
  title: 'Pages/SP7/Game Night Detail RSVP',
  component: GameNightDetailView,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Pixel-faithful matrix di sp7-game-night-detail-rsvp.jsx Mobile frames 01-10 + Desktop split-view 11-12. Hero usa GameNightDetailView (page-client). Tutte le 12 frame coperte via argTypes state axis: host vs invitee, status (Published/Cancelled/InProgress/Completed), tab (detail/voting/chat), RSVP transitions. ZERO separate story files per RSVP state variant per P245.',
      },
    },
    // The 12 Frame* stories (host-pending/host-ready/invitee-*/voting-*/cancelled/
    // in-progress/completed/mobile-tab-chat/desktop-split-*) all cover the canonical
    // `default` (steady-state, loaded) state via `mswForSp7DetailRsvpState(<mockup
    // stage name>)`, not the literal `mswForState('default')` — lint:storybook-states
    // heuristic can't see it (#2342 Task 4 bonifica). `Loading`/`ErrorState` already
    // detected via `mswForSp7DetailRsvpState('loading'|'error')` literals.
    canonicalStates: ['default', 'loading', 'error'],
  },
  argTypes: {
    id: {
      control: 'text',
      description: 'Game Night ID (drives the useGameNightDetail query — MSW handlers per state).',
    },
  },
  args: {
    id: 'gn-padovani-may17',
  },
  decorators: [
    Story => (
      <div className="min-h-dvh bg-background">
        <Story />
      </div>
    ),
  ],
};
export default meta;

type Story = StoryObj<typeof GameNightDetailView>;

// ── Stage frame canonicals (mapped 1:1 ai 12 frame mockup — Desktop primary;
// Mobile via viewport sweep Phase 4) ────────────────────────────────────────

export const Frame01_HostPending: Story = {
  name: '01 · Host · 3/8 confermati · voting attivo',
  args: { id: 'gn-padovani-may17' },
  parameters: { msw: { handlers: mswForSp7DetailRsvpState('host-pending') } },
};

export const Frame02_HostReady: Story = {
  name: '02 · Host · 8/8 confermati · "Avvia sessione live"',
  args: { id: 'gn-padovani-may17' },
  parameters: { msw: { handlers: mswForSp7DetailRsvpState('host-ready') } },
};

export const Frame03_InviteePending: Story = {
  name: '03 · Davide invitee · pending · RSVP CTA bottom-sheet',
  args: { id: 'gn-padovani-may17' },
  parameters: { msw: { handlers: mswForSp7DetailRsvpState('invitee-pending') } },
};

export const Frame04_InviteeConfirmed: Story = {
  name: '04 · Davide post-tap "Ci sarò" · celebration micro',
  args: { id: 'gn-padovani-may17' },
  parameters: { msw: { handlers: mswForSp7DetailRsvpState('invitee-confirmed') } },
};

export const Frame05_VotingActive: Story = {
  name: '05 · Tab Voting · 7 voti · Twilight in testa',
  args: { id: 'gn-padovani-may17' },
  parameters: {
    msw: { handlers: mswForSp7DetailRsvpState('voting-active') },
    docs: {
      description: {
        story:
          'Documentation-only frame. Tab Voting in mockup line 1356 — current page-client does NOT yet expose tab routing (decision 2a — host-only Draft branch). Real component target: GameVoteCard + VotingTiedResolver (post-MVP).',
      },
    },
  },
};

export const Frame06_VotingTied: Story = {
  name: '06 · Tab Voting · pareggio 3-3 · host scegli (-1h)',
  args: { id: 'gn-padovani-may17' },
  parameters: {
    msw: { handlers: mswForSp7DetailRsvpState('voting-tied') },
    docs: {
      description: {
        story: 'Documentation-only frame. Tie-breaker resolver — post-MVP feature.',
      },
    },
  },
};

export const Frame07_Cancelled: Story = {
  name: '07 · Cancellata · banner danger + crea nuova',
  args: { id: 'gn-padovani-may17' },
  parameters: { msw: { handlers: mswForSp7DetailRsvpState('cancelled') } },
};

export const Frame08_InProgress: Story = {
  name: '08 · In corso · session indigo pulsing · "Apri sessione"',
  args: { id: 'gn-padovani-may17' },
  parameters: { msw: { handlers: mswForSp7DetailRsvpState('in-progress') } },
};

export const Frame09_Completed: Story = {
  name: '09 · Completata · "Registra play record" success',
  args: { id: 'gn-padovani-may17' },
  parameters: { msw: { handlers: mswForSp7DetailRsvpState('completed') } },
};

export const Frame10_MobileTabChat: Story = {
  name: '10 · Mobile · Tab Chat fullscreen + input sticky',
  args: { id: 'gn-padovani-may17' },
  parameters: {
    msw: { handlers: mswForSp7DetailRsvpState('host-pending') },
    docs: {
      description: {
        story:
          'Documentation-only frame. Tab Chat — current page-client does NOT yet expose tab routing. Real component target: GameNightChatStream + sticky input (post-MVP).',
      },
    },
  },
};

export const Frame11_DesktopSplitDetail: Story = {
  name: '11 · Desktop · Split-view sidebar 380 + Tab Dettagli',
  args: { id: 'gn-padovani-may17' },
  parameters: { msw: { handlers: mswForSp7DetailRsvpState('host-pending') } },
};

export const Frame12_DesktopSplitVoting: Story = {
  name: '12 · Desktop · Split-view sidebar 380 + Tab Voting',
  args: { id: 'gn-padovani-may17' },
  parameters: {
    msw: { handlers: mswForSp7DetailRsvpState('voting-active') },
    docs: {
      description: {
        story: 'Documentation-only frame. Same caveat as Frame 5 — Tab Voting post-MVP.',
      },
    },
  },
};

export const Frame13_HostDraftTagged: Story = {
  name: '13 · Host · Draft · giocatori taggati + "Invia inviti" (#2963, invariante #16)',
  args: { id: 'gn-padovani-may17' },
  parameters: {
    msw: { handlers: mswForSp7DetailRsvpState('host-draft-tagged') },
    docs: {
      description: {
        story:
          'Invariante #16 (tagged vs invited): su una serata Draft i giocatori sono *taggati* (aggiunti, nessun invito inviato) — le righe Pending mostrano "Da invitare" e l\'host vede la CTA "Invia inviti" (publish → Published). Solo dopo l\'invio le righe diventano "In attesa" (invitati). Vedi #2963.',
      },
    },
  },
};

// ── State variant frames (axis = LoadState) ────────────────────────────────

export const Loading: Story = {
  name: 'State · Loading (initial fetch)',
  args: { id: 'gn-padovani-may17' },
  parameters: { msw: { handlers: mswForSp7DetailRsvpState('loading') } },
};

export const ErrorState: Story = {
  name: 'State · Error (404 not found)',
  args: { id: 'gn-padovani-not-found' },
  parameters: { msw: { handlers: mswForSp7DetailRsvpState('error') } },
};
