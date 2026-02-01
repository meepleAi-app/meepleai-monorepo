/**
 * SessionQuotaSection Component Stories (Issue #3075)
 *
 * Visual documentation for dashboard session quota widget.
 * Demonstrates different quota states in dashboard card format.
 */

import { SessionQuotaSection } from './SessionQuotaSection';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * SessionQuotaSection displays session quota status as a dashboard widget card.
 *
 * ## Features
 * - Card-based quota display with session count
 * - Color-coded warning states (yellow ≥80%, red at limit)
 * - Link to sessions management page
 * - Unlimited sessions display for admins
 * - Skeleton loading state
 * - Error handling
 *
 * ## Accessibility
 * - ✅ Semantic HTML with proper ARIA labels
 * - ✅ Color contrast compliant (WCAG AA)
 * - ✅ Keyboard accessible navigation link
 * - ✅ Screen reader friendly labels
 */
const meta = {
  title: 'Dashboard/SessionQuotaSection',
  component: SessionQuotaSection,
  parameters: {
    layout: 'padded',
    docs: {
      description: {
        component:
          'Dashboard widget card showing session quota status with link to sessions page.',
      },
    },
  },
  tags: ['autodocs'],
} satisfies Meta<typeof SessionQuotaSection>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default story - uses real useSessionQuotaWithStatus hook.
 * Will show loading state until mock data is provided.
 */
export const Default: Story = {
  parameters: {
    docs: {
      description: {
        story:
          'Default widget using real hook. In Storybook, this will show loading state unless API is mocked.',
      },
    },
  },
};

/**
 * Loading state - fetching quota data.
 * Shows skeleton placeholders for count and status.
 */
export const Loading: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/users/me/session-quota',
        method: 'GET',
        status: 200,
        delay: 10000, // Simulate slow response
      },
    ],
    docs: {
      description: {
        story: 'Loading state with skeleton placeholders while quota data is being fetched.',
      },
    },
  },
};

/**
 * Error state - failed to fetch quota data.
 * Shows error alert with retry message.
 */
export const Error: Story = {
  parameters: {
    mockData: [
      {
        url: '/api/v1/users/me/session-quota',
        method: 'GET',
        status: 500,
        response: { error: 'Internal server error' },
      },
    ],
    docs: {
      description: {
        story: 'Error state when quota data fails to load. Shows destructive alert.',
      },
    },
  },
};
