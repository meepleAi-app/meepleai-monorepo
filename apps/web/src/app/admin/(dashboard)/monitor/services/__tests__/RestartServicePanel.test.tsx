/**
 * RestartServicePanel Component Tests
 * Issue #133 — SuperAdmin Service Restart with Level 2 Confirmation
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

const mockRestartService = vi.hoisted(() => vi.fn());
const mockToast = vi.hoisted(() => vi.fn());

vi.mock('@/hooks/useToast', () => ({
  useToast: () => ({
    toast: mockToast,
  }),
}));

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      restartService: mockRestartService,
    },
  },
}));

import { RestartServicePanel } from '../RestartServicePanel';

describe('RestartServicePanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // ==================== Rendering ====================

  it('renders service list with restart buttons', () => {
    render(<RestartServicePanel />);

    expect(screen.getByTestId('restart-btn-api')).toBeInTheDocument();
    expect(screen.getByTestId('restart-btn-embedding-service')).toBeInTheDocument();
    expect(screen.getByTestId('restart-btn-reranker-service')).toBeInTheDocument();
    expect(screen.getByTestId('restart-btn-unstructured-service')).toBeInTheDocument();
    expect(screen.getByTestId('restart-btn-smoldocling-service')).toBeInTheDocument();
  });

  it('renders SuperAdmin badge', () => {
    render(<RestartServicePanel />);

    expect(screen.getByTestId('superadmin-badge')).toBeInTheDocument();
    expect(screen.getByTestId('superadmin-badge')).toHaveTextContent('SuperAdmin');
  });

  // ==================== Confirmation Dialog ====================

  it('clicking restart shows confirmation dialog', async () => {
    const user = userEvent.setup();
    render(<RestartServicePanel />);

    await user.click(screen.getByTestId('restart-btn-api'));

    expect(screen.getByTestId('confirm-dialog-api')).toBeInTheDocument();
  });

  it('confirmation requires typing exact service name', async () => {
    const user = userEvent.setup();
    render(<RestartServicePanel />);

    await user.click(screen.getByTestId('restart-btn-api'));

    // Confirm button should be disabled initially
    expect(screen.getByTestId('confirm-restart-api')).toBeDisabled();

    // Type the service id
    await user.type(screen.getByTestId('confirm-input-api'), 'api');

    // Now confirm button should be enabled
    expect(screen.getByTestId('confirm-restart-api')).toBeEnabled();
  });

  it('cancel closes dialog', async () => {
    const user = userEvent.setup();
    render(<RestartServicePanel />);

    await user.click(screen.getByTestId('restart-btn-api'));
    expect(screen.getByTestId('confirm-dialog-api')).toBeInTheDocument();

    await user.click(screen.getByTestId('confirm-cancel-api'));

    expect(screen.queryByTestId('confirm-dialog-api')).not.toBeInTheDocument();
  });

  // ==================== Restart Execution ====================

  it('successful restart shows cooldown', async () => {
    const user = userEvent.setup();
    mockRestartService.mockResolvedValue({
      message: 'Restart initiated',
      estimatedDowntime: '30-60 seconds',
    });
    render(<RestartServicePanel />);

    await user.click(screen.getByTestId('restart-btn-api'));
    await user.type(screen.getByTestId('confirm-input-api'), 'api');
    await user.click(screen.getByTestId('confirm-restart-api'));

    await waitFor(() => {
      expect(screen.getByTestId('cooldown-api')).toBeInTheDocument();
    });
  });

  it('successful restart shows result feedback', async () => {
    const user = userEvent.setup();
    mockRestartService.mockResolvedValue({
      message: 'Restart initiated',
      estimatedDowntime: '30-60 seconds',
    });
    render(<RestartServicePanel />);

    await user.click(screen.getByTestId('restart-btn-api'));
    await user.type(screen.getByTestId('confirm-input-api'), 'api');
    await user.click(screen.getByTestId('confirm-restart-api'));

    await waitFor(() => {
      expect(screen.getByTestId('result-api')).toBeInTheDocument();
    });

    expect(screen.getByText('Restart initiated')).toBeInTheDocument();
    expect(screen.getByText(/30-60 seconds/)).toBeInTheDocument();
  });

  it('failed restart shows error toast', async () => {
    const user = userEvent.setup();
    mockRestartService.mockRejectedValue(new Error('Connection refused'));
    render(<RestartServicePanel />);

    await user.click(screen.getByTestId('restart-btn-api'));
    await user.type(screen.getByTestId('confirm-input-api'), 'api');
    await user.click(screen.getByTestId('confirm-restart-api'));

    await waitFor(() => {
      expect(mockToast).toHaveBeenCalledWith(
        expect.objectContaining({
          title: 'Restart failed',
          variant: 'destructive',
        })
      );
    });
  });

  it('API error response shows error message in toast', async () => {
    const user = userEvent.setup();
    mockRestartService.mockRejectedValue(new Error('Service not found'));
    render(<RestartServicePanel />);

    await user.click(screen.getByTestId('restart-btn-api'));
    await user.type(screen.getByTestId('confirm-input-api'), 'api');
    await user.click(screen.getByTestId('confirm-restart-api'));

    await waitFor(() => {
      expect(mockToast).toHaveBeenCalledWith(
        expect.objectContaining({
          title: 'Restart failed',
          description: 'Service not found',
          variant: 'destructive',
        })
      );
    });

    // Should NOT show cooldown on failure
    expect(screen.queryByTestId('cooldown-api')).not.toBeInTheDocument();
  });
});
