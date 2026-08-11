/**
 * sp4-session-summary-skeleton — DS-17-15 #2231.
 * Mockup parity: `admin-mockups/design_files/sp4-session-summary-skeleton.{html,jsx}`.
 *
 * #2063: useParams() is mocked from navigation.segments (not `params`), and the
 * global sessions handler returns Zod-invalid/non-'Completed' data → this story
 * scopes its own valid GameSessionDto handler so the summary renders (not not-found).
 */
import { http, HttpResponse } from 'msw';

import { SessionSummaryView } from './_components/SessionSummaryView';

import type { Meta, StoryObj } from '@storybook/react';

const ID = '00000000-0000-4000-8000-000000000756';

/** GameSessionDtoSchema-valid completed session. */
const SUMMARY = {
  id: ID,
  gameId: '00000000-0000-4000-8000-0000000000aa',
  status: 'Completed',
  startedAt: '2026-05-04T19:00:00.000Z',
  completedAt: '2026-05-04T20:30:00.000Z',
  playerCount: 2,
  players: [
    { playerName: 'Marco Rossi', playerOrder: 0, color: null },
    { playerName: 'Anna Bianchi', playerOrder: 1, color: null },
  ],
  winnerName: 'Marco Rossi',
  notes: null,
  durationMinutes: 90,
};

const meta: Meta<typeof SessionSummaryView> = {
  title: 'Authenticated / sp4-session-summary-skeleton',
  component: SessionSummaryView,
  args: { sessionId: ID },
  parameters: {
    // DS-17 #2063: http.get-driven FSM (no quoted state literal) → declare states.
    canonicalStates: ['default'],
    layout: 'fullscreen',
    nextjs: {
      appDirectory: true,
      navigation: { pathname: `/sessions/${ID}`, segments: [['id', ID]] },
    },
    viewport: { defaultViewport: 'desktop' },
    msw: {
      handlers: [
        http.get('*/api/v1/sessions/:id/diary', () => HttpResponse.json([])),
        http.get('*/api/v1/live-sessions/:id/vision-snapshots', () => HttpResponse.json([])),
        http.get('*/api/v1/sessions/:id', () => HttpResponse.json(SUMMARY)),
      ],
    },
    docs: {
      description: {
        component: '#2231 DS-17-15. Session summary skeleton (Phase C-2 skeleton-first base).',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof SessionSummaryView>;

export const Default: Story = {};
