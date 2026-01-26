import {
  AlignLeft,
  AlignCenter,
  AlignRight,
  Bold,
  Italic,
  Underline,
  List,
  ListOrdered,
} from 'lucide-react';

import { ToggleGroup, ToggleGroupItem } from './toggle-group';

import type { Meta, StoryObj } from '@storybook/react';

/* DISABLED - causes Maximum update depth
/* DISABLED - causes Maximum update depth
/* DISABLED - causes Maximum update depth
/**
 * ToggleGroup component for grouped toggle buttons.
 *
 * ## shadcn/ui Component
 * Based on Radix UI Toggle Group with single/multiple selection.
 *
 * ## Features
 * - **Selection modes**: Single or multiple selection
 * - **Variants**: default, outline
 * - **Sizes**: default, sm, lg
 * - **Keyboard support**: Arrow keys, Space, Tab
 *
 * ## Accessibility
 * - ✅ Keyboard navigation (Arrow keys, Space)
 * - ✅ Focus-visible indicators
 * - ✅ ARIA toolbar/radiogroup roles
 * - ✅ Disabled state support
 */
const meta = {
  title: 'UI/ToggleGroup',
  component: ToggleGroup,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'A toggle group component for selecting one or more options from a set. Commonly used for formatting toolbars and filter groups.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    type: {
      control: 'select',
      options: ['single', 'multiple'],
      description: 'Selection mode',
      table: {
        type: { summary: '"single" | "multiple"' },
        defaultValue: { summary: 'single' },
      },
    },
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
      options: ['default', 'sm', 'lg'],
      description: 'Button size',
      table: {
        type: { summary: '"default" | "sm" | "lg"' },
        defaultValue: { summary: 'default' },
      },
    },
    disabled: {
      control: 'boolean',
      description: 'Disabled state',
    },
  },
} satisfies Meta<typeof ToggleGroup>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default single-selection toggle group.
 * One active item at a time.
 */
export const Default: Story = {
  render: () => (
    <ToggleGroup type="single" defaultValue="center">
      <ToggleGroupItem value="left" aria-label="Align left">
        <AlignLeft className="h-4 w-4" />
      </ToggleGroupItem>
      <ToggleGroupItem value="center" aria-label="Align center">
        <AlignCenter className="h-4 w-4" />
      </ToggleGroupItem>
      <ToggleGroupItem value="right" aria-label="Align right">
        <AlignRight className="h-4 w-4" />
      </ToggleGroupItem>
    </ToggleGroup>
  ),
};

/**
 * Multiple selection mode.
 * Allows selecting multiple items simultaneously.
 */
export const Multiple: Story = {
  render: () => (
    <ToggleGroup type="multiple" defaultValue={['bold', 'italic']}>
      <ToggleGroupItem value="bold" aria-label="Bold">
        <Bold className="h-4 w-4" />
      </ToggleGroupItem>
      <ToggleGroupItem value="italic" aria-label="Italic">
        <Italic className="h-4 w-4" />
      </ToggleGroupItem>
      <ToggleGroupItem value="underline" aria-label="Underline">
        <Underline className="h-4 w-4" />
      </ToggleGroupItem>
    </ToggleGroup>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Toggle group with multiple selection enabled.',
      },
    },
  },
};

/**
 * Outline variant.
 * Bordered style for better separation.
 */
export const Outline: Story = {
  render: () => (
    <ToggleGroup type="single" variant="outline" defaultValue="center">
      <ToggleGroupItem value="left" aria-label="Align left">
        <AlignLeft className="h-4 w-4" />
      </ToggleGroupItem>
      <ToggleGroupItem value="center" aria-label="Align center">
        <AlignCenter className="h-4 w-4" />
      </ToggleGroupItem>
      <ToggleGroupItem value="right" aria-label="Align right">
        <AlignRight className="h-4 w-4" />
      </ToggleGroupItem>
    </ToggleGroup>
  ),
};

/**
 * Small size variant.
 * Compact version for dense UIs.
 */
export const Small: Story = {
  render: () => (
    <ToggleGroup type="single" size="sm" defaultValue="center">
      <ToggleGroupItem value="left" aria-label="Align left">
        <AlignLeft className="h-3 w-3" />
      </ToggleGroupItem>
      <ToggleGroupItem value="center" aria-label="Align center">
        <AlignCenter className="h-3 w-3" />
      </ToggleGroupItem>
      <ToggleGroupItem value="right" aria-label="Align right">
        <AlignRight className="h-3 w-3" />
      </ToggleGroupItem>
    </ToggleGroup>
  ),
};

/**
 * Large size variant.
 * Prominent version for important controls.
 */
export const Large: Story = {
  render: () => (
    <ToggleGroup type="single" size="lg" defaultValue="center">
      <ToggleGroupItem value="left" aria-label="Align left">
        <AlignLeft className="h-5 w-5" />
      </ToggleGroupItem>
      <ToggleGroupItem value="center" aria-label="Align center">
        <AlignCenter className="h-5 w-5" />
      </ToggleGroupItem>
      <ToggleGroupItem value="right" aria-label="Align right">
        <AlignRight className="h-5 w-5" />
      </ToggleGroupItem>
    </ToggleGroup>
  ),
};

/**
 * Disabled toggle group.
 * Non-interactive state.
 */
export const Disabled: Story = {
  render: () => (
    <ToggleGroup type="single" disabled defaultValue="center">
      <ToggleGroupItem value="left" aria-label="Align left">
        <AlignLeft className="h-4 w-4" />
      </ToggleGroupItem>
      <ToggleGroupItem value="center" aria-label="Align center">
        <AlignCenter className="h-4 w-4" />
      </ToggleGroupItem>
      <ToggleGroupItem value="right" aria-label="Align right">
        <AlignRight className="h-4 w-4" />
      </ToggleGroupItem>
    </ToggleGroup>
  ),
};

/* DISABLED - Interactive stories removed
 * Text alignment toolbar and Formatting toolbar stories
 */

/**
 * List style selector.
 * Switch between different list types.
 */
export const ListStyleSelector: Story = {
  render: () => (
    <ToggleGroup type="single" variant="outline" defaultValue="bullet">
      <ToggleGroupItem value="bullet" aria-label="Bullet list">
        <List className="h-4 w-4" />
      </ToggleGroupItem>
      <ToggleGroupItem value="numbered" aria-label="Numbered list">
        <ListOrdered className="h-4 w-4" />
      </ToggleGroupItem>
    </ToggleGroup>
  ),
  parameters: {
    docs: {
      description: {
        story: 'List style selector for editor toolbars.',
      },
    },
  },
};

/* DISABLED - Interactive stories removed
 * View mode selector story
 */

/**
 * Dark theme variant.
 * Shows toggle group appearance on dark background.
 */
export const DarkTheme: Story = {
  render: () => (
    <ToggleGroup type="single" variant="outline" defaultValue="center">
      <ToggleGroupItem value="left" aria-label="Align left">
        <AlignLeft className="h-4 w-4" />
      </ToggleGroupItem>
      <ToggleGroupItem value="center" aria-label="Align center">
        <AlignCenter className="h-4 w-4" />
      </ToggleGroupItem>
      <ToggleGroupItem value="right" aria-label="Align right">
        <AlignRight className="h-4 w-4" />
      </ToggleGroupItem>
    </ToggleGroup>
  ),
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    (Story) => (
      <div className="dark p-8 bg-background">
        <Story />
      </div>
    ),
  ],
};
