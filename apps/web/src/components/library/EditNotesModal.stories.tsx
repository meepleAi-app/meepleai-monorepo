/**
 * EditNotesModal Storybook Stories (Issue #2852, Phase 4)
 *
 * Component-level stories for notes editing modal.
 * Tests character counter, validation, and save states.
 */

import type { Meta, StoryObj } from '@storybook/react';
import { within, userEvent, expect } from '@storybook/test';
import { fn } from '@storybook/test';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { EditNotesModal } from './EditNotesModal';

// Create QueryClient for React Query
const queryClient = new QueryClient({
  defaultOptions: {
    queries: { retry: false, staleTime: Infinity },
  },
});

const meta: Meta<typeof EditNotesModal> = {
  title: 'Components/Library/EditNotesModal',
  component: EditNotesModal,
  parameters: {
    layout: 'centered',
    chromatic: {
      viewports: [375, 768, 1920],
      delay: 300,
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
      description: 'Modal open state',
    },
    gameTitle: {
      control: 'text',
      description: 'Game title for display',
    },
    currentNotes: {
      control: 'text',
      description: 'Current notes value',
    },
    onClose: { action: 'closed' },
    onNotesUpdated: { action: 'notes-updated' },
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
    currentNotes: '',
    onClose: fn(),
    onNotesUpdated: fn(),
  },
};

export const WithExistingNotes: Story = {
  args: {
    isOpen: true,
    gameId: 'game-123',
    gameTitle: 'Wingspan',
    currentNotes: 'Ottimo con 3 giocatori. Espansione Europa consigliata.',
    onClose: fn(),
    onNotesUpdated: fn(),
  },
};

export const NearCharacterLimit: Story = {
  args: {
    isOpen: true,
    gameId: 'game-123',
    gameTitle: 'Gloomhaven',
    currentNotes: 'A'.repeat(470), // 470 characters, 30 remaining
    onClose: fn(),
    onNotesUpdated: fn(),
  },
};

export const AtCharacterLimit: Story = {
  args: {
    isOpen: true,
    gameId: 'game-123',
    gameTitle: 'Terraforming Mars',
    currentNotes: 'A'.repeat(500), // 500 characters, 0 remaining
    onClose: fn(),
    onNotesUpdated: fn(),
  },
};

// ============================================================================
// Interaction States
// ============================================================================

export const FocusedTextarea: Story = {
  args: {
    ...Default.args,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const textarea = canvas.getByPlaceholderText('Inserisci le tue note qui...');

    await userEvent.click(textarea);
  },
};

export const TypingNotes: Story = {
  args: {
    ...Default.args,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const textarea = canvas.getByPlaceholderText('Inserisci le tue note qui...');

    await userEvent.click(textarea);
    await userEvent.type(
      textarea,
      'Questo gioco è fantastico! Lo consiglio a tutti i giocatori esperti.',
      { delay: 30 }
    );
  },
};

export const HoverSaveButton: Story = {
  args: {
    ...WithExistingNotes.args,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const saveButton = canvas.getByRole('button', { name: /Salva/i });

    await userEvent.hover(saveButton);
  },
};

export const HoverCancelButton: Story = {
  args: {
    ...WithExistingNotes.args,
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const cancelButton = canvas.getByRole('button', { name: 'Annulla' });

    await userEvent.hover(cancelButton);
  },
};

// ============================================================================
// Loading States
// ============================================================================

export const SavingState: Story = {
  args: {
    ...WithExistingNotes.args,
  },
  parameters: {
    docs: {
      description: {
        story: 'Loading state shown during save operation (mocked)',
      },
    },
  },
};

// ============================================================================
// Edge Cases
// ============================================================================

export const EmptyNotes: Story = {
  args: {
    isOpen: true,
    gameId: 'game-123',
    gameTitle: 'Carcassonne',
    currentNotes: null,
    onClose: fn(),
    onNotesUpdated: fn(),
  },
};

export const LongGameTitle: Story = {
  args: {
    isOpen: true,
    gameId: 'game-123',
    gameTitle: 'Gloomhaven: Jaws of the Lion - Complete Edition with All Expansions',
    currentNotes: 'Great campaign game!',
    onClose: fn(),
    onNotesUpdated: fn(),
  },
};

// ============================================================================
// Responsive Layouts
// ============================================================================

export const Mobile: Story = {
  args: {
    ...WithExistingNotes.args,
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
    ...WithExistingNotes.args,
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
    ...WithExistingNotes.args,
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
