/**
 * RatingStars Storybook Stories (Issue #1830: UI-003)
 *
 * Reusable star rating component stories
 * Covers: BGG conversion, sizes, variants, half-stars
 */

import type { Meta, StoryObj } from '@storybook/react';
import { RatingStars } from './rating-stars';

const meta = {
  title: 'Components/UI/RatingStars',
  component: RatingStars,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
  argTypes: {
    rating: {
      control: { type: 'range', min: 0, max: 10, step: 0.1 },
      description: 'Rating value (0-10 for BGG, 0-5 for display)',
    },
    maxRating: {
      control: 'select',
      options: [5, 10],
      description: 'Maximum rating scale',
    },
    size: {
      control: 'select',
      options: ['sm', 'md', 'lg'],
      description: 'Star size',
    },
    showHalfStars: {
      control: 'boolean',
      description: 'Show half stars for decimal ratings',
    },
    showValue: {
      control: 'boolean',
      description: 'Display numeric rating value',
    },
    variant: {
      control: 'select',
      options: ['default', 'muted'],
      description: 'Color variant',
    },
  },
} satisfies Meta<typeof RatingStars>;

export default meta;
type Story = StoryObj<typeof meta>;

// ============================================================================
// BGG Rating Conversion (0-10 → 0-5 stars)
// ============================================================================

export const BggRatingLow: Story = {
  args: {
    rating: 3.5,
    maxRating: 10,
    showValue: true,
  },
  parameters: {
    docs: {
      description: {
        story: 'BGG rating 3.5/10 → 1.75 stars (rounded to 2 with half-star)',
      },
    },
  },
};

export const BggRatingMedium: Story = {
  args: {
    rating: 7.4,
    maxRating: 10,
    showValue: true,
  },
  parameters: {
    docs: {
      description: {
        story: 'BGG rating 7.4/10 → 3.7 stars (rounded to 4 with half-star)',
      },
    },
  },
};

export const BggRatingHigh: Story = {
  args: {
    rating: 8.9,
    maxRating: 10,
    showValue: true,
  },
  parameters: {
    docs: {
      description: {
        story: 'BGG rating 8.9/10 → 4.45 stars (rounded to 4.5 stars)',
      },
    },
  },
};

export const BggRatingPerfect: Story = {
  args: {
    rating: 10.0,
    maxRating: 10,
    showValue: true,
  },
  parameters: {
    docs: {
      description: {
        story: 'BGG rating 10/10 → 5 stars (perfect score)',
      },
    },
  },
};

// ============================================================================
// Sizes
// ============================================================================

export const SizeSmall: Story = {
  args: {
    rating: 7.8,
    maxRating: 10,
    size: 'sm',
    showValue: true,
  },
};

export const SizeMedium: Story = {
  args: {
    rating: 7.8,
    maxRating: 10,
    size: 'md',
    showValue: true,
  },
};

export const SizeLarge: Story = {
  args: {
    rating: 7.8,
    maxRating: 10,
    size: 'lg',
    showValue: true,
  },
};

// ============================================================================
// Half Stars
// ============================================================================

export const WithHalfStars: Story = {
  args: {
    rating: 7.3,
    maxRating: 10,
    showHalfStars: true,
    showValue: true,
  },
  parameters: {
    docs: {
      description: {
        story: 'Half stars enabled: 7.3/10 → 3.65 stars → 3.5 stars displayed',
      },
    },
  },
};

export const WithoutHalfStars: Story = {
  args: {
    rating: 7.3,
    maxRating: 10,
    showHalfStars: false,
    showValue: true,
  },
  parameters: {
    docs: {
      description: {
        story: 'Half stars disabled: 7.3/10 → 3.65 stars → 3 full stars displayed',
      },
    },
  },
};

// ============================================================================
// Value Display
// ============================================================================

export const WithValue: Story = {
  args: {
    rating: 8.2,
    maxRating: 10,
    showValue: true,
  },
};

export const WithoutValue: Story = {
  args: {
    rating: 8.2,
    maxRating: 10,
    showValue: false,
  },
};

// ============================================================================
// Variants
// ============================================================================

export const VariantDefault: Story = {
  args: {
    rating: 7.5,
    maxRating: 10,
    variant: 'default',
    showValue: true,
  },
  parameters: {
    docs: {
      description: {
        story: 'Default variant: yellow stars with muted empty stars',
      },
    },
  },
};

export const VariantMuted: Story = {
  args: {
    rating: 7.5,
    maxRating: 10,
    variant: 'muted',
    showValue: true,
  },
  parameters: {
    docs: {
      description: {
        story: 'Muted variant: all muted colors for subtle display',
      },
    },
  },
};

// ============================================================================
// Edge Cases
// ============================================================================

export const ZeroRating: Story = {
  args: {
    rating: 0,
    maxRating: 10,
    showValue: true,
  },
  parameters: {
    docs: {
      description: {
        story: 'Zero rating: all empty stars',
      },
    },
  },
};

export const FiveStarScale: Story = {
  args: {
    rating: 3.5,
    maxRating: 5,
    showValue: true,
  },
  parameters: {
    docs: {
      description: {
        story: 'Direct 5-star scale (no conversion needed)',
      },
    },
  },
};

// ============================================================================
// Comparison Grid
// ============================================================================

export const ComparisonGrid: Story = {
  render: () => (
    <div className="flex flex-col gap-4 p-4">
      <div className="text-sm font-semibold">BGG Rating Conversion (0-10 → 0-5 stars)</div>
      <div className="grid grid-cols-2 gap-4">
        <div>
          <div className="text-xs text-muted-foreground mb-1">Low (3.5/10)</div>
          <RatingStars rating={3.5} maxRating={10} showValue />
        </div>
        <div>
          <div className="text-xs text-muted-foreground mb-1">Medium (7.4/10)</div>
          <RatingStars rating={7.4} maxRating={10} showValue />
        </div>
        <div>
          <div className="text-xs text-muted-foreground mb-1">High (8.9/10)</div>
          <RatingStars rating={8.9} maxRating={10} showValue />
        </div>
        <div>
          <div className="text-xs text-muted-foreground mb-1">Perfect (10/10)</div>
          <RatingStars rating={10.0} maxRating={10} showValue />
        </div>
      </div>

      <div className="text-sm font-semibold mt-4">Sizes</div>
      <div className="grid grid-cols-3 gap-4">
        <div>
          <div className="text-xs text-muted-foreground mb-1">Small</div>
          <RatingStars rating={7.8} maxRating={10} size="sm" showValue />
        </div>
        <div>
          <div className="text-xs text-muted-foreground mb-1">Medium</div>
          <RatingStars rating={7.8} maxRating={10} size="md" showValue />
        </div>
        <div>
          <div className="text-xs text-muted-foreground mb-1">Large</div>
          <RatingStars rating={7.8} maxRating={10} size="lg" showValue />
        </div>
      </div>

      <div className="text-sm font-semibold mt-4">Variants</div>
      <div className="grid grid-cols-2 gap-4">
        <div>
          <div className="text-xs text-muted-foreground mb-1">Default</div>
          <RatingStars rating={7.5} maxRating={10} variant="default" showValue />
        </div>
        <div>
          <div className="text-xs text-muted-foreground mb-1">Muted</div>
          <RatingStars rating={7.5} maxRating={10} variant="muted" showValue />
        </div>
      </div>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Comprehensive comparison of all rating variants and configurations',
      },
    },
  },
};
