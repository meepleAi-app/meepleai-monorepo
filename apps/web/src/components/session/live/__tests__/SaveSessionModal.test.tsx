/**
 * SaveSessionModal — Tests
 *
 * Game Night Improvvisata — Task 20
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi, beforeEach } from 'vitest';

const mockPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
}));

const mockCreatePauseSnapshot = vi.fn();
vi.mock('@/lib/api', () => ({
  api: {
    liveSessions: {
      createPauseSnapshot: (...args: unknown[]) => mockCreatePauseSnapshot(...args),
    },
  },
}));

import { SaveSessionModal } from '../SaveSessionModal';

const DEFAULT_PROPS = {
  sessionId: 'session-123',
  gameName: 'Catan',
  isOpen: true,
  onClose: vi.fn(),
};

describe('SaveSessionModal', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders dialog with confirmation message', () => {
    render(<SaveSessionModal {...DEFAULT_PROPS} />);
    expect(screen.getByText(/vuoi salvare/i)).toBeInTheDocument();
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('shows cancel and confirm buttons', () => {
    render(<SaveSessionModal {...DEFAULT_PROPS} />);
    expect(screen.getByTestId('cancel-save')).toBeInTheDocument();
    expect(screen.getByTestId('confirm-save')).toBeInTheDocument();
  });

  it('calls onClose when cancel is clicked', async () => {
    const onClose = vi.fn();
    const user = userEvent.setup();
    render(<SaveSessionModal {...DEFAULT_PROPS} onClose={onClose} />);

    await user.click(screen.getByTestId('cancel-save'));
    expect(onClose).toHaveBeenCalled();
  });

  it('calls createPauseSnapshot and redirects on confirm', async () => {
    mockCreatePauseSnapshot.mockResolvedValueOnce({ snapshotId: 'snap-1' });
    const user = userEvent.setup();

    render(<SaveSessionModal {...DEFAULT_PROPS} />);

    await user.click(screen.getByTestId('confirm-save'));

    await waitFor(() => {
      expect(mockCreatePauseSnapshot).toHaveBeenCalledWith('session-123');
    });

    await waitFor(
      () => {
        expect(mockPush).toHaveBeenCalledWith('/library');
      },
      { timeout: 1500 }
    );
  });

  it('shows saving state while request is pending', async () => {
    let resolve: (value: unknown) => void = () => {};
    mockCreatePauseSnapshot.mockReturnValueOnce(
      new Promise(r => {
        resolve = r;
      })
    );

    const user = userEvent.setup();
    render(<SaveSessionModal {...DEFAULT_PROPS} />);

    await user.click(screen.getByTestId('confirm-save'));

    await waitFor(() => {
      expect(screen.getByText(/salvataggio/i)).toBeInTheDocument();
    });

    resolve({});
  });

  it('shows error message when save fails', async () => {
    mockCreatePauseSnapshot.mockRejectedValueOnce(new Error('Network error'));
    const user = userEvent.setup();
    render(<SaveSessionModal {...DEFAULT_PROPS} />);

    await user.click(screen.getByTestId('confirm-save'));

    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument();
    });
  });

  it('is not rendered when isOpen is false', () => {
    render(<SaveSessionModal {...DEFAULT_PROPS} isOpen={false} />);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});
