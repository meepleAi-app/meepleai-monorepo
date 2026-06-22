/**
 * sp4-play-records-new — DS-17-13 #2220.
 * Mockup parity: `admin-mockups/design_files/sp4-play-records-new.{html,jsx}`.
 *
 * DEC-A5 canonical states: default / empty (N/A — create form has no list) /
 * loading (submit-pending) / error (submit-failure) / sse (N/A — no SSE source).
 */
import { http, HttpResponse } from 'msw';

import PlayRecordNewPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

const meta: Meta<typeof PlayRecordNewPage> = {
  title: 'Authenticated / sp4-play-records-new',
  component: PlayRecordNewPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true, navigation: { pathname: '/play-records/new' } },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2348 US-INT-2b. Create form. States: default + submit-loading + submit-error. ' +
          'empty/sse are Not Applicable (no list data, no SSE source).',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof PlayRecordNewPage>;

export const Default: Story = {};

export const Loading: Story = {
  parameters: {
    msw: {
      handlers: [http.post(`${API_BASE}/api/v1/play-records`, () => new Promise(() => {}))],
    },
  },
};

export const Error: Story = {
  parameters: {
    msw: {
      handlers: [
        http.post(`${API_BASE}/api/v1/play-records`, () =>
          HttpResponse.json({ error: 'server_error' }, { status: 500 })
        ),
      ],
    },
  },
};

// #2348: deep-link prefill from a completed GameNight (?gameNightId=).
export const FromGameNight: Story = {
  parameters: {
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: '/play-records/new',
        query: { gameNightId: 'gn-1' },
      },
    },
    msw: {
      handlers: [
        http.get(`${API_BASE}/api/v1/game-nights/gn-1`, () =>
          HttpResponse.json({
            id: 'gn-1',
            organizerId: 'u-org',
            organizerName: 'Org',
            title: 'Sabato boardgame',
            description: null,
            scheduledAt: '2026-05-17T20:00:00.000Z',
            location: 'Padova',
            maxPlayers: 6,
            gameIds: ['game-1'],
            status: 'Completed',
            acceptedCount: 1,
            pendingCount: 0,
            totalInvited: 1,
            createdAt: '2026-05-01T00:00:00.000Z',
          })
        ),
        http.get(`${API_BASE}/api/v1/game-nights/gn-1/rsvps`, () =>
          HttpResponse.json([
            {
              id: 'r1',
              userId: 'u-1',
              userName: 'Marco',
              status: 'Accepted',
              respondedAt: null,
              createdAt: '2026-05-02T00:00:00.000Z',
            },
          ])
        ),
        http.get(`${API_BASE}/api/v1/games/game-1`, () =>
          HttpResponse.json({ id: 'game-1', title: 'Brass Birmingham' })
        ),
      ],
    },
  },
};
