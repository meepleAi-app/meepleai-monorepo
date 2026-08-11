/**
 * @mockup admin-mockups/design_files/chat-fullscreen.html
 *
 * chat-fullscreen — DS-17 residual MIGRATE (#2971, umbrella #2063).
 *
 * Route /chat/[threadId] → ChatThreadView (chat-unified). On mount:
 * getThreadById → GET /api/v1/chat-threads/:id → ChatThreadDto (Zod-validated).
 * DEC-A5 states: default (thread w/ messages) / empty (thread, no messages) /
 * loading (spinner) / error (500). `sse` (streaming answer) needs a play
 * interaction + ReadableStream mock + gameId-gated branch → out of scope here.
 *
 * The Default/Empty GET fixtures MUST be ChatThreadDtoSchema-valid (uuid ids +
 * RFC3339 datetimes WITH offset) or the Zod parse drops silently into error.
 */
import { http, HttpResponse } from 'msw';

import { ChatThreadView } from '@/components/chat-unified/ChatThreadView';

import type { Meta, StoryObj } from '@storybook/react';

const THREAD_ID = '22222222-2222-4222-8222-222222222222';
const THREAD_URL = '*/api/v1/chat-threads/:id';

/** ChatThreadDtoSchema-valid fixture (gameId null = dependency-free). */
const BASE_THREAD = {
  id: THREAD_ID,
  gameId: null,
  agentId: null,
  agentType: null,
  title: 'Regole di Catan',
  createdAt: '2026-07-15T10:00:00.000Z',
  lastMessageAt: '2026-07-15T10:05:00.000Z',
  messageCount: 2,
  messages: [
    { content: 'Come funziona il ladro?', role: 'user', timestamp: '2026-07-15T10:04:00.000Z' },
    {
      content: 'Il ladro blocca la produzione della casella su cui viene spostato.',
      role: 'assistant',
      timestamp: '2026-07-15T10:05:00.000Z',
    },
  ],
};

const meta: Meta<typeof ChatThreadView> = {
  title: 'Authenticated / chat-fullscreen',
  component: ChatThreadView,
  args: { threadId: THREAD_ID },
  parameters: {
    layout: 'fullscreen',
    // DS-17 #2063: on-mount http.get (no quoted state literal) → declare states.
    canonicalStates: ['default', 'empty', 'loading', 'error'],
    nextjs: { appDirectory: true, navigation: { pathname: `/chat/${THREAD_ID}` } },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2971 DS-17 MIGRATE. Fullscreen chat thread. Default = loaded thread with ' +
          'messages; Empty = thread with no messages; Loading = spinner; Error = 500.',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof ChatThreadView>;

/** Default: loaded thread with a user + assistant message. */
export const Default: Story = {
  parameters: {
    msw: { handlers: [http.get(THREAD_URL, () => HttpResponse.json(BASE_THREAD))] },
  },
};

/** Empty: thread exists but has no messages yet. */
export const Empty: Story = {
  parameters: {
    msw: {
      handlers: [
        http.get(THREAD_URL, () =>
          HttpResponse.json({ ...BASE_THREAD, messageCount: 0, messages: [] })
        ),
      ],
    },
  },
};

/** Loading: GET never resolves — shows the spinner. */
export const Loading: Story = {
  parameters: {
    msw: { handlers: [http.get(THREAD_URL, () => new Promise(() => {}))] },
  },
};

/** Error: GET returns 500 → error state. */
export const Error: Story = {
  parameters: {
    msw: {
      handlers: [
        http.get(THREAD_URL, () => HttpResponse.json({ error: 'server_error' }, { status: 500 })),
      ],
    },
  },
};
