/**
 * QuickActions Storybook Stories (Issue #1834: UI-007)
 *
 * Comprehensive stories for QuickActions component
 * Covers: default actions, custom actions, variants, responsive behavior
 */

import type { Meta, StoryObj } from '@storybook/react';
import { QuickActions } from './QuickActions';
import { QuickActionCard } from './QuickActionCard';
import { PlusCircle, MessageSquarePlus, Settings, Upload, Download, Trash2 } from 'lucide-react';

const meta = {
  title: 'Components/Dashboard/QuickActions',
  component: QuickActions,
  parameters: {
    layout: 'padded',
  },
  tags: ['autodocs'],
  argTypes: {
    actions: {
      description: 'Custom actions array (optional, uses defaults if not provided)',
    },
  },
} satisfies Meta<typeof QuickActions>;

export default meta;
type Story = StoryObj<typeof meta>;

// ============================================================================
// Default Actions Stories
// ============================================================================

export const Default: Story = {
  args: {},
  parameters: {
    docs: {
      description: {
        story: 'Default quick actions: Add Game + New Chat',
      },
    },
  },
};

export const DefaultWithContainer: Story = {
  render: () => (
    <div className="max-w-3xl mx-auto p-6 bg-background rounded-lg border">
      <h2 className="text-2xl font-quicksand font-bold mb-4">Quick Actions</h2>
      <QuickActions />
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Default actions in a typical dashboard container',
      },
    },
  },
};

// ============================================================================
// Custom Actions Stories
// ============================================================================

export const CustomActions: Story = {
  args: {
    actions: [
      {
        id: 'settings',
        icon: Settings,
        title: 'Settings',
        description: 'Configure your preferences',
        onClick: () => console.log('Settings clicked'),
        variant: 'default',
      },
      {
        id: 'upload',
        icon: Upload,
        title: 'Upload',
        description: 'Upload game rules PDF',
        onClick: () => console.log('Upload clicked'),
        variant: 'secondary',
      },
    ],
  },
  parameters: {
    docs: {
      description: {
        story: 'Custom actions with different icons and variants',
      },
    },
  },
};

export const ManyActions: Story = {
  args: {
    actions: [
      {
        id: 'add-game',
        icon: PlusCircle,
        title: 'Add Game',
        onClick: () => console.log('Add Game'),
        variant: 'default',
      },
      {
        id: 'new-chat',
        icon: MessageSquarePlus,
        title: 'New Chat',
        onClick: () => console.log('New Chat'),
        variant: 'default',
      },
      {
        id: 'upload',
        icon: Upload,
        title: 'Upload PDF',
        onClick: () => console.log('Upload'),
        variant: 'secondary',
      },
      {
        id: 'download',
        icon: Download,
        title: 'Export Data',
        onClick: () => console.log('Download'),
        variant: 'secondary',
      },
    ],
  },
  parameters: {
    docs: {
      description: {
        story: 'Grid automatically wraps to multiple rows with 4+ actions',
      },
    },
  },
};

// ============================================================================
// Variant Stories
// ============================================================================

export const MixedVariants: Story = {
  args: {
    actions: [
      {
        id: 'primary-action',
        icon: PlusCircle,
        title: 'Primary Action',
        description: 'Default variant',
        onClick: () => console.log('Primary'),
        variant: 'default',
      },
      {
        id: 'secondary-action',
        icon: Settings,
        title: 'Secondary Action',
        description: 'Secondary variant',
        onClick: () => console.log('Secondary'),
        variant: 'secondary',
      },
    ],
  },
  parameters: {
    docs: {
      description: {
        story: 'Default vs Secondary variants side-by-side',
      },
    },
  },
};

// ============================================================================
// Interactive States
// ============================================================================

export const WithClickHandler: Story = {
  args: {
    actions: [
      {
        id: 'alert-action',
        icon: MessageSquarePlus,
        title: 'Click Me',
        description: 'Shows browser alert',
        onClick: () => alert('Action clicked!'),
        variant: 'default',
      },
      {
        id: 'console-action',
        icon: Settings,
        title: 'Console Log',
        description: 'Logs to console',
        onClick: () => console.log('Console action triggered'),
        variant: 'secondary',
      },
    ],
  },
  parameters: {
    docs: {
      description: {
        story: 'Interactive actions with actual click handlers (check console)',
      },
    },
  },
};

export const HoverState: Story = {
  args: {},
  parameters: {
    pseudo: { hover: true },
    docs: {
      description: {
        story: 'Hover state: translateY(-4px) + shadow-lg + icon scale(1.1)',
      },
    },
  },
};

export const FocusState: Story = {
  args: {},
  parameters: {
    pseudo: { focus: true },
    docs: {
      description: {
        story: 'Keyboard focus state for accessibility',
      },
    },
  },
};

// ============================================================================
// Responsive Layout
// ============================================================================

export const ResponsiveLayout: Story = {
  render: () => (
    <div className="space-y-8">
      <div>
        <h3 className="text-lg font-semibold mb-2">Mobile (1 column)</h3>
        <div className="max-w-xs">
          <QuickActions />
        </div>
      </div>
      <div>
        <h3 className="text-lg font-semibold mb-2">Desktop (2 columns)</h3>
        <div className="max-w-3xl">
          <QuickActions />
        </div>
      </div>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Responsive grid: 1 column (mobile) → 2 columns (tablet+)',
      },
    },
  },
};

// ============================================================================
// QuickActionCard Isolated Stories
// ============================================================================

const cardMeta = {
  title: 'Components/Dashboard/QuickActionCard',
  component: QuickActionCard,
  parameters: {
    layout: 'padded',
  },
  tags: ['autodocs'],
} satisfies Meta<typeof QuickActionCard>;

export { cardMeta as QuickActionCardMeta };

type CardStory = StoryObj<typeof QuickActionCard>;

export const CardDefault: CardStory = {
  args: {
    icon: PlusCircle,
    title: 'Add Game',
    description: 'Add a new board game to your collection',
    onClick: () => console.log('Card clicked'),
    variant: 'default',
  },
};

export const CardSecondary: CardStory = {
  args: {
    icon: Settings,
    title: 'Settings',
    description: 'Configure your preferences',
    onClick: () => console.log('Settings clicked'),
    variant: 'secondary',
  },
};

export const CardWithoutDescription: CardStory = {
  args: {
    icon: MessageSquarePlus,
    title: 'New Chat',
    onClick: () => console.log('Chat clicked'),
    variant: 'default',
  },
};

export const CardLongTitle: CardStory = {
  args: {
    icon: Upload,
    title: 'Upload Multiple Game Rules PDF Documents at Once',
    description: 'This is a very long description that should truncate properly with ellipsis',
    onClick: () => console.log('Upload clicked'),
    variant: 'default',
  },
};

export const CardHoverState: CardStory = {
  args: {
    icon: PlusCircle,
    title: 'Hover Me',
    description: 'Card with hover effect',
    onClick: () => {},
    variant: 'default',
  },
  parameters: {
    pseudo: { hover: true },
  },
};
