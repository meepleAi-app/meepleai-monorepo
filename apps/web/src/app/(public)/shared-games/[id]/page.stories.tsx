/**
 * sp3-shared-game-detail — DS-17-10 #2208 sub-issue.
 *
 * Mockup parity: `admin-mockups/design_files/sp3-shared-game-detail.{html,jsx}`.
 * Post Stage 0 BGG cleanup (commit 564d854b9) — KB entry boardgamegeek.com removed.
 *
 * #2063: the route page (`page.tsx`) is an async SERVER component (`await params` +
 * `getSharedGameDetail`), which @storybook/nextjs cannot render. So this story renders
 * the CLIENT orchestrator `SharedGameDetailPageClient` directly with props (it seeds
 * React Query via `useSharedGameDetail({ id, initialData: detail })` → renders on mount,
 * no MSW round-trip). The fixture is `.parse()`d so schema defaults fill the output type.
 */
import { SharedGameDetailSchema } from '@/lib/api/schemas/shared-games.schemas';

import { SharedGameDetailPageClient } from './page-client';

import type { Meta, StoryObj } from '@storybook/react';

const ID = '11111111-1111-4111-8111-111111111111';

/** SharedGameDetailSchema-valid fixture (parse fills defaulted fields for the output type). */
const DETAIL = SharedGameDetailSchema.parse({
  id: ID,
  bggId: null,
  title: 'Fixture Game',
  yearPublished: 2020,
  description: 'A shared game fixture.',
  minPlayers: 2,
  maxPlayers: 4,
  playingTimeMinutes: 60,
  minAge: 10,
  complexityRating: 2.5,
  averageRating: 7.0,
  imageUrl: '',
  thumbnailUrl: '',
  rules: null,
  status: 'Published',
  createdBy: '22222222-2222-4222-8222-222222222222',
  modifiedBy: null,
  createdAt: '2024-01-01T00:00:00Z',
  modifiedAt: null,
  faqs: [],
  erratas: [],
  designers: [],
  publishers: [],
  categories: [],
  mechanics: [],
});

const meta: Meta<typeof SharedGameDetailPageClient> = {
  title: 'Public / sp3-shared-game-detail',
  component: SharedGameDetailPageClient,
  args: { id: ID, detail: DETAIL, contributors: [] },
  parameters: {
    // DS-17 #2063: renders the client orchestrator with props (no quoted state literal).
    canonicalStates: ['default'],
    layout: 'fullscreen',
    nextjs: {
      appDirectory: true,
      navigation: { pathname: `/shared-games/${ID}` },
    },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2208 DS-17-10. Public community view of a shared game (client orchestrator `page-client.tsx`; the async server `page.tsx` wrapper is not Storybook-renderable).',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof SharedGameDetailPageClient>;

export const Default: Story = {};
