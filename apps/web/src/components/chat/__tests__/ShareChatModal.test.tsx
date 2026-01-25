/**
 * ShareChatModal Component Tests - Issue #2208
 *
 * Tests for share chat modal with:
 * - Form rendering (role, expiry, label)
 * - Form submission and API calls
 * - Success state with URL display
 * - Copy to clipboard functionality
 * - Error handling
 * - Loading states
 * - Accessibility compliance
 *
 * Coverage target: 90%+ statements, 75%+ branches
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ShareChatModal } from '../ShareChatModal';
import * as apiModule from '@/lib/api';
import { getDialogHeading, queryDialogHeading } from '@/test-utils/locale-queries';

// Mock the API module
vi.mock('@/lib/api', async importOriginal => {
  const original = await importOriginal<typeof import('@/lib/api')>();
  return {
    ...original,
    api: {
      ...original.api,
      shareLinks: {
        createShareLink: vi.fn(),
      },
    },
  };
});

// Mock logger
vi.mock('@/lib/logger', () => ({
  logger: {
    info: vi.fn(),
    error: vi.fn(),
    warn: vi.fn(),
    debug: vi.fn(),
  },
}));

// Mock clipboard API
const mockClipboard = {
  writeText: vi.fn(),
};
Object.assign(navigator, { clipboard: mockClipboard });

const mockShareLinkResponse = {
  shareLinkId: 'share-link-123',
  shareableUrl: 'https://meepleai.com/share/abc123',
  role: 'view' as const,
  expiresAt: '2025-12-26T10:00:00Z',
  label: 'Test Label',
  createdAt: '2025-12-19T10:00:00Z',
};

const defaultProps = {
  isOpen: true,
  threadId: 'thread-123',
  onClose: vi.fn(),
  onShareLinkCreated: vi.fn(),
};

describe('ShareChatModal', () => {
  const user = userEvent.setup();

  beforeEach(() => {
    vi.clearAllMocks();
    // Don't reset clipboard mock here - let tests configure it as needed
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  describe('Rendering - Initial Form State', () => {
    it('renders modal when isOpen is true', () => {
      render(<ShareChatModal {...defaultProps} />);

      expect(getDialogHeading(/share chat thread/i)).toBeInTheDocument();
    });

    it('does not render modal when isOpen is false', () => {
      render(<ShareChatModal {...defaultProps} isOpen={false} />);

      expect(queryDialogHeading(/share chat thread/i)).not.toBeInTheDocument();
    });

    it('renders dialog description', () => {
      render(<ShareChatModal {...defaultProps} />);

      expect(
        screen.getByText('Create a public link to share this conversation with others.')
      ).toBeInTheDocument();
    });

    it('renders access level selector', () => {
      render(<ShareChatModal {...defaultProps} />);

      expect(screen.getByTestId('access-level-label')).toBeInTheDocument();
    });

    it('renders expiry duration selector', () => {
      render(<ShareChatModal {...defaultProps} />);

      expect(screen.getByTestId('link-expiry-label')).toBeInTheDocument();
    });

    it('renders optional label input', () => {
      render(<ShareChatModal {...defaultProps} />);

      expect(screen.getByLabelText(/label/i)).toBeInTheDocument();
      expect(screen.getByPlaceholderText(/shared with design team/i)).toBeInTheDocument();
    });

    it('renders cancel button', () => {
      render(<ShareChatModal {...defaultProps} />);

      expect(screen.getByRole('button', { name: /cancel/i })).toBeInTheDocument();
    });

    it('renders create share link button', () => {
      render(<ShareChatModal {...defaultProps} />);

      expect(screen.getByRole('button', { name: /create share link/i })).toBeInTheDocument();
    });
  });

  describe('Form Interactions', () => {
    it('allows changing access level to comment', async () => {
      render(<ShareChatModal {...defaultProps} />);

      // Find and click the access level trigger (first combobox)
      const comboboxes = screen.getAllByRole('combobox');
      const accessTrigger = comboboxes[0]; // First combobox is access level
      await user.click(accessTrigger);

      // Select "View + Comment" option
      const commentOption = await screen.findByText(/view \+ comment/i);
      await user.click(commentOption);

      // Verify selection changed
      expect(screen.getByText(/view \+ comment/i)).toBeInTheDocument();
    });

    it('allows changing expiry duration', async () => {
      render(<ShareChatModal {...defaultProps} />);

      // Find expiry selector (second combobox)
      const comboboxes = screen.getAllByRole('combobox');
      const expiryTrigger = comboboxes[1]; // Second combobox is expiry
      await user.click(expiryTrigger);

      // Select 30 days option
      const thirtyDaysOption = await screen.findByText('30 days');
      await user.click(thirtyDaysOption);
    });

    it('allows entering a label', async () => {
      render(<ShareChatModal {...defaultProps} />);

      const labelInput = screen.getByPlaceholderText(/shared with design team/i);
      await user.type(labelInput, 'Shared with Marketing');

      expect(labelInput).toHaveValue('Shared with Marketing');
    });

    it('closes modal when cancel button is clicked', async () => {
      const onClose = vi.fn();
      render(<ShareChatModal {...defaultProps} onClose={onClose} />);

      const cancelButton = screen.getByRole('button', { name: /cancel/i });
      await user.click(cancelButton);

      expect(onClose).toHaveBeenCalledTimes(1);
    });
  });

  describe('Form Submission', () => {
    it('calls createShareLink API on form submission', async () => {
      vi.mocked(apiModule.api.shareLinks.createShareLink).mockResolvedValue(mockShareLinkResponse);

      render(<ShareChatModal {...defaultProps} />);

      const submitButton = screen.getByRole('button', { name: /create share link/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(apiModule.api.shareLinks.createShareLink).toHaveBeenCalledWith({
          threadId: 'thread-123',
          role: 'view',
          expiryDays: 7,
          label: undefined,
        });
      });
    });

    it('passes label when provided', async () => {
      vi.mocked(apiModule.api.shareLinks.createShareLink).mockResolvedValue(mockShareLinkResponse);

      render(<ShareChatModal {...defaultProps} />);

      const labelInput = screen.getByPlaceholderText(/shared with design team/i);
      await user.type(labelInput, 'Test Label');

      const submitButton = screen.getByRole('button', { name: /create share link/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(apiModule.api.shareLinks.createShareLink).toHaveBeenCalledWith(
          expect.objectContaining({
            label: 'Test Label',
          })
        );
      });
    });

    it('trims whitespace from label', async () => {
      vi.mocked(apiModule.api.shareLinks.createShareLink).mockResolvedValue(mockShareLinkResponse);

      render(<ShareChatModal {...defaultProps} />);

      const labelInput = screen.getByPlaceholderText(/shared with design team/i);
      await user.type(labelInput, '  Test Label  ');

      const submitButton = screen.getByRole('button', { name: /create share link/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(apiModule.api.shareLinks.createShareLink).toHaveBeenCalledWith(
          expect.objectContaining({
            label: 'Test Label',
          })
        );
      });
    });

    it('shows loading state during submission', async () => {
      vi.mocked(apiModule.api.shareLinks.createShareLink).mockImplementation(
        () => new Promise(() => {}) // Never resolves
      );

      render(<ShareChatModal {...defaultProps} />);

      const submitButton = screen.getByRole('button', { name: /create share link/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText(/creating/i)).toBeInTheDocument();
      });
    });

    it('disables cancel button during submission', async () => {
      vi.mocked(apiModule.api.shareLinks.createShareLink).mockImplementation(
        () => new Promise(() => {})
      );

      render(<ShareChatModal {...defaultProps} />);

      const submitButton = screen.getByRole('button', { name: /create share link/i });
      await user.click(submitButton);

      await waitFor(() => {
        const cancelButton = screen.getByRole('button', { name: /cancel/i });
        expect(cancelButton).toBeDisabled();
      });
    });

    it('calls onShareLinkCreated callback on success', async () => {
      const onShareLinkCreated = vi.fn();
      vi.mocked(apiModule.api.shareLinks.createShareLink).mockResolvedValue(mockShareLinkResponse);

      render(<ShareChatModal {...defaultProps} onShareLinkCreated={onShareLinkCreated} />);

      const submitButton = screen.getByRole('button', { name: /create share link/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(onShareLinkCreated).toHaveBeenCalledWith(mockShareLinkResponse);
      });
    });
  });

  describe('Success State', () => {
    beforeEach(async () => {
      vi.mocked(apiModule.api.shareLinks.createShareLink).mockResolvedValue(mockShareLinkResponse);
    });

    it('displays success view after link creation', async () => {
      render(<ShareChatModal {...defaultProps} />);

      const submitButton = screen.getByRole('button', { name: /create share link/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(getDialogHeading(/share link created/i)).toBeInTheDocument();
      });
    });

    it('displays shareable URL', async () => {
      render(<ShareChatModal {...defaultProps} />);

      const submitButton = screen.getByRole('button', { name: /create share link/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByDisplayValue(mockShareLinkResponse.shareableUrl)).toBeInTheDocument();
      });
    });

    it('displays access level badge', async () => {
      render(<ShareChatModal {...defaultProps} />);

      const submitButton = screen.getByRole('button', { name: /create share link/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText('View Only')).toBeInTheDocument();
      });
    });

    it('displays comment badge for comment role', async () => {
      vi.mocked(apiModule.api.shareLinks.createShareLink).mockResolvedValue({
        ...mockShareLinkResponse,
        role: 'comment',
      });

      render(<ShareChatModal {...defaultProps} />);

      const submitButton = screen.getByRole('button', { name: /create share link/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText('View + Comment')).toBeInTheDocument();
      });
    });

    it('displays expiry date', async () => {
      render(<ShareChatModal {...defaultProps} />);

      const submitButton = screen.getByRole('button', { name: /create share link/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText('Expires')).toBeInTheDocument();
        expect(screen.getByText(/december/i)).toBeInTheDocument();
      });
    });

    it('displays info alert about revoking link', async () => {
      render(<ShareChatModal {...defaultProps} />);

      const submitButton = screen.getByRole('button', { name: /create share link/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText(/you can revoke this link/i)).toBeInTheDocument();
      });
    });

    it('displays done button in success state', async () => {
      render(<ShareChatModal {...defaultProps} />);

      const submitButton = screen.getByRole('button', { name: /create share link/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /done/i })).toBeInTheDocument();
      });
    });

    it('closes modal when done button is clicked', async () => {
      const onClose = vi.fn();
      render(<ShareChatModal {...defaultProps} onClose={onClose} />);

      const submitButton = screen.getByRole('button', { name: /create share link/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /done/i })).toBeInTheDocument();
      });

      const doneButton = screen.getByRole('button', { name: /done/i });
      await user.click(doneButton);

      expect(onClose).toHaveBeenCalled();
    });
  });

  describe('Copy to Clipboard', () => {
    beforeEach(async () => {
      vi.mocked(apiModule.api.shareLinks.createShareLink).mockResolvedValue(mockShareLinkResponse);
      mockClipboard.writeText.mockResolvedValue(undefined);
    });

    it('copies URL to clipboard on copy button click', async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true });
      const localUser = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });

      render(<ShareChatModal {...defaultProps} />);

      const submitButton = screen.getByRole('button', { name: /create share link/i });
      await localUser.click(submitButton);

      // Wait for success state to fully render
      await waitFor(() => {
        expect(screen.getByRole('button', { name: /copy link to clipboard/i })).toBeInTheDocument();
      });

      // Get the copy button and click it
      const copyButton = screen.getByRole('button', { name: /copy link to clipboard/i });
      await localUser.click(copyButton);

      // Verify the button changes to show "Copied!" which indicates clipboard write succeeded
      await waitFor(() => {
        expect(screen.getByRole('button', { name: /copied!/i })).toBeInTheDocument();
      });

      vi.useRealTimers();
    });

    it('shows copied state after successful copy', async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true });
      const userWithTimers = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });

      render(<ShareChatModal {...defaultProps} />);

      const submitButton = screen.getByRole('button', { name: /create share link/i });
      await userWithTimers.click(submitButton);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /copy link to clipboard/i })).toBeInTheDocument();
      });

      const copyButton = screen.getByRole('button', { name: /copy link to clipboard/i });
      await userWithTimers.click(copyButton);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /copied/i })).toBeInTheDocument();
      });

      vi.useRealTimers();
    });

    it('resets copied state after 2 seconds', async () => {
      vi.useFakeTimers({ shouldAdvanceTime: true });
      const userWithTimers = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });

      render(<ShareChatModal {...defaultProps} />);

      const submitButton = screen.getByRole('button', { name: /create share link/i });
      await userWithTimers.click(submitButton);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /copy link to clipboard/i })).toBeInTheDocument();
      });

      const copyButton = screen.getByRole('button', { name: /copy link to clipboard/i });
      await userWithTimers.click(copyButton);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /copied/i })).toBeInTheDocument();
      });

      // Advance time by 2 seconds
      await vi.advanceTimersByTimeAsync(2000);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /copy link to clipboard/i })).toBeInTheDocument();
      });

      vi.useRealTimers();
    });

    it('handles clipboard API failure gracefully', async () => {
      mockClipboard.writeText.mockRejectedValue(new Error('Clipboard not available'));
      const localUser = userEvent.setup();

      render(<ShareChatModal {...defaultProps} />);

      const submitButton = screen.getByRole('button', { name: /create share link/i });
      await localUser.click(submitButton);

      // Wait for success state to fully render
      await waitFor(
        () => {
          expect(screen.getByDisplayValue(mockShareLinkResponse.shareableUrl)).toBeInTheDocument();
          expect(getDialogHeading(/share link created/i)).toBeInTheDocument();
        },
        { timeout: 3000 }
      );

      const copyButton = await screen.findByRole('button', { name: /copy link to clipboard/i });
      await localUser.click(copyButton);

      // Should not crash - button should still be accessible after failed copy
      // The component logs the error but doesn't change the UI
      expect(copyButton).toBeInTheDocument();
    });
  });

  describe('Error Handling', () => {
    it('displays error message on API failure', async () => {
      vi.mocked(apiModule.api.shareLinks.createShareLink).mockRejectedValue(
        new Error('Network error')
      );

      render(<ShareChatModal {...defaultProps} />);

      const submitButton = screen.getByRole('button', { name: /create share link/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText('Network error')).toBeInTheDocument();
      });
    });

    it('displays generic error message for non-Error objects', async () => {
      vi.mocked(apiModule.api.shareLinks.createShareLink).mockRejectedValue('Unknown error');

      render(<ShareChatModal {...defaultProps} />);

      const submitButton = screen.getByRole('button', { name: /create share link/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText('Failed to create share link')).toBeInTheDocument();
      });
    });

    it('clears error when modal is closed and reopened', async () => {
      vi.mocked(apiModule.api.shareLinks.createShareLink).mockRejectedValue(
        new Error('Network error')
      );

      const { rerender } = render(<ShareChatModal {...defaultProps} />);

      const submitButton = screen.getByRole('button', { name: /create share link/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText('Network error')).toBeInTheDocument();
      });

      // Close modal
      rerender(<ShareChatModal {...defaultProps} isOpen={false} />);

      // Reopen modal
      rerender(<ShareChatModal {...defaultProps} isOpen={true} />);

      expect(screen.queryByText('Network error')).not.toBeInTheDocument();
    });
  });

  describe('State Reset on Modal Close', () => {
    it('resets form data when modal closes', async () => {
      const { rerender } = render(<ShareChatModal {...defaultProps} />);

      // Enter some data
      const labelInput = screen.getByPlaceholderText(/shared with design team/i);
      await user.type(labelInput, 'Test Label');

      expect(labelInput).toHaveValue('Test Label');

      // Close modal
      rerender(<ShareChatModal {...defaultProps} isOpen={false} />);

      // Reopen modal
      rerender(<ShareChatModal {...defaultProps} isOpen={true} />);

      const newLabelInput = screen.getByPlaceholderText(/shared with design team/i);
      expect(newLabelInput).toHaveValue('');
    });

    it('resets to form view after success state when modal reopens', async () => {
      vi.mocked(apiModule.api.shareLinks.createShareLink).mockResolvedValue(mockShareLinkResponse);

      const { rerender } = render(<ShareChatModal {...defaultProps} />);

      // Create share link
      const submitButton = screen.getByRole('button', { name: /create share link/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(getDialogHeading(/share link created/i)).toBeInTheDocument();
      });

      // Close modal
      rerender(<ShareChatModal {...defaultProps} isOpen={false} />);

      // Reopen modal
      rerender(<ShareChatModal {...defaultProps} isOpen={true} />);

      // Should show form again
      expect(getDialogHeading(/share chat thread/i)).toBeInTheDocument();
      expect(queryDialogHeading(/share link created/i)).not.toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('has accessible dialog title', () => {
      render(<ShareChatModal {...defaultProps} />);

      expect(screen.getByRole('dialog')).toHaveAccessibleName(/share chat thread/i);
    });

    it('has accessible label input', () => {
      render(<ShareChatModal {...defaultProps} />);

      const labelInput = screen.getByLabelText(/label/i);
      expect(labelInput).toBeInTheDocument();
    });

    it('has accessible copy button', async () => {
      vi.mocked(apiModule.api.shareLinks.createShareLink).mockResolvedValue(mockShareLinkResponse);

      render(<ShareChatModal {...defaultProps} />);

      const submitButton = screen.getByRole('button', { name: /create share link/i });
      await user.click(submitButton);

      await waitFor(() => {
        const copyButton = screen.getByRole('button', { name: /copy link to clipboard/i });
        expect(copyButton).toBeInTheDocument();
      });
    });

    it('has accessible shareable URL input in success state', async () => {
      vi.mocked(apiModule.api.shareLinks.createShareLink).mockResolvedValue(mockShareLinkResponse);

      render(<ShareChatModal {...defaultProps} />);

      const submitButton = screen.getByRole('button', { name: /create share link/i });
      await user.click(submitButton);

      await waitFor(() => {
        const urlInput = screen.getByLabelText(/shareable link/i);
        expect(urlInput).toBeInTheDocument();
      });
    });
  });

  describe('Edge Cases', () => {
    it('handles empty label correctly (sends undefined)', async () => {
      vi.mocked(apiModule.api.shareLinks.createShareLink).mockResolvedValue(mockShareLinkResponse);

      render(<ShareChatModal {...defaultProps} />);

      // Leave label empty
      const submitButton = screen.getByRole('button', { name: /create share link/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(apiModule.api.shareLinks.createShareLink).toHaveBeenCalledWith({
          threadId: 'thread-123',
          role: 'view',
          expiryDays: 7,
          label: undefined,
        });
      });
    });

    it('handles whitespace-only label as empty', async () => {
      vi.mocked(apiModule.api.shareLinks.createShareLink).mockResolvedValue(mockShareLinkResponse);

      render(<ShareChatModal {...defaultProps} />);

      const labelInput = screen.getByPlaceholderText(/shared with design team/i);
      await user.type(labelInput, '   ');

      const submitButton = screen.getByRole('button', { name: /create share link/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(apiModule.api.shareLinks.createShareLink).toHaveBeenCalledWith(
          expect.objectContaining({
            label: undefined,
          })
        );
      });
    });

    it('does not call onShareLinkCreated if not provided', async () => {
      vi.mocked(apiModule.api.shareLinks.createShareLink).mockResolvedValue(mockShareLinkResponse);

      render(<ShareChatModal {...defaultProps} onShareLinkCreated={undefined} />);

      const submitButton = screen.getByRole('button', { name: /create share link/i });
      await user.click(submitButton);

      // Should complete without error
      await waitFor(() => {
        expect(getDialogHeading(/share link created/i)).toBeInTheDocument();
      });
    });
  });
});
