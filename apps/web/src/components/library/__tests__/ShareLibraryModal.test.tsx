/**
 * ShareLibraryModal Component Tests (Issue #2614)
 *
 * Test Coverage:
 * - Modal open/close behavior
 * - Loading state
 * - Create new share link
 * - Update existing share link
 * - Copy URL to clipboard
 * - Revoke share link
 * - Privacy level selection
 * - Include notes toggle
 * - Expiration date input
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { ShareLibraryModal } from '../ShareLibraryModal';
import { toast } from '@/components/layout/Toast';
import { createMockShareLink } from '@/__tests__/fixtures/common-fixtures';

// Mock the hooks
const mockUseLibraryShareLink = vi.fn();
const mockCreateMutation = {
  mutateAsync: vi.fn(),
  isPending: false,
};
const mockUpdateMutation = {
  mutateAsync: vi.fn(),
  isPending: false,
};
const mockRevokeMutation = {
  mutateAsync: vi.fn(),
  isPending: false,
};

vi.mock('@/hooks/queries', () => ({
  useLibraryShareLink: (isOpen: boolean) => mockUseLibraryShareLink(isOpen),
  useCreateShareLink: () => mockCreateMutation,
  useUpdateShareLink: () => mockUpdateMutation,
  useRevokeShareLink: () => mockRevokeMutation,
}));

// Mock toast
vi.mock('@/components/layout/Toast', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

// Mock clipboard API
const mockWriteText = vi.fn();
vi.stubGlobal('navigator', {
  clipboard: {
    writeText: mockWriteText,
  },
});

describe('ShareLibraryModal', () => {
  const mockOnClose = vi.fn();

  // Use fixture factory instead of magic strings
  const defaultShareLink = createMockShareLink({
    shareToken: 'token-123',
    shareUrl: 'https://example.com/share/abc123',
    viewCount: 42,
    lastAccessedAt: '2024-01-15T10:30:00Z',
  });

  beforeEach(() => {
    vi.clearAllMocks();
    mockUseLibraryShareLink.mockReturnValue({
      data: null,
      isLoading: false,
    });
    mockCreateMutation.mutateAsync.mockResolvedValue({});
    mockCreateMutation.isPending = false;
    mockUpdateMutation.mutateAsync.mockResolvedValue({});
    mockUpdateMutation.isPending = false;
    mockRevokeMutation.mutateAsync.mockResolvedValue({});
    mockRevokeMutation.isPending = false;
    mockWriteText.mockResolvedValue(undefined);
  });

  describe('Modal Behavior', () => {
    it('renders dialog when isOpen is true', () => {
      render(<ShareLibraryModal isOpen={true} onClose={mockOnClose} />);

      expect(screen.getByRole('dialog')).toBeInTheDocument();
      expect(screen.getByText('Condividi Libreria')).toBeInTheDocument();
    });

    it('does not render dialog when isOpen is false', () => {
      render(<ShareLibraryModal isOpen={false} onClose={mockOnClose} />);

      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    it('calls onClose when close button is clicked', async () => {
      const user = userEvent.setup();
      render(<ShareLibraryModal isOpen={true} onClose={mockOnClose} />);

      const closeButton = screen.getByRole('button', { name: /chiudi/i });
      await user.click(closeButton);

      expect(mockOnClose).toHaveBeenCalledTimes(1);
    });
  });

  describe('Loading State', () => {
    it('shows loading spinner when fetching share link', () => {
      mockUseLibraryShareLink.mockReturnValue({
        data: null,
        isLoading: true,
      });

      render(<ShareLibraryModal isOpen={true} onClose={mockOnClose} />);

      expect(document.querySelector('.animate-spin')).toBeInTheDocument();
    });

    it('hides loading spinner after data is loaded', () => {
      mockUseLibraryShareLink.mockReturnValue({
        data: defaultShareLink,
        isLoading: false,
      });

      render(<ShareLibraryModal isOpen={true} onClose={mockOnClose} />);

      // Content should be visible
      expect(screen.getByText('Link Attivo')).toBeInTheDocument();
    });
  });

  describe('Create Share Link', () => {
    it('shows "Crea Link" button when no active link exists', () => {
      render(<ShareLibraryModal isOpen={true} onClose={mockOnClose} />);

      expect(screen.getByRole('button', { name: /crea link/i })).toBeInTheDocument();
    });

    it('creates new share link with default settings', async () => {
      const user = userEvent.setup();
      render(<ShareLibraryModal isOpen={true} onClose={mockOnClose} />);

      const createButton = screen.getByRole('button', { name: /crea link/i });
      await user.click(createButton);

      expect(mockCreateMutation.mutateAsync).toHaveBeenCalledWith({
        privacyLevel: 'unlisted',
        includeNotes: false,
        expiresAt: null,
      });
      expect(toast.success).toHaveBeenCalledWith('Link di condivisione creato!');
    });

    it('creates share link with public privacy level', async () => {
      const user = userEvent.setup();
      render(<ShareLibraryModal isOpen={true} onClose={mockOnClose} />);

      // Select public privacy
      const publicOption = screen.getByText('Pubblico');
      await user.click(publicOption);

      const createButton = screen.getByRole('button', { name: /crea link/i });
      await user.click(createButton);

      expect(mockCreateMutation.mutateAsync).toHaveBeenCalledWith(
        expect.objectContaining({ privacyLevel: 'public' })
      );
    });

    it('creates share link with notes included', async () => {
      const user = userEvent.setup();
      render(<ShareLibraryModal isOpen={true} onClose={mockOnClose} />);

      const notesSwitch = screen.getByRole('switch', { name: /includi note personali/i });
      await user.click(notesSwitch);

      const createButton = screen.getByRole('button', { name: /crea link/i });
      await user.click(createButton);

      expect(mockCreateMutation.mutateAsync).toHaveBeenCalledWith(
        expect.objectContaining({ includeNotes: true })
      );
    });

    it('shows error toast when creation fails', async () => {
      mockCreateMutation.mutateAsync.mockRejectedValue(new Error('Network error'));
      const user = userEvent.setup();
      render(<ShareLibraryModal isOpen={true} onClose={mockOnClose} />);

      const createButton = screen.getByRole('button', { name: /crea link/i });
      await user.click(createButton);

      await waitFor(() => {
        expect(toast.error).toHaveBeenCalledWith('Network error');
      });
    });
  });

  describe('Active Share Link Display', () => {
    beforeEach(() => {
      mockUseLibraryShareLink.mockReturnValue({
        data: defaultShareLink,
        isLoading: false,
      });
    });

    it('displays share URL in input', () => {
      render(<ShareLibraryModal isOpen={true} onClose={mockOnClose} />);

      const urlInput = screen.getByDisplayValue(defaultShareLink.shareUrl);
      expect(urlInput).toBeInTheDocument();
      expect(urlInput).toHaveAttribute('readonly');
    });

    it('displays view count badge', () => {
      render(<ShareLibraryModal isOpen={true} onClose={mockOnClose} />);

      // View count is split across elements
      expect(screen.getByText('42', { exact: false })).toBeInTheDocument();
      expect(screen.getByText(/visualizzazioni/i)).toBeInTheDocument();
    });

    it('displays last accessed date when available', () => {
      render(<ShareLibraryModal isOpen={true} onClose={mockOnClose} />);

      expect(screen.getByText(/ultimo accesso/i)).toBeInTheDocument();
    });

    it('shows "Aggiorna Impostazioni" button', () => {
      render(<ShareLibraryModal isOpen={true} onClose={mockOnClose} />);

      expect(screen.getByRole('button', { name: /aggiorna impostazioni/i })).toBeInTheDocument();
    });
  });

  describe('Update Share Link', () => {
    beforeEach(() => {
      mockUseLibraryShareLink.mockReturnValue({
        data: defaultShareLink,
        isLoading: false,
      });
    });

    it('updates share link settings', async () => {
      const user = userEvent.setup();
      render(<ShareLibraryModal isOpen={true} onClose={mockOnClose} />);

      // Toggle notes on
      const notesSwitch = screen.getByRole('switch', { name: /includi note personali/i });
      await user.click(notesSwitch);

      const updateButton = screen.getByRole('button', { name: /aggiorna impostazioni/i });
      await user.click(updateButton);

      expect(mockUpdateMutation.mutateAsync).toHaveBeenCalledWith({
        shareToken: 'token-123',
        request: expect.objectContaining({ includeNotes: true }),
      });
      expect(toast.success).toHaveBeenCalledWith('Impostazioni condivisione aggiornate.');
    });
  });

  describe('Copy URL', () => {
    beforeEach(() => {
      mockUseLibraryShareLink.mockReturnValue({
        data: defaultShareLink,
        isLoading: false,
      });
    });

    it('renders copy button for active link', () => {
      render(<ShareLibraryModal isOpen={true} onClose={mockOnClose} />);

      const copyButton = screen.getByRole('button', { name: /copia link/i });
      expect(copyButton).toBeInTheDocument();
    });
  });

  describe('Revoke Share Link', () => {
    beforeEach(() => {
      mockUseLibraryShareLink.mockReturnValue({
        data: defaultShareLink,
        isLoading: false,
      });
    });

    it('shows revoke button when active link exists', () => {
      render(<ShareLibraryModal isOpen={true} onClose={mockOnClose} />);

      expect(screen.getByRole('button', { name: /revoca link/i })).toBeInTheDocument();
    });

    it('shows confirmation dialog when revoke is clicked', async () => {
      const user = userEvent.setup();
      render(<ShareLibraryModal isOpen={true} onClose={mockOnClose} />);

      const revokeButton = screen.getByRole('button', { name: /revoca link/i });
      await user.click(revokeButton);

      expect(screen.getByText(/questa azione disabiliterà permanentemente/i)).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /conferma revoca/i })).toBeInTheDocument();
    });

    it('hides confirmation when cancel is clicked', async () => {
      const user = userEvent.setup();
      render(<ShareLibraryModal isOpen={true} onClose={mockOnClose} />);

      const revokeButton = screen.getByRole('button', { name: /revoca link/i });
      await user.click(revokeButton);

      const cancelButton = screen.getByRole('button', { name: /annulla/i });
      await user.click(cancelButton);

      expect(screen.queryByText(/questa azione disabiliterà permanentemente/i)).not.toBeInTheDocument();
    });

    it('revokes share link when confirmed', async () => {
      const user = userEvent.setup();
      render(<ShareLibraryModal isOpen={true} onClose={mockOnClose} />);

      const revokeButton = screen.getByRole('button', { name: /revoca link/i });
      await user.click(revokeButton);

      const confirmButton = screen.getByRole('button', { name: /conferma revoca/i });
      await user.click(confirmButton);

      expect(mockRevokeMutation.mutateAsync).toHaveBeenCalledWith('token-123');
      expect(toast.success).toHaveBeenCalledWith('Link di condivisione revocato.');
    });
  });

  describe('Privacy Level Selection', () => {
    it('renders both privacy options', () => {
      render(<ShareLibraryModal isOpen={true} onClose={mockOnClose} />);

      expect(screen.getByText('Non elencato')).toBeInTheDocument();
      expect(screen.getByText('Pubblico')).toBeInTheDocument();
    });

    it('unlisted is selected by default', () => {
      render(<ShareLibraryModal isOpen={true} onClose={mockOnClose} />);

      const unlistedRadio = screen.getByRole('radio', { name: /non elencato/i });
      expect(unlistedRadio).toBeChecked();
    });

    it('allows switching to public', async () => {
      const user = userEvent.setup();
      render(<ShareLibraryModal isOpen={true} onClose={mockOnClose} />);

      const publicLabel = screen.getByText('Pubblico');
      await user.click(publicLabel);

      const publicRadio = screen.getByRole('radio', { name: /pubblico/i });
      expect(publicRadio).toBeChecked();
    });
  });

  describe('Expiration Date', () => {
    it('renders expiration date input', () => {
      render(<ShareLibraryModal isOpen={true} onClose={mockOnClose} />);

      expect(screen.getByLabelText(/data di scadenza/i)).toBeInTheDocument();
    });

    it('creates link with expiration date when set', async () => {
      const user = userEvent.setup();
      render(<ShareLibraryModal isOpen={true} onClose={mockOnClose} />);

      const dateInput = screen.getByLabelText(/data di scadenza/i);
      await user.type(dateInput, '2025-12-31T23:59');

      const createButton = screen.getByRole('button', { name: /crea link/i });
      await user.click(createButton);

      expect(mockCreateMutation.mutateAsync).toHaveBeenCalledWith(
        expect.objectContaining({
          expiresAt: expect.any(String),
        })
      );
    });
  });

  describe('Form State Initialization', () => {
    it('loads existing settings when modal opens with active link', () => {
      const activeLink = {
        ...defaultShareLink,
        privacyLevel: 'public',
        includeNotes: true,
      };
      mockUseLibraryShareLink.mockReturnValue({
        data: activeLink,
        isLoading: false,
      });

      render(<ShareLibraryModal isOpen={true} onClose={mockOnClose} />);

      const publicRadio = screen.getByRole('radio', { name: /pubblico/i });
      expect(publicRadio).toBeChecked();

      const notesSwitch = screen.getByRole('switch', { name: /includi note personali/i });
      expect(notesSwitch).toBeChecked();
    });

    it('resets to defaults when no active link', () => {
      mockUseLibraryShareLink.mockReturnValue({
        data: null,
        isLoading: false,
      });

      render(<ShareLibraryModal isOpen={true} onClose={mockOnClose} />);

      const unlistedRadio = screen.getByRole('radio', { name: /non elencato/i });
      expect(unlistedRadio).toBeChecked();

      const notesSwitch = screen.getByRole('switch', { name: /includi note personali/i });
      expect(notesSwitch).not.toBeChecked();
    });
  });
});
