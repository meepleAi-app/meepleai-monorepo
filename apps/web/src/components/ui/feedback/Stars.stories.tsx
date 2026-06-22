import { Stars } from './Stars';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * Fractional 5-star rating display.
 *
 * ## Canonical primitive
 * Lifted from `features/toolkit-detail/Stars` in #1469 to make the
 * component reusable cross-feature (game-detail BGG rating, player-detail
 * favorite-rating, etc.). The toolkit-detail entry now re-exports this
 * module.
 *
 * ## Features
 * - **Sub-step granularity**: any `value` ∈ [0.0, 5.0] renders precisely via
 *   a percentage-width clip on the foreground row (no half-icon glyphs).
 * - **Clamped input**: values outside [0, 5] (including NaN / Infinity)
 *   render as 0 or 5 — never throws.
 * - **Optional numeric suffix**: `showNumeric` renders e.g. `"4.7"` next to
 *   the stars in tabular-nums font.
 * - **Accessibility**: `role="img"` with an `aria-label` that defaults to
 *   `"Rating: {value} / 5"` (override via `ariaLabel` for localized text).
 *
 * ## Accessibility
 * - ✅ ARIA role="img"
 * - ✅ aria-label with current rating value
 * - ✅ Decorative glyphs hidden from screen readers (`aria-hidden`)
 */
const meta = {
  title: 'UI/Feedback/Stars',
  component: Stars,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Fractional 5-star rating display with percentage-width clipping for sub-step values.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    value: {
      control: { type: 'range', min: 0, max: 5, step: 0.1 },
      description: 'Rating value (0.0–5.0, clamped)',
    },
    showNumeric: {
      control: 'boolean',
      description: 'Render the numeric value next to the stars',
    },
    ariaLabel: {
      control: 'text',
      description: 'Accessible label override (default: `Rating: {value} / 5`)',
    },
    className: {
      control: 'text',
      description: 'Additional Tailwind classes forwarded to the root',
    },
  },
} satisfies Meta<typeof Stars>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: { value: 4.7 },
};

export const Empty: Story = {
  args: { value: 0 },
};

export const Half: Story = {
  args: { value: 2.5 },
};

export const Full: Story = {
  args: { value: 5 },
};

export const WithNumeric: Story = {
  args: { value: 4.7, showNumeric: true },
};

export const Localized: Story = {
  name: 'Localized aria-label',
  args: {
    value: 3,
    ariaLabel: 'Valutazione BGG: 3 stelle su 5',
    showNumeric: true,
  },
};

/**
 * The component clamps unexpected input (negative numbers, values > 5, NaN)
 * to a safe 0–5 range without throwing.
 */
export const ClampedInput: Story = {
  name: 'Clamped (NaN / out-of-range)',
  args: { value: Number.NaN, showNumeric: true },
};
