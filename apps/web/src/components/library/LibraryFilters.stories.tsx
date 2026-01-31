/**
 * LibraryFilters Storybook Stories (Issue #2852, Phase 4)
 *
 * Component-level stories for filter panel with search, chips, and sort.
 * Tests all filter states and interactions.
 */

import { within, userEvent } from '@storybook/test';
import { fn } from '@storybook/test';

import type { GameStateType } from '@/lib/api/schemas/library.schemas';

import { LibraryFilters } from './LibraryFilters';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof LibraryFilters> = {
  title: 'Components/Library/LibraryFilters',
  component: LibraryFilters,
  parameters: {
    layout: 'padded',
    chromatic: {
      viewports: [375, 768, 1920],
      delay: 200,
    },
  },
  tags: ['autodocs'],
  argTypes: {
    searchQuery: {
      control: 'text',
      description: 'Current search query',
    },
    favoritesOnly: {
      control: 'boolean',
      description: 'Favorites filter enabled',
    },
    stateFilter: {
      control: 'object',
      description: 'Active state filters',
    },
    sortBy: {
      control: 'select',
      options: ['addedAt', 'title', 'favorite'],
      description: 'Sort field',
    },
    sortDescending: {
      control: 'boolean',
      description: 'Sort descending',
    },
    onSearchChange: { action: 'search-changed' },
    onFavoritesChange: { action: 'favorites-changed' },
    onStateFilterChange: { action: 'state-filter-changed' },
    onSortChange: { action: 'sort-changed' },
    onClearFilters: { action: 'filters-cleared' },
  },
};

export default meta;
type Story = StoryObj<typeof meta>;

// Mock stat counts
const mockStatCounts = {
  total: 42,
  favorites: 12,
  nuovo: 8,
  inPrestito: 5,
  wishlist: 10,
  owned: 19,
};

// ============================================================================
// Default States
// ============================================================================

export const Default: Story = {
  args: {
    searchQuery: '',
    favoritesOnly: false,
    stateFilter: [],
    sortBy: 'addedAt',
    sortDescending: true,
    onSearchChange: fn(),
    onFavoritesChange: fn(),
    onStateFilterChange: fn(),
    onSortChange: fn(),
    onClearFilters: fn(),
    stateCounts: mockStatCounts,
  },
};

export const WithSearch: Story = {
  args: {
    ...Default.args,
    searchQuery: 'Azul',
  },
};

export const FavoritesOnly: Story = {
  args: {
    ...Default.args,
    favoritesOnly: true,
  },
};

export const StateFilterNuovo: Story = {
  args: {
    ...Default.args,
    stateFilter: ['Nuovo' as GameStateType],
  },
};

export const StateFilterMultiple: Story = {
  args: {
    ...Default.args,
    stateFilter: ['Nuovo' as GameStateType, 'Wishlist' as GameStateType],
  },
};

export const SortByTitle: Story = {
  args: {
    ...Default.args,
    sortBy: 'title',
    sortDescending: false,
  },
};

export const AllFiltersActive: Story = {
  args: {
    ...Default.args,
    searchQuery: 'Wingspan',
    favoritesOnly: true,
    sortBy: 'favorite',
    sortDescending: true,
  },
};

// ============================================================================
// Stat Count Variations
// ============================================================================

export const WithoutStatCounts: Story = {
  args: {
    ...Default.args,
    stateCounts: undefined,
  },
};

export const EmptyLibrary: Story = {
  args: {
    ...Default.args,
    stateCounts: {
      total: 0,
      favorites: 0,
      nuovo: 0,
      inPrestito: 0,
      wishlist: 0,
      owned: 0,
    },
  },
};

// ============================================================================
// Interaction States
// ============================================================================

export const SearchInteraction: Story = {
  args: {
    ...Default.args,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const searchInput = canvas.getByLabelText('Search library');

    await userEvent.click(searchInput);
    await userEvent.type(searchInput, 'Gloomhaven', { delay: 50 });
  },
};

export const ChipHoverState: Story = {
  args: {
    ...Default.args,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const allChip = canvas.getByTestId('filter-chip-all');

    await userEvent.hover(allChip);
  },
};

export const ChipClickInteraction: Story = {
  args: {
    ...Default.args,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const favoritesChip = canvas.getByTestId('filter-chip-favorites');

    await userEvent.click(favoritesChip);
  },
};

export const SortDropdownOpen: Story = {
  args: {
    ...Default.args,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const sortTrigger = canvas.getByLabelText('Sort library');

    await userEvent.click(sortTrigger);
  },
};

export const ClearFiltersButton: Story = {
  args: {
    ...AllFiltersActive.args,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const clearButton = canvas.getByText('Pulisci Filtri');

    await userEvent.hover(clearButton);
  },
};

// ============================================================================
// Responsive Layouts
// ============================================================================

export const Mobile: Story = {
  args: {
    ...AllFiltersActive.args,
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
    ...AllFiltersActive.args,
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
    ...AllFiltersActive.args,
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
