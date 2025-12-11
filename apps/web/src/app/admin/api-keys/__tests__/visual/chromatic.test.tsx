import type { Meta, StoryObj } from '@storybook/react';
import { within, userEvent, expect } from '@storybook/test';
import { ApiKeysPageClient } from '../../client';
import { AuthProvider } from '@/components/auth/AuthProvider';

const meta: Meta<typeof ApiKeysPageClient> = {
  title: 'Admin/API Keys/Visual Tests',
  component: ApiKeysPageClient,
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
type Story = StoryObj<typeof ApiKeysPageClient>;

const mockApiKeysData = {
  keys: [
    {
      apiKey: {
        id: '11111111-1111-1111-1111-111111111111',
        keyName: 'Production API Key',
        keyPrefix: 'mpl_prod_',
        scopes: 'read:games,read:rules,write:sessions',
        createdAt: '2025-01-01T00:00:00Z',
        expiresAt: '2026-01-01T00:00:00Z',
        lastUsedAt: '2025-12-11T10:30:00Z',
        isActive: true,
      },
      usageStats: {
        keyId: '11111111-1111-1111-1111-111111111111',
        totalUsageCount: 1543,
        lastUsedAt: '2025-12-11T10:30:00Z',
        usageCountLast24Hours: 45,
        usageCountLast7Days: 312,
        usageCountLast30Days: 1234,
        averageRequestsPerDay: 41.13,
      },
    },
    {
      apiKey: {
        id: '22222222-2222-2222-2222-222222222222',
        keyName: 'Development API Key',
        keyPrefix: 'mpl_dev_',
        scopes: 'read:games,read:rules',
        createdAt: '2025-06-15T12:00:00Z',
        expiresAt: null,
        lastUsedAt: '2025-12-10T15:20:00Z',
        isActive: true,
      },
      usageStats: {
        keyId: '22222222-2222-2222-2222-222222222222',
        totalUsageCount: 234,
        lastUsedAt: '2025-12-10T15:20:00Z',
        usageCountLast24Hours: 12,
        usageCountLast7Days: 89,
        usageCountLast30Days: 234,
        averageRequestsPerDay: 7.8,
      },
    },
    {
      apiKey: {
        id: '33333333-3333-3333-3333-333333333333',
        keyName: 'Revoked Test Key',
        keyPrefix: 'mpl_test_',
        scopes: 'read:games',
        createdAt: '2025-03-10T08:00:00Z',
        expiresAt: null,
        lastUsedAt: '2025-11-01T09:00:00Z',
        isActive: false,
      },
      usageStats: {
        keyId: '33333333-3333-3333-3333-333333333333',
        totalUsageCount: 567,
        lastUsedAt: '2025-11-01T09:00:00Z',
        usageCountLast24Hours: 0,
        usageCountLast7Days: 0,
        usageCountLast30Days: 0,
        averageRequestsPerDay: 0,
      },
    },
    {
      apiKey: {
        id: '44444444-4444-4444-4444-444444444444',
        keyName: 'Expired Key',
        keyPrefix: 'mpl_exp_',
        scopes: 'read:games,read:rules',
        createdAt: '2024-01-01T00:00:00Z',
        expiresAt: '2024-12-31T23:59:59Z',
        lastUsedAt: '2024-12-30T10:00:00Z',
        isActive: true,
      },
      usageStats: {
        keyId: '44444444-4444-4444-4444-444444444444',
        totalUsageCount: 8923,
        lastUsedAt: '2024-12-30T10:00:00Z',
        usageCountLast24Hours: 0,
        usageCountLast7Days: 0,
        usageCountLast30Days: 0,
        averageRequestsPerDay: 0,
      },
    },
  ],
  count: 4,
  filters: {
    userId: null,
    includeRevoked: false,
  },
};

/**
 * Visual Test: Default state with API keys list
 */
export const DefaultView: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/api-keys/stats': mockApiKeysData,
    },
  },
};

/**
 * Visual Test: Empty state (no API keys)
 */
export const EmptyState: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/api-keys/stats': {
        keys: [],
        count: 0,
        filters: {
          userId: null,
          includeRevoked: false,
        },
      },
    },
  },
};

/**
 * Visual Test: Loading state
 */
export const LoadingState: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/api-keys/stats': {
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
      '/api/v1/admin/api-keys/stats': {
        status: 500,
        error: 'Internal Server Error',
      },
    },
  },
};

/**
 * Visual Test: Create key dialog open
 */
export const CreateKeyDialog: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/api-keys/stats': mockApiKeysData,
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const createButton = await canvas.findByTestId('create-key-button');
    await userEvent.click(createButton);

    // Wait for dialog to open
    await expect(canvas.findByText('Create New API Key')).resolves.toBeInTheDocument();
  },
};

/**
 * Visual Test: Usage statistics dialog
 */
export const UsageStatsDialog: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/api-keys/stats': mockApiKeysData,
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Find and click stats button (BarChart3 icon)
    const statsButtons = await canvas.findAllByRole('button');
    const statsButton = statsButtons.find(btn => {
      const svg = btn.querySelector('svg.lucide-bar-chart-3');
      return svg !== null;
    });

    if (statsButton) {
      await userEvent.click(statsButton);
      await expect(canvas.findByText('Usage Statistics')).resolves.toBeInTheDocument();
    }
  },
};

/**
 * Visual Test: Delete confirmation dialog
 */
export const DeleteConfirmationDialog: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/api-keys/stats': mockApiKeysData,
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Find and click delete button (Trash2 icon)
    const deleteButtons = await canvas.findAllByRole('button');
    const deleteButton = deleteButtons.find(btn => {
      const svg = btn.querySelector('svg.lucide-trash-2');
      return svg !== null;
    });

    if (deleteButton) {
      await userEvent.click(deleteButton);
      await expect(canvas.findByText('Delete API Key')).resolves.toBeInTheDocument();
    }
  },
};

/**
 * Visual Test: Filtered by status (Active only)
 */
export const FilteredByStatus: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/api-keys/stats': mockApiKeysData,
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Wait for data to load
    await canvas.findByText('Production API Key');

    // Open status filter
    const statusSelect = await canvas.findByRole('combobox', { name: /status/i });
    await userEvent.click(statusSelect);

    // Select "Active"
    const activeOption = await canvas.findByRole('option', { name: 'Active' });
    await userEvent.click(activeOption);
  },
};

/**
 * Visual Test: Search filter applied
 */
export const SearchFiltered: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/api-keys/stats': mockApiKeysData,
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Wait for data to load
    await canvas.findByText('Production API Key');

    // Type in search input
    const searchInput = await canvas.findByPlaceholderText('Key name or prefix...');
    await userEvent.type(searchInput, 'Production');
  },
};

/**
 * Visual Test: Bulk selection active
 */
export const BulkSelectionActive: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/api-keys/stats': mockApiKeysData,
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Wait for data to load
    await canvas.findByText('Production API Key');

    // Select checkboxes
    const checkboxes = await canvas.findAllByRole('checkbox');
    await userEvent.click(checkboxes[1]); // First key
    await userEvent.click(checkboxes[2]); // Second key
  },
};

/**
 * Visual Test: Mobile responsive view
 */
export const MobileView: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/api-keys/stats': mockApiKeysData,
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
      '/api/v1/admin/api-keys/stats': mockApiKeysData,
    },
    viewport: {
      defaultViewport: 'tablet',
    },
  },
};
