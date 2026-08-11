/**
 * @mockup admin-mockups/design_files/sp4-session-skeleton-live.html
 *
 * sp4-session-skeleton-live — DS-17 residual MIGRATE (#2971, umbrella #2063).
 *
 * Route /sessions/[id]/live → SessionLiveView (generic skeleton renderer, epic
 * #2354 G1 #2374). Primary loader: useLiveSession → GET /api/v1/live-sessions/:id
 * → LiveSessionDto (Zod-validated). FSM: loading → error → not-found(null) → default.
 * DEC-A5 states: default / loading / error.
 *
 * ⚠️ Storybook limitation (honest, documented): the Default state's secondary
 * cascade opens a native EventSource (SSE) to .../stream and a SignalR hub, which
 * MSW's Service Worker cannot mock. So Default renders the real 2-col layout WITH
 * a "reconnecting" ConnectionLostBanner overlay — the layout is faithful, the
 * connection banner is an environment artifact, not masking. Loading/Error render
 * the shell states cleanly.
 */
import { http, HttpResponse } from 'msw';

import SessionLivePage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const ID = '33333333-3333-4333-8333-333333333333';
const SESSION_URL = '*/api/v1/live-sessions/:id';

/** LiveSessionDtoSchema-valid fixture (empty rosters + empty scoring config). */
const VALID_SESSION = {
  id: ID,
  sessionCode: 'ABC123',
  gameId: '44444444-4444-4444-8444-444444444444',
  gameName: 'Catan',
  gameSlug: 'catan',
  createdByUserId: '55555555-5555-4555-8555-555555555555',
  status: 'InProgress',
  visibility: 'Private',
  groupId: null,
  createdAt: '2026-07-15T10:00:00.000Z',
  startedAt: '2026-07-15T10:01:00.000Z',
  pausedAt: null,
  completedAt: null,
  updatedAt: '2026-07-15T10:05:00.000Z',
  lastSavedAt: '2026-07-15T10:05:00.000Z',
  currentTurnIndex: 0,
  currentTurnPlayerId: null,
  agentMode: 'None',
  notes: null,
  players: [],
  teams: [],
  roundScores: [],
  scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
};

const meta: Meta<typeof SessionLivePage> = {
  title: 'Authenticated / sp4-session-skeleton-live',
  component: SessionLivePage,
  parameters: {
    layout: 'fullscreen',
    // DS-17 #2063: http.get-driven FSM (no quoted state literal) → declare states.
    canonicalStates: ['default', 'loading', 'error'],
    nextjs: {
      appDirectory: true,
      // @storybook/nextjs mocks useParams() from navigation.segments (key-value
      // tuples), NOT `params`. SessionLiveView reads useParams().id → resolveSessionId.
      navigation: { pathname: `/sessions/${ID}/live`, segments: [['id', ID]] },
    },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2971 DS-17 MIGRATE. Generic live-session skeleton renderer. Default = loaded ' +
          'layout (SSE/SignalR not mockable → reconnecting banner overlay); Loading = shell ' +
          'skeleton; Error = error shell.',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof SessionLivePage>;

/** Default: loaded session layout (with a reconnecting banner — SSE unmockable). */
export const Default: Story = {
  parameters: {
    msw: { handlers: [http.get(SESSION_URL, () => HttpResponse.json(VALID_SESSION))] },
  },
};

/** Loading: GET never resolves — shows the loading shell. */
export const Loading: Story = {
  parameters: {
    msw: { handlers: [http.get(SESSION_URL, () => new Promise(() => {}))] },
  },
};

/** Error: GET returns 500 → error shell. */
export const Error: Story = {
  parameters: {
    msw: {
      handlers: [
        http.get(SESSION_URL, () => HttpResponse.json({ error: 'server_error' }, { status: 500 })),
      ],
    },
  },
};
