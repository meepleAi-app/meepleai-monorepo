/**
 * FavoriteToggle Storybook Stories (Issue #2852, Phase 4)
 *
 * Component-level stories for favorite toggle button.
 * Tests favorite/unfavorite states, hover, and loading.
 */

import { within, userEvent } from '@storybook/test';
import { fn } from '@storybook/test';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { FavoriteToggle } from './FavoriteToggle';

import type { Meta, StoryObj } from '@storybook/react';

// Create QueryClient for React Query
const queryClient = new QueryClient({
  defaultOptions: {
    queries: { retry: false, staleTime: Infinity },
  },
});

const meta: Meta<typeof FavoriteToggle> = {
  title: 'Components/Library/FavoriteToggle',
  component: FavoriteToggle,
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
    isFavorite: {
      control: 'boolean',
      description: 'Current favorite status',
    },
    gameTitle: {
      control: 'text',
      description: 'Game title for toast messages',
    },
    variant: {
      control: 'select',
      options: ['default', 'outline', 'secondary', 'ghost'],
      description: 'Button variant',
    },
    size: {
      control: 'select',
      options: ['default', 'sm', 'lg', 'icon'],
      description: 'Button size',
    },
    onToggled: { action: 'toggled' },
  },
};

export default meta;
type Story = StoryObj<typeof meta>;

// ============================================================================
// Default States
// ============================================================================

export const NotFavorite: Story = {
  args: {
    gameId: 'game-123',
    isFavorite: false,
    gameTitle: 'Azul',
    onToggled: fn(),
  },
};

export const IsFavorite: Story = {
  args: {
    gameId: 'game-123',
    isFavorite: true,
    gameTitle: 'Wingspan',
    onToggled: fn(),
  },
};

// ============================================================================
// Interaction States
// ============================================================================

export const HoverNotFavorite: Story = {
  args: {
    ...NotFavorite.args,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const button = canvas.getByRole('button');

    await userEvent.hover(button);
  },
};

export const HoverFavorite: Story = {
  args: {
    ...IsFavorite.args,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const button = canvas.getByRole('button');

    await userEvent.hover(button);
  },
};

export const FocusState: Story = {
  args: {
    ...NotFavorite.args,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const button = canvas.getByRole('button');

    button.focus();
  },
};

export const ActiveState: Story = {
  args: {
    ...NotFavorite.args,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const button = canvas.getByRole('button');

    await userEvent.click(button);
  },
};

// ============================================================================
// Loading State
// ============================================================================

export const LoadingState: Story = {
  args: {
    ...NotFavorite.args,
  },
  parameters: {
    docs: {
      description: {
        story: 'Loading spinner shown during toggle operation (mocked)',
      },
    },
  },
};

// ============================================================================
// Disabled State
// ============================================================================

export const Disabled: Story = {
  args: {
    gameId: 'game-123',
    isFavorite: false,
    gameTitle: 'Gloomhaven',
    disabled: true,
    onToggled: fn(),
  },
};

export const DisabledFavorite: Story = {
  args: {
    gameId: 'game-123',
    isFavorite: true,
    gameTitle: 'Terraforming Mars',
    disabled: true,
    onToggled: fn(),
  },
};

// ============================================================================
// Variants
// ============================================================================

export const VariantDefault: Story = {
  args: {
    ...NotFavorite.args,
    variant: 'default',
  },
};

export const VariantOutline: Story = {
  args: {
    ...NotFavorite.args,
    variant: 'outline',
  },
};

export const VariantSecondary: Story = {
  args: {
    ...NotFavorite.args,
    variant: 'secondary',
  },
};

export const VariantGhost: Story = {
  args: {
    ...IsFavorite.args,
    variant: 'ghost',
  },
};

// ============================================================================
// Sizes
// ============================================================================

export const SizeDefault: Story = {
  args: {
    ...IsFavorite.args,
    size: 'default',
  },
};

export const SizeSmall: Story = {
  args: {
    ...IsFavorite.args,
    size: 'sm',
  },
};

export const SizeLarge: Story = {
  args: {
    ...IsFavorite.args,
    size: 'lg',
  },
};

export const SizeIcon: Story = {
  args: {
    ...IsFavorite.args,
    size: 'icon',
  },
};

// ============================================================================
// Comparison Grid
// ============================================================================

export const StateComparison: Story = {
  render: () => (
    <QueryClientProvider client={queryClient}>
      <div className="flex gap-8 items-center">
        <div className="flex flex-col gap-2 items-center">
          <FavoriteToggle gameId="game-1" isFavorite={false} gameTitle="Not Favorite" />
          <span className="text-xs text-muted-foreground">Not Favorite</span>
        </div>
        <div className="flex flex-col gap-2 items-center">
          <FavoriteToggle gameId="game-2" isFavorite={true} gameTitle="Favorite" />
          <span className="text-xs text-muted-foreground">Favorite</span>
        </div>
        <div className="flex flex-col gap-2 items-center">
          <FavoriteToggle gameId="game-3" isFavorite={false} gameTitle="Disabled" disabled />
          <span className="text-xs text-muted-foreground">Disabled</span>
        </div>
      </div>
    </QueryClientProvider>
  ),
};

export const SizeComparison: Story = {
  render: () => (
    <QueryClientProvider client={queryClient}>
      <div className="flex gap-4 items-center">
        <FavoriteToggle gameId="game-1" isFavorite={true} gameTitle="Small" size="sm" />
        <FavoriteToggle gameId="game-2" isFavorite={true} gameTitle="Default" />
        <FavoriteToggle gameId="game-3" isFavorite={true} gameTitle="Large" size="lg" />
      </div>
    </QueryClientProvider>
  ),
};
