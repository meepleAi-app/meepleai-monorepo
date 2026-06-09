/**
 * @mockup admin-mockups/design_files/sp4-dashboard.html
 *
 * DashboardClient page-mock story — DS-17-6-v2 pilot.
 *
 * Demonstrates the page-mock migration pattern Phase 3 sweep will copy:
 *   1. Side-by-side col Client component (CSF 3 standard)
 *   2. Page-mock fixtures from `__tests__/fixtures/mockup-pilots/`
 *   3. MSW per-story handler overrides for hook-driven state matrix
 *   4. Decorators inherited from `.storybook/preview.tsx` (MockAuth + QueryClient + Theme)
 *
 * Refs: spec docs/superpowers/specs/2026-06-09-ds-17-phase-2-design.md, umbrella #2063.
 */

import { http, HttpResponse } from 'msw';

import {
  MOCK_DASHBOARD_ACTIVE_SESSIONS,
  MOCK_DASHBOARD_ACTIVE_SESSIONS_EMPTY,
  MOCK_DASHBOARD_COMPLETED_GAMENIGHTS,
  MOCK_DASHBOARD_COMPLETED_GAMENIGHTS_EMPTY,
  MOCK_DASHBOARD_FRIENDS_ACTIVITY,
  MOCK_DASHBOARD_FRIENDS_ACTIVITY_EMPTY,
  MOCK_DASHBOARD_GAMES,
  MOCK_DASHBOARD_GAMES_EMPTY,
  MOCK_DASHBOARD_LIBRARY_STATS,
  MOCK_DASHBOARD_LIBRARY_STATS_EMPTY,
  MOCK_DASHBOARD_UPCOMING_GAMENIGHTS,
  MOCK_DASHBOARD_UPCOMING_GAMENIGHTS_EMPTY,
} from '@/__tests__/fixtures/mockup-pilots';

import { DashboardClient } from './DashboardClient';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof DashboardClient> = {
  title: 'Pages/SP4/Dashboard / Mockup Pilot',
  component: DashboardClient,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Pixel-faithful to `admin-mockups/design_files/sp4-dashboard.html`. Verify side-by-side col mockup in browser.',
      },
    },
  },
};
export default meta;

type Story = StoryObj<typeof DashboardClient>;

// ── Default ───────────────────────────────────────────────────────────────────

export const Default: Story = {
  parameters: {
    msw: {
      handlers: [
        http.get('*/api/v1/games', () => HttpResponse.json(MOCK_DASHBOARD_GAMES)),
        http.get('*/api/v1/sessions/active', () =>
          HttpResponse.json(MOCK_DASHBOARD_ACTIVE_SESSIONS)
        ),
        http.get('*/api/v1/game-nights/upcoming', () =>
          HttpResponse.json(MOCK_DASHBOARD_UPCOMING_GAMENIGHTS)
        ),
        http.get('*/api/v1/game-nights/completed', () =>
          HttpResponse.json(MOCK_DASHBOARD_COMPLETED_GAMENIGHTS)
        ),
        http.get('*/api/v1/library/stats', () => HttpResponse.json(MOCK_DASHBOARD_LIBRARY_STATS)),
        http.get('*/api/v1/dashboard/friends-activity', () =>
          HttpResponse.json(MOCK_DASHBOARD_FRIENDS_ACTIVITY)
        ),
      ],
    },
  },
};

// ── Empty ─────────────────────────────────────────────────────────────────────

export const Empty: Story = {
  parameters: {
    msw: {
      handlers: [
        http.get('*/api/v1/games', () => HttpResponse.json(MOCK_DASHBOARD_GAMES_EMPTY)),
        http.get('*/api/v1/sessions/active', () =>
          HttpResponse.json(MOCK_DASHBOARD_ACTIVE_SESSIONS_EMPTY)
        ),
        http.get('*/api/v1/game-nights/upcoming', () =>
          HttpResponse.json(MOCK_DASHBOARD_UPCOMING_GAMENIGHTS_EMPTY)
        ),
        http.get('*/api/v1/game-nights/completed', () =>
          HttpResponse.json(MOCK_DASHBOARD_COMPLETED_GAMENIGHTS_EMPTY)
        ),
        http.get('*/api/v1/library/stats', () =>
          HttpResponse.json(MOCK_DASHBOARD_LIBRARY_STATS_EMPTY)
        ),
        http.get('*/api/v1/dashboard/friends-activity', () =>
          HttpResponse.json(MOCK_DASHBOARD_FRIENDS_ACTIVITY_EMPTY)
        ),
      ],
    },
  },
};

// ── Loading ───────────────────────────────────────────────────────────────────

export const Loading: Story = {
  parameters: {
    msw: {
      handlers: [
        // Never-resolving handlers keep queries in loading state
        http.get('*/api/v1/games', () => new Promise<Response>(() => {})),
        http.get('*/api/v1/sessions/active', () => new Promise<Response>(() => {})),
        http.get('*/api/v1/game-nights/upcoming', () => new Promise<Response>(() => {})),
        http.get('*/api/v1/game-nights/completed', () => new Promise<Response>(() => {})),
        http.get('*/api/v1/library/stats', () => new Promise<Response>(() => {})),
        http.get('*/api/v1/dashboard/friends-activity', () => new Promise<Response>(() => {})),
      ],
    },
  },
};

// ── Error ─────────────────────────────────────────────────────────────────────

export const Error: Story = {
  parameters: {
    msw: {
      handlers: [
        http.get('*/api/v1/games', () =>
          HttpResponse.json({ error: 'server error' }, { status: 500 })
        ),
        http.get('*/api/v1/sessions/active', () =>
          HttpResponse.json({ error: 'server error' }, { status: 500 })
        ),
        http.get('*/api/v1/game-nights/upcoming', () =>
          HttpResponse.json({ error: 'server error' }, { status: 500 })
        ),
        http.get('*/api/v1/game-nights/completed', () =>
          HttpResponse.json({ error: 'server error' }, { status: 500 })
        ),
        http.get('*/api/v1/library/stats', () =>
          HttpResponse.json({ error: 'server error' }, { status: 500 })
        ),
        http.get('*/api/v1/dashboard/friends-activity', () =>
          HttpResponse.json({ error: 'server error' }, { status: 500 })
        ),
      ],
    },
  },
};
