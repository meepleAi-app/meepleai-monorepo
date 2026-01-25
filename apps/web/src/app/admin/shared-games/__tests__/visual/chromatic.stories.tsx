/**
 * Shared Games Catalog - Visual Tests
 * Issue #2372 - Phase 3: Frontend Admin UI
 *
 * Chromatic visual regression tests for the SharedGameCatalog admin interface.
 * Covers: list view, filters, pagination, actions, responsive design
 */

import type { Meta, StoryObj } from '@storybook/react';
import { within, userEvent, expect } from 'storybook/test';

import { SharedGamesClient } from '../../client';
import { AuthProvider } from '@/components/auth/AuthProvider';

const meta: Meta<typeof SharedGamesClient> = {
  title: 'Admin/SharedGames/CatalogList/Visual Tests',
  component: SharedGamesClient,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      disableSnapshot: false,
      delay: 500,
    },
  },
  decorators: [
    Story => (
      <AuthProvider>
        <div className="min-h-screen bg-gray-50">
          <Story />
        </div>
      </AuthProvider>
    ),
  ],
};

export default meta;
type Story = StoryObj<typeof SharedGamesClient>;

// ========== Mock Data ==========

const mockCategories = [
  { id: 'cat-1', name: 'Strategia', gameCount: 45 },
  { id: 'cat-2', name: 'Famiglia', gameCount: 32 },
  { id: 'cat-3', name: 'Party', gameCount: 28 },
  { id: 'cat-4', name: 'Cooperativo', gameCount: 19 },
  { id: 'cat-5', name: 'Fantascienza', gameCount: 15 },
];

const mockMechanics = [
  { id: 'mech-1', name: 'Dice Rolling', gameCount: 67 },
  { id: 'mech-2', name: 'Hand Management', gameCount: 54 },
  { id: 'mech-3', name: 'Area Control', gameCount: 38 },
  { id: 'mech-4', name: 'Worker Placement', gameCount: 29 },
  { id: 'mech-5', name: 'Deck Building', gameCount: 24 },
];

const mockGames = [
  {
    id: '11111111-1111-1111-1111-111111111111',
    title: 'I Coloni di Catan',
    yearPublished: 1995,
    description: 'Classico gioco di strategia e commercio',
    minPlayers: 3,
    maxPlayers: 4,
    playingTimeMinutes: 75,
    minAge: 10,
    complexityRating: 2.3,
    averageRating: 7.1,
    imageUrl: 'https://example.com/catan.jpg',
    thumbnailUrl: 'https://example.com/catan-thumb.jpg',
    bggId: 13,
    status: 1,
    hasRules: true,
    categoryCount: 2,
    mechanicCount: 3,
    createdAt: '2024-01-15T10:00:00Z',
    updatedAt: '2024-06-20T14:30:00Z',
  },
  {
    id: '22222222-2222-2222-2222-222222222222',
    title: 'Ticket to Ride',
    yearPublished: 2004,
    description: 'Costruisci linee ferroviarie attraverso Europa e America',
    minPlayers: 2,
    maxPlayers: 5,
    playingTimeMinutes: 60,
    minAge: 8,
    complexityRating: 1.8,
    averageRating: 7.4,
    imageUrl: 'https://example.com/ticket.jpg',
    thumbnailUrl: 'https://example.com/ticket-thumb.jpg',
    bggId: 9209,
    status: 1,
    hasRules: true,
    categoryCount: 1,
    mechanicCount: 2,
    createdAt: '2024-02-10T08:00:00Z',
    updatedAt: '2024-05-15T11:00:00Z',
  },
  {
    id: '33333333-3333-3333-3333-333333333333',
    title: 'Gloomhaven',
    yearPublished: 2017,
    description: 'Epico dungeon crawler cooperativo',
    minPlayers: 1,
    maxPlayers: 4,
    playingTimeMinutes: 120,
    minAge: 14,
    complexityRating: 3.8,
    averageRating: 8.8,
    imageUrl: 'https://example.com/gloomhaven.jpg',
    thumbnailUrl: 'https://example.com/gloomhaven-thumb.jpg',
    bggId: 174430,
    status: 0, // Draft
    hasRules: false,
    categoryCount: 3,
    mechanicCount: 5,
    createdAt: '2024-03-20T15:00:00Z',
    updatedAt: '2024-04-10T09:00:00Z',
  },
  {
    id: '44444444-4444-4444-4444-444444444444',
    title: 'Codenames',
    yearPublished: 2015,
    description: 'Gioco di parole a squadre',
    minPlayers: 2,
    maxPlayers: 8,
    playingTimeMinutes: 15,
    minAge: 14,
    complexityRating: 1.3,
    averageRating: 7.6,
    imageUrl: 'https://example.com/codenames.jpg',
    thumbnailUrl: 'https://example.com/codenames-thumb.jpg',
    bggId: 178900,
    status: 2, // Archived
    hasRules: true,
    categoryCount: 2,
    mechanicCount: 2,
    createdAt: '2024-01-05T12:00:00Z',
    updatedAt: '2024-07-01T16:00:00Z',
  },
  {
    id: '55555555-5555-5555-5555-555555555555',
    title: 'Wingspan',
    yearPublished: 2019,
    description: 'Competizione di collezione uccelli',
    minPlayers: 1,
    maxPlayers: 5,
    playingTimeMinutes: 70,
    minAge: 10,
    complexityRating: 2.4,
    averageRating: 8.1,
    imageUrl: 'https://example.com/wingspan.jpg',
    thumbnailUrl: 'https://example.com/wingspan-thumb.jpg',
    bggId: 266192,
    status: 1,
    hasRules: true,
    categoryCount: 2,
    mechanicCount: 4,
    createdAt: '2024-04-12T10:00:00Z',
    updatedAt: '2024-06-28T14:00:00Z',
  },
];

