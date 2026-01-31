/**
 * AI Models Dashboard - Visual Tests
 * Issue #2521 - AI Models Runtime Configuration
 * Issue #2852 - Phase 3: Chromatic Visual Regression Testing
 *
 * Chromatic visual regression tests for AI models management interface.
 * Covers: model configuration, primary model selection, cost tracking, responsive design
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { AuthProvider } from '@/components/auth/AuthProvider';

import { AiModelsClient } from './client';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof AiModelsClient> = {
  title: 'Pages/Admin/AIModels',
  component: AiModelsClient,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      viewports: [375, 768, 1920],
      delay: 300,
      diffThreshold: 0.2,
    },
  },
  tags: ['autodocs'],
  decorators: [
    Story => {
      const queryClient = new QueryClient({
        defaultOptions: {
          queries: {
            retry: false,
            refetchInterval: false,
          },
        },
      });
      return (
        <QueryClientProvider client={queryClient}>
          <AuthProvider>
            <Story />
          </AuthProvider>
        </QueryClientProvider>
      );
    },
  ],
};

export default meta;
type Story = StoryObj<typeof AiModelsClient>;

// ========== Mock Data ==========

const mockModels = [
  {
    id: 'model-1',
    name: 'GPT-4',
    provider: 'OpenAI',
    isPrimary: true,
    isEnabled: true,
    costPerInputToken: 0.00003,
    costPerOutputToken: 0.00006,
    maxTokens: 8192,
    temperature: 0.7,
    topP: 0.9,
    createdAt: '2024-01-10T10:00:00Z',
    updatedAt: '2024-01-20T14:30:00Z',
  },
  {
    id: 'model-2',
    name: 'GPT-3.5-turbo',
    provider: 'OpenAI',
    isPrimary: false,
    isEnabled: true,
    costPerInputToken: 0.000001,
    costPerOutputToken: 0.000002,
    maxTokens: 4096,
    temperature: 0.7,
    topP: 0.9,
    createdAt: '2024-01-10T10:00:00Z',
    updatedAt: '2024-01-15T09:00:00Z',
  },
  {
    id: 'model-3',
    name: 'Claude 3 Opus',
    provider: 'Anthropic',
    isPrimary: false,
    isEnabled: true,
    costPerInputToken: 0.000015,
    costPerOutputToken: 0.000075,
    maxTokens: 4096,
    temperature: 0.7,
    topP: 0.9,
    createdAt: '2024-02-01T08:00:00Z',
    updatedAt: '2024-02-10T12:00:00Z',
  },
  {
    id: 'model-4',
    name: 'Gemini Pro',
    provider: 'Google',
    isPrimary: false,
    isEnabled: false,
    costPerInputToken: 0.0000005,
    costPerOutputToken: 0.0000015,
    maxTokens: 2048,
    temperature: 0.7,
    topP: 0.9,
    createdAt: '2024-02-15T10:00:00Z',
    updatedAt: '2024-02-20T16:00:00Z',
  },
];

const mockCostData = {
  currentMonth: {
    totalCost: 1247.85,
    inputTokens: 15234567,
    outputTokens: 8765432,
    requestCount: 5623,
  },
  lastMonth: {
    totalCost: 1105.32,
    inputTokens: 13456789,
    outputTokens: 7654321,
    requestCount: 4987,
  },
  budgetAlert: {
    isActive: false,
    monthlyBudget: 2000,
    currentSpend: 1247.85,
    percentageUsed: 62.4,
  },
};

const mockHighCostData = {
  ...mockCostData,
  currentMonth: {
    ...mockCostData.currentMonth,
    totalCost: 1850.75,
  },
  budgetAlert: {
    isActive: true,
    monthlyBudget: 2000,
    currentSpend: 1850.75,
    percentageUsed: 92.5,
  },
};

// ========== Stories ==========

/**
 * Default view with models and cost tracking
 */
