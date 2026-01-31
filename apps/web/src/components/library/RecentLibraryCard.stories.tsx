/**
 * RecentLibraryCard Storybook Stories (Issue #2852, Phase 4)
 *
 * Component-level stories for dashboard widget card.
 * Tests favorites, timestamps, and hover states.
 */

import type { Meta, StoryObj } from '@storybook/react';
import { within, userEvent } from '@storybook/test';

import type { UserLibraryEntry } from '@/lib/api';

import { RecentLibraryCard } from './RecentLibraryCard';

const meta: Meta<typeof RecentLibraryCard> = {
  title: 'Components/Library/RecentLibraryCard',
  component: RecentLibraryCard,
  parameters: {
    layout: 'padded',
    chromatic: {
      viewports: [375, 768, 1920],
      delay: 200,
    },
    nextjs: {
      appDirectory: true,
      navigation: {
        push: () => console.log('Navigate to library'),
      },
    },
  },
  tags: ['autodocs'],
  decorators: [
    (Story) => (
      <div className="max-w-sm">
        <Story />
      </div>
    ),
  ],
};

export default meta;
type Story = StoryObj<typeof meta>;

// Mock game data
const mockGameBase: UserLibraryEntry = {
  id: '123e4567-e89b-12d3-a456-426614174000',
  userId: '789e4567-e89b-12d3-a456-426614174000',
  gameId: '456e4567-e89b-12d3-a456-426614174000',
  gameTitle: 'Azul',
  gamePublisher: 'Plan B Games',
  gameYearPublished: 2017,
  gameImageUrl:
    'https://cf.geekdo-images.com/aPSHJO0d0XOpQR5X-wJonw__imagepage/img/q4uWd2nXGeNcCMz_5sGW4Qsrw6c=/fit-in/900x600/filters:no_upscale():strip_icc()/pic6973671.png',
  addedAt: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString(), // 2 days ago
  notes: null,
  isFavorite: false,
  currentState: 'Owned',
  stateChangedAt: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString(),
  hasPdfDocuments: true,
};

// ============================================================================
// Default States
// ============================================================================

export const Default: Story = {
  args: {
    game: mockGameBase,
  },
};

export const Favorite: Story = {
  args: {
    game: {
      ...mockGameBase,
      isFavorite: true,
    },
  },
};

export const NoImage: Story = {
  args: {
    game: {
      ...mockGameBase,
      gameImageUrl: null,
    },
  },
};

export const LongTitle: Story = {
  args: {
    game: {
      ...mockGameBase,
      gameTitle: 'Gloomhaven: Jaws of the Lion - Complete Edition with Expansions',
    },
  },
};

// ============================================================================
// Time Variations
// ============================================================================

export const AddedJustNow: Story = {
  args: {
    game: {
      ...mockGameBase,
      addedAt: new Date(Date.now() - 30 * 1000).toISOString(), // 30 seconds ago
    },
  },
};

export const AddedOneHourAgo: Story = {
  args: {
    game: {
      ...mockGameBase,
      addedAt: new Date(Date.now() - 60 * 60 * 1000).toISOString(), // 1 hour ago
    },
  },
};

export const AddedOneDayAgo: Story = {
  args: {
    game: {
      ...mockGameBase,
      addedAt: new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString(), // 1 day ago
    },
  },
};

export const AddedOneWeekAgo: Story = {
  args: {
    game: {
      ...mockGameBase,
      addedAt: new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString(), // 1 week ago
    },
  },
};

// ============================================================================
// Interaction States
// ============================================================================

export const HoverState: Story = {
  args: {
    game: mockGameBase,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const card = canvas.getByTestId('recent-library-card');

    await userEvent.hover(card);
  },
};

export const ButtonHover: Story = {
  args: {
    game: mockGameBase,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const button = canvas.getByRole('link', { name: 'Gestisci' });

    await userEvent.hover(button);
  },
};

// ============================================================================
// Multiple Cards Layout (Grid Preview)
// ============================================================================

export const GridLayout: Story = {
  render: () => (
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
      <RecentLibraryCard
        game={{
          ...mockGameBase,
          gameTitle: 'Azul',
          isFavorite: false,
        }}
      />
      <RecentLibraryCard
        game={{
          ...mockGameBase,
          gameId: 'game-2',
          gameTitle: 'Wingspan',
          isFavorite: true,
          addedAt: new Date(Date.now() - 1 * 24 * 60 * 60 * 1000).toISOString(),
        }}
      />
      <RecentLibraryCard
        game={{
          ...mockGameBase,
          gameId: 'game-3',
          gameTitle: 'Gloomhaven',
          gameImageUrl: null,
          addedAt: new Date(Date.now() - 3 * 24 * 60 * 60 * 1000).toISOString(),
        }}
      />
    </div>
  ),
  parameters: {
    layout: 'fullscreen',
  },
};
