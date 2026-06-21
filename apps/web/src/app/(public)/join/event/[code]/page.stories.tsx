/**
 * @mockup admin-mockups/design_files/sp7-game-night-join-public.jsx
 *
 * Public Game Night RSVP (anonymous /join/event/[code]) argTypes matrix story —
 * DS-17 Phase D-1 (sub-issue #2174).
 *
 * Multi-state mockup canonical pick (P245):
 *   The mockup is the public, anonymous variant of `sp7-game-night-detail-rsvp`
 *   (host/invitee authenticated). 6 states + 2 FSM-derived states are covered
 *   via MSW handlers on the public invitation endpoints:
 *     - GET  /api/v1/game-nights/invitations/{token}
 *     - POST /api/v1/game-nights/invitations/{token}/respond
 *
 *   HERO COMPONENT: `PublicJoinEventView` (the page-client at
 *   apps/web/src/app/(public)/join/event/[code]/_components/PublicJoinEventView.tsx)
 *   is the canonical render-mode hero. It wires `useGameNightInvitation` +
 *   `useRespondToInvitation` internally — story drives via MSW handlers
 *   per state.
 *
 *   DO NOT create separate story files per FSM variant; cover via the
 *   `state` axis (Sp7JoinPublicState union). Mirrors DS-17-9 auth-flow pattern.
 *
 * Stage axis (sp7-game-night-join-public.jsx STATES — 6 frames + 2 FSM states):
 *   pending-empty / pending-filled / submitting / already-responded /
 *   token-expired / rate-limited / token-invalid / loading
 *
 * Variant simplifications vs sp7-game-night-detail-rsvp:
 *   - NO admin controls (edit, cancel, voting tools)
 *   - NO "Invite more" / share actions
 *   - NO Maybe button (backend public POST validator accepts only Accepted/Declined)
 *   - NO ConnectionBar (public route is not real-time SSE)
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-11-sp6-7-nano-cluster-design.md,
 *       umbrella #2063, sub-issue #2174 (Phase D-1).
 */

import {
  mswForSp7JoinPublicState,
  MOCK_SP7_JOIN_PUBLIC_TOKEN,
} from '@/__tests__/fixtures/mockup-pilots/sp6-7-nano/sp7-game-night-join-public';
import { PublicJoinEventView } from '@/app/(public)/join/event/[code]/_components/PublicJoinEventView';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof PublicJoinEventView> = {
  title: 'Pages/SP7/Game Night Join Public',
  component: PublicJoinEventView,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Pixel-faithful matrix di sp7-game-night-join-public.jsx 6 stati pubblici (no host controls, no SSE) + 2 FSM states (loading, token-invalid). Hero usa `PublicJoinEventView` (page-client `/join/event/[code]`). Tutte le frame coperte via MSW handlers su `/api/v1/game-nights/invitations/{token}` + `/respond`. Variant `*-public` del sp7-game-night-detail-rsvp.',
      },
    },
  },
  argTypes: {
    code: {
      control: 'text',
      description:
        'Opaque invitation token (drives the useGameNightInvitation query — MSW handlers per state).',
    },
  },
  args: {
    code: MOCK_SP7_JOIN_PUBLIC_TOKEN,
  },
};
export default meta;

type Story = StoryObj<typeof PublicJoinEventView>;

// ── Stage frame canonicals (mapped 1:1 ai 6 frame mockup) ──────────────────

export const Frame01_PendingEmpty: Story = {
  name: '01 · Pending · form pulito, nessun displayName, idle',
  parameters: { msw: { handlers: mswForSp7JoinPublicState('pending-empty') } },
};

export const Frame02_PendingFilled: Story = {
  name: "02 · Pending · user digita displayName 'Marco', idle",
  parameters: {
    msw: { handlers: mswForSp7JoinPublicState('pending-filled') },
    docs: {
      description: {
        story:
          'Documentation-only frame. The "filled" affordance is a user-interaction seed (typing "Marco" in the displayName input). Story renders identical baseline to Frame 01 — the parent `PublicRsvpForm` owns the input state.',
      },
    },
  },
};

export const Frame03_Submitting: Story = {
  name: '03 · Submitting · POST in flight, Accept button spinner',
  parameters: {
    msw: { handlers: mswForSp7JoinPublicState('submitting') },
    docs: {
      description: {
        story:
          'In-flight submitting state — POST is mocked as `delay("infinite")` so the spinner pins to the Accept button when the user clicks Confermo.',
      },
    },
  },
};

export const Frame04_AlreadyResponded: Story = {
  name: "04 · Already responded · 200 OK con respondedByName='Marco'",
  parameters: { msw: { handlers: mswForSp7JoinPublicState('already-responded') } },
};

export const Frame05_TokenExpired: Story = {
  name: '05 · GET 410 (Expired) — terminal banner',
  parameters: { msw: { handlers: mswForSp7JoinPublicState('token-expired') } },
};

export const Frame06_RateLimited: Story = {
  name: '06 · 429 Rate-limited con Retry-After countdown',
  parameters: { msw: { handlers: mswForSp7JoinPublicState('rate-limited') } },
};

// ── State variant frames (FSM completeness — axis = LoadState) ─────────────

export const StateTokenInvalid: Story = {
  name: 'State · Token invalid (404 not found)',
  parameters: { msw: { handlers: mswForSp7JoinPublicState('token-invalid') } },
};

export const StateLoading: Story = {
  name: 'State · Loading (initial fetch)',
  parameters: { msw: { handlers: mswForSp7JoinPublicState('loading') } },
};
