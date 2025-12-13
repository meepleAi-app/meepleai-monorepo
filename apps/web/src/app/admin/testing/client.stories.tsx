import type { Meta, StoryObj } from '@storybook/react';
import { TestingDashboardClient } from './client';

const meta: Meta<typeof TestingDashboardClient> = {
  title: 'Admin/TestingDashboard',
  component: TestingDashboardClient,
  tags: ['autodocs'],
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Consolidated testing dashboard displaying results from accessibility (Issue #841), performance (Issue #842), and E2E coverage (Issue #843) automated tests.',
      },
    },
  },
};

export default meta;
type Story = StoryObj<typeof TestingDashboardClient>;

/**
 * Default state showing the Accessibility tab.
 * Displays Lighthouse score, axe violations, WCAG compliance levels, and critical issues.
 */
export const Default: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Dashboard with Accessibility tab active by default.',
      },
    },
  },
};

/**
 * Accessibility tab with perfect scores (no violations).
 * This represents the target state after all issues are resolved.
 */
export const AccessibilityPerfect: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Accessibility tab showing ideal state with perfect compliance.',
      },
    },
  },
};

/**
 * Performance tab showing Core Web Vitals and budget status.
 * Displays LCP, FID, CLS metrics and resource budgets for JS, CSS, and images.
 */
export const PerformanceTab: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Performance metrics including Core Web Vitals and budget status.',
      },
    },
  },
};

/**
 * E2E Coverage tab showing test execution results.
 * Displays coverage percentage, pass rate, flaky rate, and critical user journeys.
 */
export const E2ECoverageTab: Story = {
  parameters: {
    docs: {
      description: {
        story: 'E2E test results showing coverage and critical journey status.',
      },
    },
  },
};

/**
 * Interactive state allowing tab switching for visual regression testing.
 * Tests all three tab states and their transitions.
 */
export const Interactive: Story = {
  parameters: {
    docs: {
      description: {
        story: 'Interactive dashboard for testing tab switching and state transitions.',
      },
    },
    chromatic: {
      // Enable interaction testing for Chromatic
      disableSnapshot: false,
      delay: 300, // Wait 300ms after interactions
    },
  },
};