const mockGamesData = {
  games: mockGames,
  totalCount: 42,
  pageSize: 10,
  pageNumber: 1,
  totalPages: 5,
};

// ========== Stories ==========

/**
 * Default View - Games list with data
 */
export const DefaultView: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games': mockGamesData,
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
  },
};

/**
 * Empty State - No games in catalog
 */
export const EmptyState: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games': {
        games: [],
        totalCount: 0,
        pageSize: 10,
        pageNumber: 1,
        totalPages: 0,
      },
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
  },
};

/**
 * Loading State - Initial data fetch
 */
export const LoadingState: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games': { delay: 'infinite' },
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
  },
};

/**
 * Error State - Failed to load games
 */
export const ErrorState: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games': {
        error: { message: 'Errore nel caricamento dei giochi' },
        status: 500,
      },
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
  },
};

/**
 * Filtered by Status - Draft games only
 */
export const FilteredByDraft: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games': {
        games: mockGames.filter(g => g.status === 0),
        totalCount: 1,
        pageSize: 10,
        pageNumber: 1,
        totalPages: 1,
      },
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Wait for initial load
    await canvas.findByText('Catalogo Giochi');

    // Select Draft status filter
    const statusFilter = await canvas.findByRole('combobox', { name: /stato/i });
    await userEvent.click(statusFilter);

    const draftOption = await canvas.findByRole('option', { name: /bozza/i });
    await userEvent.click(draftOption);
  },
};

/**
 * Filtered by Status - Published games
 */
export const FilteredByPublished: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games': {
        games: mockGames.filter(g => g.status === 1),
        totalCount: 3,
        pageSize: 10,
        pageNumber: 1,
        totalPages: 1,
      },
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
  },
};

/**
 * Search Results - Searching for "Catan"
 */
export const SearchResults: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games': {
        games: [mockGames[0]],
        totalCount: 1,
        pageSize: 10,
        pageNumber: 1,
        totalPages: 1,
      },
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    await canvas.findByText('Catalogo Giochi');

    // Type in search box
    const searchInput = await canvas.findByPlaceholderText(/cerca giochi/i);
    await userEvent.type(searchInput, 'Catan');
  },
};

/**
 * Pagination - Second page
 */
export const PaginationSecondPage: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games': {
        ...mockGamesData,
        pageNumber: 2,
      },
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
  },
};

/**
 * Many Pages - Large catalog
 */
export const ManyPages: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games': {
        games: mockGames,
        totalCount: 250,
        pageSize: 10,
        pageNumber: 1,
        totalPages: 25,
      },
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
  },
};

/**
 * Actions Menu Open - Dropdown visible
 */
export const ActionsMenuOpen: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games': mockGamesData,
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Wait for table to load
    await canvas.findByText('I Coloni di Catan');

    // Click first actions menu
    const actionButtons = await canvas.findAllByRole('button', { name: /azioni/i });
    if (actionButtons.length > 0) {
      await userEvent.click(actionButtons[0]);
    }
  },
};

/**
 * Delete Confirmation Dialog
 */
export const DeleteConfirmation: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games': mockGamesData,
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    await canvas.findByText('I Coloni di Catan');

    // Open actions menu
    const actionButtons = await canvas.findAllByRole('button', { name: /azioni/i });
    if (actionButtons.length > 0) {
      await userEvent.click(actionButtons[0]);
    }

    // Click delete option
    const deleteOption = await canvas.findByText(/elimina/i);
    await userEvent.click(deleteOption);

    // Verify dialog opened
    await expect(canvas.findByText(/conferma eliminazione/i)).resolves.toBeInTheDocument();
  },
};

/**
 * Mobile View - Responsive table
 */
export const MobileView: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games': mockGamesData,
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
};

/**
 * Tablet View - Medium screen
 */
export const TabletView: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games': mockGamesData,
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
    viewport: {
      defaultViewport: 'tablet',
    },
  },
};

/**
 * Single Game - Only one game in catalog
 */
export const SingleGame: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games': {
        games: [mockGames[0]],
        totalCount: 1,
        pageSize: 10,
        pageNumber: 1,
        totalPages: 1,
      },
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
  },
};

/**
 * All Draft Games - Pre-publish state
 */
export const AllDraftGames: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games': {
        games: mockGames.map(g => ({ ...g, status: 0 })),
        totalCount: 5,
        pageSize: 10,
        pageNumber: 1,
        totalPages: 1,
      },
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
  },
};

/**
 * No Rules Games - Games without rules content
 */
export const NoRulesGames: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games': {
        games: mockGames.map(g => ({ ...g, hasRules: false })),
        totalCount: 5,
        pageSize: 10,
        pageNumber: 1,
        totalPages: 1,
      },
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
  },
};
