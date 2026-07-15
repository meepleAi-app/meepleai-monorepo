/**
 * sp4-agent-detail — DS-17-12 #2214 sub-issue.
 *
 * Mockup parity: `admin-mockups/design_files/sp4-agent-detail.{html,jsx}`.
 *
 * #2063: useParams() mocked from navigation.segments (UUID required by AgentDtoSchema).
 * The global agents handler is host-scoped to :8080, but httpClient uses a relative
 * URL in Storybook → it never intercepts. So this story scopes its own wildcard-host
 * handler returning a valid AgentDto.
 */
import { http, HttpResponse } from 'msw';

import AgentDetailPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const ID = '11111111-1111-4111-8111-111111111111';

/** AgentDtoSchema-valid fixture. */
const AGENT = {
  id: ID,
  name: 'Chess Expert',
  type: 'qa',
  strategyName: 'hybrid-rag',
  strategyParameters: {},
  isActive: true,
  createdAt: '2026-07-15T10:00:00.000Z',
  lastInvokedAt: null,
  invocationCount: 0,
  isRecentlyUsed: false,
  isIdle: true,
};

const meta: Meta<typeof AgentDetailPage> = {
  title: 'Authenticated / sp4-agent-detail',
  component: AgentDetailPage,
  parameters: {
    // DS-17 #2063: http.get-driven (no quoted state literal) → declare states.
    canonicalStates: ['default'],
    layout: 'fullscreen',
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: `/agents/${ID}`,
        segments: [['id', ID]],
      },
    },
    viewport: { defaultViewport: 'desktop' },
    msw: {
      handlers: [http.get('*/api/v1/agents/:id', () => HttpResponse.json(AGENT))],
    },
    docs: {
      description: {
        component: '#2214 DS-17-12. Authenticated agent detail view.',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof AgentDetailPage>;

export const Default: Story = {};
