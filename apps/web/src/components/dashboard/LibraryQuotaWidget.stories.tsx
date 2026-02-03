/**
 * LibraryQuotaWidget Storybook Stories (Issue #2857)
 *
 * Comprehensive stories for LibraryQuotaWidget component
 * Covers: loading, error, various usage levels, color coding, CTAs
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import type { LibraryQuotaResponse } from '@/lib/api';

import { LibraryQuotaWidget } from './LibraryQuotaWidget';

import type { Meta, StoryObj } from '@storybook/react';

// ============================================================================
// Mock Data
// ============================================================================

const createMockQuota = (override: Partial<LibraryQuotaResponse>): LibraryQuotaResponse => ({
  currentCount: 25,
  maxAllowed: 100,
  userTier: 'normal',
  remainingSlots: 75,
  percentageUsed: 25,
  ...override,
});

// ============================================================================
// Wrapper Component for Stories
// ============================================================================

// Create a wrapper that provides QueryClient and mocks the hook
const createWrapper = (mockData: LibraryQuotaResponse | null, _isLoading = false, _error: Error | null = null) => {
  // We'll use the actual component but with mocked data injected via decorator
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        refetchOnWindowFocus: false,
      },
    },
  });

  // Pre-populate the cache with mock data
  if (mockData) {
    queryClient.setQueryData(['library', 'quota'], mockData);
  }

  return function Wrapper({ children }: { children: React.ReactNode }) {
    return (
      <QueryClientProvider client={queryClient}>
        {children}
      </QueryClientProvider>
    );
  };
};

// ============================================================================
// Meta
// ============================================================================

const meta = {
  title: 'Components/Dashboard/LibraryQuotaWidget',
  component: LibraryQuotaWidget,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component: `
Dashboard widget showing library quota with animated progress bar.

**Features:**
- Animated progress fill (1.2s ease-out)
- Color-coded: Green (<70%), Yellow (70-90%), Red (>90%)
- Displays X/Y games text
- CTA: 'Manage Library' or 'Upgrade Plan' if >90%
        `,
      },
    },
  },
  tags: ['autodocs'],
  decorators: [
    (Story) => (
      <div className="w-[350px]">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof LibraryQuotaWidget>;

export default meta;
type Story = StoryObj<typeof meta>;

// ============================================================================
// Low Usage Stories (Green, < 70%)
// ============================================================================

export const LowUsage: Story = {
  decorators: [
    (Story) => {
      const Wrapper = createWrapper(createMockQuota({
        currentCount: 25,
        maxAllowed: 100,
        remainingSlots: 75,
        percentageUsed: 25,
      }));
      return (
        <Wrapper>
          <Story />
        </Wrapper>
      );
    },
  ],
  parameters: {
    docs: {
      description: {
        story: 'Low usage (25%) - Green progress bar, "Manage Library" CTA',
      },
    },
  },
};

export const EmptyLibrary: Story = {
  decorators: [
    (Story) => {
      const Wrapper = createWrapper(createMockQuota({
        currentCount: 0,
        maxAllowed: 50,
        remainingSlots: 50,
        percentageUsed: 0,
        userTier: 'free',
      }));
      return (
        <Wrapper>
          <Story />
        </Wrapper>
      );
    },
  ],
  parameters: {
    docs: {
      description: {
        story: 'Empty library (0%) - Green progress bar, "Manage Library" CTA',
      },
    },
  },
};

export const MidLowUsage: Story = {
  decorators: [
    (Story) => {
      const Wrapper = createWrapper(createMockQuota({
        currentCount: 50,
        maxAllowed: 100,
        remainingSlots: 50,
        percentageUsed: 50,
      }));
      return (
        <Wrapper>
          <Story />
        </Wrapper>
      );
    },
  ],
  parameters: {
    docs: {
      description: {
        story: 'Mid-low usage (50%) - Green progress bar, "Manage Library" CTA',
      },
    },
  },
};

// ============================================================================
// Medium Usage Stories (Yellow, 70-90%)
// ============================================================================

export const MediumUsageAt70: Story = {
  decorators: [
    (Story) => {
      const Wrapper = createWrapper(createMockQuota({
        currentCount: 70,
        maxAllowed: 100,
        remainingSlots: 30,
        percentageUsed: 70,
      }));
      return (
        <Wrapper>
          <Story />
        </Wrapper>
      );
    },
  ],
  parameters: {
    docs: {
      description: {
        story: 'At 70% threshold - Yellow progress bar starts, "Manage Library" CTA',
      },
    },
  },
};

export const MediumUsage: Story = {
  decorators: [
    (Story) => {
      const Wrapper = createWrapper(createMockQuota({
        currentCount: 80,
        maxAllowed: 100,
        remainingSlots: 20,
        percentageUsed: 80,
      }));
      return (
        <Wrapper>
          <Story />
        </Wrapper>
      );
    },
  ],
  parameters: {
    docs: {
      description: {
        story: 'Medium usage (80%) - Yellow progress bar, "Manage Library" CTA',
      },
    },
  },
};

export const MediumUsageAt90: Story = {
  decorators: [
    (Story) => {
      const Wrapper = createWrapper(createMockQuota({
        currentCount: 90,
        maxAllowed: 100,
        remainingSlots: 10,
        percentageUsed: 90,
      }));
      return (
        <Wrapper>
          <Story />
        </Wrapper>
      );
    },
  ],
  parameters: {
    docs: {
      description: {
        story: 'At 90% threshold - Still yellow, "Manage Library" CTA (>90% triggers upgrade)',
      },
    },
  },
};

// ============================================================================
// High Usage Stories (Red, > 90%)
// ============================================================================

export const HighUsage: Story = {
  decorators: [
    (Story) => {
      const Wrapper = createWrapper(createMockQuota({
        currentCount: 92,
        maxAllowed: 100,
        remainingSlots: 8,
        percentageUsed: 92,
        userTier: 'premium',
      }));
      return (
        <Wrapper>
          <Story />
        </Wrapper>
      );
    },
  ],
  parameters: {
    docs: {
      description: {
        story: 'High usage (92%) - Red progress bar, "Upgrade Plan" CTA appears',
      },
    },
  },
};

export const NearlyFull: Story = {
  decorators: [
    (Story) => {
      const Wrapper = createWrapper(createMockQuota({
        currentCount: 98,
        maxAllowed: 100,
        remainingSlots: 2,
        percentageUsed: 98,
        userTier: 'premium',
      }));
      return (
        <Wrapper>
          <Story />
        </Wrapper>
      );
    },
  ],
  parameters: {
    docs: {
      description: {
        story: 'Nearly full (98%) - Red progress bar, "Upgrade Plan" CTA',
      },
    },
  },
};

export const AtLimit: Story = {
  decorators: [
    (Story) => {
      const Wrapper = createWrapper(createMockQuota({
        currentCount: 100,
        maxAllowed: 100,
        remainingSlots: 0,
        percentageUsed: 100,
        userTier: 'premium',
      }));
      return (
        <Wrapper>
          <Story />
        </Wrapper>
      );
    },
  ],
  parameters: {
    docs: {
      description: {
        story: 'At limit (100%) - Red progress bar fully filled, "Upgrade Plan" CTA',
      },
    },
  },
};

// ============================================================================
// Different Tier Stories
// ============================================================================

export const FreeTier: Story = {
  decorators: [
    (Story) => {
      const Wrapper = createWrapper(createMockQuota({
        currentCount: 15,
        maxAllowed: 20,
        remainingSlots: 5,
        percentageUsed: 75,
        userTier: 'free',
      }));
      return (
        <Wrapper>
          <Story />
        </Wrapper>
      );
    },
  ],
  parameters: {
    docs: {
      description: {
        story: 'Free tier (75% of 20 games) - Yellow progress bar',
      },
    },
  },
};

export const PremiumTier: Story = {
  decorators: [
    (Story) => {
      const Wrapper = createWrapper(createMockQuota({
        currentCount: 150,
        maxAllowed: 500,
        remainingSlots: 350,
        percentageUsed: 30,
        userTier: 'premium',
      }));
      return (
        <Wrapper>
          <Story />
        </Wrapper>
      );
    },
  ],
  parameters: {
    docs: {
      description: {
        story: 'Premium tier (30% of 500 games) - Green progress bar with large quota',
      },
    },
  },
};

// ============================================================================
// State Stories
// ============================================================================

export const Loading: Story = {
  decorators: [
    (Story) => {
      const queryClient = new QueryClient({
        defaultOptions: {
          queries: {
            retry: false,
            refetchOnWindowFocus: false,
          },
        },
      });
      // Don't set any data - hook will return loading state
      queryClient.setQueryDefaults(['library', 'quota'], {
        enabled: false,
      });
      return (
        <QueryClientProvider client={queryClient}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
  parameters: {
    docs: {
      description: {
        story: 'Loading state with skeleton placeholders',
      },
    },
  },
};

// Note: Error state would require mocking the hook directly which is complex in Storybook
// The component handles errors gracefully by showing an alert

// ============================================================================
// Layout Stories
// ============================================================================

export const InDashboardGrid: Story = {
  decorators: [
    (Story) => {
      const Wrapper = createWrapper(createMockQuota({
        currentCount: 45,
        maxAllowed: 100,
        remainingSlots: 55,
        percentageUsed: 45,
      }));
      return (
        <Wrapper>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4 w-[800px]">
            <Story />
            <div className="rounded-lg border p-4">Other Widget Placeholder</div>
            <div className="rounded-lg border p-4">Other Widget Placeholder</div>
          </div>
        </Wrapper>
      );
    },
  ],
  parameters: {
    docs: {
      description: {
        story: 'Widget in a typical dashboard grid layout',
      },
    },
  },
};

export const WithCustomClassName: Story = {
  args: {
    className: 'border-2 border-primary shadow-lg',
  },
  decorators: [
    (Story) => {
      const Wrapper = createWrapper(createMockQuota({
        currentCount: 60,
        maxAllowed: 100,
        remainingSlots: 40,
        percentageUsed: 60,
      }));
      return (
        <Wrapper>
          <Story />
        </Wrapper>
      );
    },
  ],
  parameters: {
    docs: {
      description: {
        story: 'Widget with custom className for additional styling',
      },
    },
  },
};

// ============================================================================
// Color Comparison Story
// ============================================================================

export const ColorComparison: Story = {
  decorators: [
    () => {
      // Create multiple query clients for different states
      const lowQueryClient = new QueryClient();
      lowQueryClient.setQueryData(['library', 'quota'], createMockQuota({
        currentCount: 30,
        maxAllowed: 100,
        percentageUsed: 30,
      }));

      const medQueryClient = new QueryClient();
      medQueryClient.setQueryData(['library', 'quota'], createMockQuota({
        currentCount: 80,
        maxAllowed: 100,
        percentageUsed: 80,
      }));

      const highQueryClient = new QueryClient();
      highQueryClient.setQueryData(['library', 'quota'], createMockQuota({
        currentCount: 95,
        maxAllowed: 100,
        percentageUsed: 95,
      }));

      return (
        <div className="flex flex-col gap-4 w-[350px]">
          <div>
            <p className="text-sm text-muted-foreground mb-2">Green (&lt;70%)</p>
            <QueryClientProvider client={lowQueryClient}>
              <LibraryQuotaWidget />
            </QueryClientProvider>
          </div>
          <div>
            <p className="text-sm text-muted-foreground mb-2">Yellow (70-90%)</p>
            <QueryClientProvider client={medQueryClient}>
              <LibraryQuotaWidget />
            </QueryClientProvider>
          </div>
          <div>
            <p className="text-sm text-muted-foreground mb-2">Red (&gt;90%)</p>
            <QueryClientProvider client={highQueryClient}>
              <LibraryQuotaWidget />
            </QueryClientProvider>
          </div>
        </div>
      );
    },
  ],
  parameters: {
    docs: {
      description: {
        story: 'Side-by-side comparison of all three color states',
      },
    },
  },
};
