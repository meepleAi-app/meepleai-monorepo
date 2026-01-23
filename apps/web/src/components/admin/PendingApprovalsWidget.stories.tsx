import type { Meta, StoryObj } from '@storybook/react';
import { PendingApprovalsWidget } from './PendingApprovalsWidget';

const meta: Meta<typeof PendingApprovalsWidget> = {
  title: 'Admin/PendingApprovalsWidget',
  component: PendingApprovalsWidget,
  tags: ['autodocs'],
  parameters: {
    layout: 'padded',
    docs: {
      description: {
        component:
          'Widget for displaying pending game approvals with quick approve/reject actions. Part of the Admin Dashboard. See Issue #2789.',
      },
    },
  },
  argTypes: {
    className: {
      control: 'text',
      description: 'Additional CSS classes',
    },
    limit: {
      control: { type: 'number', min: 1, max: 10 },
      description: 'Maximum number of items to display',
    },
  },
};

export default meta;
type Story = StoryObj<typeof PendingApprovalsWidget>;

// ============================================================================
// Stories
// ============================================================================

/**
 * Default state with mock data
 */
export const Default: Story = {
  args: {
    limit: 3,
  },
};

/**
 * Widget with custom limit (showing 5 items)
 */
export const CustomLimit: Story = {
  args: {
    limit: 5,
  },
};

/**
 * Widget with custom styling
 */
export const WithCustomStyling: Story = {
  args: {
    className: 'border-2 border-blue-200 shadow-lg',
    limit: 3,
  },
};

/**
 * Empty state - no pending approvals
 */
export const EmptyState: Story = {
  args: {
    limit: 3,
  },
  parameters: {
    docs: {
      description: {
        story: 'Displays empty state when there are no pending approvals.',
      },
    },
  },
};

/**
 * Loading state
 */
export const Loading: Story = {
  args: {
    limit: 3,
  },
  parameters: {
    docs: {
      description: {
        story: 'Shows skeleton loaders while fetching data.',
      },
    },
  },
};

/**
 * Error state
 */
export const ErrorState: Story = {
  args: {
    limit: 3,
  },
  parameters: {
    docs: {
      description: {
        story: 'Displays error message when API request fails.',
      },
    },
  },
};

/**
 * Single item
 */
export const SingleItem: Story = {
  args: {
    limit: 1,
  },
  parameters: {
    docs: {
      description: {
        story: 'Widget with only one pending approval.',
      },
    },
  },
};

/**
 * Many items (exceeds limit)
 */
export const ManyItems: Story = {
  args: {
    limit: 3,
  },
  parameters: {
    docs: {
      description: {
        story: 'Widget showing "Vedi tutti" link when total exceeds limit.',
      },
    },
  },
};

/**
 * Compact variant (limit=2)
 */
export const Compact: Story = {
  args: {
    limit: 2,
  },
  parameters: {
    docs: {
      description: {
        story: 'Compact version showing only 2 items.',
      },
    },
  },
};

/**
 * Interactive demo
 */
export const Interactive: Story = {
  args: {
    limit: 3,
  },
  parameters: {
    docs: {
      description: {
        story: 'Fully interactive widget - click approve/reject buttons to test actions.',
      },
    },
  },
};
