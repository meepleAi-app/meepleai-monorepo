import { Separator } from './separator';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * Separator component for visual division of content sections.
 *
 * ## shadcn/ui Component
 * Based on Radix UI Separator with horizontal and vertical orientations.
 *
 * ## Features
 * - **2 orientations**: horizontal, vertical
 * - **Decorative**: Optional semantic vs visual-only separator
 * - **Flexible sizing**: Customizable width and height
 * - **Semantic HTML**: Proper ARIA role for accessibility
 *
 * ## Accessibility
 * - ✅ ARIA role="separator" (when not decorative)
 * - ✅ Decorative option for purely visual separators
 * - ✅ Proper orientation attribute
 * - ✅ Screen reader friendly
 */
const meta = {
  title: 'UI/Separator',
  component: Separator,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component: 'Visually or semantically separates content.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    orientation: {
      control: 'select',
      options: ['horizontal', 'vertical'],
      description: 'Separator orientation',
      table: {
        type: { summary: '"horizontal" | "vertical"' },
        defaultValue: { summary: 'horizontal' },
      },
    },
    decorative: {
      control: 'boolean',
      description: 'Whether separator is decorative or semantic',
      table: {
        defaultValue: { summary: 'true' },
      },
    },
  },
  decorators: [
    Story => (
      <div className="w-[400px]">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof Separator>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default horizontal separator.
 * Standard divider for content sections.
 */
export const Default: Story = {
  args: {
    orientation: 'horizontal',
  },
};

/**
 * Vertical separator.
 * Divider for horizontal layouts.
 */
export const Vertical: Story = {
  render: () => (
    <div className="flex h-20 items-center">
      <div className="px-4">Item 1</div>
      <Separator orientation="vertical" />
      <div className="px-4">Item 2</div>
      <Separator orientation="vertical" />
      <div className="px-4">Item 3</div>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Vertical separators between inline items.',
      },
    },
  },
};

/**
 * Text section divider.
 * Separates text content sections.
 */
export const TextDivider: Story = {
  render: () => (
    <div>
      <div className="space-y-1">
        <h4 className="text-sm font-medium leading-none">Radix Primitives</h4>
        <p className="text-sm text-muted-foreground">An open-source UI component library.</p>
      </div>
      <Separator className="my-4" />
      <div className="flex h-5 items-center space-x-4 text-sm">
        <div>Blog</div>
        <Separator orientation="vertical" />
        <div>Docs</div>
        <Separator orientation="vertical" />
        <div>Source</div>
      </div>
    </div>
  ),
};

/**
 * List separator.
 * Divides list items.
 */
export const ListSeparator: Story = {
  render: () => (
    <div className="space-y-4">
      <div>
        <h4 className="text-sm font-medium">Item 1</h4>
        <p className="text-sm text-muted-foreground">Description for item 1</p>
      </div>
      <Separator />
      <div>
        <h4 className="text-sm font-medium">Item 2</h4>
        <p className="text-sm text-muted-foreground">Description for item 2</p>
      </div>
      <Separator />
      <div>
        <h4 className="text-sm font-medium">Item 3</h4>
        <p className="text-sm text-muted-foreground">Description for item 3</p>
      </div>
    </div>
  ),
};

/**
 * Menu separator.
 * Divides menu sections.
 */
export const MenuSeparator: Story = {
  render: () => (
    <div className="w-48 space-y-2 p-2 border rounded-md">
      <div className="px-2 py-1.5 text-sm font-semibold">Account</div>
      <div className="px-2 py-1.5 text-sm hover:bg-accent cursor-pointer rounded-sm">Profile</div>
      <div className="px-2 py-1.5 text-sm hover:bg-accent cursor-pointer rounded-sm">Settings</div>
      <Separator className="my-1" />
      <div className="px-2 py-1.5 text-sm hover:bg-accent cursor-pointer rounded-sm">Log out</div>
    </div>
  ),
};

/**
 * Card divider.
 * Separates card sections.
 */
export const CardDivider: Story = {
  render: () => (
    <div className="w-full max-w-md border rounded-lg p-6 space-y-4">
      <div>
        <h3 className="text-lg font-semibold">Card Title</h3>
        <p className="text-sm text-muted-foreground">Card description goes here</p>
      </div>
      <Separator />
      <div className="space-y-2">
        <p className="text-sm">This is the main content of the card.</p>
        <p className="text-sm text-muted-foreground">Additional details can be added here.</p>
      </div>
      <Separator />
      <div className="flex justify-end gap-2">
        <button className="px-4 py-2 text-sm">Cancel</button>
        <button className="px-4 py-2 text-sm bg-primary text-primary-foreground rounded-md">
          Confirm
        </button>
      </div>
    </div>
  ),
};

/**
 * Toolbar separator.
 * Divides toolbar action groups.
 */
export const ToolbarSeparator: Story = {
  render: () => (
    <div className="flex items-center space-x-2 p-2 border rounded-md">
      <button className="px-2 py-1 text-sm hover:bg-accent rounded">Bold</button>
      <button className="px-2 py-1 text-sm hover:bg-accent rounded">Italic</button>
      <Separator orientation="vertical" className="h-6" />
      <button className="px-2 py-1 text-sm hover:bg-accent rounded">Left</button>
      <button className="px-2 py-1 text-sm hover:bg-accent rounded">Center</button>
      <button className="px-2 py-1 text-sm hover:bg-accent rounded">Right</button>
      <Separator orientation="vertical" className="h-6" />
      <button className="px-2 py-1 text-sm hover:bg-accent rounded">Link</button>
    </div>
  ),
};

/**
 * Dark theme variant.
 * Shows separator appearance on dark background.
 */
export const DarkTheme: Story = {
  render: () => (
    <div className="space-y-6">
      <div className="space-y-1">
        <h4 className="text-sm font-medium leading-none">Horizontal Separator</h4>
        <p className="text-sm text-muted-foreground">Divides content vertically</p>
      </div>
      <Separator />
      <div className="flex h-20 items-center">
        <div className="px-4">Vertical</div>
        <Separator orientation="vertical" />
        <div className="px-4">Separators</div>
        <Separator orientation="vertical" />
        <div className="px-4">Between</div>
        <Separator orientation="vertical" />
        <div className="px-4">Items</div>
      </div>
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
