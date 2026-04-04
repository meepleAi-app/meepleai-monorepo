/**
 * InviteModal — Tests
 *
 * Game Night Improvvisata — Task 19
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi, beforeEach } from 'vitest';

// Mock qrcode.react to avoid SVG rendering complexity in tests
vi.mock('qrcode.react', () => ({
  QRCodeSVG: ({ value, 'aria-label': ariaLabel }: { value: string; 'aria-label'?: string }) => (
    <svg data-testid="qr-code" aria-label={ariaLabel} data-value={value} />
  ),
}));

import { InviteModal } from '../InviteModal';

const DEFAULT_PROPS = {
  inviteCode: 'ABC123',
  shareLink: 'https://app.meepleai.it/join/ABC123',
  isOpen: true,
  onClose: vi.fn(),
};

describe('InviteModal', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders dialog when isOpen is true', () => {
    render(<InviteModal {...DEFAULT_PROPS} />);
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByText('Invita giocatori')).toBeInTheDocument();
  });

  it('does not render dialog when isOpen is false', () => {
    render(<InviteModal {...DEFAULT_PROPS} isOpen={false} />);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('displays the QR code SVG', () => {
    render(<InviteModal {...DEFAULT_PROPS} />);
    const qr = screen.getByTestId('qr-code');
    expect(qr).toBeInTheDocument();
    expect(qr).toHaveAttribute('data-value', DEFAULT_PROPS.shareLink);
  });

  it('displays the session invite code', () => {
    render(<InviteModal {...DEFAULT_PROPS} />);
    expect(screen.getByTestId('invite-code')).toHaveTextContent('ABC123');
  });

  it('shows the copy link button', () => {
    render(<InviteModal {...DEFAULT_PROPS} />);
    expect(screen.getByTestId('copy-link-button')).toBeInTheDocument();
    expect(screen.getByText(/copia link/i)).toBeInTheDocument();
  });

  it('copy link button is visible and interactive', async () => {
    // jsdom doesn't implement Clipboard API — we test the button is present and clickable
    const user = userEvent.setup();
    render(<InviteModal {...DEFAULT_PROPS} />);

    const copyBtn = screen.getByTestId('copy-link-button');
    expect(copyBtn).toBeInTheDocument();
    expect(copyBtn).not.toBeDisabled();

    // Verify it can be clicked without throwing
    await user.click(copyBtn);
  });

  it('shows confirmation text after clipboard copy', async () => {
    const mockWriteText = vi.fn().mockResolvedValue(undefined);
    // Use defineProperty with writable:true to avoid "only a getter" error in jsdom
    const clipboardDesc = Object.getOwnPropertyDescriptor(navigator, 'clipboard');
    if (!clipboardDesc) {
      // jsdom: clipboard not defined — define it
      Object.defineProperty(navigator, 'clipboard', {
        value: { writeText: mockWriteText },
        configurable: true,
        writable: true,
      });
    } else {
      // Already defined — spy on it
      vi.spyOn(navigator.clipboard, 'writeText').mockResolvedValue(undefined);
    }

    const user = userEvent.setup();
    render(<InviteModal {...DEFAULT_PROPS} />);

    await user.click(screen.getByTestId('copy-link-button'));

    await waitFor(() => {
      expect(screen.getByText(/link copiato/i)).toBeInTheDocument();
    });
  });

  it('calls onClose when dialog is closed', async () => {
    const onClose = vi.fn();
    const user = userEvent.setup();
    render(<InviteModal {...DEFAULT_PROPS} onClose={onClose} />);

    // Close via Escape key
    await user.keyboard('{Escape}');

    expect(onClose).toHaveBeenCalled();
  });
});
