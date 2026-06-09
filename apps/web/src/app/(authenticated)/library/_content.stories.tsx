/**
 * @mockup admin-mockups/design_files/sp4-library-desktop.html
 *
 * LibraryContent page-mock story — DS-17-6-v2 pilot.
 *
 * Refs: spec, umbrella #2063.
 *
 * Known limitation: `LibraryContent` writes to `useRecentsStore` Zustand store
 * on mount. Without a per-story reset decorator the store accumulates across
 * Storybook navigations; benign for screenshot capture but visible if you
 * inspect the recents list in browser. Documented as Phase 2 acceptable
 * limitation in `docs/for-developers/frontend/page-mock-story-pattern.md`.
 */

import { http, HttpResponse } from 'msw';

import { MOCK_LIBRARY_GAMES, MOCK_LIBRARY_GAMES_EMPTY } from '@/__tests__/fixtures/mockup-pilots';

import { LibraryContent } from './_content';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof LibraryContent> = {
  title: 'Pages/SP4/Library / Mockup Pilot',
  component: LibraryContent,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Pixel-faithful to `admin-mockups/design_files/sp4-library-desktop.html`. LibraryHub carousel + AddGameDrawer.',
      },
    },
  },
};
export default meta;

type Story = StoryObj<typeof LibraryContent>;

export const Default: Story = {
  parameters: {
    msw: {
      handlers: [http.get('*/api/v1/library/games', () => HttpResponse.json(MOCK_LIBRARY_GAMES))],
    },
  },
};

export const Empty: Story = {
  parameters: {
    msw: {
      handlers: [
        http.get('*/api/v1/library/games', () => HttpResponse.json(MOCK_LIBRARY_GAMES_EMPTY)),
      ],
    },
  },
};

export const Loading: Story = {
  parameters: {
    msw: {
      handlers: [http.get('*/api/v1/library/games', () => new Promise<Response>(() => {}))],
    },
  },
};
