import type { Meta, StoryObj } from '@storybook/react';
import { Button } from './button';
import { Mail, Loader2, ChevronRight } from 'lucide-react';

/**
 * Button component with multiple variants, sizes, and states.
 *
 * ## shadcn/ui Component
 * Based on Radix UI Slot with class-variance-authority for variants.
 *
 * ## Features
 * - **6 variants**: default, destructive, outline, secondary, ghost, link
 * - **4 sizes**: default, sm, lg, icon
 * - **States**: default, disabled, loading
 * - **Composition**: Supports icons, asChild prop for composition
 *
 * ## Accessibility
 * - ✅ Keyboard navigation (Tab, Enter, Space)
 * - ✅ Focus-visible ring indicator
 * - ✅ Disabled state with pointer-events-none
 * - ✅ ARIA attributes inherited from button element
 */
const meta = {
  title: 'UI/Button',
  component: Button,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'A versatile button component with multiple variants, sizes, and states. Supports composition via asChild prop.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: 'select',
      options: ['default', 'destructive', 'outline', 'secondary', 'ghost', 'link'],
      description: 'Visual style variant',
      table: {
        type: { summary: '"default" | "destructive" | "outline" | "secondary" | "ghost" | "link"' },
        defaultValue: { summary: 'default' },
      },
    },
    size: {
      control: 'select',
      options: ['default', 'sm', 'lg', 'icon'],
      description: 'Button size',
      table: {
        type: { summary: '"default" | "sm" | "lg" | "icon"' },
        defaultValue: { summary: 'default' },
      },
    },
    asChild: {
      control: 'boolean',
      description: 'Merge props with child element (Radix Slot)',
      table: {
        type: { summary: 'boolean' },
        defaultValue: { summary: 'false' },
      },
    },
    disabled: {
      control: 'boolean',
      description: 'Disabled state',
    },
  },
} satisfies Meta<typeof Button>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default button with primary background.
 * Most common button style for primary actions.
 */
export const Default: Story = {
  args: {
    children: 'Button',
    variant: 'default',
  },
};

/**
 * Destructive button for dangerous actions.
 * Use for delete, remove, or irreversible operations.
 */
export const Destructive: Story = {
  args: {
    children: 'Delete',
    variant: 'destructive',
  },
};

/**
 * Outline button with border.
 * Use for secondary actions or less prominent buttons.
 */
export const Outline: Story = {
  args: {
    children: 'Outline',
    variant: 'outline',
  },
};

/**
 * Secondary button with muted background.
 * Use for tertiary actions or complementary buttons.
 */
export const Secondary: Story = {
  args: {
    children: 'Secondary',
    variant: 'secondary',
  },
};

/**
 * Ghost button with transparent background.
 * Use for minimal UI or toolbar actions.
 */
export const Ghost: Story = {
  args: {
    children: 'Ghost',
    variant: 'ghost',
  },
};

/**
 * Link button styled as hyperlink.
 * Use for navigation or text-like actions.
 */
export const Link: Story = {
  args: {
    children: 'Link',
    variant: 'link',
  },
};

/**
 * Small button size.
 * Use for compact UIs or dense layouts.
 */
export const Small: Story = {
  args: {
    children: 'Small',
    size: 'sm',
  },
};

/**
 * Large button size.
 * Use for prominent calls-to-action.
 */
export const Large: Story = {
  args: {
    children: 'Large',
    size: 'lg',
  },
};

/**
 * Icon-only button.
 * Use for toolbar actions or space-constrained UIs.
 */
export const Icon: Story = {
  args: {
    size: 'icon',
    children: <ChevronRight />,
  },
};

/**
 * Disabled button state.
 * Non-interactive with reduced opacity.
 */
export const Disabled: Story = {
  args: {
    children: 'Disabled',
    disabled: true,
  },
};

/**
 * Button with leading icon.
 * Common pattern for actions with visual context.
 */
export const WithIcon: Story = {
  args: {
    children: (
      <>
        <Mail className="mr-2 h-4 w-4" />
        Login with Email
      </>
    ),
  },
};

/**
 * Loading button with spinner.
 * Shows loading state during async operations.
 */
export const Loading: Story = {
  args: {
    disabled: true,
    children: (
      <>
        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
        Please wait
      </>
    ),
  },
};

/**
 * All variants comparison.
 * Visual comparison of all button variants.
 */
export const AllVariants: Story = {
  render: () => (
    <div className="flex flex-col gap-4">
      <div className="flex gap-2">
        <Button variant="default">Default</Button>
        <Button variant="destructive">Destructive</Button>
        <Button variant="outline">Outline</Button>
      </div>
      <div className="flex gap-2">
        <Button variant="secondary">Secondary</Button>
        <Button variant="ghost">Ghost</Button>
        <Button variant="link">Link</Button>
      </div>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Comparison of all available button variants.',
      },
    },
  },
};

/**
 * All sizes comparison.
 * Visual comparison of all button sizes.
 */
export const AllSizes: Story = {
  render: () => (
    <div className="flex items-center gap-4">
      <Button size="sm">Small</Button>
      <Button size="default">Default</Button>
      <Button size="lg">Large</Button>
      <Button size="icon">
        <ChevronRight />
      </Button>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Comparison of all available button sizes.',
      },
    },
  },
};

/**
 * Dark theme variant.
 * Shows button appearance on dark background.
 */
export const DarkTheme: Story = {
  args: {
    children: 'Dark Theme',
  },
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
