/**
 * sp4-play-records-detail — DS-17-13 #2220.
 * Mockup parity: `admin-mockups/design_files/sp4-play-records-detail.{html,jsx}`.
 *
 * DEC-A5 states: default / empty (record w/ no scores) / loading / error (404). sse N/A.
 *
 * The detail page reads `useParams().id` — the `params: { id: ID }` in the nextjs
 * navigation block ensures Storybook resolves the dynamic segment correctly.
 * Default fetches fixture `pr-won-1` (competitive win, 2 players, global MSW handlers).
 */
import { http, HttpResponse } from 'msw';

import PlayRecordDetailPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';
const ID = 'pr-won-1';

const meta: Meta<typeof PlayRecordDetailPage> = {
  title: 'Authenticated / sp4-play-records-detail',
  component: PlayRecordDetailPage,
  parameters: {
    // DS-17 #2063: heuristic can't read named exports/http.get; declare states explicitly.
    canonicalStates: ['default', 'empty', 'loading', 'error'],
    layout: 'fullscreen',
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: `/play-records/${ID}`,
        params: { id: ID },
      },
    },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2349 US-INT-2c. Detail view. sse N/A (no SSE source). ' +
          'Default = won fixture (pr-won-1). Empty = no-scores fixture (pr-coop-1). ' +
          'Loading = infinite-pending GET. Error = 404 response.',
      },
    },
  },
};

export default meta;
type Story = StoryObj<typeof PlayRecordDetailPage>;

/** Default: loaded competitive-win record (pr-won-1, 2 players, Catan). */
export const Default: Story = {};

/**
 * Empty: cooperative record with no scores (outcomeType=none, winnerPlayerIds=[]).
 * Exercises the "no-scores / no winner" branch of the UI.
 */
export const Empty: Story = {
  parameters: {
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: '/play-records/pr-coop-1',
        params: { id: 'pr-coop-1' },
      },
    },
    msw: {
      handlers: [
        http.get(`${API_BASE}/api/v1/play-records/pr-coop-1`, () =>
          HttpResponse.json({
            id: 'pr-coop-1',
            gameId: 'game-3',
            gameName: 'Pandemic',
            sessionDate: new Date(Date.now() - 10 * 24 * 60 * 60 * 1000).toISOString(),
            duration: '01:45:00',
            status: 'Completed',
            players: [
              {
                id: 'plr-5',
                userId: 'user-me',
                displayName: 'Marco',
                scores: [],
                totalScore: null,
              },
              {
                id: 'plr-6',
                userId: 'user-2',
                displayName: 'Anna',
                scores: [],
                totalScore: null,
              },
            ],
            scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
            createdByUserId: 'user-me',
            visibility: 'Private',
            startTime: null,
            endTime: null,
            notes: 'We won together!',
            location: null,
            createdAt: new Date().toISOString(),
            updatedAt: new Date().toISOString(),
            winnerPlayerIds: [],
            outcomeType: 'none',
            playerCount: 2,
          })
        ),
      ],
    },
  },
};

/** Loading: GET /play-records/:id never resolves — shows skeleton state. */
export const Loading: Story = {
  parameters: {
    msw: {
      handlers: [http.get(`${API_BASE}/api/v1/play-records/:id`, () => new Promise(() => {}))],
    },
  },
};

/** Error: GET /play-records/:id returns 404 — shows not-found / error state. */
export const Error: Story = {
  parameters: {
    msw: {
      handlers: [
        http.get(`${API_BASE}/api/v1/play-records/:id`, () =>
          HttpResponse.json({ error: 'not_found' }, { status: 404 })
        ),
      ],
    },
  },
};
