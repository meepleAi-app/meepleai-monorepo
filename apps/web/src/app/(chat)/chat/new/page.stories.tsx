/**
 * Storybook coverage — /chat/new (new-chat entry).
 *
 * No page-mock exists for this route (MOCKUPS_INDEX § Chat is empty). Canonical-state
 * coverage stories rendering the REAL ChatEntryOrchestrator against MSW fixtures,
 * mirroring chat/[threadId]/page.stories.tsx — no @mockup tag (no HTML twin).
 *
 * The route page.tsx uses next/dynamic(ssr:false); the story imports the underlying
 * ChatEntryOrchestrator directly to skip the dynamic-import spinner (same trick the
 * [threadId] story uses with ChatThreadView).
 *
 * On mount the GameSelector fetches:
 *   GET /private-games (paginated { items, ... }) — private games grid (on-mount)
 *   GET /library       (paginated) — shared-library tab (lazy; handler declared anyway)
 * The private-games GET drives the canonical state. States: default (games) / empty /
 * loading / error. AgentSelector always renders the 5 system agents (auto default),
 * so the "Inizia Chat" CTA is enabled without selecting a game.
 * Fixtures MUST be Zod-valid (uuid v4 ownerId + RFC3339 datetimes with offset).
 */
import { http, HttpResponse } from 'msw';

import { ChatEntryOrchestrator } from '@/components/chat/entry';

import type { Meta, StoryObj } from '@storybook/react';

const PRIVATE_GAMES_URL = '*/api/v1/private-games';
const LIBRARY_URL = '*/api/v1/library';

const OWNER_ID = '7f3e9a1c-2b4d-4e6f-8a1b-0c2d4e6f8a1b';

/** PaginatedPrivateGamesResponse — { items: PrivateGameDto[], page, ... }. */
const PRIVATE_GAMES = {
  items: [
    {
      id: 'aa11bb22-cc33-dd44-ee55-ff6677889900',
      ownerId: OWNER_ID,
      source: 'Manual',
      bggId: null,
      title: 'Prototipo Casalingo',
      minPlayers: 2,
      maxPlayers: 4,
      yearPublished: 2025,
      description: 'Gioco autoprodotto per test.',
      playingTimeMinutes: 60,
      minAge: 10,
      complexityRating: 2.5,
      imageUrl: null,
      thumbnailUrl: null,
      createdAt: '2026-06-01T12:00:00.000Z',
      updatedAt: null,
      bggSyncedAt: null,
      canProposeToCatalog: false,
      agentDefinitionId: null,
    },
    {
      id: 'bb22cc33-dd44-ee55-ff66-778899001122',
      ownerId: OWNER_ID,
      source: 'BoardGameGeek',
      bggId: 174430,
      title: 'Gloomhaven (copia personale)',
      minPlayers: 1,
      maxPlayers: 4,
      yearPublished: 2017,
      description: null,
      playingTimeMinutes: 120,
      minAge: 14,
      complexityRating: 3.9,
      imageUrl: null,
      thumbnailUrl: null,
      createdAt: '2026-06-10T08:30:00.000Z',
      updatedAt: '2026-06-11T09:00:00.000Z',
      bggSyncedAt: '2026-06-10T08:30:05.000Z',
      canProposeToCatalog: true,
      agentDefinitionId: null,
    },
  ],
  page: 1,
  pageSize: 100,
  totalCount: 2,
  totalPages: 1,
  hasNextPage: false,
  hasPreviousPage: false,
};

const emptyPaginated = {
  items: [],
  page: 1,
  pageSize: 100,
  totalCount: 0,
  totalPages: 0,
  hasNextPage: false,
  hasPreviousPage: false,
};

/** Shared-library tab is lazy; keep it populated across states so a tab-switch isn't broken. */
const okLibrary = http.get(LIBRARY_URL, () => HttpResponse.json(emptyPaginated));

const meta: Meta<typeof ChatEntryOrchestrator> = {
  title: 'Authenticated / chat-new',
  component: ChatEntryOrchestrator,
  parameters: {
    layout: 'fullscreen',
    canonicalStates: ['default', 'empty', 'loading', 'error'],
    nextjs: { appDirectory: true, navigation: { pathname: '/chat/new' } },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          'New-chat entry. Default = private games grid + agent selector; Empty = no ' +
          'private games; Loading = games skeleton; Error = games load error alert.',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof ChatEntryOrchestrator>;

/** Default: private games grid loaded. */
export const Default: Story = {
  parameters: {
    msw: {
      handlers: [http.get(PRIVATE_GAMES_URL, () => HttpResponse.json(PRIVATE_GAMES)), okLibrary],
    },
  },
};

/** Empty: no private games. */
export const Empty: Story = {
  parameters: {
    msw: {
      handlers: [http.get(PRIVATE_GAMES_URL, () => HttpResponse.json(emptyPaginated)), okLibrary],
    },
  },
};

/** Loading: private-games GET never resolves — games skeleton. */
export const Loading: Story = {
  parameters: {
    msw: {
      handlers: [http.get(PRIVATE_GAMES_URL, () => new Promise<Response>(() => {})), okLibrary],
    },
  },
};

/** Error: private-games GET returns 500 → error alert. */
export const Error: Story = {
  parameters: {
    msw: {
      handlers: [
        http.get(PRIVATE_GAMES_URL, () =>
          HttpResponse.json({ error: 'server_error' }, { status: 500 })
        ),
        okLibrary,
      ],
    },
  },
};
