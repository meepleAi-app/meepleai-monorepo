/**
 * UserGameCard Storybook Stories (Issue #3185: AGT-011)
 *
 * Comprehensive stories for Grid and List view modes
 * Covers: agent status, PDF status, actions, states
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import type { UserLibraryEntry } from '@/lib/api';

import { UserGameCard } from './UserGameCard';

import type { Meta, StoryObj } from '@storybook/react';

// Create QueryClient for React Query
const queryClient = new QueryClient({
  defaultOptions: {
    queries: { retry: false, staleTime: Infinity },
  },
});

const meta = {
  title: 'Components/Library/UserGameCard',
  component: UserGameCard,
  parameters: {
    layout: 'padded',
    // Mock Next.js router for navigation
    nextjs: {
      appDirectory: true,
      navigation: {
        push: () => console.log('Navigate'),
      },
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
    viewMode: {
      control: 'select',
      options: ['grid', 'list'],
      description: 'Card display variant',
    },
    selectionMode: {
      control: 'boolean',
      description: 'Enable selection mode with checkbox',
    },
    onConfigureAgent: { action: 'configure-agent' },
    onUploadPdf: { action: 'upload-pdf' },
    onEditNotes: { action: 'edit-notes' },
    onRemove: { action: 'remove' },
    onAskAgent: { action: 'ask-agent' },
  },
} satisfies Meta<typeof UserGameCard>;

export default meta;
type Story = StoryObj<typeof meta>;

// ============================================================================
// Mock Data
// ============================================================================

const mockGameBase: UserLibraryEntry = {
  id: '123e4567-e89b-12d3-a456-426614174000',
  userId: '789e4567-e89b-12d3-a456-426614174000',
  gameId: '456e4567-e89b-12d3-a456-426614174000',
  gameTitle: 'Azul',
  gamePublisher: 'Plan B Games',
  gameYearPublished: 2017,
  gameImageUrl:
    'https://cf.geekdo-images.com/aPSHJO0d0XOpQR5X-wJonw__imagepage/img/q4uWd2nXGeNcCMz_5sGW4Qsrw6c=/fit-in/900x600/filters:no_upscale():strip_icc()/pic6973671.png',
  addedAt: '2024-01-15T10:00:00Z',
  notes: null,
  isFavorite: false,
  currentState: 'Owned',
  stateChangedAt: '2024-01-15T10:00:00Z',
  hasPdfDocuments: true,
};

const mockGameWithNotes: UserLibraryEntry = {
  ...mockGameBase,
  id: '223e4567-e89b-12d3-a456-426614174001',
  gameId: '556e4567-e89b-12d3-a456-426614174001',
  gameTitle: 'Wingspan',
  gamePublisher: 'Stonemaier Games',
  notes: 'Ottimo con 3 giocatori. Espansione Europa consigliata.',
  isFavorite: true,
  currentState: 'Nuovo',
  hasPdfDocuments: false,
};

const mockGameFavorite: UserLibraryEntry = {
  ...mockGameBase,
  id: '323e4567-e89b-12d3-a456-426614174002',
  gameId: '656e4567-e89b-12d3-a456-426614174002',
  gameTitle: 'Gloomhaven',
  gamePublisher: 'Cephalofair Games',
  isFavorite: true,
  currentState: 'InPrestito',
};

const mockGameWishlist: UserLibraryEntry = {
  ...mockGameBase,
  id: '423e4567-e89b-12d3-a456-426614174003',
  gameId: '756e4567-e89b-12d3-a456-426614174003',
  gameTitle: 'Terraforming Mars',
  gamePublisher: 'Stronghold Games',
  currentState: 'Wishlist',
  isFavorite: false,
};

const mockGameNoImage: UserLibraryEntry = {
  ...mockGameBase,
  id: '523e4567-e89b-12d3-a456-426614174004',
  gameId: '856e4567-e89b-12d3-a456-426614174004',
  gameTitle: 'Carcassonne',
  gamePublisher: 'Hans im Glück',
  gameImageUrl: null,
};

// ============================================================================
// Stories - Grid View
// ============================================================================

export const GridDefault: Story = {
  args: {
    game: mockGameBase,
    viewMode: 'grid',
  },
};

export const GridWithAgent: Story = {
  args: {
    game: mockGameWithNotes,
    viewMode: 'grid',
  },
  parameters: {
    docs: {
      description: {
        story: 'Card with configured AI agent (gear icon visible on cover)',
      },
    },
  },
};

export const GridFavorite: Story = {
  args: {
    game: mockGameFavorite,
    viewMode: 'grid',
  },
};

export const GridWishlist: Story = {
  args: {
    game: mockGameWishlist,
    viewMode: 'grid',
  },
};

export const GridNoImage: Story = {
  args: {
    game: mockGameNoImage,
    viewMode: 'grid',
  },
};

export const GridSelectionMode: Story = {
  args: {
    game: mockGameBase,
    viewMode: 'grid',
    selectionMode: true,
    isSelected: false,
  },
};

export const GridSelected: Story = {
  args: {
    game: mockGameBase,
    viewMode: 'grid',
    selectionMode: true,
    isSelected: true,
  },
};

// ============================================================================
// Stories - List View
// ============================================================================

export const ListDefault: Story = {
  args: {
    game: mockGameBase,
    viewMode: 'list',
  },
};

export const ListWithNotes: Story = {
  args: {
    game: mockGameWithNotes,
    viewMode: 'list',
  },
};

export const ListFavorite: Story = {
  args: {
    game: mockGameFavorite,
    viewMode: 'list',
  },
};

export const ListNoImage: Story = {
  args: {
    game: mockGameNoImage,
    viewMode: 'list',
  },
};

export const ListSelectionMode: Story = {
  args: {
    game: mockGameBase,
    viewMode: 'list',
    selectionMode: true,
    isSelected: false,
  },
};

// ============================================================================
// Stories - States (Grid)
// ============================================================================

export const StateNuovo: Story = {
  args: {
    game: {
      ...mockGameBase,
      currentState: 'Nuovo',
    },
    viewMode: 'grid',
  },
};

export const StateInPrestito: Story = {
  args: {
    game: {
      ...mockGameBase,
      currentState: 'InPrestito',
    },
    viewMode: 'grid',
  },
};

export const StateWishlist: Story = {
  args: {
    game: {
      ...mockGameBase,
      currentState: 'Wishlist',
    },
    viewMode: 'grid',
  },
};

export const StateOwned: Story = {
  args: {
    game: {
      ...mockGameBase,
      currentState: 'Owned',
    },
    viewMode: 'grid',
  },
};

// ============================================================================
// Stories - Interactions
// ============================================================================

export const AllActionsInteractive: Story = {
  args: {
    game: mockGameWithNotes,
    viewMode: 'grid',
  },
  parameters: {
    docs: {
      description: {
        story: 'Click "Chatta", "Ask Agent", or "Azioni" to test callbacks',
      },
    },
  },
};
