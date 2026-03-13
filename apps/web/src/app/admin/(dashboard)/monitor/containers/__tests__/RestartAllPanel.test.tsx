/**
 * RestartAllPanel Component Tests
 * Issue #144 — Container Management tests (Restart All #145)
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

const mockRestartService = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      restartService: mockRestartService,
    },
  },
}));

const mockToast = vi.hoisted(() => vi.fn());

vi.mock('@/hooks/use-toast', () => ({
  useToast: () => ({ toast: mockToast }),
}));

import { RestartAllPanel } from '../RestartAllPanel';

describe('RestartAllPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders panel with restart button', () => {
    render(<RestartAllPanel />);

    expect(screen.getByTestId('restart-all-panel')).toBeInTheDocument();
    expect(screen.getByTestId('restart-all-btn')).toBeInTheDocument();
    expect(screen.getByText('Restart All Services', { selector: 'h2' })).toBeInTheDocument();
  });

  it('shows SuperAdmin badge', () => {
    render(<RestartAllPanel />);

    expect(screen.getByTestId('superadmin-badge')).toBeInTheDocument();
  });

  it('shows confirmation dialog on button click', async () => {
    const user = userEvent.setup();
    render(<RestartAllPanel />);

    await user.click(screen.getByTestId('restart-all-btn'));

    expect(screen.getByTestId('restart-all-confirm')).toBeInTheDocument();
    expect(screen.getByTestId('restart-all-confirm-input')).toBeInTheDocument();
  });

  it('confirm button is disabled until correct text entered', async () => {
    const user = userEvent.setup();
    render(<RestartAllPanel />);

    await user.click(screen.getByTestId('restart-all-btn'));

    const executeBtn = screen.getByTestId('restart-all-execute');
    expect(executeBtn).toBeDisabled();

    await user.type(screen.getByTestId('restart-all-confirm-input'), 'RESTART ALL');

    expect(executeBtn).not.toBeDisabled();
  });

  it('cancel button hides confirmation dialog', async () => {
    const user = userEvent.setup();
    render(<RestartAllPanel />);

    await user.click(screen.getByTestId('restart-all-btn'));
    expect(screen.getByTestId('restart-all-confirm')).toBeInTheDocument();

    await user.click(screen.getByTestId('restart-all-cancel'));
    expect(screen.queryByTestId('restart-all-confirm')).not.toBeInTheDocument();
  });

  it('executes restart all in dependency order', async () => {
    const user = userEvent.setup();
    mockRestartService.mockResolvedValue({ message: 'Restarted', estimatedDowntime: '10s' });
    render(<RestartAllPanel />);

    await user.click(screen.getByTestId('restart-all-btn'));
    await user.type(screen.getByTestId('restart-all-confirm-input'), 'RESTART ALL');
    await user.click(screen.getByTestId('restart-all-execute'));

    await waitFor(() => {
      expect(screen.getByTestId('restart-progress')).toBeInTheDocument();
    });

    // Should call restartService for each service
    await waitFor(
      () => {
        expect(mockRestartService).toHaveBeenCalledTimes(5);
      },
      { timeout: 10000 }
    );

    // Verify services were called
    expect(mockRestartService).toHaveBeenCalledWith('embedding-service');
    expect(mockRestartService).toHaveBeenCalledWith('reranker-service');
    expect(mockRestartService).toHaveBeenCalledWith('api');
  });

  it('shows toast on successful restart', async () => {
    const user = userEvent.setup();
    mockRestartService.mockResolvedValue({ message: 'Restarted', estimatedDowntime: '10s' });
    render(<RestartAllPanel />);

    await user.click(screen.getByTestId('restart-all-btn'));
    await user.type(screen.getByTestId('restart-all-confirm-input'), 'RESTART ALL');
    await user.click(screen.getByTestId('restart-all-execute'));

    await waitFor(
      () => {
        expect(mockToast).toHaveBeenCalledWith(
          expect.objectContaining({ title: 'All services restarted' })
        );
      },
      { timeout: 10000 }
    );
  });

  it('handles partial failure gracefully', async () => {
    const user = userEvent.setup();
    mockRestartService
      .mockResolvedValueOnce({ message: 'OK', estimatedDowntime: '5s' })
      .mockRejectedValueOnce(new Error('Connection refused'))
      .mockResolvedValueOnce({ message: 'OK', estimatedDowntime: '5s' })
      .mockResolvedValueOnce({ message: 'OK', estimatedDowntime: '5s' })
      .mockResolvedValueOnce({ message: 'OK', estimatedDowntime: '5s' });

    render(<RestartAllPanel />);

    await user.click(screen.getByTestId('restart-all-btn'));
    await user.type(screen.getByTestId('restart-all-confirm-input'), 'RESTART ALL');
    await user.click(screen.getByTestId('restart-all-execute'));

    await waitFor(
      () => {
        expect(mockToast).toHaveBeenCalledWith(
          expect.objectContaining({ title: 'Restart completed with errors' })
        );
      },
      { timeout: 10000 }
    );
  });
});
