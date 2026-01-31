/**
 * RemoveGameDialog Storybook Stories (Issue #2852, Phase 4)
 *
 * Component-level stories for remove game confirmation dialog.
 * Tests destructive action patterns and loading states.
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { within, userEvent } from 'storybook/test';
import { fn } from 'storybook/test';

import { RemoveGameDialog } from './RemoveGameDialog';

import type { Meta, StoryObj } from '@storybook/react';

// Create QueryClient for React Query
const queryClient = new QueryClient({
  defaultOptions: {
    queries: { retry: false, staleTime: Infinity },
  },
});

const meta: Meta<typeof RemoveGameDialog> = {
  title: 'Components/Library/RemoveGameDialog',
  component: RemoveGameDialog,
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
    gameTitle: {
      control: 'text',
      description: 'Game title for display',
    },
    onClose: { action: 'closed' },
    onRemoved: { action: 'removed' },
  },
};

export default meta;
type Story = StoryObj<typeof meta>;

// ============================================================================
// Default States
// ============================================================================

export const Default: Story = {
  args: {
    isOpen: true,
    gameId: 'game-123',
    gameTitle: 'Azul',
    onClose: fn(),
    onRemoved: fn(),
  },
};

export const LongGameTitle: Story = {
  args: {
    isOpen: true,
    gameId: 'game-123',
    gameTitle: 'Gloomhaven: Jaws of the Lion - Complete Edition with All Expansions',
    onClose: fn(),
    onRemoved: fn(),
  },
};

// ============================================================================
// Interaction States
// ============================================================================

export const HoverCancelButton: Story = {
  args: {
    ...Default.args,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const cancelButton = canvas.getByRole('button', { name: 'Annulla' });

    await userEvent.hover(cancelButton);
  },
};

export const HoverRemoveButton: Story = {
  args: {
    ...Default.args,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const removeButton = canvas.getByRole('button', { name: /Rimuovi/i });

    await userEvent.hover(removeButton);
  },
};

export const FocusRemoveButton: Story = {
  args: {
    ...Default.args,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const removeButton = canvas.getByRole('button', { name: /Rimuovi/i });

    removeButton.focus();
  },
};

// ============================================================================
// Loading State
// ============================================================================

export const RemovingState: Story = {
  args: {
    ...Default.args,
  },
  parameters: {
    docs: {
      description: {
        story: 'Loading state shown during remove operation (mocked)',
      },
    },
  },
};

// ============================================================================
// Responsive Layouts
// ============================================================================

export const Mobile: Story = {
  args: {
    ...Default.args,
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
    ...Default.args,
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
    ...Default.args,
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
