/**
 * Storybook coverage — /chat/agents/create (agent creation wizard).
 *
 * No page-mock exists for this route (MOCKUPS_INDEX § Chat is empty). Canonical-state
 * coverage stories rendering the REAL AgentCreationWizard against MSW fixtures,
 * mirroring chat/[threadId]/page.stories.tsx — no @mockup tag (no HTML twin).
 *
 * The route page.tsx uses next/dynamic(ssr:false); the story imports AgentCreationWizard
 * directly to skip the dynamic-import spinner.
 *
 * Step 1 (GameCollectionPicker) fetches on mount:
 *   GET /library (paginated { items, ... }) — the user's game collection
 * The library GET drives the canonical state. States: default (collection) / empty
 * (LibraryEmpty) / loading (skeleton). There is NO distinct error surface: the step-1
 * fetch does `.catch(console.error)` and collapses a 500 into the empty view — so `error`
 * is intentionally NOT declared as a canonical state here.
 * Fixtures MUST be Zod-valid (uuid v4 id/userId + RFC3339 datetimes with offset).
 */
import { http, HttpResponse } from 'msw';

import { AgentCreationWizard } from '@/components/chat-unified/AgentCreationWizard';

import type { Meta, StoryObj } from '@storybook/react';

const LIBRARY_URL = '*/api/v1/library';

const USER_ID = '7f3e9a1c-2b4d-4e6f-8a1b-0c2d4e6f8a1b';

/** PaginatedLibraryResponse — { items: UserLibraryEntry[], page, ... }. entry 1 has a KB. */
const LIBRARY = {
  items: [
    {
      id: '5e6f7a8b-9c0d-4e1f-8a2b-3c4d5e6f7a8b',
      userId: USER_ID,
      gameId: 'catan-seed-0001',
      gameTitle: 'Catan',
      gamePublisher: 'Kosmos',
      gameYearPublished: 1995,
      gameIconUrl: null,
      gameImageUrl: null,
      coverUrl: null,
      addedAt: '2026-05-20T14:00:00.000Z',
      notes: null,
      isFavorite: true,
      currentState: 'Owned',
      stateChangedAt: null,
      stateNotes: null,
      hasKb: true,
      kbCardCount: 2,
      kbIndexedCount: 2,
      kbProcessingCount: 0,
      ownershipDeclaredAt: '2026-05-20T14:05:00.000Z',
      hasRagAccess: true,
      agentIsOwned: true,
      minPlayers: 3,
      maxPlayers: 4,
      playingTimeMinutes: 75,
      complexityRating: 2.3,
      averageRating: 7.1,
      timesPlayed: 5,
      lastPlayed: '2026-07-10T20:30:00.000Z',
      privateGameId: null,
      isPrivateGame: false,
      canProposeToCatalog: false,
    },
    {
      id: '6f7a8b9c-0d1e-4f2a-9b3c-4d5e6f7a8b9c',
      userId: USER_ID,
      gameId: 'wingspan-seed-0002',
      gameTitle: 'Wingspan',
      gamePublisher: 'Stonemaier Games',
      gameYearPublished: 2019,
      gameIconUrl: null,
      gameImageUrl: null,
      coverUrl: null,
      addedAt: '2026-06-02T11:15:00.000Z',
      notes: 'Espansione Oceania da provare',
      isFavorite: false,
      currentState: 'Nuovo',
      stateChangedAt: null,
      stateNotes: null,
      hasKb: false,
      kbCardCount: 0,
      kbIndexedCount: 0,
      kbProcessingCount: 0,
      ownershipDeclaredAt: null,
      hasRagAccess: false,
      agentIsOwned: true,
      minPlayers: 1,
      maxPlayers: 5,
      playingTimeMinutes: 60,
      complexityRating: 2.4,
      averageRating: 8.0,
      timesPlayed: 0,
      lastPlayed: null,
      privateGameId: null,
      isPrivateGame: false,
      canProposeToCatalog: false,
    },
  ],
  page: 1,
  pageSize: 100,
  totalCount: 2,
  totalPages: 1,
  hasNextPage: false,
  hasPreviousPage: false,
};

const emptyLibrary = {
  items: [],
  page: 1,
  pageSize: 100,
  totalCount: 0,
  totalPages: 0,
  hasNextPage: false,
  hasPreviousPage: false,
};

const meta: Meta<typeof AgentCreationWizard> = {
  title: 'Authenticated / chat-agent-create',
  component: AgentCreationWizard,
  parameters: {
    layout: 'fullscreen',
    canonicalStates: ['default', 'empty', 'loading'],
    nextjs: { appDirectory: true, navigation: { pathname: '/chat/agents/create' } },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          'Agent creation wizard (step 1 = game collection picker). Default = collection ' +
          'grid; Empty = LibraryEmpty; Loading = skeleton. No error state (a 500 collapses ' +
          'to empty, by design).',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof AgentCreationWizard>;

/** Default: game collection loaded (step 1). */
export const Default: Story = {
  parameters: {
    msw: { handlers: [http.get(LIBRARY_URL, () => HttpResponse.json(LIBRARY))] },
  },
};

/** Empty: no games in the collection → LibraryEmpty. */
export const Empty: Story = {
  parameters: {
    msw: { handlers: [http.get(LIBRARY_URL, () => HttpResponse.json(emptyLibrary))] },
  },
};

/** Loading: library GET never resolves — collection skeleton. */
export const Loading: Story = {
  parameters: {
    msw: { handlers: [http.get(LIBRARY_URL, () => new Promise<Response>(() => {}))] },
  },
};
