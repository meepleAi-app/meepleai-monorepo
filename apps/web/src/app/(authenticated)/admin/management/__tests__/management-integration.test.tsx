/**
 * Management Integration Tests - Issue #903
 *
 * Unit tests for the integrated management page
 * Tests all 3 tabs: API Keys, Users, Activity Timeline
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ManagementPageClient } from '../client';
import { AuthProvider } from '@/components/auth/AuthProvider';
import * as api from '@/lib/api';

// Mock API
vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getApiKeysWithStats: vi.fn(),
      deleteApiKey: vi.fn(),
      exportApiKeysToCSV: vi.fn(),
      getAllUsers: vi.fn(),
      deleteUser: vi.fn(),
      exportUsersToCSV: vi.fn(),
      importUsersFromCSV: vi.fn(),
      getSystemActivity: vi.fn(),
    },
  },
}));

// Mock AuthProvider
vi.mock('@/components/auth/AuthProvider', () => ({
  AuthProvider: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  useAuthUser: () => ({
    user: {
      id: 'admin-1',
      email: 'admin@example.com',
      displayName: 'Admin User',
      role: 'Admin',
    },
    loading: false,
  }),
}));

// Mock AdminAuthGuard
vi.mock('@/components/admin', () => ({
  AdminAuthGuard: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  BulkActionBar: ({ selectedCount, actions }: any) => (
    <div data-testid="bulk-action-bar">
      <span>{selectedCount} selected</span>
      {actions.map((action: any) => (
        <button key={action.id} onClick={() => action.onClick(selectedCount)}>
          {action.label}
        </button>
      ))}
    </div>
  ),
}));

// Mock components
vi.mock('@/components/admin/ApiKeyFilterPanel', () => ({
  ApiKeyFilterPanel: ({ filters, onFiltersChange }: any) => (
    <div data-testid="api-key-filter-panel">
      <input
        data-testid="filter-search"
        value={filters.search || ''}
        onChange={e => onFiltersChange({ ...filters, search: e.target.value })}
      />
    </div>
  ),
}));

vi.mock('@/components/modals/ApiKeyCreationModal', () => ({
  ApiKeyCreationModal: ({ isOpen, onClose, onApiKeyCreated }: any) =>
    isOpen ? (
      <div data-testid="api-key-creation-modal">
        <button
          onClick={() => {
            onApiKeyCreated({ keyName: 'Test Key', id: 'new-key-1' });
            onClose();
          }}
        >
          Create
        </button>
        <button onClick={onClose}>Close</button>
      </div>
    ) : null,
}));

vi.mock('@/components/admin/UserActivityTimeline', () => ({
  UserActivityTimeline: ({ events }: any) => (
    <div data-testid="user-activity-timeline">
      {events.map((event: any) => (
        <div key={event.id} data-testid={`activity-${event.id}`}>
          {event.description}
        </div>
      ))}
    </div>
  ),
}));

const mockApiKeys = [
  {
    apiKey: {
      id: 'key-1',
      keyName: 'Test Key 1',
      keyPrefix: 'mpl_test_abc',
      scopes: 'read:games',
      isActive: true,
      createdAt: '2025-01-01T00:00:00Z',
      expiresAt: null,
      lastUsedAt: '2025-01-10T00:00:00Z',
    },
    usageStats: {
      totalUsageCount: 100,
      usageCountLast24Hours: 10,
      usageCountLast7Days: 50,
      usageCountLast30Days: 90,
      averageRequestsPerDay: 3.0,
      lastUsedAt: '2025-01-10T00:00:00Z',
    },
  },
];

const mockUsers = [
  {
    id: 'user-1',
    email: 'user1@example.com',
    displayName: 'User One',
    role: 'User',
    createdAt: '2025-01-01T00:00:00Z',
  },
];

const mockActivities = {
  activities: [
    {
      id: 'activity-1',
      action: 'user.login',
      resource: 'Authentication',
      result: 'Success',
      createdAt: '2025-01-10T00:00:00Z',
      resourceId: null,
      details: null,
      ipAddress: '192.168.1.1',
    },
  ],
  totalCount: 1,
};

describe('ManagementPageClient', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    (api.api.admin.getApiKeysWithStats as any).mockResolvedValue({
      keys: mockApiKeys,
      count: 1,
    });
    (api.api.admin.getAllUsers as any).mockResolvedValue({
      users: mockUsers,
      total: 1,
    });
    (api.api.admin.getSystemActivity as any).mockResolvedValue(mockActivities);
  });

  describe('Rendering', () => {
    it('should render management page with tabs', async () => {
      render(
        <AuthProvider>
          <ManagementPageClient />
        </AuthProvider>
      );

      expect(screen.getByText('System Management')).toBeInTheDocument();
      expect(screen.getByText('API Keys')).toBeInTheDocument();
      expect(screen.getByText('Users')).toBeInTheDocument();
      expect(screen.getByText('Activity')).toBeInTheDocument();
    });

    it('should load API keys on mount', async () => {
      render(<ManagementPageClient />);

      await waitFor(() => {
        expect(api.api.admin.getApiKeysWithStats).toHaveBeenCalled();
      });

      expect(screen.getByText(/1 API key\(s\) found/i)).toBeInTheDocument();
    });
  });

  describe('API Keys Tab', () => {
    it('should allow creating a new API key', async () => {
      const user = userEvent.setup();
      render(<ManagementPageClient />);

      // Click create button
      const createButton = screen.getByRole('button', { name: /create key/i });
      await user.click(createButton);

      // Modal should open
      expect(screen.getByTestId('api-key-creation-modal')).toBeInTheDocument();

      // Create key
      const modalCreateButton = within(screen.getByTestId('api-key-creation-modal')).getByText(
        'Create'
      );
      await user.click(modalCreateButton);

      // Should refetch keys
      await waitFor(() => {
        expect(api.api.admin.getApiKeysWithStats).toHaveBeenCalledTimes(2);
      });
    });

    it('should export API keys to CSV', async () => {
      const user = userEvent.setup();
      const mockBlob = new Blob(['csv data'], { type: 'text/csv' });
      (api.api.admin.exportApiKeysToCSV as any).mockResolvedValue(mockBlob);

      // Mock URL.createObjectURL
      global.URL.createObjectURL = vi.fn(() => 'blob:test');
      global.URL.revokeObjectURL = vi.fn();

      render(<ManagementPageClient />);

      const exportButton = screen.getByRole('button', { name: /export csv/i });
      await user.click(exportButton);

      await waitFor(() => {
        expect(api.api.admin.exportApiKeysToCSV).toHaveBeenCalled();
      });
    });

    it('should apply filters', async () => {
      const user = userEvent.setup();
      render(<ManagementPageClient />);

      const filterInput = screen.getByTestId('filter-search');
      await user.type(filterInput, 'test');

      expect(filterInput).toHaveValue('test');
    });
  });

  describe('Users Tab', () => {
    it('should switch to users tab and load users', async () => {
      const user = userEvent.setup();
      render(<ManagementPageClient />);

      const usersTab = screen.getByRole('tab', { name: /users/i });
      await user.click(usersTab);

      await waitFor(() => {
        expect(api.api.admin.getAllUsers).toHaveBeenCalled();
      });

      expect(screen.getByText(/1 user\(s\) found/i)).toBeInTheDocument();
    });

    it('should export users to CSV', async () => {
      const user = userEvent.setup();
      const mockBlob = new Blob(['csv data'], { type: 'text/csv' });
      (api.api.admin.exportUsersToCSV as any).mockResolvedValue(mockBlob);

      global.URL.createObjectURL = vi.fn(() => 'blob:test');
      global.URL.revokeObjectURL = vi.fn();

      render(<ManagementPageClient />);

      const usersTab = screen.getByRole('tab', { name: /users/i });
      await user.click(usersTab);

      await waitFor(() => {
        expect(screen.getByText(/export csv/i)).toBeInTheDocument();
      });

      const exportButton = screen.getAllByRole('button', { name: /export csv/i })[0];
      await user.click(exportButton);

      await waitFor(() => {
        expect(api.api.admin.exportUsersToCSV).toHaveBeenCalled();
      });
    });
  });

  describe('Activity Tab', () => {
    it('should switch to activity tab and load activities', async () => {
      const user = userEvent.setup();
      render(<ManagementPageClient />);

      const activityTab = screen.getByRole('tab', { name: /activity/i });
      await user.click(activityTab);

      await waitFor(() => {
        expect(api.api.admin.getSystemActivity).toHaveBeenCalled();
      });

      expect(screen.getByTestId('user-activity-timeline')).toBeInTheDocument();
    });

    it('should display activity events', async () => {
      const user = userEvent.setup();
      render(<ManagementPageClient />);

      const activityTab = screen.getByRole('tab', { name: /activity/i });
      await user.click(activityTab);

      await waitFor(() => {
        expect(screen.getByTestId('activity-activity-1')).toBeInTheDocument();
      });
    });
  });

  describe('Error Handling', () => {
    it('should handle API key fetch error', async () => {
      (api.api.admin.getApiKeysWithStats as any).mockRejectedValue(new Error('Failed to fetch'));

      render(<ManagementPageClient />);

      await waitFor(() => {
        expect(screen.getByText(/failed to load api keys/i)).toBeInTheDocument();
      });
    });

    it('should handle users fetch error', async () => {
      const user = userEvent.setup();
      (api.api.admin.getAllUsers as any).mockRejectedValue(new Error('Failed to fetch'));

      render(<ManagementPageClient />);

      const usersTab = screen.getByRole('tab', { name: /users/i });
      await user.click(usersTab);

      await waitFor(() => {
        expect(screen.getByText(/failed to load users/i)).toBeInTheDocument();
      });
    });
  });
});
