import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// ============================================================================
// Mocks
// ============================================================================

const mockCreateInvite = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    sessionInvites: {
      createInvite: mockCreateInvite,
    },
  },
}));

// Mock QRCodeSVG
vi.mock('qrcode.react', () => ({
  QRCodeSVG: ({ value, ...props }: { value: string; [key: string]: unknown }) => (
    <svg data-testid="invite-qr-code" data-value={value} {...props} />
  ),
}));

// Mock Dialog to render content directly when open
vi.mock('@/components/ui/overlays/dialog', () => ({
  Dialog: ({ open, children }: { open: boolean; children: React.ReactNode }) =>
    open ? <div data-testid="dialog">{children}</div> : null,
  DialogContent: ({ children, ...props }: { children: React.ReactNode }) => (
    <div {...props}>{children}</div>
  ),
  DialogHeader: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  DialogTitle: ({ children, ...props }: { children: React.ReactNode }) => (
    <h2 {...props}>{children}</h2>
  ),
  DialogDescription: ({ children, ...props }: { children: React.ReactNode }) => (
    <p {...props}>{children}</p>
  ),
}));

vi.mock('@/components/ui/primitives/button', () => ({
  Button: ({
    children,
    ...props
  }: React.ButtonHTMLAttributes<HTMLButtonElement> & { children: React.ReactNode }) => (
    <button {...props}>{children}</button>
  ),
}));

vi.mock('@/components/ui/feedback/skeleton', () => ({
  Skeleton: (props: React.HTMLAttributes<HTMLDivElement>) => (
    <div data-testid="skeleton" {...props} />
  ),
}));

import { InviteModal } from '@/components/session/InviteModal';

// ============================================================================
// Test Data
// ============================================================================

const SESSION_ID = '123e4567-e89b-12d3-a456-426614174000';

const MOCK_INVITE_RESULT = {
  pin: '847291',
  linkToken: 'abc123def456',
  expiresAt: new Date(Date.now() + 5 * 60 * 1000).toISOString(), // 5 minutes from now
};

// ============================================================================
// Tests
// ============================================================================

describe('InviteModal', () => {
  const defaultProps = {
    sessionId: SESSION_ID,
    open: true,
    onOpenChange: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows loading state initially', () => {
    mockCreateInvite.mockReturnValue(new Promise(() => {})); // Never resolves

    render(<InviteModal {...defaultProps} />);

    expect(screen.getByTestId('invite-loading')).toBeInTheDocument();
    expect(screen.getAllByTestId('skeleton').length).toBeGreaterThan(0);
  });

  it('displays PIN after API response', async () => {
    mockCreateInvite.mockResolvedValue(MOCK_INVITE_RESULT);

    render(<InviteModal {...defaultProps} />);

    await waitFor(() => {
      expect(screen.getByTestId('invite-pin')).toBeInTheDocument();
    });

    expect(screen.getByTestId('invite-pin')).toHaveTextContent('847291');
  });

  it('displays QR code after API response', async () => {
    mockCreateInvite.mockResolvedValue(MOCK_INVITE_RESULT);

    render(<InviteModal {...defaultProps} />);

    await waitFor(() => {
      expect(screen.getByTestId('invite-qr-code')).toBeInTheDocument();
    });

    const qrCode = screen.getByTestId('invite-qr-code');
    expect(qrCode.getAttribute('data-value')).toContain(SESSION_ID);
    expect(qrCode.getAttribute('data-value')).toContain(MOCK_INVITE_RESULT.linkToken);
  });

  it('copy link button copies join URL to clipboard', async () => {
    mockCreateInvite.mockResolvedValue(MOCK_INVITE_RESULT);

    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', {
      value: { writeText },
      writable: true,
      configurable: true,
    });

    render(<InviteModal {...defaultProps} />);

    await waitFor(() => {
      expect(screen.getByTestId('copy-link-button')).toBeInTheDocument();
    });

    // Use fireEvent instead of userEvent for async clipboard handler with fake timers
    const { fireEvent: fe } = await import('@testing-library/react');
    await act(async () => {
      fe.click(screen.getByTestId('copy-link-button'));
    });

    await waitFor(() => {
      expect(writeText).toHaveBeenCalledTimes(1);
    });
    const copiedUrl = writeText.mock.calls[0][0] as string;
    expect(copiedUrl).toContain(
      `/sessions/${SESSION_ID}/join?token=${MOCK_INVITE_RESULT.linkToken}`
    );
  });

  it('shows countdown timer', async () => {
    mockCreateInvite.mockResolvedValue(MOCK_INVITE_RESULT);

    render(<InviteModal {...defaultProps} />);

    await waitFor(() => {
      expect(screen.getByTestId('invite-countdown')).toBeInTheDocument();
    });

    // Should display "Expires in X:XX"
    expect(screen.getByTestId('invite-countdown').textContent).toMatch(/Expires in \d+:\d{2}/);
  });

  it('calls createInvite with sessionId when opened', async () => {
    mockCreateInvite.mockResolvedValue(MOCK_INVITE_RESULT);

    render(<InviteModal {...defaultProps} />);

    await waitFor(() => {
      expect(mockCreateInvite).toHaveBeenCalledWith(SESSION_ID, {});
    });
  });

  it('does not render content when closed', () => {
    render(<InviteModal {...defaultProps} open={false} />);

    expect(screen.queryByTestId('dialog')).not.toBeInTheDocument();
  });

  it('shows error state when API fails', async () => {
    mockCreateInvite.mockRejectedValue(new Error('Network error'));

    render(<InviteModal {...defaultProps} />);

    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument();
    });

    expect(screen.getByRole('alert')).toHaveTextContent('Failed to create invite');
  });
});