export const Default: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/ai-models',
          method: 'GET',
          status: 200,
          response: {
            items: mockModels,
            total: 4,
          },
        },
        {
          url: '/api/v1/ai-models/cost-tracking',
          method: 'GET',
          status: 200,
          response: mockCostData,
        },
      ],
    },
  },
};

/**
 * Loading state
 */
export const Loading: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/ai-models',
          method: 'GET',
          delay: 5000,
          status: 200,
          response: { items: [], total: 0 },
        },
        {
          url: '/api/v1/ai-models/cost-tracking',
          method: 'GET',
          delay: 5000,
          status: 200,
          response: mockCostData,
        },
      ],
    },
  },
};

/**
 * Empty state - no models configured
 */
export const Empty: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/ai-models',
          method: 'GET',
          status: 200,
          response: {
            items: [],
            total: 0,
          },
        },
        {
          url: '/api/v1/ai-models/cost-tracking',
          method: 'GET',
          status: 200,
          response: {
            ...mockCostData,
            currentMonth: {
              totalCost: 0,
              inputTokens: 0,
              outputTokens: 0,
              requestCount: 0,
            },
          },
        },
      ],
    },
  },
};

/**
 * Error loading models
 */
export const ErrorState: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/ai-models',
          method: 'GET',
          status: 500,
          response: { error: 'Failed to load AI models' },
        },
        {
          url: '/api/v1/ai-models/cost-tracking',
          method: 'GET',
          status: 200,
          response: mockCostData,
        },
      ],
    },
  },
};

/**
 * High cost usage with budget alert
 */
export const HighCostUsage: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/ai-models',
          method: 'GET',
          status: 200,
          response: {
            items: mockModels,
            total: 4,
          },
        },
        {
          url: '/api/v1/ai-models/cost-tracking',
          method: 'GET',
          status: 200,
          response: mockHighCostData,
        },
      ],
    },
  },
};

/**
 * All models enabled
 */
export const AllEnabled: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/ai-models',
          method: 'GET',
          status: 200,
          response: {
            items: mockModels.map(m => ({ ...m, isEnabled: true })),
            total: 4,
          },
        },
        {
          url: '/api/v1/ai-models/cost-tracking',
          method: 'GET',
          status: 200,
          response: mockCostData,
        },
      ],
    },
  },
};

/**
 * All models disabled
 */
export const AllDisabled: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/ai-models',
          method: 'GET',
          status: 200,
          response: {
            items: mockModels.map(m => ({ ...m, isEnabled: false })),
            total: 4,
          },
        },
        {
          url: '/api/v1/ai-models/cost-tracking',
          method: 'GET',
          status: 200,
          response: mockCostData,
        },
      ],
    },
  },
};

/**
 * Mobile view
 */
export const MobileView: Story = {
  ...Default,
  parameters: {
    ...Default.parameters,
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      viewports: [375],
    },
  },
};

/**
 * Tablet view
 */
export const TabletView: Story = {
  ...Default,
  parameters: {
    ...Default.parameters,
    viewport: {
      defaultViewport: 'tablet',
    },
    chromatic: {
      viewports: [768],
    },
  },
};

/**
 * Single model configured
 */
export const SingleModel: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/ai-models',
          method: 'GET',
          status: 200,
          response: {
            items: [mockModels[0]],
            total: 1,
          },
        },
        {
          url: '/api/v1/ai-models/cost-tracking',
          method: 'GET',
          status: 200,
          response: mockCostData,
        },
      ],
    },
  },
};

/**
 * Low cost usage (under 50% budget)
 */
export const LowCostUsage: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/ai-models',
          method: 'GET',
          status: 200,
          response: {
            items: mockModels,
            total: 4,
          },
        },
        {
          url: '/api/v1/ai-models/cost-tracking',
          method: 'GET',
          status: 200,
          response: {
            ...mockCostData,
            currentMonth: {
              ...mockCostData.currentMonth,
              totalCost: 450.25,
            },
            budgetAlert: {
              isActive: false,
              monthlyBudget: 2000,
              currentSpend: 450.25,
              percentageUsed: 22.5,
            },
          },
        },
      ],
    },
  },
};
