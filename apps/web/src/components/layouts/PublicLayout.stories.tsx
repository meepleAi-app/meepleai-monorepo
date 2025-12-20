/**
 * PublicLayout Storybook Stories - Issue #2230
 *
 * Showcases different states of the PublicLayout component.
 */

import { fn } from 'storybook/test';

import { PublicLayout } from './PublicLayout';

import type { Meta, StoryObj } from '@storybook/react';

const meta = {
  title: 'Components/Layouts/PublicLayout',
  component: PublicLayout,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Complete layout wrapper for public pages, composing PublicHeader + content + PublicFooter.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    children: {
      description: 'Page content to render',
      control: false,
    },
    user: {
      description: 'Current user object (undefined if not authenticated)',
      control: 'object',
    },
    onLogout: {
      description: 'Callback function when user logs out',
      action: 'logout',
    },
    showNewsletter: {
      description: 'Show newsletter section in footer',
      control: 'boolean',
    },
    containerWidth: {
      description: 'Content container max width',
      control: 'select',
      options: ['sm', 'md', 'lg', 'xl', 'full'],
    },
    className: {
      description: 'Additional CSS classes for main content',
      control: 'text',
    },
  },
  args: {
    onLogout: fn(),
    children: (
      <div className="space-y-6">
        <h1 className="text-4xl font-bold">Welcome to MeepleAI</h1>
        <p className="text-lg text-muted-foreground">
          Il tuo assistente AI per le regole dei giochi da tavolo.
        </p>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div className="p-6 border rounded-lg">
            <h3 className="text-lg font-semibold mb-2">Feature 1</h3>
            <p className="text-sm text-muted-foreground">
              Lorem ipsum dolor sit amet, consectetur adipiscing elit.
            </p>
          </div>
          <div className="p-6 border rounded-lg">
            <h3 className="text-lg font-semibold mb-2">Feature 2</h3>
            <p className="text-sm text-muted-foreground">
              Sed do eiusmod tempor incididunt ut labore et dolore.
            </p>
          </div>
          <div className="p-6 border rounded-lg">
            <h3 className="text-lg font-semibold mb-2">Feature 3</h3>
            <p className="text-sm text-muted-foreground">
              Ut enim ad minim veniam, quis nostrud exercitation.
            </p>
          </div>
        </div>
      </div>
    ),
  },
} satisfies Meta<typeof PublicLayout>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default layout - not authenticated
 */
export const Default: Story = {
  args: {},
};

/**
 * Layout with authenticated user
 */
export const Authenticated: Story = {
  args: {
    user: {
      name: 'Mario Rossi',
      email: 'mario.rossi@example.com',
      avatar: 'https://i.pravatar.cc/150?img=12',
    },
  },
};

/**
 * Layout with small container width
 */
export const SmallContainer: Story = {
  args: {
    containerWidth: 'sm',
  },
};

/**
 * Layout with medium container width
 */
export const MediumContainer: Story = {
  args: {
    containerWidth: 'md',
  },
};

/**
 * Layout with large container width
 */
export const LargeContainer: Story = {
  args: {
    containerWidth: 'lg',
  },
};

/**
 * Layout with XL container width
 */
export const XLContainer: Story = {
  args: {
    containerWidth: 'xl',
  },
};

/**
 * Layout with newsletter in footer
 */
export const WithNewsletter: Story = {
  args: {
    showNewsletter: true,
    user: {
      name: 'Anna Bianchi',
      email: 'anna.bianchi@example.com',
    },
  },
};

/**
 * Mobile view
 */
export const Mobile: Story = {
  args: {},
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
};

/**
 * Tablet view
 */
export const Tablet: Story = {
  args: {
    user: {
      name: 'Luigi Verdi',
      email: 'luigi.verdi@example.com',
      avatar: 'https://i.pravatar.cc/150?img=7',
    },
  },
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
  },
};

/**
 * Long content to test footer sticky behavior
 */
export const LongContent: Story = {
  args: {
    children: (
      <div className="space-y-6">
        <h1 className="text-4xl font-bold">Long Content Page</h1>
        {Array.from({ length: 20 }).map((_, i) => (
          <div key={i} className="p-6 border rounded-lg">
            <h3 className="text-lg font-semibold mb-2">Section {i + 1}</h3>
            <p className="text-sm text-muted-foreground">
              Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor
              incididunt ut labore et dolore magna aliqua.
            </p>
          </div>
        ))}
      </div>
    ),
  },
};

/**
 * Short content to test footer sticky behavior
 */
export const ShortContent: Story = {
  args: {
    children: (
      <div>
        <h1 className="text-2xl font-bold">Short Content</h1>
        <p className="mt-4">This page has minimal content to test footer positioning.</p>
      </div>
    ),
  },
};

/**
 * Custom styling example
 */
export const CustomStyling: Story = {
  args: {
    className: 'bg-gradient-to-b from-primary/10 to-background',
    user: {
      name: 'Test User',
      email: 'test@example.com',
    },
  },
};
