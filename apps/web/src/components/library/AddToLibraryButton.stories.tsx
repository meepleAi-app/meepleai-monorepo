/**
 * AddToLibraryButton Storybook Stories (Issue #2852, Phase 4)
 *
 * Component-level stories for add/remove library button.
 * Tests in/out of library states, loading, disabled, and quota limits.
 */

import type { Meta, StoryObj } from '@storybook/react';
import { within, userEvent } from '@storybook/test';
import { fn } from '@storybook/test';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { AddToLibraryButton } from './AddToLibraryButton';

// Create QueryClient with mock data
const createMockQueryClient = (inLibrary = false, quotaReached = false) => {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: Infinity },
    },
  });

  // Mock game library status
  queryClient.setQueryData(['game-in-library', 'game-123'], {
    inLibrary,
  });

  // Mock library quota
  queryClient.setQueryData(['library-quota'], {
    currentCount: quotaReached ? 50 : 10,
    maxAllowed: 50,
    userTier: 'free',
  });

  return queryClient;
};

const meta: Meta<typeof AddToLibraryButton> = {
  title: 'Components/Library/AddToLibraryButton',
  component: AddToLibraryButton,
  parameters: {
    layout: 'centered',
    chromatic: {
      viewports: [375, 768, 1920],
      delay: 200,
    },
  },
  tags: ['autodocs'],
  argTypes: {
    gameTitle: {
      control: 'text',
      description: 'Game title for toast messages',
    },
    showLabel: {
      control: 'boolean',
      description: 'Show text label',
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
    onAdded: { action: 'added' },
    onRemoved: { action: 'removed' },
  },
};

export default meta;
type Story = StoryObj<typeof meta>;

// ============================================================================
// Default States (Not in Library)
// ============================================================================

export const NotInLibrary: Story = {
  args: {
    gameId: 'game-123',
    gameTitle: 'Azul',
    showLabel: true,
    onAdded: fn(),
    onRemoved: fn(),
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(false, false)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
};

export const NotInLibraryIconOnly: Story = {
  args: {
    gameId: 'game-123',
    gameTitle: 'Wingspan',
    showLabel: false,
    size: 'icon',
    onAdded: fn(),
    onRemoved: fn(),
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(false, false)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
};

export const NotInLibrarySmall: Story = {
  args: {
    gameId: 'game-123',
    gameTitle: 'Gloomhaven',
    showLabel: true,
    size: 'sm',
    onAdded: fn(),
    onRemoved: fn(),
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(false, false)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
};

// ============================================================================
// In Library States
// ============================================================================

export const InLibrary: Story = {
  args: {
    gameId: 'game-123',
    gameTitle: 'Azul',
    showLabel: true,
    onAdded: fn(),
    onRemoved: fn(),
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(true, false)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
};

export const InLibraryIconOnly: Story = {
  args: {
    gameId: 'game-123',
    gameTitle: 'Wingspan',
    showLabel: false,
    size: 'icon',
    onAdded: fn(),
    onRemoved: fn(),
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(true, false)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
};

// ============================================================================
// Quota Limit States
// ============================================================================

export const QuotaReached: Story = {
  args: {
    gameId: 'game-123',
    gameTitle: 'Terraforming Mars',
    showLabel: true,
    onAdded: fn(),
    onRemoved: fn(),
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(false, true)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
  parameters: {
    docs: {
      description: {
        story: 'Button is disabled when library quota is reached. Hover to see tooltip.',
      },
    },
  },
};

export const QuotaReachedWithTooltip: Story = {
  args: {
    ...QuotaReached.args,
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(false, true)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const button = canvas.getByRole('button');

    await userEvent.hover(button);
  },
};

// ============================================================================
// Interaction States
// ============================================================================

export const HoverNotInLibrary: Story = {
  args: {
    ...NotInLibrary.args,
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(false, false)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const button = canvas.getByRole('button');

    await userEvent.hover(button);
  },
};

export const HoverInLibrary: Story = {
  args: {
    ...InLibrary.args,
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(true, false)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const button = canvas.getByRole('button');

    await userEvent.hover(button);
  },
};

export const FocusState: Story = {
  args: {
    ...NotInLibrary.args,
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(false, false)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const button = canvas.getByRole('button');

    button.focus();
  },
};

// ============================================================================
// Loading State
// ============================================================================

export const LoadingState: Story = {
  args: {
    ...NotInLibrary.args,
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(false, false)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
  parameters: {
    docs: {
      description: {
        story: 'Loading spinner shown during add/remove operation (mocked)',
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
    gameTitle: 'Carcassonne',
    showLabel: true,
    disabled: true,
    onAdded: fn(),
    onRemoved: fn(),
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(false, false)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
};

// ============================================================================
// Variants
// ============================================================================

export const VariantDefault: Story = {
  args: {
    ...NotInLibrary.args,
    variant: 'default',
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(false, false)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
};

export const VariantOutline: Story = {
  args: {
    ...NotInLibrary.args,
    variant: 'outline',
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(false, false)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
};

export const VariantGhost: Story = {
  args: {
    ...NotInLibrary.args,
    variant: 'ghost',
  },
  decorators: [
    (Story) => (
      <QueryClientProvider client={createMockQueryClient(false, false)}>
        <Story />
      </QueryClientProvider>
    ),
  ],
};

// ============================================================================
// Responsive Preview
// ============================================================================

export const ResponsiveGrid: Story = {
  render: () => (
    <QueryClientProvider client={createMockQueryClient(false, false)}>
      <div className="flex flex-col gap-4">
        <AddToLibraryButton gameId="game-1" gameTitle="Large" size="lg" />
        <AddToLibraryButton gameId="game-2" gameTitle="Default" />
        <AddToLibraryButton gameId="game-3" gameTitle="Small" size="sm" />
        <AddToLibraryButton gameId="game-4" gameTitle="Icon" size="icon" showLabel={false} />
      </div>
    </QueryClientProvider>
  ),
};
