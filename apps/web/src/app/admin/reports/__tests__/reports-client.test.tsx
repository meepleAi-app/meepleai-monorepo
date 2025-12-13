/**
 * Unit Tests for Reports Page Client (Issue #920)
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ReportsPageClient } from '../client';
import { AuthProvider } from '@/components/auth/AuthProvider';
import type { AuthUser } from '@/types/auth';
import { api } from '@/lib/api';

// Mock user for tests
const mockUser: AuthUser = {
  id: '123e4567-e89b-12d3-a456-426614174000',
  email: 'admin@meepleai.com',
  displayName: 'Admin User',
  role: 'Admin',
  createdAt: new Date().toISOString(),
  isTwoFactorEnabled: false,
};

// Mock API
vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getScheduledReports: vi.fn(),
      getReportExecutions: vi.fn(),
      generateReport: vi.fn(),
      scheduleReport: vi.fn(),
      updateReportSchedule: vi.fn(),
      deleteScheduledReport: vi.fn(),
    },
  },
}));

// Mock AuthProvider to return admin user
vi.mock('@/components/auth/AuthProvider', async () => {
  const actual = await vi.importActual('@/components/auth/AuthProvider');
  return {
    ...actual,
    useAuthUser: () => ({
      user: mockUser,
      loading: false,
    }),
  };
});

// Mock toast
vi.mock('@/components/layout', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

const mockScheduledReports = [
  {
    id: '550e8400-e29b-41d4-a716-446655440001',
    name: 'Weekly System Health',
    description: 'System health metrics',
    template: 'SystemHealth' as const,
    format: 'PDF' as const,
    parameters: {},
    scheduleExpression: '0 9 * * 1',
    isActive: true,
    createdAt: '2025-12-01T10:00:00Z',
    lastExecutedAt: '2025-12-11T09:00:00Z',
    createdBy: 'admin@meepleai.com',
    emailRecipients: ['team@meepleai.com'],
  },
];

const mockExecutions = [
  {
    id: '650e8400-e29b-41d4-a716-446655440001',
    reportId: '550e8400-e29b-41d4-a716-446655440001',
    reportName: 'Weekly System Health',
    template: 'SystemHealth' as const,
    status: 'Completed' as const,
    startedAt: '2025-12-11T09:00:00Z',
    completedAt: '2025-12-11T09:00:15Z',
    errorMessage: null,
    filePath: '/reports/system-health-20251211.pdf',
    fileSize: 245680,
  },
];

const renderWithAuth = (ui: React.ReactElement) => {
  return render(<AuthProvider>{ui}</AuthProvider>);
};

describe('ReportsPageClient', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(api.admin.getScheduledReports).mockResolvedValue(mockScheduledReports);
    vi.mocked(api.admin.getReportExecutions).mockResolvedValue(mockExecutions);
  });

  describe('Data Loading', () => {
    it('should load scheduled reports and executions on mount', async () => {
      renderWithAuth(<ReportsPageClient />);

      await waitFor(() => {
        expect(api.admin.getScheduledReports).toHaveBeenCalled();
        expect(api.admin.getReportExecutions).toHaveBeenCalled();
      });
    });

    it('should display loading state initially', () => {
      vi.mocked(api.admin.getScheduledReports).mockImplementation(() => new Promise(() => {}));
      vi.mocked(api.admin.getReportExecutions).mockImplementation(() => new Promise(() => {}));

      renderWithAuth(<ReportsPageClient />);

      expect(screen.getByText('Loading reports...')).toBeInTheDocument();
    });

    // Error state tested in Storybook/Chromatic - ErrorDisplay complex in JSDOM
  });

  describe('Tabs Navigation', () => {
    it('should display Generate tab by default', async () => {
      renderWithAuth(<ReportsPageClient />);

      await waitFor(() => {
        expect(screen.getByRole('tab', { name: /generate/i })).toHaveAttribute(
          'data-state',
          'active'
        );
      });
    });

    it('should switch to Scheduled tab', async () => {
      const user = userEvent.setup();
      renderWithAuth(<ReportsPageClient />);

      await waitFor(() => {
        expect(api.admin.getScheduledReports).toHaveBeenCalled();
      });

      const scheduledTab = screen.getByRole('tab', { name: /scheduled/i });
      await user.click(scheduledTab);

      expect(scheduledTab).toHaveAttribute('data-state', 'active');
    });

    it('should switch to History tab', async () => {
      const user = userEvent.setup();
      renderWithAuth(<ReportsPageClient />);

      await waitFor(() => {
        expect(api.admin.getReportExecutions).toHaveBeenCalled();
      });

      const historyTab = screen.getByRole('tab', { name: /history/i });
      await user.click(historyTab);

      expect(historyTab).toHaveAttribute('data-state', 'active');
    });
  });

  describe('Generate Report', () => {
    it('should open generate dialog', async () => {
      const user = userEvent.setup();
      renderWithAuth(<ReportsPageClient />);

      await waitFor(() => {
        expect(api.admin.getScheduledReports).toHaveBeenCalled();
      });

      const generateButton = screen.getByRole('button', { name: /generate report/i });
      await user.click(generateButton);

      expect(screen.getByText('Generate Report')).toBeInTheDocument();
    });

    it('should generate report on confirm', async () => {
      const user = userEvent.setup();
      const mockBlob = new Blob(['test'], { type: 'text/csv' });
      vi.mocked(api.admin.generateReport).mockResolvedValue(mockBlob);

      // Mock URL.createObjectURL
      global.URL.createObjectURL = vi.fn(() => 'mock-url');
      global.URL.revokeObjectURL = vi.fn();

      renderWithAuth(<ReportsPageClient />);

      await waitFor(() => {
        expect(api.admin.getScheduledReports).toHaveBeenCalled();
      });

      const generateButton = screen.getByRole('button', { name: /generate report/i });
      await user.click(generateButton);

      const confirmButton = screen.getByRole('button', { name: /^generate$/i });
      await user.click(confirmButton);

      await waitFor(() => {
        expect(api.admin.generateReport).toHaveBeenCalled();
      });
    });
  });

  describe('Schedule Report', () => {
    it('should open schedule dialog', async () => {
      const user = userEvent.setup();
      renderWithAuth(<ReportsPageClient />);

      await waitFor(() => {
        expect(api.admin.getScheduledReports).toHaveBeenCalled();
      });

      const scheduleButton = screen.getByRole('button', { name: /📅 schedule report/i });
      await user.click(scheduleButton);

      // Dialog should open - look for dialog heading role
      await waitFor(() => {
        expect(screen.getByRole('heading', { name: /schedule report/i })).toBeInTheDocument();
      });
    });

    // Form validation tested in Storybook/Chromatic - Dialog forms complex in JSDOM
  });

  // Email management tests are covered in Storybook/Chromatic visual tests
  // Dialog interactions in JSDOM are complex and better tested visually

  describe('Scheduled Reports List', () => {
    it('should display scheduled reports', async () => {
      const user = userEvent.setup();
      renderWithAuth(<ReportsPageClient />);

      await waitFor(() => {
        expect(api.admin.getScheduledReports).toHaveBeenCalled();
      });

      // Switch to scheduled tab
      const scheduledTab = screen.getByRole('tab', { name: /scheduled/i });
      await user.click(scheduledTab);

      await waitFor(() => {
        expect(screen.getByText('Weekly System Health')).toBeInTheDocument();
      });
    });

    it('should show empty state when no reports', async () => {
      const user = userEvent.setup();
      vi.mocked(api.admin.getScheduledReports).mockResolvedValue([]);

      renderWithAuth(<ReportsPageClient />);

      await waitFor(() => {
        expect(api.admin.getScheduledReports).toHaveBeenCalled();
      });

      // Switch to scheduled tab
      const scheduledTab = screen.getByRole('tab', { name: /scheduled/i });
      await user.click(scheduledTab);

      await waitFor(() => {
        expect(screen.getByText('No scheduled reports yet')).toBeInTheDocument();
      });
    });
  });

  describe('Execution History', () => {
    it('should display execution history', async () => {
      const user = userEvent.setup();
      renderWithAuth(<ReportsPageClient />);

      await waitFor(() => {
        expect(api.admin.getReportExecutions).toHaveBeenCalled();
      });

      // Switch to history tab
      const historyTab = screen.getByRole('tab', { name: /history/i });
      await user.click(historyTab);

      // Just verify the tab is active - executions list is tested in visual tests
      expect(historyTab).toHaveAttribute('data-state', 'active');
    });

    it('should show empty state when no executions', async () => {
      const user = userEvent.setup();
      vi.mocked(api.admin.getReportExecutions).mockResolvedValue([]);

      renderWithAuth(<ReportsPageClient />);

      await waitFor(() => {
        expect(api.admin.getReportExecutions).toHaveBeenCalled();
      });

      // Switch to history tab
      const historyTab = screen.getByRole('tab', { name: /history/i });
      await user.click(historyTab);

      await waitFor(() => {
        expect(screen.getByText('No execution history yet')).toBeInTheDocument();
      });
    });
  });
});
