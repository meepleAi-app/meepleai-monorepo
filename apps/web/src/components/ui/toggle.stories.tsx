import type { Meta, StoryObj } from '@storybook/react';
import { Toggle } from './toggle';
import { Bold, Italic, Underline, AlignLeft, AlignCenter, AlignRight } from 'lucide-react';

/**
 * Toggle component for on/off state controls with button styling.
 *
 * ## shadcn/ui Component
 * Based on Radix UI Toggle with variant and size options.
 *
 * ## Features
 * - **2 variants**: default, outline
 * - **3 sizes**: sm, default, lg
 * - **Pressed state**: Visual indicator for active state
 * - **Icon support**: Works great with icons
 *
 * ## Accessibility
 * - ✅ ARIA role="button" with pressed state
 * - ✅ Keyboard navigation (Space, Enter)
 * - ✅ Focus ring indicator
 * - ✅ Screen reader friendly
 */
const meta = {
  title: 'UI/Toggle',
  component: Toggle,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component: 'A two-state button that can be either on or off.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: 'select',
      options: ['default', 'outline'],
      description: 'Visual style variant',
      table: {
        type: { summary: '"default" | "outline"' },
        defaultValue: { summary: 'default' },
      },
    },
    size: {
      control: 'select',
      options: ['sm', 'default', 'lg'],
      description: 'Toggle size',
      table: {
        type: { summary: '"sm" | "default" | "lg"' },
        defaultValue: { summary: 'default' },
      },
    },
    disabled: {
      control: 'boolean',
      description: 'Disabled state',
    },
  },
} satisfies Meta<typeof Toggle>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default toggle.
 * Basic toggle with text.
 */
export const Default: Story = {
  args: {
    children: 'Toggle',
    variant: 'default',
  },
};

/**
 * Outline variant.
 * Toggle with border styling.
 */
export const Outline: Story = {
  args: {
    children: 'Outline',
    variant: 'outline',
  },
};

/**
 * With icon.
 * Toggle with icon only.
 */
export const WithIcon: Story = {
  args: {
    children: <Bold className="h-4 w-4" />,
    'aria-label': 'Toggle bold',
  },
};

/**
 * Small size.
 * Compact toggle for dense UIs.
 */
export const Small: Story = {
  args: {
    children: <Italic className="h-4 w-4" />,
    size: 'sm',
    'aria-label': 'Toggle italic',
  },
};

/**
 * Large size.
 * Larger toggle for emphasis.
 */
export const Large: Story = {
  args: {
    children: <Underline className="h-4 w-4" />,
    size: 'lg',
    'aria-label': 'Toggle underline',
  },
};

/**
 * Disabled state.
 * Non-interactive toggle.
 */
export const Disabled: Story = {
  args: {
    children: 'Disabled',
    disabled: true,
  },
};

/**
 * Text formatting toolbar.
 * Common text editor pattern.
 */
export const TextFormattingToolbar: Story = {
  render: () => (
    <div className="flex items-center gap-1 p-2 border rounded-md">
      <Toggle aria-label="Toggle bold">
        <Bold className="h-4 w-4" />
      </Toggle>
      <Toggle aria-label="Toggle italic">
        <Italic className="h-4 w-4" />
      </Toggle>
      <Toggle aria-label="Toggle underline">
        <Underline className="h-4 w-4" />
      </Toggle>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Toggle group for text formatting controls.',
      },
    },
  },
};

/**
 * Alignment toolbar.
 * Text alignment toggle group.
 */
export const AlignmentToolbar: Story = {
  render: () => (
    <div className="flex items-center gap-1 p-2 border rounded-md">
      <Toggle aria-label="Align left" defaultPressed>
        <AlignLeft className="h-4 w-4" />
      </Toggle>
      <Toggle aria-label="Align center">
        <AlignCenter className="h-4 w-4" />
      </Toggle>
      <Toggle aria-label="Align right">
        <AlignRight className="h-4 w-4" />
      </Toggle>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Toggle group for text alignment options.',
      },
    },
  },
};

