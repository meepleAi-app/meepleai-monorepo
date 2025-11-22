import type { Meta, StoryObj } from '@storybook/react';
import { Toggle } from './toggle';
import { Bold, Italic, Underline, AlignLeft, AlignCenter, AlignRight } from 'lucide-react';

/**
 * Toggle component for boolean state with visual feedback.
 * Based on Radix UI Toggle primitive.
 */
const meta = {
  title: 'UI/Toggle',
  component: Toggle,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: 'select',
      options: ['default', 'outline'],
      description: 'Visual style variant of the toggle',
    },
    size: {
      control: 'select',
      options: ['default', 'sm', 'lg'],
      description: 'Size variant of the toggle',
    },
    disabled: {
      control: 'boolean',
      description: 'Whether the toggle is disabled',
    },
  },
} satisfies Meta<typeof Toggle>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default toggle
 */
export const Default: Story = {
  args: {
    children: <Bold className="h-4 w-4" />,
  },
};

/**
 * Outline variant
 */
export const Outline: Story = {
  args: {
    variant: 'outline',
    children: <Italic className="h-4 w-4" />,
  },
};

/**
 * Small size
 */
export const Small: Story = {
  args: {
    size: 'sm',
    children: <Bold className="h-4 w-4" />,
  },
};

/**
 * Large size
 */
export const Large: Story = {
  args: {
    size: 'lg',
    children: <Bold className="h-4 w-4" />,
  },
};

/**
 * Disabled toggle
 */
export const Disabled: Story = {
  args: {
    disabled: true,
    children: <Bold className="h-4 w-4" />,
  },
};

/**
 * With text
 */
export const WithText: Story = {
  args: {
    children: (
      <>
        <Bold className="h-4 w-4" />
        Bold
      </>
    ),
  },
};

/**
 * Text formatting example
 */
export const TextFormatting: Story = {
  render: () => (
    <div className="flex gap-2">
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
};

/**
 * Text alignment example
 */
export const TextAlignment: Story = {
  render: () => (
    <div className="flex gap-2">
      <Toggle aria-label="Align left">
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
};

/**
 * Outline variant group
 */
export const OutlineGroup: Story = {
  render: () => (
    <div className="flex gap-2">
      <Toggle variant="outline" aria-label="Toggle bold">
        <Bold className="h-4 w-4" />
      </Toggle>
      <Toggle variant="outline" aria-label="Toggle italic">
        <Italic className="h-4 w-4" />
      </Toggle>
      <Toggle variant="outline" aria-label="Toggle underline">
        <Underline className="h-4 w-4" />
      </Toggle>
    </div>
  ),
};

/**
 * Mixed sizes
 */
export const MixedSizes: Story = {
  render: () => (
    <div className="flex items-center gap-2">
      <Toggle size="sm" aria-label="Small toggle">
        <Bold className="h-4 w-4" />
      </Toggle>
      <Toggle aria-label="Default toggle">
        <Bold className="h-4 w-4" />
      </Toggle>
      <Toggle size="lg" aria-label="Large toggle">
        <Bold className="h-4 w-4" />
      </Toggle>
    </div>
  ),
};

/**
 * Pressed state
 */
export const Pressed: Story = {
  args: {
    pressed: true,
    children: <Bold className="h-4 w-4" />,
  },
};

/**
 * Editor toolbar example
 */
export const EditorToolbar: Story = {
  render: () => (
    <div className="inline-flex gap-1 p-1 border rounded-lg bg-background">
      <Toggle variant="outline" size="sm" aria-label="Bold">
        <Bold className="h-4 w-4" />
      </Toggle>
      <Toggle variant="outline" size="sm" aria-label="Italic">
        <Italic className="h-4 w-4" />
      </Toggle>
      <Toggle variant="outline" size="sm" aria-label="Underline">
        <Underline className="h-4 w-4" />
      </Toggle>
      <div className="w-px bg-border mx-1" />
      <Toggle variant="outline" size="sm" aria-label="Align left">
        <AlignLeft className="h-4 w-4" />
      </Toggle>
      <Toggle variant="outline" size="sm" aria-label="Align center">
        <AlignCenter className="h-4 w-4" />
      </Toggle>
      <Toggle variant="outline" size="sm" aria-label="Align right">
        <AlignRight className="h-4 w-4" />
      </Toggle>
    </div>
  ),
};
