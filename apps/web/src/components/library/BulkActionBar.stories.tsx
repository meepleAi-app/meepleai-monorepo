/**
 * BulkActionBar Storybook Stories (Issue #2852, Phase 4)
 *
 * Component-level stories for bulk action toolbar.
 * Tests selection states, dropdown menus, and responsive layouts.
 */

import type { Meta, StoryObj } from '@storybook/react';
import { within, userEvent } from '@storybook/test';
import { fn } from '@storybook/test';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

import { BulkActionBar } from './BulkActionBar';

// Create QueryClient for React Query
const queryClient = new QueryClient({
  defaultOptions: {
    queries: { retry: false, staleTime: Infinity },
  },
});

const meta: Meta<typeof BulkActionBar> = {
  title: 'Components/Library/BulkActionBar',
  component: BulkActionBar,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      viewports: [375, 768, 1920],
      delay: 300,
    },
  },
  tags: ['autodocs'],
  decorators: [
    (Story) => (
      <QueryClientProvider client={queryClient}>
        <div className="min-h-[300px] bg-muted/20 p-4">
          <Story />
        </div>
      </QueryClientProvider>
    ),
  ],
  argTypes: {
    selectedCount: {
      control: 'number',
      description: 'Number of selected games',
    },
    onClearSelection: { action: 'clear-selection' },
    onSelectAll: { action: 'select-all' },
    onDeselectAll: { action: 'deselect-all' },
  },
};

export default meta;
type Story = StoryObj<typeof meta>;

// Mock library data
const mockGames: UserLibraryEntry[] = [
  {
    id: '1',
    userId: 'user-1',
    gameId: 'game-1',
    gameTitle: 'Azul',
    gamePublisher: 'Plan B Games',
    gameYearPublished: 2017,
    gameImageUrl: 'https://example.com/azul.png',
    addedAt: '2024-01-15T10:00:00Z',
    notes: null,
    isFavorite: false,
    currentState: 'Owned',
    stateChangedAt: '2024-01-15T10:00:00Z',
    hasPdfDocuments: true,
  },
  {
    id: '2',
    userId: 'user-1',
    gameId: 'game-2',
    gameTitle: 'Wingspan',
    gamePublisher: 'Stonemaier Games',
    gameYearPublished: 2019,
    gameImageUrl: 'https://example.com/wingspan.png',
    addedAt: '2024-01-16T10:00:00Z',
    notes: null,
    isFavorite: false,
    currentState: 'Wishlist',
    stateChangedAt: '2024-01-16T10:00:00Z',
    hasPdfDocuments: false,
  },
  {
    id: '3',
    userId: 'user-1',
    gameId: 'game-3',
    gameTitle: 'Gloomhaven',
    gamePublisher: 'Cephalofair Games',
    gameYearPublished: 2017,
    gameImageUrl: 'https://example.com/gloomhaven.png',
    addedAt: '2024-01-17T10:00:00Z',
    notes: 'Great campaign game',
    isFavorite: true,
    currentState: 'Owned',
    stateChangedAt: '2024-01-17T10:00:00Z',
    hasPdfDocuments: false,
  },
];

const allGameIds = mockGames.map(g => g.gameId);

// ============================================================================
// Selection States
// ============================================================================

export const TwoSelected: Story = {
  args: {
    selectedCount: 2,
    selectedIds: ['game-1', 'game-2'],
    allGameIds,
    games: mockGames,
    onClearSelection: fn(),
    onSelectAll: fn(),
    onDeselectAll: fn(),
  },
};

export const AllSelected: Story = {
  args: {
    selectedCount: 3,
    selectedIds: allGameIds,
    allGameIds,
    games: mockGames,
    onClearSelection: fn(),
    onSelectAll: fn(),
    onDeselectAll: fn(),
  },
};

export const SingleSelected: Story = {
  args: {
    selectedCount: 1,
    selectedIds: ['game-1'],
    allGameIds,
    games: mockGames,
    onClearSelection: fn(),
    onSelectAll: fn(),
    onDeselectAll: fn(),
  },
};

// ============================================================================
// Interaction States
// ============================================================================

export const ChangeStateDropdownOpen: Story = {
  args: {
    ...TwoSelected.args,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    // Desktop dropdown
    const changeStateButton = canvas.queryByRole('button', { name: /Cambia Stato/i });

    if (changeStateButton) {
      await userEvent.click(changeStateButton);
    }
  },
};

export const ExportDropdownOpen: Story = {
  args: {
    ...TwoSelected.args,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    // Desktop export dropdown
    const exportButtons = canvas.queryAllByRole('button', { name: /Esporta/i });
    const desktopExportButton = exportButtons.find(btn => btn.classList.contains('sm:flex'));

    if (desktopExportButton) {
      await userEvent.click(desktopExportButton);
    }
  },
};

export const HoverFavoriteButton: Story = {
  args: {
    ...TwoSelected.args,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    // Desktop favorite button
    const favoriteButtons = canvas.queryAllByRole('button', { name: /Preferiti/i });
    const desktopFavoriteButton = favoriteButtons.find(btn => btn.classList.contains('sm:flex'));

    if (desktopFavoriteButton) {
      await userEvent.hover(desktopFavoriteButton);
    }
  },
};

export const HoverRemoveButton: Story = {
  args: {
    ...TwoSelected.args,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    // Desktop remove button
    const removeButtons = canvas.queryAllByRole('button', { name: /Rimuovi/i });
    const desktopRemoveButton = removeButtons.find(btn => btn.classList.contains('sm:flex'));

    if (desktopRemoveButton) {
      await userEvent.hover(desktopRemoveButton);
    }
  },
};

export const HoverClearButton: Story = {
  args: {
    ...TwoSelected.args,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const clearButton = canvas.getByLabelText('Esci dalla selezione');

    await userEvent.hover(clearButton);
  },
};

// ============================================================================
// Responsive Layouts
// ============================================================================

export const Mobile: Story = {
  args: {
    ...TwoSelected.args,
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
    ...TwoSelected.args,
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
    ...TwoSelected.args,
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

// ============================================================================
// Mobile-Specific Interactions
// ============================================================================

export const MobileChangeStateOpen: Story = {
  args: {
    ...TwoSelected.args,
  },
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      viewports: [375],
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    // Mobile icon-only button
    const changeStateButton = canvas.getByLabelText('Cambia Stato');

    await userEvent.click(changeStateButton);
  },
};

export const MobileExportOpen: Story = {
  args: {
    ...TwoSelected.args,
  },
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      viewports: [375],
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    // Mobile icon-only export button
    const exportButtons = canvas.queryAllByLabelText('Esporta');
    const mobileExportButton = exportButtons.find(btn => btn.classList.contains('sm:hidden'));

    if (mobileExportButton) {
      await userEvent.click(mobileExportButton);
    }
  },
};
