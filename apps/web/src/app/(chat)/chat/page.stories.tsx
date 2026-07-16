/**
 * Storybook coverage — /chat (chat list).
 *
 * No page-mock exists for this route (MOCKUPS_INDEX § Chat is empty; a
 * chat-fullscreen page-mock is tracked by #2971). These are canonical-state
 * coverage stories that render the REAL ChatListPage against MSW fixtures,
 * mirroring the sibling pattern in chat/[threadId]/page.stories.tsx — hence no
 * @mockup tag (there is no HTML twin to mirror).
 *
 * Route /chat → ChatListPage. Auth-gated via useAuth (Storybook preview's
 * MockAuthProvider supplies user.id='storybook-user', so the queries run). On mount:
 *   GET /users/:userId/chat-sessions/recent → ChatSessionSummaryDto[] (bare array, Zod-validated)
 *   GET /users/:userId/chat-sessions/limit  → { limit, used, tier }
 * States: default (sessions) / empty / loading / error. The recent fixtures MUST be
 * Zod-valid (uuid v4 ids + RFC3339 datetimes WITH offset) or the client throws
 * SchemaValidationError and the query lands in error.
 */
import { http, HttpResponse } from 'msw';

import ChatListPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const RECENT_URL = '*/api/v1/users/:userId/chat-sessions/recent';
const LIMIT_URL = '*/api/v1/users/:userId/chat-sessions/limit';

/** ChatSessionSummaryDto[] — bare array (client wraps into { sessions, totalCount }). */
const RECENT_SESSIONS = [
  {
    id: '1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d',
    userId: '7f3e9a1c-2b4d-4e6f-8a1b-0c2d4e6f8a1b',
    gameId: 'catan-seed-0001',
    gameTitle: 'Catan',
    agentId: '3c4d5e6f-7a8b-4c9d-8e0f-1a2b3c4d5e6f',
    agentType: 'rules',
    agentName: 'Arbitro Regole',
    title: 'Setup iniziale a 4 giocatori',
    messageCount: 6,
    lastMessagePreview: 'Quindi il ladro si sposta sul deserto?',
    createdAt: '2026-07-15T09:12:00.000Z',
    lastMessageAt: '2026-07-15T09:41:00.000Z',
    isArchived: false,
  },
  {
    id: '2b3c4d5e-6f7a-4b8c-9d0e-1f2a3b4c5d6e',
    userId: '7f3e9a1c-2b4d-4e6f-8a1b-0c2d4e6f8a1b',
    gameId: 'wingspan-seed-0002',
    gameTitle: 'Wingspan',
    agentId: null,
    agentType: null,
    agentName: null,
    title: 'Dubbi punteggio uova',
    messageCount: 2,
    lastMessagePreview: 'Le uova valgono 1 punto ciascuna?',
    createdAt: '2026-07-14T18:03:00.000Z',
    lastMessageAt: '2026-07-14T18:07:30.000Z',
    isArchived: false,
  },
];

const LIMIT_LOW = { limit: 50, used: 3, tier: 'free' };
const LIMIT_NEAR_CAP = { limit: 50, used: 45, tier: 'free' };

/** Limit handler always resolves; the recent handler drives the canonical state. */
const okLimit = http.get(LIMIT_URL, () => HttpResponse.json(LIMIT_LOW));

const meta: Meta<typeof ChatListPage> = {
  title: 'Authenticated / chat-list',
  component: ChatListPage,
  parameters: {
    layout: 'fullscreen',
    // DS-17 #2063: on-mount http.get (no quoted state literal) → declare states.
    canonicalStates: ['default', 'empty', 'loading', 'error'],
    nextjs: { appDirectory: true, navigation: { pathname: '/chat' } },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          'Chat list. Default = grouped recent sessions; Empty = no sessions; ' +
          'Loading = skeleton; Error = 500. TierNearCap = quota banner (used/limit ≥ 0.8).',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof ChatListPage>;

/** Default: recent sessions grouped by agent. */
export const Default: Story = {
  parameters: {
    msw: {
      handlers: [http.get(RECENT_URL, () => HttpResponse.json(RECENT_SESSIONS)), okLimit],
    },
  },
};

/** Empty: no chat sessions yet. */
export const Empty: Story = {
  parameters: {
    msw: {
      handlers: [http.get(RECENT_URL, () => HttpResponse.json([])), okLimit],
    },
  },
};

/** Loading: recent GET never resolves — shows the skeleton. */
export const Loading: Story = {
  parameters: {
    msw: {
      handlers: [http.get(RECENT_URL, () => new Promise<Response>(() => {})), okLimit],
    },
  },
};

/** Error: recent GET returns 500 → error alert. */
export const Error: Story = {
  parameters: {
    msw: {
      handlers: [
        http.get(RECENT_URL, () => HttpResponse.json({ error: 'server_error' }, { status: 500 })),
        okLimit,
      ],
    },
  },
};

/** TierNearCap: sessions load AND the tier banner shows (45/50 = 0.9 ≥ 0.8). */
export const TierNearCap: Story = {
  parameters: {
    msw: {
      handlers: [
        http.get(RECENT_URL, () => HttpResponse.json(RECENT_SESSIONS)),
        http.get(LIMIT_URL, () => HttpResponse.json(LIMIT_NEAR_CAP)),
      ],
    },
  },
};
