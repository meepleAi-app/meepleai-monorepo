/**
 * sp4-play-records-edit — DS-17-13 #2220.
 * Mockup parity: `admin-mockups/design_files/sp4-play-records-edit.{html,jsx}`.
 *
 * DEC-A5 states: default (prefilled form) / empty (record w/ no editable values) /
 * loading / error (404). sse N/A (no SSE source).
 *
 * Default showcases the AC-4.2 prefill fix: the form renders with the loaded
 * record's sessionDate, notes, and location already populated (pr-won-1 fixture).
 *
 * The edit page reads `useParams().id` — the `params: { id: ID }` in the nextjs
 * navigation block ensures Storybook resolves the dynamic segment correctly.
 */
import { http, HttpResponse } from 'msw';

import PlayRecordEditPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';
const ID = 'pr-won-1';

const meta: Meta<typeof PlayRecordEditPage> = {
  title: 'Authenticated / sp4-play-records-edit',
  component: PlayRecordEditPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: `/play-records/${ID}/edit`,
        params: { id: ID },
      },
    },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2349 US-INT-2c. Edit form. Default = prefilled from pr-won-1 fixture (AC-4.2 fix). ' +
          'sse N/A (no SSE source). Empty = record with blank notes/location.',
      },
    },
  },
};

export default meta;
type Story = StoryObj<typeof PlayRecordEditPage>;

/**
 * Default: loaded and prefilled edit form.
 * pr-won-1 has notes="Great game" — the global MSW handler serves it.
 * Showcases the AC-4.2 prefill that was previously broken.
 */
export const Default: Story = {};

/**
 * Empty: record with no notes/location — blank editable fields.
 * Uses pr-inprogress-1 which has notes=null, location=null.
 */
export const Empty: Story = {
  parameters: {
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: '/play-records/pr-inprogress-1/edit',
        params: { id: 'pr-inprogress-1' },
      },
    },
    msw: {
      handlers: [
        http.get(`${API_BASE}/api/v1/play-records/pr-inprogress-1`, () =>
          HttpResponse.json({
            id: 'pr-inprogress-1',
            gameId: null,
            gameName: 'My Custom Game',
            sessionDate: new Date().toISOString(),
            duration: null,
            status: 'InProgress',
            players: [
              {
                id: 'plr-7',
                userId: 'user-me',
                displayName: 'Marco',
                scores: [],
                totalScore: null,
              },
            ],
            scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
            createdByUserId: 'user-me',
            visibility: 'Private',
            startTime: new Date().toISOString(),
            endTime: null,
            notes: null,
            location: null,
            createdAt: new Date().toISOString(),
            updatedAt: new Date().toISOString(),
            winnerPlayerIds: [],
            outcomeType: 'competitive',
            playerCount: 1,
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
