import type { Meta, StoryObj } from '@storybook/react';
import { within, userEvent, expect } from 'storybook/test';
import { AdminPageClient } from '../../client';
import { AuthProvider } from '@/components/auth/AuthProvider';

const meta: Meta<typeof AdminPageClient> = {
  title: 'Admin/Configuration/Visual Tests',
  component: AdminPageClient,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      disableSnapshot: false,
      delay: 500,
    },
  },
  decorators: [
    Story => (
      <AuthProvider>
        <div className="min-h-screen bg-gray-50">
          <Story />
        </div>
      </AuthProvider>
    ),
  ],
};

export default meta;
type Story = StoryObj<typeof AdminPageClient>;

const mockConfigurationsData = {
  items: [
    {
      id: '11111111-1111-1111-1111-111111111111',
      key: 'Features.EnableNewGameCatalog',
      value: 'true',
      category: 'Features',
      isActive: true,
      createdAt: '2025-01-01T00:00:00Z',
      updatedAt: '2025-01-15T10:30:00Z',
    },
    {
      id: '22222222-2222-2222-2222-222222222222',
      key: 'Features.EnableAdvancedSearch',
      value: 'false',
      category: 'Features',
      isActive: true,
      createdAt: '2025-01-01T00:00:00Z',
      updatedAt: '2025-01-15T10:30:00Z',
    },
    {
      id: '33333333-3333-3333-3333-333333333333',
      key: 'RateLimiting.AnonymousRpm',
      value: '10',
      category: 'RateLimiting',
      isActive: true,
      createdAt: '2025-01-01T00:00:00Z',
      updatedAt: '2025-01-15T10:30:00Z',
    },
    {
      id: '44444444-4444-4444-4444-444444444444',
      key: 'RateLimiting.AuthenticatedRpm',
      value: '60',
      category: 'RateLimiting',
      isActive: true,
      createdAt: '2025-01-01T00:00:00Z',
      updatedAt: '2025-01-15T10:30:00Z',
    },
    {
      id: '55555555-5555-5555-5555-555555555555',
      key: 'AiLlm.DefaultModel',
      value: 'gpt-4',
      category: 'AiLlm',
      isActive: true,
      createdAt: '2025-01-01T00:00:00Z',
      updatedAt: '2025-01-15T10:30:00Z',
    },
    {
      id: '66666666-6666-6666-6666-666666666666',
      key: 'AiLlm.Temperature',
      value: '0.7',
      category: 'AiLlm',
      isActive: true,
      createdAt: '2025-01-01T00:00:00Z',
      updatedAt: '2025-01-15T10:30:00Z',
    },
    {
      id: '77777777-7777-7777-7777-777777777777',
      key: 'Rag.TopK',
      value: '5',
      category: 'Rag',
      isActive: true,
      createdAt: '2025-01-01T00:00:00Z',
      updatedAt: '2025-01-15T10:30:00Z',
    },
    {
      id: '88888888-8888-8888-8888-888888888888',
      key: 'Rag.MinConfidence',
      value: '0.70',
      category: 'Rag',
      isActive: true,
      createdAt: '2025-01-01T00:00:00Z',
      updatedAt: '2025-01-15T10:30:00Z',
    },
  ],
  totalCount: 8,
  page: 1,
  pageSize: 100,
};

const mockCategoriesData = ['Features', 'RateLimiting', 'AiLlm', 'Rag'];

/**
 * Visual Test: Default view with configurations (Feature Flags tab)
 */
export const DefaultView: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/configuration': mockConfigurationsData,
      '/api/v1/admin/configuration/categories': mockCategoriesData,
    },
  },
};

/**
 * Visual Test: Empty state (no configurations)
 */
export const EmptyState: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/configuration': {
        items: [],
        totalCount: 0,
        page: 1,
        pageSize: 100,
      },
      '/api/v1/admin/configuration/categories': [],
    },
  },
};

/**
 * Visual Test: Loading state
 */
export const LoadingState: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/configuration': {
        delay: 'infinite',
      },
      '/api/v1/admin/configuration/categories': {
        delay: 'infinite',
      },
    },
  },
};

/**
 * Visual Test: Error state
 */
export const ErrorState: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/configuration': {
        status: 500,
        error: 'Internal Server Error',
      },
      '/api/v1/admin/configuration/categories': {
        status: 500,
        error: 'Internal Server Error',
      },
    },
  },
};

/**
 * Visual Test: Rate Limiting tab
 */
export const RateLimitingTab: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/configuration': mockConfigurationsData,
      '/api/v1/admin/configuration/categories': mockCategoriesData,
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Wait for data to load
    await canvas.findByText('Configuration Management');

    // Find and click Rate Limiting tab
    const rateLimitingTab = await canvas.findByRole('button', { name: /rate limiting/i });
    await userEvent.click(rateLimitingTab);

    // Verify tab content loaded
    await expect(canvas.findByText('Rate Limiting Configuration')).resolves.toBeInTheDocument();
  },
};

/**
 * Visual Test: AI / LLM tab
 */
export const AiLlmTab: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/configuration': mockConfigurationsData,
      '/api/v1/admin/configuration/categories': mockCategoriesData,
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Wait for data to load
    await canvas.findByText('Configuration Management');

    // Find and click AI / LLM tab
    const aiLlmTab = await canvas.findByRole('button', { name: /ai \/ llm/i });
    await userEvent.click(aiLlmTab);

    // Verify tab content loaded
    await expect(canvas.findByText('AI / LLM Configuration')).resolves.toBeInTheDocument();
  },
};

/**
 * Visual Test: RAG tab
 */
export const RagTab: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/configuration': mockConfigurationsData,
      '/api/v1/admin/configuration/categories': mockCategoriesData,
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Wait for data to load
    await canvas.findByText('Configuration Management');

    // Find and click RAG tab
    const ragTab = await canvas.findByRole('button', { name: /^rag/i });
    await userEvent.click(ragTab);

    // Verify tab content loaded
    await expect(canvas.findByText('RAG Configuration')).resolves.toBeInTheDocument();
  },
};

/**
 * Visual Test: Banner dismissed
 */
export const BannerDismissed: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/configuration': mockConfigurationsData,
      '/api/v1/admin/configuration/categories': mockCategoriesData,
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Wait for data to load
    await canvas.findByText('Configuration Management');

    // Find and click dismiss button on banner
    const dismissButton = await canvas.findByRole('button', { name: /dismiss banner/i });
    await userEvent.click(dismissButton);
  },
};

/**
 * Visual Test: With stats footer
 */
export const WithStatsFooter: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/configuration': mockConfigurationsData,
      '/api/v1/admin/configuration/categories': mockCategoriesData,
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Wait for data to load and verify stats
    await canvas.findByText('Total Configurations');
    await canvas.findByText('8'); // Total count
    await canvas.findByText('Active');
    await canvas.findByText('Categories');
    await canvas.findByText('4'); // Category count
  },
};

/**
 * Visual Test: Mobile responsive view
 */
export const MobileView: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/configuration': mockConfigurationsData,
      '/api/v1/admin/configuration/categories': mockCategoriesData,
    },
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
};

/**
 * Visual Test: Tablet responsive view
 */
export const TabletView: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/configuration': mockConfigurationsData,
      '/api/v1/admin/configuration/categories': mockCategoriesData,
    },
    viewport: {
      defaultViewport: 'tablet',
    },
  },
};
