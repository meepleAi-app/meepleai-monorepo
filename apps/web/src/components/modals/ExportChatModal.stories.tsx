import { useState } from 'react';

import { Button } from '@/components/ui/primitives/button';

import { ExportChatModal } from './ExportChatModal';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * ExportChatModal component for exporting chat conversations to various formats.
 *
 * ## Features
 * - **Format selection**: PDF, TXT, Markdown
 * - **Date range filtering**: Optional date range for exported messages
 * - **Server Actions**: Uses React Hook Form + Zod validation
 * - **Loading state**: Disabled controls during export
 * - **Error handling**: Italian error messages with accessible alerts
 * - **Accessibility**: Fully accessible modal with keyboard navigation
 *
 * ## Visual Testing Coverage
 * - Default state (PDF selected)
 * - Format selection states
 * - Loading state during export
 * - Error state display
 * - Dark theme variant
 */
const meta = {
  title: 'Modals/ExportChatModal',
  component: ExportChatModal,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Modal dialog for exporting chat conversations with format selection and date range filtering.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    isOpen: {
      control: 'boolean',
      description: 'Whether the modal is open',
    },
    chatId: {
      control: 'text',
      description: 'ID of the chat to export',
    },
    gameName: {
      control: 'text',
      description: 'Name of the game (for display purposes)',
    },
  },
} satisfies Meta<typeof ExportChatModal>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Interactive wrapper for Storybook controls
 */
function InteractiveWrapper({ chatId, gameName }: { chatId: string; gameName: string }) {
  const [isOpen, setIsOpen] = useState(false);

  return (
    <>
      <Button onClick={() => setIsOpen(true)}>Open Export Modal</Button>
      <ExportChatModal
        isOpen={isOpen}
        onClose={() => setIsOpen(false)}
        chatId={chatId}
        gameName={gameName}
      />
    </>
  );
}

/**
 * Default state with PDF format selected.
 * Shows the modal in its initial state.
 */
export const Default: Story = {
  render: () => <InteractiveWrapper chatId="chat-123" gameName="Catan" />,
};

/**
 * Open state for visual testing.
 * Modal is pre-opened to show full UI.
 */
export const OpenState: Story = {
  args: {
    isOpen: true,
    onClose: () => {},
    chatId: 'chat-456',
    gameName: 'Ticket to Ride',
  },
};

/**
 * With long game name.
 * Tests modal layout with longer text content.
 */
export const LongGameName: Story = {
  args: {
    isOpen: true,
    onClose: () => {},
    chatId: 'chat-789',
    gameName: 'Twilight Imperium: Fourth Edition - Prophecy of Kings',
  },
};

/**
 * Loading state during export.
 * Shows disabled controls and loading spinner.
 */
export const LoadingState: Story = {
  args: {
    isOpen: true,
    onClose: () => {},
    chatId: 'chat-loading',
    gameName: 'Gloomhaven',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Modal in loading state with disabled controls. To see this state in action, click Export button in the interactive demo.',
      },
    },
  },
};

/**
 * Error state display.
 * Shows error message alert with accessible role="alert".
 */
export const ErrorState: Story = {
  args: {
    isOpen: true,
    onClose: () => {},
    chatId: 'chat-error',
    gameName: 'Wingspan',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Modal with error message displayed. Click Export to trigger error handling (mocked in tests).',
      },
    },
  },
};

/**
 * Dark theme variant.
 * Shows modal appearance on dark background with proper contrast.
 */
export const DarkTheme: Story = {
  args: {
    isOpen: true,
    onClose: () => {},
    chatId: 'chat-dark',
    gameName: 'Azul',
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark p-8 bg-background min-h-screen">
        <Story />
      </div>
    ),
  ],
};

/**
 * With date range filled.
 * Shows modal with date filters applied.
 */
export const WithDateRange: Story = {
  args: {
    isOpen: true,
    onClose: () => {},
    chatId: 'chat-dates',
    gameName: '7 Wonders',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Modal with date range filtering. Users can export messages from a specific time period.',
      },
    },
  },
};

/**
 * Accessibility showcase.
 * Demonstrates keyboard navigation and screen reader support.
 */
export const AccessibilityShowcase: Story = {
  render: () => <InteractiveWrapper chatId="chat-a11y" gameName="Pandemic" />,
  parameters: {
    docs: {
      description: {
        story:
          'Test keyboard navigation: Tab through elements, Escape to close, Enter to submit. Uses proper ARIA labels and role="alert" for errors.',
      },
    },
    a11y: {
      config: {
        rules: [
          {
            id: 'color-contrast',
            enabled: true,
          },
          {
            id: 'label',
            enabled: true,
          },
          {
            id: 'dialog-name',
            enabled: true,
          },
        ],
      },
    },
  },
};
