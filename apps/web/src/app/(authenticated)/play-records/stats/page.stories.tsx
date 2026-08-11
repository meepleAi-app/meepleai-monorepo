/**
 * sp4-play-records-stats — DS-17-13 #2220.
 * Mockup parity: `admin-mockups/design_files/sp4-play-records-stats.{html,jsx}`.
 *
 * DEC-A5 states: default / empty / loading / error. sse N/A (no SSE source).
 *
 * Empty payload field names match the exact PlayerStatisticsSchema keys consumed by
 * StatisticsView/StatsHero (totalSessions, totalWins, gamePlayCounts,
 * averageScoresByGame, totalDurationMinutes, winByGame, mostPlayedGames).
 */
import { http, HttpResponse } from 'msw';

import StatisticsPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';
const STATS = `${API_BASE}/api/v1/play-records/statistics`;

const meta: Meta<typeof StatisticsPage> = {
  title: 'Authenticated / sp4-play-records-stats',
  component: StatisticsPage,
  parameters: {
    // DS-17 #2063: heuristic can't read named exports/http.get; declare states explicitly.
    canonicalStates: ['default', 'empty', 'loading', 'error'],
    layout: 'fullscreen',
    nextjs: { appDirectory: true, navigation: { pathname: '/play-records/stats' } },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2350 US-INT-2d. Stats dashboard. sse N/A (no SSE source). ' +
          'Default = global MSW handler (3 sessions, real data). ' +
          'Empty = zero-sessions payload. Loading = infinite-pending GET. Error = 500.',
      },
    },
  },
};
export default meta;
type Story = StoryObj<typeof StatisticsPage>;

/** Default: served by the global MSW handler (seedStats with 3 sessions). */
export const Default: Story = {};

/**
 * Empty: zero-sessions statistics — verifies the "no data / empty" branch of StatsHero.
 * Field names match PlayerStatisticsSchema exactly (verified against play-records.schemas.ts).
 */
export const Empty: Story = {
  parameters: {
    msw: {
      handlers: [
        http.get(STATS, () =>
          HttpResponse.json({
            totalSessions: 0,
            totalWins: 0,
            gamePlayCounts: {},
            averageScoresByGame: {},
            totalDurationMinutes: 0,
            winByGame: [],
            mostPlayedGames: [],
          })
        ),
      ],
    },
  },
};

/** Loading: GET /statistics never resolves — shows skeleton state. */
export const Loading: Story = {
  parameters: {
    msw: {
      handlers: [http.get(STATS, () => new Promise(() => {}))],
    },
  },
};

/** Error: GET /statistics returns 500 — shows error state. */
export const Error: Story = {
  parameters: {
    msw: {
      handlers: [
        http.get(STATS, () => HttpResponse.json({ error: 'server_error' }, { status: 500 })),
      ],
    },
  },
};
