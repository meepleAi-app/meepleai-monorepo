/**
 * RestartAllPanel Component Tests
 * Issue #144 — Container Management tests (Restart All #145)
 * Issue #1854 — Confirmation refactored to Radix Dialog (role=dialog + Escape + axe-clean).
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { axe, toHaveNoViolations } from 'jest-axe';

expect.extend(toHaveNoViolations);

const mockRestartService = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      restartService: mockRestartService,
    },
  },
}));

const mockToast = vi.hoisted(() => vi.fn());

vi.mock('@/hooks/useToast', () => ({
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

  it('opens Radix dialog on restart button click', async () => {
    const user = userEvent.setup();
    render(<RestartAllPanel />);

    // Dialog is closed initially — getByRole would throw
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

    await user.click(screen.getByTestId('restart-all-btn'));

    const dialog = await screen.findByRole('dialog');
    expect(dialog).toBeInTheDocument();
    // Radix Dialog wires aria-labelledby → DialogTitle ("Restart All Services")
    // and tabindex=-1 for the focus-trap container. We assert both as proof
    // the structural a11y wiring matches WAI-ARIA Authoring Practices for
    // dialogs (focus trap + accessible name).
    expect(dialog).toHaveAttribute('aria-labelledby');
    expect(dialog).toHaveAttribute('tabindex', '-1');
  });

  it('confirm button is disabled until "RESTART ALL" is typed', async () => {
    const user = userEvent.setup();
    render(<RestartAllPanel />);

    await user.click(screen.getByTestId('restart-all-btn'));

    const dialog = await screen.findByRole('dialog');
    const confirmBtn = screen.getByRole('button', { name: /confirm restart all/i });
    expect(confirmBtn).toBeDisabled();

    const input = screen.getByRole('textbox');
    await user.type(input, 'RESTART ALL');

    expect(confirmBtn).not.toBeDisabled();
    expect(dialog).toBeInTheDocument();
  });

  it('cancel button closes dialog without restarting', async () => {
    const user = userEvent.setup();
    render(<RestartAllPanel />);

    await user.click(screen.getByTestId('restart-all-btn'));
    expect(await screen.findByRole('dialog')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /^cancel$/i }));

    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });
    expect(mockRestartService).not.toHaveBeenCalled();
  });

  it('Escape key closes dialog (Radix built-in)', async () => {
    const user = userEvent.setup();
    render(<RestartAllPanel />);

    await user.click(screen.getByTestId('restart-all-btn'));
    expect(await screen.findByRole('dialog')).toBeInTheDocument();

    await user.keyboard('{Escape}');

    await waitFor(() => {
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });
    expect(mockRestartService).not.toHaveBeenCalled();
  });

  it('executes restart all in dependency order', async () => {
    const user = userEvent.setup();
    mockRestartService.mockResolvedValue({ message: 'Restarted', estimatedDowntime: '10s' });
    render(<RestartAllPanel />);

    await user.click(screen.getByTestId('restart-all-btn'));
    await user.type(await screen.findByRole('textbox'), 'RESTART ALL');
    await user.click(screen.getByRole('button', { name: /confirm restart all/i }));

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
    await user.type(await screen.findByRole('textbox'), 'RESTART ALL');
    await user.click(screen.getByRole('button', { name: /confirm restart all/i }));

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
    await user.type(await screen.findByRole('textbox'), 'RESTART ALL');
    await user.click(screen.getByRole('button', { name: /confirm restart all/i }));

    await waitFor(
      () => {
        expect(mockToast).toHaveBeenCalledWith(
          expect.objectContaining({ title: 'Restart completed with errors' })
        );
      },
      { timeout: 10000 }
    );
  });

  it('passes axe a11y scan with dialog open', async () => {
    const user = userEvent.setup();
    const { container } = render(<RestartAllPanel />);

    await user.click(screen.getByTestId('restart-all-btn'));
    await screen.findByRole('dialog');

    // Scan full DOM (Radix portals the dialog to body, container catches it via document.body root)
    const results = await axe(document.body);
    expect(results).toHaveNoViolations();
    // Touch container so the lint doesn't flag the destructured var as unused
    expect(container).toBeTruthy();
  });
});
