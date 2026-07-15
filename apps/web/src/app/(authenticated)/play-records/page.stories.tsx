/**
 * sp4-play-records-index — DS-17-13 #2220.
 * Mockup parity: `admin-mockups/design_files/sp4-play-records-index.{html,jsx}`.
 *
 * DEC-A5 states: default (loaded list) / empty (first-run, no records) /
 * loading (skeleton) / error (500). sse N/A.
 *
 * The page renders <PlayHistory/> which fetches via usePlayHistory →
 * React Query GET /api/v1/play-records/history. Default relies on the global
 * MSW history handler; Empty/Loading/Error override that endpoint. (#2063 ratchet:
 * corrected from a false "store-driven, loading/error N/A" downshift.)
 */
import { http, HttpResponse } from 'msw';

import PlayRecordsPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';
const HISTORY = `${API_BASE}/api/v1/play-records/history`;

const meta: Meta<typeof PlayRecordsPage> = {
  title: 'Authenticated / sp4-play-records-index',
  component: PlayRecordsPage,
  parameters: {
    // DS-17 #2063: PlayHistory renders skeleton/error/empty/default from a React
    // Query fetch; the heuristic can't read those, so declare states explicitly.
    canonicalStates: ['default', 'empty', 'loading', 'error'],
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: { component: '#2220 DS-17-13. Play records index list.' },
    },
  },
};

export default meta;

type Story = StoryObj<typeof PlayRecordsPage>;

/** Default: loaded history list (global MSW history handler). */
export const Default: Story = {};

/** Empty: first-run empty state — history returns zero records. */
export const Empty: Story = {
  parameters: {
    msw: {
      handlers: [
        http.get(HISTORY, () =>
          HttpResponse.json({ records: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 })
        ),
      ],
    },
  },
};

/** Loading: GET /history never resolves — shows skeleton shimmer. */
export const Loading: Story = {
  parameters: {
    msw: { handlers: [http.get(HISTORY, () => new Promise(() => {}))] },
  },
};

/** Error: GET /history returns 500 — shows error alert with retry. */
export const Error: Story = {
  parameters: {
    msw: {
      handlers: [
        http.get(HISTORY, () => HttpResponse.json({ error: 'server_error' }, { status: 500 })),
      ],
    },
  },
};
