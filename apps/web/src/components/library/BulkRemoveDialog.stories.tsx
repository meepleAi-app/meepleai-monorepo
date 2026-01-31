/**
 * BulkRemoveDialog Storybook Stories (Issue #2852, Phase 4)
 *
 * Component-level stories for bulk remove confirmation dialog.
 * Tests multiple game removal flow and loading states.
 */

import { within, userEvent } from '@storybook/test';
import { fn } from '@storybook/test';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { BulkRemoveDialog } from './BulkRemoveDialog';

import type { Meta, StoryObj } from '@storybook/react';

// Create QueryClient for React Query
const queryClient = new QueryClient({
  defaultOptions: {
    queries: { retry: false, staleTime: Infinity },
  },
});

const meta: Meta<typeof BulkRemoveDialog> = {
  title: 'Components/Library/BulkRemoveDialog',
  component: BulkRemoveDialog,
  parameters: {
    layout: 'centered',
    chromatic: {
      viewports: [375, 768, 1920],
      delay: 200,
    },
  },
  tags: ['autodocs'],
  decorators: [
    (Story) => (
      <QueryClientProvider client={queryClient}>
        <Story />
      </QueryClientProvider>
    ),
  ],
  argTypes: {
    isOpen: {
      control: 'boolean',
      description: 'Dialog open state',
    },
    gameIds: {
      control: 'object',
      description: 'Array of game IDs to remove',
    },
    gameTitles: {
      control: 'object',
      description: 'Array of game titles for display',
    },
    onClose: { action: 'closed' },
    onSuccess: { action: 'success' },
  },
};

export default meta;
type Story = StoryObj<typeof meta>;

// Mock data
const mockTwoGames = {
  gameIds: ['game-1', 'game-2'],
  gameTitles: ['Azul', 'Wingspan'],
};

const mockFiveGames = {
  gameIds: ['game-1', 'game-2', 'game-3', 'game-4', 'game-5'],
  gameTitles: ['Azul', 'Wingspan', 'Gloomhaven', 'Terraforming Mars', 'Carcassonne'],
};

const mockManyGames = {
  gameIds: Array.from({ length: 12 }, (_, i) => `game-${i + 1}`),
  gameTitles: [
    'Azul',
    'Wingspan',
    'Gloomhaven',
    'Terraforming Mars',
    'Carcassonne',
    'Ticket to Ride',
    'Splendor',
    'Pandemic',
    '7 Wonders',
    'Codenames',
    'Agricola',
    'Puerto Rico',
  ],
};

// ============================================================================
// Default States
// ============================================================================

export const TwoGames: Story = {
  args: {
    isOpen: true,
    ...mockTwoGames,
    onClose: fn(),
    onSuccess: fn(),
  },
};

export const FiveGames: Story = {
  args: {
    isOpen: true,
    ...mockFiveGames,
    onClose: fn(),
    onSuccess: fn(),
  },
};

export const ManyGames: Story = {
  args: {
    isOpen: true,
    ...mockManyGames,
    onClose: fn(),
    onSuccess: fn(),
  },
  parameters: {
    docs: {
      description: {
        story: 'Shows first 5 games with "...e altri 7 giochi" indicator',
      },
    },
  },
};

// ============================================================================
// Long Titles
// ============================================================================

export const LongGameTitles: Story = {
  args: {
    isOpen: true,
    gameIds: ['game-1', 'game-2', 'game-3'],
    gameTitles: [
      'Gloomhaven: Jaws of the Lion - Complete Edition',
      'Wingspan: European Expansion - Deluxe Edition',
      'Terraforming Mars: Big Box Edition with All Expansions',
    ],
    onClose: fn(),
    onSuccess: fn(),
  },
};

// ============================================================================
// Interaction States
// ============================================================================

export const HoverCancelButton: Story = {
  args: {
    ...TwoGames.args,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const cancelButton = canvas.getByRole('button', { name: 'Annulla' });

    await userEvent.hover(cancelButton);
  },
};

export const HoverRemoveButton: Story = {
  args: {
    ...TwoGames.args,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const removeButton = canvas.getByRole('button', { name: /Rimuovi 2 giochi/i });

    await userEvent.hover(removeButton);
  },
};

export const FocusRemoveButton: Story = {
  args: {
    ...TwoGames.args,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const removeButton = canvas.getByRole('button', { name: /Rimuovi 2 giochi/i });

    removeButton.focus();
  },
};

// ============================================================================
// Loading State
// ============================================================================

export const RemovingState: Story = {
  args: {
    ...TwoGames.args,
  },
  parameters: {
    docs: {
      description: {
        story: 'Loading state shown during bulk remove operation (mocked)',
      },
    },
  },
};

// ============================================================================
// Edge Cases
// ============================================================================

export const SingleGameInBulk: Story = {
  args: {
    isOpen: true,
    gameIds: ['game-1'],
    gameTitles: ['Azul'],
    onClose: fn(),
    onSuccess: fn(),
  },
  parameters: {
    docs: {
      description: {
        story: 'Edge case: bulk dialog with only 1 game',
      },
    },
  },
};

// ============================================================================
// Responsive Layouts
// ============================================================================

export const Mobile: Story = {
  args: {
    ...FiveGames.args,
  },
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      viewports: [375],
    },
  },
};

export const Tablet: Story = {
  args: {
    ...FiveGames.args,
  },
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
    chromatic: {
      viewports: [768],
    },
  },
};

export const Desktop: Story = {
  args: {
    ...FiveGames.args,
  },
  parameters: {
    viewport: {
      defaultViewport: 'desktop',
    },
    chromatic: {
      viewports: [1920],
    },
  },
};
