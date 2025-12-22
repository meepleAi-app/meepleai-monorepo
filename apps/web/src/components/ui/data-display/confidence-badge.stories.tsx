/**
 * ConfidenceBadge Storybook Stories
 *
 * Visual regression tests and component showcase for ConfidenceBadge.
 * Integrated with Chromatic for automated visual testing.
 *
 * @see Issue #1832 (UI-005)
 */

import { ConfidenceBadge } from './confidence-badge';

import type { Meta, StoryObj } from '@storybook/react';

const meta = {
  title: 'UI/ConfidenceBadge',
  component: ConfidenceBadge,
  parameters: {
    layout: 'centered',
    chromatic: {
      // Enable visual regression testing for all stories
      disableSnapshot: false,
    },
  },
  tags: ['autodocs'],
  argTypes: {
    confidence: {
      control: { type: 'range', min: 0, max: 100, step: 1 },
      description: 'Confidence score (0-100)',
    },
    showTooltip: {
      control: 'boolean',
      description: 'Show explanatory tooltip',
    },
  },
} satisfies Meta<typeof ConfidenceBadge>;

export default meta;
type Story = StoryObj<typeof meta>;

// ============================================================================
// Primary Stories - Main Confidence Levels
// ============================================================================

/**
 * High Confidence (≥85%)
 * Green badge indicating high accuracy expected
 */
export const HighConfidence: Story = {
  args: {
    confidence: 95,
    showTooltip: true,
  },
};

/**
 * Medium Confidence (70-84%)
 * Yellow badge indicating moderate confidence
 */
export const MediumConfidence: Story = {
  args: {
    confidence: 75,
    showTooltip: true,
  },
};

/**
 * Low Confidence (<70%)
 * Red badge indicating uncertainty, manual verification recommended
 */
export const LowConfidence: Story = {
  args: {
    confidence: 55,
    showTooltip: true,
  },
};

// ============================================================================
// Boundary Testing - Edge Cases
// ============================================================================

/**
 * Maximum Confidence (100%)
 * Edge case: Perfect confidence score
 */
export const MaximumConfidence: Story = {
  args: {
    confidence: 100,
    showTooltip: true,
  },
};

/**
 * Minimum Confidence (0%)
 * Edge case: Zero confidence score
 */
export const MinimumConfidence: Story = {
  args: {
    confidence: 0,
    showTooltip: true,
  },
};

/**
 * High Threshold Boundary (85%)
 * Exact threshold between medium and high confidence
 */
export const HighThresholdBoundary: Story = {
  args: {
    confidence: 85,
    showTooltip: true,
  },
};

/**
 * Medium Threshold Boundary (70%)
 * Exact threshold between low and medium confidence
 */
export const MediumThresholdBoundary: Story = {
  args: {
    confidence: 70,
    showTooltip: true,
  },
};

/**
 * Just Below High Threshold (84%)
 * Testing medium confidence upper bound
 */
export const JustBelowHighThreshold: Story = {
  args: {
    confidence: 84,
    showTooltip: true,
  },
};

/**
 * Just Below Medium Threshold (69%)
 * Testing low confidence upper bound
 */
export const JustBelowMediumThreshold: Story = {
  args: {
    confidence: 69,
    showTooltip: true,
  },
};

// ============================================================================
// Interaction Variants
// ============================================================================

/**
 * Without Tooltip
 * Badge only, no explanatory tooltip (accessibility: aria-label only)
 */
export const WithoutTooltip: Story = {
  args: {
    confidence: 85,
    showTooltip: false,
  },
};

/**
 * With Custom Class
 * Badge with additional styling
 */
export const WithCustomClass: Story = {
  args: {
    confidence: 90,
    showTooltip: true,
    className: 'shadow-lg ring-2 ring-offset-2 ring-green-500',
  },
};

// ============================================================================
// Chromatic Visual Regression Grid
// ============================================================================

/**
 * All Confidence Levels Grid
 * Comprehensive visual regression test showing all confidence states
 */
export const AllLevelsGrid: Story = {
  render: () => (
    <div className="flex flex-col gap-6 p-8">
      <div>
        <h3 className="text-lg font-semibold mb-3">High Confidence (≥85%)</h3>
        <div className="flex gap-4 items-center">
          <ConfidenceBadge confidence={100} />
          <ConfidenceBadge confidence={95} />
          <ConfidenceBadge confidence={90} />
          <ConfidenceBadge confidence={85} />
        </div>
      </div>

      <div>
        <h3 className="text-lg font-semibold mb-3">Medium Confidence (70-84%)</h3>
        <div className="flex gap-4 items-center">
          <ConfidenceBadge confidence={84} />
          <ConfidenceBadge confidence={80} />
          <ConfidenceBadge confidence={75} />
          <ConfidenceBadge confidence={70} />
        </div>
      </div>

      <div>
        <h3 className="text-lg font-semibold mb-3">Low Confidence (&lt;70%)</h3>
        <div className="flex gap-4 items-center">
          <ConfidenceBadge confidence={69} />
          <ConfidenceBadge confidence={50} />
          <ConfidenceBadge confidence={25} />
          <ConfidenceBadge confidence={0} />
        </div>
      </div>

      <div>
        <h3 className="text-lg font-semibold mb-3">Without Tooltip</h3>
        <div className="flex gap-4 items-center">
          <ConfidenceBadge confidence={95} showTooltip={false} />
          <ConfidenceBadge confidence={75} showTooltip={false} />
          <ConfidenceBadge confidence={55} showTooltip={false} />
        </div>
      </div>
    </div>
  ),
  parameters: {
    chromatic: {
      // Longer delay for grid rendering
      delay: 300,
    },
  },
};

// ============================================================================
// Accessibility Testing
// ============================================================================

/**
 * Accessibility: ARIA Labels
 * Ensures all badges have proper aria-label attributes
 */
export const AccessibilityARIA: Story = {
  render: () => (
    <div className="flex flex-col gap-4 p-8">
      <div>
        <p className="text-sm text-muted-foreground mb-2">
          Hover to inspect aria-label attributes in DevTools
        </p>
      </div>
      <div className="flex gap-4">
        <ConfidenceBadge confidence={95} showTooltip={false} />
        <ConfidenceBadge confidence={75} showTooltip={false} />
        <ConfidenceBadge confidence={55} showTooltip={false} />
      </div>
    </div>
  ),
};

/**
 * Accessibility: Keyboard Navigation
 * Tooltip should be accessible via keyboard focus
 */
export const AccessibilityKeyboard: Story = {
  render: () => (
    <div className="flex flex-col gap-4 p-8">
      <div>
        <p className="text-sm text-muted-foreground mb-2">
          Tab through badges to test keyboard navigation and tooltip triggers
        </p>
      </div>
      <div className="flex gap-4">
        <ConfidenceBadge confidence={95} />
        <ConfidenceBadge confidence={75} />
        <ConfidenceBadge confidence={55} />
      </div>
    </div>
  ),
};

// ============================================================================
// Dark Mode Testing
// ============================================================================

/**
 * Dark Mode Compatibility
 * Ensures badges are visible and aesthetically pleasing in dark mode
 */
export const DarkMode: Story = {
  args: {
    confidence: 85,
    showTooltip: true,
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
};
