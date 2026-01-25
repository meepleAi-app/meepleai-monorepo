import { Slider } from './slider';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * Slider component for numeric input with visual feedback.
 *
 * ## shadcn/ui Component
 * Based on Radix UI Slider with accessible keyboard controls.
 *
 * ## Features
 * - **Range selection**: Single or multiple values
 * - **Keyboard support**: Arrow keys, Home, End, Page Up/Down
 * - **Accessibility**: ARIA attributes, focus indicators
 * - **Customizable**: Min, max, step values
 *
 * ## Accessibility
 * - ✅ Keyboard navigation (Arrow keys, Home, End)
 * - ✅ Focus-visible ring indicator
 * - ✅ ARIA slider role with value/min/max
 * - ✅ Disabled state support
 */
const meta = {
  title: 'UI/Slider',
  component: Slider,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'A slider component for selecting numeric values with visual track and thumb. Supports single and range selection.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    defaultValue: {
      control: 'object',
      description: 'Default value(s) for uncontrolled slider',
      table: {
        type: { summary: 'number[]' },
      },
    },
    value: {
      control: 'object',
      description: 'Controlled value(s)',
      table: {
        type: { summary: 'number[]' },
      },
    },
    min: {
      control: 'number',
      description: 'Minimum value',
      table: {
        type: { summary: 'number' },
        defaultValue: { summary: '0' },
      },
    },
    max: {
      control: 'number',
      description: 'Maximum value',
      table: {
        type: { summary: 'number' },
        defaultValue: { summary: '100' },
      },
    },
    step: {
      control: 'number',
      description: 'Step increment',
      table: {
        type: { summary: 'number' },
        defaultValue: { summary: '1' },
      },
    },
    disabled: {
      control: 'boolean',
      description: 'Disabled state',
    },
  },
  decorators: [
    (Story) => (
      <div className="w-80">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof Slider>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default single-value slider.
 * Most common use case for numeric input.
 */
export const Default: Story = {
  args: {
    defaultValue: [50],
    min: 0,
    max: 100,
    step: 1,
  },
};

/**
 * Slider with custom range (1-10).
 * Useful for ratings or small numeric selections.
 */
export const CustomRange: Story = {
  args: {
    defaultValue: [5],
    min: 1,
    max: 10,
    step: 1,
  },
};

/**
 * Slider with decimal steps.
 * Allows fractional values for precision.
 */
export const DecimalStep: Story = {
  args: {
    defaultValue: [2.5],
    min: 0,
    max: 5,
    step: 0.5,
  },
};

/**
 * Range slider with two thumbs.
 * Use for selecting min-max ranges (e.g., price filters).
 */
export const RangeSlider: Story = {
  args: {
    defaultValue: [25, 75],
    min: 0,
    max: 100,
    step: 5,
  },
};

/**
 * Disabled slider state.
 * Non-interactive with reduced opacity.
 */
export const Disabled: Story = {
  args: {
    defaultValue: [50],
    disabled: true,
  },
};

/**
 * Volume control example.
 * Common use case with 0-100 range.
 */
export const VolumeControl: Story = {
  render: () => (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <label className="text-sm font-medium">Volume</label>
        <span className="text-sm text-muted-foreground">75%</span>
      </div>
      <Slider
        defaultValue={[75]}
        min={0}
        max={100}
        step={1}
        className="w-full"
      />
      <p className="text-xs text-muted-foreground">
        Note: Move slider to see real-time updates
      </p>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Interactive volume control with real-time value display.',
      },
    },
  },
};

/**
 * Price range filter example.
 * Dual-thumb slider for filtering price ranges.
 */
export const PriceRangeFilter: Story = {
  render: () => (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <label className="text-sm font-medium">Price Range</label>
        <span className="text-sm text-muted-foreground">$20 - $80</span>
      </div>
      <Slider
        defaultValue={[20, 80]}
        min={0}
        max={100}
        step={5}
        className="w-full"
      />
      <p className="text-xs text-muted-foreground">
        Drag handles to adjust price range
      </p>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Dual-thumb slider for selecting price ranges in filters.',
      },
    },
  },
};

/**
 * Rating selector (1-5 stars).
 * Discrete steps for star ratings.
 */
export const RatingSelector: Story = {
  render: () => (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <label className="text-sm font-medium">Rating</label>
        <span className="text-sm text-muted-foreground">⭐⭐⭐ (3/5)</span>
      </div>
      <Slider
        defaultValue={[3]}
        min={1}
        max={5}
        step={1}
        className="w-full"
      />
      <p className="text-xs text-muted-foreground">
        Slide to select rating (1-5 stars)
      </p>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Slider for selecting star ratings with visual feedback.',
      },
    },
  },
};

/**
 * Dark theme variant.
 * Shows slider appearance on dark background.
 */
export const DarkTheme: Story = {
  args: {
    defaultValue: [50],
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    (Story) => (
      <div className="dark w-80 p-8 bg-background">
        <Story />
      </div>
    ),
  ],
};
