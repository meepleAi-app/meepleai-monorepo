/**
 * Alert Rules Page Integration Tests - Issue #2253
 * Tests template application workflow from button click to form pre-fill
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import AlertRulesPage from '../page';

// Mock auth context
vi.mock('@/components/auth/AuthProvider', () => ({
  useAuthUser: () => ({
    user: {
      id: 'admin-1',
      email: 'admin@test.com',
      role: 'Admin',
      displayName: 'Admin User',
    },
    loading: false,
  }),
}));

// Mock API
const mockTemplates = [
  {
    name: 'High Error Rate',
    alertType: 'HighErrorRate',
    severity: 'Critical',
    thresholdValue: 5,
    thresholdUnit: '%',
    durationMinutes: 10,
    description: 'Triggers when error rate exceeds 5% for 10 minutes',
    category: 'Errors',
  },
  {
    name: 'Slow Response Time',
    alertType: 'HighLatency',
    severity: 'Warning',
    thresholdValue: 1000,
    thresholdUnit: 'ms',
    durationMinutes: 5,
    description: 'Triggers when response time exceeds 1000ms',
    category: 'Performance',
  },
];

vi.mock('@/lib/api/alert-rules.api', () => ({
  alertRulesApi: {
    getAll: vi.fn(() => Promise.resolve([])),
    getTemplates: vi.fn(() => Promise.resolve(mockTemplates)),
    create: vi.fn(() => Promise.resolve()),
    update: vi.fn(() => Promise.resolve()),
    toggle: vi.fn(() => Promise.resolve()),
    delete: vi.fn(() => Promise.resolve()),
  },
}));

describe('Alert Rules Page - Template Application', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
      },
    });
  });

  const renderPage = () => {
    return render(
      <QueryClientProvider client={queryClient}>
        <AlertRulesPage />
      </QueryClientProvider>
    );
  };

  it('applies template and opens create dialog with pre-filled form', async () => {
    const user = userEvent.setup();
    renderPage();

    // Wait for page to load
    await waitFor(() => {
      expect(screen.getByRole('tab', { name: /templates/i })).toBeInTheDocument();
    });

    // Switch to Templates tab
    await user.click(screen.getByRole('tab', { name: /templates/i }));

    // Wait for templates to load
    await waitFor(() => {
      expect(screen.getByText('High Error Rate')).toBeInTheDocument();
    });

    // Find and click "Apply Template" button for first template
    const applyButtons = screen.getAllByRole('button', { name: /apply template/i });
    await user.click(applyButtons[0]);

    // Dialog should open
    await waitFor(() => {
      expect(screen.getByRole('dialog')).toBeInTheDocument();
      expect(screen.getByText('Create Alert Rule')).toBeInTheDocument();
    });

    // Form should be pre-filled with template data
    expect(screen.getByLabelText(/name/i)).toHaveValue('High Error Rate');
    expect(screen.getByLabelText(/alert type/i)).toHaveValue('HighErrorRate');
    expect(screen.getByLabelText(/threshold value/i)).toHaveValue(5);
    expect(screen.getByLabelText(/unit/i)).toHaveValue('%');
    expect(screen.getByLabelText(/duration/i)).toHaveValue(10);
    expect(screen.getByLabelText(/description/i)).toHaveValue(
      'Triggers when error rate exceeds 5% for 10 minutes'
    );
  });

  it('applies different template and shows correct data', async () => {
    const user = userEvent.setup();
    renderPage();

    await waitFor(() => {
      expect(screen.getByRole('tab', { name: /templates/i })).toBeInTheDocument();
    });

    await user.click(screen.getByRole('tab', { name: /templates/i }));

    await waitFor(() => {
      expect(screen.getByText('Slow Response Time')).toBeInTheDocument();
    });

    // Click second template
    const applyButtons = screen.getAllByRole('button', { name: /apply template/i });
    await user.click(applyButtons[1]);

    await waitFor(() => {
      expect(screen.getByRole('dialog')).toBeInTheDocument();
    });

    // Verify second template data
    expect(screen.getByLabelText(/name/i)).toHaveValue('Slow Response Time');
    expect(screen.getByLabelText(/alert type/i)).toHaveValue('HighLatency');
    expect(screen.getByLabelText(/threshold value/i)).toHaveValue(1000);
    expect(screen.getByLabelText(/unit/i)).toHaveValue('ms');
  });

  it('clears template data when dialog is cancelled', async () => {
    const user = userEvent.setup();
    renderPage();

    await waitFor(() => {
      expect(screen.getByRole('tab', { name: /templates/i })).toBeInTheDocument();
    });

    await user.click(screen.getByRole('tab', { name: /templates/i }));

    await waitFor(() => {
      expect(screen.getByText('High Error Rate')).toBeInTheDocument();
    });

    const applyButtons = screen.getAllByRole('button', { name: /apply template/i });
    await user.click(applyButtons[0]);

    await waitFor(() => {
      expect(screen.getByRole('dialog')).toBeInTheDocument();
    });

    // Cancel dialog
    await user.click(screen.getByRole('button', { name: /cancel/i }));

    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    // Open create dialog manually (without template)
    await user.click(screen.getByRole('tab', { name: /rules/i }));
    await user.click(screen.getByRole('button', { name: /create rule/i }));

    await waitFor(() => {
      expect(screen.getByRole('dialog')).toBeInTheDocument();
    });

    // Form should be empty (template data cleared)
    expect(screen.getByLabelText(/name/i)).toHaveValue('');
    expect(screen.getByLabelText(/alert type/i)).toHaveValue('');
  });

  it('allows user to modify template data before submitting', async () => {
    const user = userEvent.setup();
    renderPage();

    await waitFor(() => {
      expect(screen.getByRole('tab', { name: /templates/i })).toBeInTheDocument();
    });

    await user.click(screen.getByRole('tab', { name: /templates/i }));

    await waitFor(() => {
      expect(screen.getByText('High Error Rate')).toBeInTheDocument();
    });

    const applyButtons = screen.getAllByRole('button', { name: /apply template/i });
    await user.click(applyButtons[0]);

    await waitFor(() => {
      expect(screen.getByRole('dialog')).toBeInTheDocument();
    });

    // Modify template data
    const nameInput = screen.getByLabelText(/name/i);
    await user.clear(nameInput);
    await user.type(nameInput, 'Custom Error Alert');

    expect(nameInput).toHaveValue('Custom Error Alert');

    // Threshold should still have template value
    expect(screen.getByLabelText(/threshold value/i)).toHaveValue(5);
  });
});
