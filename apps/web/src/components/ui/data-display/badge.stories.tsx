import { Badge } from './badge';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * Badge component for labels, tags, status indicators, and counts.
 *
 * ## shadcn/ui Component
 * Built with class-variance-authority for variant management.
 *
 * ## Features
 * - **4 variants**: default, secondary, destructive, outline
 * - **Compact design**: Small text with rounded corners
 * - **Hover states**: Interactive feedback on all variants
 * - **Focus indicators**: Accessible keyboard navigation
 *
 * ## Accessibility
 * - ✅ Focus ring indicator for keyboard navigation
 * - ✅ Color contrast compliant (WCAG AA)
 * - ✅ Supports aria-label for screen readers
 * - ✅ Semantic div element with inline-flex display
 */
const meta = {
  title: 'UI/Badge',
  component: Badge,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Small count and labeling component for tags, status indicators, and notifications.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: 'select',
      options: ['default', 'secondary', 'destructive', 'outline'],
      description: 'Visual style variant',
      table: {
        type: { summary: '"default" | "secondary" | "destructive" | "outline"' },
        defaultValue: { summary: 'default' },
      },
    },
    children: {
      control: 'text',
      description: 'Badge content',
    },
  },
} satisfies Meta<typeof Badge>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default badge variant.
 * Primary style with solid background, ideal for important labels.
 */
export const Default: Story = {
  args: {
    children: 'Badge',
    variant: 'default',
  },
};

/**
 * Secondary badge variant.
 * Muted background for less prominent labels.
 */
export const Secondary: Story = {
  args: {
    children: 'Secondary',
    variant: 'secondary',
  },
};

/**
 * Destructive badge variant.
 * Use for errors, warnings, or critical status indicators.
 */
export const Destructive: Story = {
  args: {
    children: 'Destructive',
    variant: 'destructive',
  },
};

/**
 * Outline badge variant.
 * Border-only style for subtle labeling.
 */
export const Outline: Story = {
  args: {
    children: 'Outline',
    variant: 'outline',
  },
};

/**
 * Status indicators.
 * Common pattern for system status badges.
 */
export const StatusIndicators: Story = {
  render: () => (
    <div className="flex flex-wrap gap-2">
      <Badge variant="default">Active</Badge>
      <Badge variant="secondary">Pending</Badge>
      <Badge variant="destructive">Failed</Badge>
      <Badge variant="outline">Inactive</Badge>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Status badges for application states.',
      },
    },
  },
};

/**
 * Count badges.
 * Numeric indicators for notifications and quantities.
 */
export const CountBadges: Story = {
  render: () => (
    <div className="flex flex-wrap gap-4">
      <div className="flex items-center gap-2">
        <span className="text-sm text-muted-foreground">Messages</span>
        <Badge variant="default">12</Badge>
      </div>
      <div className="flex items-center gap-2">
        <span className="text-sm text-muted-foreground">Notifications</span>
        <Badge variant="destructive">3</Badge>
      </div>
      <div className="flex items-center gap-2">
        <span className="text-sm text-muted-foreground">Updates</span>
        <Badge variant="secondary">5</Badge>
      </div>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Badges used as notification counters.',
      },
    },
  },
};

/**
 * Tag badges.
 * Labels for categorization and filtering.
 */
export const Tags: Story = {
  render: () => (
    <div className="flex flex-wrap gap-2">
      <Badge variant="outline">React</Badge>
      <Badge variant="outline">TypeScript</Badge>
      <Badge variant="outline">Tailwind</Badge>
      <Badge variant="outline">Next.js</Badge>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Badges as tags for content categorization.',
      },
    },
  },
};

/**
 * Priority levels.
 * Color-coded priority indicators.
 */
export const PriorityLevels: Story = {
  render: () => (
    <div className="flex flex-col gap-2">
      <div className="flex items-center gap-2">
        <Badge variant="destructive">High</Badge>
        <span className="text-sm text-muted-foreground">Critical priority</span>
      </div>
      <div className="flex items-center gap-2">
        <Badge variant="default">Medium</Badge>
        <span className="text-sm text-muted-foreground">Normal priority</span>
      </div>
      <div className="flex items-center gap-2">
        <Badge variant="secondary">Low</Badge>
        <span className="text-sm text-muted-foreground">Optional priority</span>
      </div>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Priority indicators with descriptive labels.',
      },
    },
  },
};

/**
 * Inline badges.
 * Badges within text content.
 */
export const InlineUsage: Story = {
  render: () => (
    <p className="text-sm">
      This feature is <Badge variant="default">New</Badge> and currently in{' '}
      <Badge variant="secondary">Beta</Badge>
    </p>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Badges integrated within text content.',
      },
    },
  },
};

/**
 * Custom styled badges.
 * Examples of className customization.
 */
export const CustomStyling: Story = {
  render: () => (
    <div className="flex flex-wrap gap-2">
      <Badge className="bg-blue-500 text-white hover:bg-blue-600">Custom Blue</Badge>
      <Badge className="bg-green-500 text-white hover:bg-green-600">Custom Green</Badge>
      <Badge className="bg-purple-500 text-white hover:bg-purple-600">Custom Purple</Badge>
      <Badge className="bg-orange-500 text-white hover:bg-orange-600">Custom Orange</Badge>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Custom color badges using className override.',
      },
    },
  },
};

/**
 * Long text badge.
 * Tests badge with extended content.
 */
export const LongText: Story = {
  args: {
    children: 'This is a longer badge label for testing',
    variant: 'default',
  },
};

/**
 * All variants comparison.
 * Visual comparison of all badge variants.
 */
export const AllVariants: Story = {
  render: () => (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap gap-2">
        <Badge variant="default">Default</Badge>
        <Badge variant="secondary">Secondary</Badge>
      </div>
      <div className="flex flex-wrap gap-2">
        <Badge variant="destructive">Destructive</Badge>
        <Badge variant="outline">Outline</Badge>
      </div>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Comparison of all available badge variants.',
      },
    },
  },
};

/**
 * Dark theme variant.
 * Shows badge appearance on dark background.
 */
export const DarkTheme: Story = {
  render: () => (
    <div className="flex flex-wrap gap-2">
      <Badge variant="default">Default</Badge>
      <Badge variant="secondary">Secondary</Badge>
      <Badge variant="destructive">Destructive</Badge>
      <Badge variant="outline">Outline</Badge>
    </div>
  ),
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark p-8 bg-background">
        <Story />
      </div>
    ),
  ],
};
