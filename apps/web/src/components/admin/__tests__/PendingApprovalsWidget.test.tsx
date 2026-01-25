import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PendingApprovalsWidget } from '../PendingApprovalsWidget';
import { api } from '@/lib/api';
import { toast } from 'sonner';

// ============================================================================
// Mocks
// ============================================================================

vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: {
      getPendingApprovals: vi.fn(),
      approvePublication: vi.fn(),
      rejectPublication: vi.fn(),
    },
  },
}));

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

// ============================================================================
// Test Data
// ============================================================================

const mockGames = [
  {
    id: '123e4567-e89b-12d3-a456-426614174000',
    bggId: 13,
    title: 'Catan',
    status: 1, // PendingApproval
    yearPublished: 1995,
    description: 'A game about trading and building',
    minPlayers: 3,
    maxPlayers: 4,
    playingTimeMinutes: 90,
    minAge: 10,
    complexityRating: 2.5,
    averageRating: 7.2,
    imageUrl: 'https://example.com/catan.jpg',
    thumbnailUrl: 'https://example.com/catan-thumb.jpg',
    createdAt: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(), // 2h ago
    modifiedAt: null,
  },
  {
    id: '223e4567-e89b-12d3-a456-426614174001',
    bggId: 9209,
    title: 'Ticket to Ride',
    status: 1,
    yearPublished: 2004,
    description: 'A railway-themed board game',
    minPlayers: 2,
    maxPlayers: 5,
    playingTimeMinutes: 45,
    minAge: 8,
    complexityRating: 1.9,
    averageRating: 7.5,
    imageUrl: 'https://example.com/ttr.jpg',
    thumbnailUrl: 'https://example.com/ttr-thumb.jpg',
    createdAt: new Date(Date.now() - 5 * 60 * 60 * 1000).toISOString(), // 5h ago
    modifiedAt: null,
  },
  {
    id: '323e4567-e89b-12d3-a456-426614174002',
    bggId: 30549,
    title: 'Pandemic',
    status: 1,
    yearPublished: 2008,
    description: 'Cooperative disease fighting game',
    minPlayers: 2,
    maxPlayers: 4,
    playingTimeMinutes: 45,
    minAge: 8,
    complexityRating: 2.4,
    averageRating: 7.6,
    imageUrl: 'https://example.com/pandemic.jpg',
    thumbnailUrl: 'https://example.com/pandemic-thumb.jpg',
    createdAt: new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString(), // 1d ago
    modifiedAt: null,
  },
];

// ============================================================================
// Tests
// ============================================================================