/**
 * With text and icon.
 * Toggle combining icon and label.
 */
export const WithTextAndIcon: Story = {
  render: () => (
    <Toggle aria-label="Toggle bold">
      <Bold className="mr-2 h-4 w-4" />
      Bold
    </Toggle>
  ),
};

/**
 * All variants comparison.
 * Visual comparison of variants.
 */
export const AllVariants: Story = {
  render: () => (
    <div className="flex flex-col gap-4">
      <div className="flex gap-2">
        <Toggle variant="default">Default</Toggle>
        <Toggle variant="outline">Outline</Toggle>
      </div>
      <div className="flex gap-2">
        <Toggle variant="default" defaultPressed>
          Pressed Default
        </Toggle>
        <Toggle variant="outline" defaultPressed>
          Pressed Outline
        </Toggle>
      </div>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Comparison of all toggle variants and states.',
      },
    },
  },
};

/**
 * All sizes comparison.
 * Visual comparison of sizes.
 */
export const AllSizes: Story = {
  render: () => (
    <div className="flex items-center gap-4">
      <Toggle size="sm">
        <Bold className="h-4 w-4" />
      </Toggle>
      <Toggle size="default">
        <Bold className="h-4 w-4" />
      </Toggle>
      <Toggle size="lg">
        <Bold className="h-4 w-4" />
      </Toggle>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Comparison of all available toggle sizes.',
      },
    },
  },
};

/**
 * Editor toolbar example.
 * Complete rich text editor toolbar.
 */
export const EditorToolbar: Story = {
  render: () => (
    <div className="flex flex-wrap items-center gap-1 p-2 border rounded-md">
      <div className="flex gap-1">
        <Toggle size="sm" aria-label="Toggle bold">
          <Bold className="h-4 w-4" />
        </Toggle>
        <Toggle size="sm" aria-label="Toggle italic">
          <Italic className="h-4 w-4" />
        </Toggle>
        <Toggle size="sm" aria-label="Toggle underline">
          <Underline className="h-4 w-4" />
        </Toggle>
      </div>
      <div className="h-6 w-px bg-border mx-1" />
      <div className="flex gap-1">
        <Toggle size="sm" aria-label="Align left" defaultPressed>
          <AlignLeft className="h-4 w-4" />
        </Toggle>
        <Toggle size="sm" aria-label="Align center">
          <AlignCenter className="h-4 w-4" />
        </Toggle>
        <Toggle size="sm" aria-label="Align right">
          <AlignRight className="h-4 w-4" />
        </Toggle>
      </div>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Complete editor toolbar with grouped toggles.',
      },
    },
  },
};

/**
 * Dark theme variant.
 * Shows toggle appearance on dark background.
 */
export const DarkTheme: Story = {
  render: () => (
    <div className="space-y-4">
      <div className="flex gap-2">
        <Toggle variant="default">
          <Bold className="h-4 w-4" />
        </Toggle>
        <Toggle variant="outline">
          <Italic className="h-4 w-4" />
        </Toggle>
      </div>
      <div className="flex gap-2">
        <Toggle variant="default" defaultPressed>
          <Bold className="h-4 w-4" />
        </Toggle>
        <Toggle variant="outline" defaultPressed>
          <Italic className="h-4 w-4" />
        </Toggle>
      </div>
      <div className="flex items-center gap-1 p-2 border rounded-md">
        <Toggle size="sm" aria-label="Toggle bold">
          <Bold className="h-4 w-4" />
        </Toggle>
        <Toggle size="sm" aria-label="Toggle italic" defaultPressed>
          <Italic className="h-4 w-4" />
        </Toggle>
        <Toggle size="sm" aria-label="Toggle underline">
          <Underline className="h-4 w-4" />
        </Toggle>
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
