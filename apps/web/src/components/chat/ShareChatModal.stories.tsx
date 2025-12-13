/**
 * ShareChatModal Storybook Stories (Issue #2052)
 *
 * Visual testing for shareable chat thread link creation modal.
 * Tests both form state and success state with URL display.
 */

import type { Meta, StoryObj } from '@storybook/react';
import { ShareChatModal } from './ShareChatModal';
import { useState } from 'react';
import { fn } from 'storybook/test';

const meta: Meta<typeof ShareChatModal> = {
  title: 'Chat/ShareChatModal',
  component: ShareChatModal,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Modal for creating shareable links for chat threads with role selection (view/comment) and expiry configuration.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    isOpen: {
      control: 'boolean',
      description: 'Whether the modal is open',
    },
    threadId: {
      control: 'text',
      description: 'Chat thread ID to share',
    },
    onClose: {
      action: 'closed',
      description: 'Callback when modal should close',
    },
    onShareLinkCreated: {
      action: 'share-link-created',
      description: 'Callback when share link is successfully created',
    },
  },
};

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Interactive wrapper for Storybook control
 */
function InteractiveWrapper(props: {
  children: (isOpen: boolean, setIsOpen: (open: boolean) => void) => React.ReactNode;
}) {
  const [isOpen, setIsOpen] = useState(true);
  return <>{props.children(isOpen, setIsOpen)}</>;
}

/**
 * Default state: Modal open with empty form (view role, 7 days)
 */
export const Default: Story = {
  args: {
    isOpen: true,
    threadId: '550e8400-e29b-41d4-a716-446655440000',
    onClose: fn(),
    onShareLinkCreated: fn(),
  },
  render: args => (
    <InteractiveWrapper>
      {(isOpen, setIsOpen) => (
        <ShareChatModal
          {...args}
          isOpen={isOpen}
          onClose={() => {
            setIsOpen(false);
            args.onClose?.();
          }}
        />
      )}
    </InteractiveWrapper>
  ),
};

/**
 * View only role selected
 */
export const ViewOnlyRole: Story = {
  args: {
    isOpen: true,
    threadId: '550e8400-e29b-41d4-a716-446655440001',
    onClose: fn(),
    onShareLinkCreated: fn(),
  },
  parameters: {
    docs: {
      description: {
        story: 'Modal with view-only access level selected. Recipients can read but not comment.',
      },
    },
  },
  render: args => (
    <InteractiveWrapper>
      {(isOpen, setIsOpen) => (
        <ShareChatModal
          {...args}
          isOpen={isOpen}
          onClose={() => {
            setIsOpen(false);
            args.onClose?.();
          }}
        />
      )}
    </InteractiveWrapper>
  ),
};

/**
 * Comment role with 30-day expiry
 */
export const CommentRoleLongExpiry: Story = {
  args: {
    isOpen: true,
    threadId: '550e8400-e29b-41d4-a716-446655440002',
    onClose: fn(),
    onShareLinkCreated: fn(),
  },
  parameters: {
    docs: {
      description: {
        story: 'Modal configured for comment access with 30-day expiration period.',
      },
    },
  },
  render: args => (
    <InteractiveWrapper>
      {(isOpen, setIsOpen) => (
        <ShareChatModal
          {...args}
          isOpen={isOpen}
          onClose={() => {
            setIsOpen(false);
            args.onClose?.();
          }}
        />
      )}
    </InteractiveWrapper>
  ),
};

/**
 * Closed state (for testing open/close behavior)
 */
export const Closed: Story = {
  args: {
    isOpen: false,
    threadId: '550e8400-e29b-41d4-a716-446655440003',
    onClose: fn(),
    onShareLinkCreated: fn(),
  },
  parameters: {
    docs: {
      description: {
        story: 'Modal in closed state. Useful for testing open/close transitions.',
      },
    },
  },
};

/**
 * Dark mode
 */
export const DarkMode: Story = {
  args: {
    isOpen: true,
    threadId: '550e8400-e29b-41d4-a716-446655440004',
    onClose: fn(),
    onShareLinkCreated: fn(),
  },
  parameters: {
    docs: {
      description: {
        story: 'Shows the modal in dark mode.',
      },
    },
    backgrounds: {
      default: 'dark',
    },
  },
  decorators: [
    Story => (
      <div className="dark">
        <Story />
      </div>
    ),
  ],
  render: args => (
    <InteractiveWrapper>
      {(isOpen, setIsOpen) => (
        <ShareChatModal
          {...args}
          isOpen={isOpen}
          onClose={() => {
            setIsOpen(false);
            args.onClose?.();
          }}
        />
      )}
    </InteractiveWrapper>
  ),
};
