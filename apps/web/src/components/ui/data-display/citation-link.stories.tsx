/**
 * CitationLink Storybook Stories
 *
 * Visual regression testing with Chromatic for CitationLink component.
 * Tests all interaction states and accessibility features.
 *
 * @see Issue #1833 (UI-006)
 */

import { CitationLink } from './citation-link';

import type { Meta, StoryObj } from '@storybook/react';

// Mock function for onClick handlers in Storybook 10
const fn = () => () => {};

// ============================================================================
// Story Configuration
// ============================================================================

const meta = {
  title: 'UI/CitationLink',
  component: CitationLink,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Orange accent badge for inline PDF citations with jump-to-page functionality. ' +
          'Part of the MeepleAI citation system (UI-006).',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    pageNumber: {
      control: { type: 'number', min: 1, max: 100 },
      description: 'PDF page number to reference',
    },
    documentName: {
      control: 'text',
      description: 'Optional document name for context',
    },
    onClick: {
      action: 'clicked',
      description: 'Click handler for jump-to-page action',
    },
    className: {
      control: 'text',
      description: 'Additional CSS classes',
    },
  },
  args: {
    onClick: fn(),
  },
} satisfies Meta<typeof CitationLink>;

export default meta;
type Story = StoryObj<typeof meta>;

// ============================================================================
// Base Stories
// ============================================================================

/**
 * Default citation link with page 5 reference
 */
export const Default: Story = {
  args: {
    pageNumber: 5,
  },
};

/**
 * Citation link with custom document name
 */
export const WithDocumentName: Story = {
  args: {
    pageNumber: 12,
    documentName: 'Catan',
  },
};

/**
 * Non-clickable citation link (display only)
 */
export const NonClickable: Story = {
  args: {
    pageNumber: 3,
    onClick: undefined,
  },
};

/**
 * Citation link with very high page number
 */
export const HighPageNumber: Story = {
  args: {
    pageNumber: 999,
  },
};

// ============================================================================
// Interaction States
// ============================================================================

/**
 * Clickable citation link (hover to see hover state)
 */
export const Clickable: Story = {
  args: {
    pageNumber: 7,
  },
  parameters: {
    docs: {
      description: {
        story: 'Hover over the badge to see the hover state (darker orange).',
      },
    },
  },
};

/**
 * Multiple citation links inline
 */
export const InlineMultiple: Story = {
  render: () => (
    <div className="flex flex-wrap gap-2 p-4">
      <CitationLink pageNumber={1} onClick={fn()} />
      <CitationLink pageNumber={5} onClick={fn()} />
      <CitationLink pageNumber={12} onClick={fn()} />
      <CitationLink pageNumber={23} onClick={fn()} />
      <CitationLink pageNumber={99} onClick={fn()} />
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Multiple citation links displayed inline (common use case in chat responses).',
      },
    },
  },
};

// ============================================================================
// Accessibility Testing
// ============================================================================

/**
 * Keyboard navigation test
 */
export const KeyboardNavigation: Story = {
  args: {
    pageNumber: 8,
  },
  parameters: {
    docs: {
      description: {
        story:
          'Focus on the badge with Tab key, then press Enter or Space to trigger click action. ' +
          'WCAG 2.1 AA compliant keyboard navigation.',
      },
    },
  },
};

/**
 * Screen reader test
 */
export const ScreenReaderFriendly: Story = {
  args: {
    pageNumber: 15,
    documentName: 'Terraforming Mars',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Screen readers announce: "Jump to Terraforming Mars p.15 in PDF, button". ' +
          'Non-clickable badges announce: "Reference to Regolamento p.15, status".',
      },
    },
  },
};

// ============================================================================
// Dark Mode Testing (Chromatic)
// ============================================================================

/**
 * Dark mode compatibility
 */
export const DarkMode: Story = {
  args: {
    pageNumber: 10,
  },
  parameters: {
    backgrounds: { default: 'dark' },
    docs: {
      description: {
        story: 'Orange badge maintains good contrast in dark mode (WCAG AA).',
      },
    },
  },
  decorators: [
    Story => (
      <div className="dark bg-slate-950 p-8">
        <Story />
      </div>
    ),
  ],
};

// ============================================================================
// Custom Styling
// ============================================================================

/**
 * Custom styled citation link
 */
export const CustomStyled: Story = {
  args: {
    pageNumber: 42,
    className: 'text-base px-4 py-2 rounded-full shadow-lg',
  },
  parameters: {
    docs: {
      description: {
        story: 'Custom styling via className prop (larger size, rounded, shadow).',
      },
    },
  },
};

// ============================================================================
// Edge Cases
// ============================================================================

/**
 * Minimum page number (1)
 */
export const MinimumPage: Story = {
  args: {
    pageNumber: 1,
  },
};

/**
 * Very long document name
 */
export const LongDocumentName: Story = {
  args: {
    pageNumber: 5,
    documentName: 'Advanced Squad Leader: Deluxe Edition Rulebook',
  },
  parameters: {
    docs: {
      description: {
        story: 'Badge adapts to long document names (text wrapping).',
      },
    },
  },
};

/**
 * Invalid page number handling
 */
export const InvalidPageNumber: Story = {
  render: () => (
    <div className="flex flex-col gap-4 p-4">
      <div>
        <p className="text-sm text-gray-600 mb-2">Valid: pageNumber=5</p>
        <CitationLink pageNumber={5} onClick={fn()} />
      </div>
      <div>
        <p className="text-sm text-gray-600 mb-2">Invalid: pageNumber=0 (renders null)</p>
        <CitationLink pageNumber={0} onClick={fn()} />
      </div>
      <div>
        <p className="text-sm text-gray-600 mb-2">Invalid: pageNumber=-5 (renders null)</p>
        <CitationLink pageNumber={-5} onClick={fn()} />
      </div>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Component validates page numbers and renders null for invalid values.',
      },
    },
  },
};