describe('PendingApprovalsWidget', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  // ==========================================================================
  // Rendering Tests
  // ==========================================================================

  describe('rendering', () => {
    it('renders title correctly', async () => {
      vi.mocked(api.sharedGames.getPendingApprovals).mockResolvedValue({
        items: [],
        total: 0,
        page: 1,
        pageSize: 3,
      });

      render(<PendingApprovalsWidget />);

      await waitFor(() => {
        expect(screen.getByTestId('widget-title')).toHaveTextContent('In Attesa di Approvazione');
      });
    });

    it('displays all pending games', async () => {
      vi.mocked(api.sharedGames.getPendingApprovals).mockResolvedValue({
        items: mockGames,
        total: 3,
        page: 1,
        pageSize: 3,
      });

      render(<PendingApprovalsWidget />);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
        expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
        expect(screen.getByText('Pandemic')).toBeInTheDocument();
      });
    });

    it('displays relative time', async () => {
      vi.mocked(api.sharedGames.getPendingApprovals).mockResolvedValue({
        items: [mockGames[0]],
        total: 1,
        page: 1,
        pageSize: 3,
      });

      render(<PendingApprovalsWidget />);

      await waitFor(() => {
        expect(screen.getByText(/2h fa/)).toBeInTheDocument();
      });
    });

    it('displays badge with total count', async () => {
      vi.mocked(api.sharedGames.getPendingApprovals).mockResolvedValue({
        items: mockGames,
        total: 3,
        page: 1,
        pageSize: 3,
      });

      render(<PendingApprovalsWidget />);

      await waitFor(() => {
        expect(screen.getByText('3')).toBeInTheDocument();
      });
    });

    it('displays "Vedi tutti" link when total exceeds limit', async () => {
      vi.mocked(api.sharedGames.getPendingApprovals).mockResolvedValue({
        items: mockGames,
        total: 10,
        page: 1,
        pageSize: 3,
      });

      render(<PendingApprovalsWidget limit={3} />);

      await waitFor(() => {
        const link = screen.getByTestId('view-all-link');
        expect(link).toBeInTheDocument();
        expect(link).toHaveAttribute('href', '/admin/shared-games?status=pending');
      });
    });

    it('does not display "Vedi tutti" link when total equals limit', async () => {
      vi.mocked(api.sharedGames.getPendingApprovals).mockResolvedValue({
        items: mockGames,
        total: 3,
        page: 1,
        pageSize: 3,
      });

      render(<PendingApprovalsWidget limit={3} />);

      await waitFor(() => {
        expect(screen.queryByTestId('view-all-link')).not.toBeInTheDocument();
      });
    });

    it('renders action buttons for each game', async () => {
      vi.mocked(api.sharedGames.getPendingApprovals).mockResolvedValue({
        items: [mockGames[0]],
        total: 1,
        page: 1,
        pageSize: 3,
      });

      render(<PendingApprovalsWidget />);

      await waitFor(() => {
        expect(screen.getByTestId('approve-btn-123e4567-e89b-12d3-a456-426614174000')).toBeInTheDocument();
        expect(screen.getByTestId('reject-btn-123e4567-e89b-12d3-a456-426614174000')).toBeInTheDocument();
        expect(screen.getByTestId('preview-btn-123e4567-e89b-12d3-a456-426614174000')).toBeInTheDocument();
      });
    });
  });

  // ==========================================================================
  // Empty State Tests
  // ==========================================================================

  describe('empty state', () => {
    it('displays empty state when no games pending', async () => {
      vi.mocked(api.sharedGames.getPendingApprovals).mockResolvedValue({
        items: [],
        total: 0,
        page: 1,
        pageSize: 3,
      });

      render(<PendingApprovalsWidget />);

      await waitFor(() => {
        expect(screen.getByTestId('empty-state')).toBeInTheDocument();
        expect(screen.getByTestId('empty-state-message')).toHaveTextContent('Nessun gioco in attesa di approvazione');
      });
    });

    it('does not display badge when total is 0', async () => {
      vi.mocked(api.sharedGames.getPendingApprovals).mockResolvedValue({
        items: [],
        total: 0,
        page: 1,
        pageSize: 3,
      });

      render(<PendingApprovalsWidget />);

      await waitFor(() => {
        expect(screen.queryByText('0')).not.toBeInTheDocument();
      });
    });
  });

  // ==========================================================================
  // Loading State Tests
  // ==========================================================================

  describe('loading state', () => {
    it('shows skeleton loaders when loading', () => {
      vi.mocked(api.sharedGames.getPendingApprovals).mockImplementation(
        () => new Promise(() => {}) // Never resolves
      );

      render(<PendingApprovalsWidget limit={3} />);

      expect(screen.getByTestId('widget-skeleton')).toBeInTheDocument();
      expect(screen.getAllByRole('generic').filter(el => el.className.includes('animate-pulse'))).toHaveLength(3);
    });
  });

  // ==========================================================================
  // Error State Tests
  // ==========================================================================

  describe('error state', () => {
    it('displays error message when fetch fails', async () => {
      const consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
      vi.mocked(api.sharedGames.getPendingApprovals).mockRejectedValue(
        new Error('Network error')
      );

      render(<PendingApprovalsWidget />);

      await waitFor(() => {
        expect(screen.getByRole('alert')).toBeInTheDocument();
        expect(screen.getByTestId('widget-error')).toHaveTextContent('Errore nel caricamento delle approvazioni in attesa');
      });

      expect(toast.error).toHaveBeenCalledWith('Impossibile caricare le approvazioni');
      consoleErrorSpy.mockRestore();
    });
  });

  // ==========================================================================
  // Interaction Tests
  // ==========================================================================

  describe('interactions', () => {
    it('calls approvePublication when approve button clicked', async () => {
      const user = userEvent.setup();
      vi.mocked(api.sharedGames.getPendingApprovals).mockResolvedValue({
        items: [mockGames[0]],
        total: 1,
        page: 1,
        pageSize: 3,
      });
      vi.mocked(api.sharedGames.approvePublication).mockResolvedValue();

      render(<PendingApprovalsWidget />);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
      });

      const approveBtn = screen.getByTestId('approve-btn-123e4567-e89b-12d3-a456-426614174000');
      await user.click(approveBtn);

      await waitFor(() => {
        expect(api.sharedGames.approvePublication).toHaveBeenCalledWith(
          '123e4567-e89b-12d3-a456-426614174000'
        );
        expect(toast.success).toHaveBeenCalledWith('"Catan" approvato con successo');
      });
    });

    it('calls rejectPublication when reject button clicked', async () => {
      const user = userEvent.setup();
      vi.mocked(api.sharedGames.getPendingApprovals).mockResolvedValue({
        items: [mockGames[0]],
        total: 1,
        page: 1,
        pageSize: 3,
      });
      vi.mocked(api.sharedGames.rejectPublication).mockResolvedValue();

      render(<PendingApprovalsWidget />);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
      });

      const rejectBtn = screen.getByTestId('reject-btn-123e4567-e89b-12d3-a456-426614174000');
      await user.click(rejectBtn);

      await waitFor(() => {
        expect(api.sharedGames.rejectPublication).toHaveBeenCalledWith(
          '123e4567-e89b-12d3-a456-426614174000',
          'Rifiutato dall\'amministratore'
        );
        expect(toast.success).toHaveBeenCalledWith('"Catan" rifiutato');
      });
    });

    it('removes game from list after successful approval (optimistic update)', async () => {
      const user = userEvent.setup();
      vi.mocked(api.sharedGames.getPendingApprovals).mockResolvedValue({
        items: mockGames,
        total: 3,
        page: 1,
        pageSize: 3,
      });
      vi.mocked(api.sharedGames.approvePublication).mockResolvedValue();

      render(<PendingApprovalsWidget />);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
      });

      const approveBtn = screen.getByTestId('approve-btn-123e4567-e89b-12d3-a456-426614174000');
      await user.click(approveBtn);

      await waitFor(() => {
        expect(screen.queryByText('Catan')).not.toBeInTheDocument();
      });
    });

    it('disables buttons while processing', async () => {
      const user = userEvent.setup();
      vi.mocked(api.sharedGames.getPendingApprovals).mockResolvedValue({
        items: [mockGames[0]],
        total: 1,
        page: 1,
        pageSize: 3,
      });
      vi.mocked(api.sharedGames.approvePublication).mockImplementation(
        () => new Promise(resolve => setTimeout(resolve, 1000))
      );

      render(<PendingApprovalsWidget />);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
      });

      const approveBtn = screen.getByTestId('approve-btn-123e4567-e89b-12d3-a456-426614174000');
      const rejectBtn = screen.getByTestId('reject-btn-123e4567-e89b-12d3-a456-426614174000');

      await user.click(approveBtn);

      expect(approveBtn).toBeDisabled();
      expect(rejectBtn).toBeDisabled();
    });

    it('shows error toast and refetches data when approval fails', async () => {
      const user = userEvent.setup();
      const consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});

      vi.mocked(api.sharedGames.getPendingApprovals).mockResolvedValue({
        items: [mockGames[0]],
        total: 1,
        page: 1,
        pageSize: 3,
      });
      vi.mocked(api.sharedGames.approvePublication).mockRejectedValue(
        new Error('Server error')
      );

      render(<PendingApprovalsWidget />);

      await waitFor(() => {
        expect(screen.getByText('Catan')).toBeInTheDocument();
      });

      const approveBtn = screen.getByTestId('approve-btn-123e4567-e89b-12d3-a456-426614174000');
      await user.click(approveBtn);

      await waitFor(() => {
        expect(toast.error).toHaveBeenCalledWith('Errore nell\'approvazione del gioco');
        expect(api.sharedGames.getPendingApprovals).toHaveBeenCalledTimes(2); // Initial + refetch
      });

      consoleErrorSpy.mockRestore();
    });

    it('navigates to preview when preview button clicked', async () => {
      vi.mocked(api.sharedGames.getPendingApprovals).mockResolvedValue({
        items: [mockGames[0]],
        total: 1,
        page: 1,
        pageSize: 3,
      });

      render(<PendingApprovalsWidget />);

      await waitFor(() => {
        const previewLink = screen.getByTestId('preview-btn-123e4567-e89b-12d3-a456-426614174000');
        expect(previewLink).toHaveAttribute(
          'href',
          '/admin/shared-games/123e4567-e89b-12d3-a456-426614174000'
        );
      });
    });
  });

  // ==========================================================================
  // Props Tests
  // ==========================================================================

  describe('props', () => {
    it('respects custom limit prop', async () => {
      vi.mocked(api.sharedGames.getPendingApprovals).mockResolvedValue({
        items: [],
        total: 0,
        page: 1,
        pageSize: 5,
      });

      render(<PendingApprovalsWidget limit={5} />);

      await waitFor(() => {
        expect(api.sharedGames.getPendingApprovals).toHaveBeenCalledWith({
          pageNumber: 1,
          pageSize: 5,
        });
      });
    });

    it('applies custom className', async () => {
      vi.mocked(api.sharedGames.getPendingApprovals).mockResolvedValue({
        items: [],
        total: 0,
        page: 1,
        pageSize: 3,
      });

      render(<PendingApprovalsWidget className="custom-class" />);

      await waitFor(() => {
        const widget = screen.getByTestId('pending-approvals-widget');
        expect(widget).toHaveClass('custom-class');
      });
    });

    it('uses custom data-testid', async () => {
      vi.mocked(api.sharedGames.getPendingApprovals).mockResolvedValue({
        items: [],
        total: 0,
        page: 1,
        pageSize: 3,
      });

      render(<PendingApprovalsWidget data-testid="custom-widget" />);

      await waitFor(() => {
        expect(screen.getByTestId('custom-widget')).toBeInTheDocument();
      });
    });
  });

  // ==========================================================================
  // Accessibility Tests
  // ==========================================================================

  describe('accessibility', () => {
    it('uses semantic HTML with proper roles', async () => {
      vi.mocked(api.sharedGames.getPendingApprovals).mockResolvedValue({
        items: mockGames,
        total: 3,
        page: 1,
        pageSize: 3,
      });

      render(<PendingApprovalsWidget />);

      await waitFor(() => {
        expect(screen.getByRole('list')).toBeInTheDocument();
        expect(screen.getAllByRole('listitem')).toHaveLength(3);
      });
    });

    it('provides screen reader text for icon buttons', async () => {
      vi.mocked(api.sharedGames.getPendingApprovals).mockResolvedValue({
        items: [mockGames[0]],
        total: 1,
        page: 1,
        pageSize: 3,
      });

      render(<PendingApprovalsWidget />);

      await waitFor(() => {
        expect(screen.getByText('Visualizza dettagli', { selector: '.sr-only' })).toBeInTheDocument();
      });
    });

    it('uses alert role for error messages', async () => {
      const consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
      vi.mocked(api.sharedGames.getPendingApprovals).mockRejectedValue(
        new Error('Network error')
      );

      render(<PendingApprovalsWidget />);

      await waitFor(() => {
        expect(screen.getByRole('alert')).toBeInTheDocument();
      });

      consoleErrorSpy.mockRestore();
    });
  });
});
