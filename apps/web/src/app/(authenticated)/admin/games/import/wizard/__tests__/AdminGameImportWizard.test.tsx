/**
 * Admin Game Import Wizard Component Tests - Issue #4161
 *
 * Test coverage:
 * - Initial render and auth states
 * - Step navigation (next/back buttons)
 * - Store integration
 * - Error display
 * - Wizard submission
 * - Reset functionality
 *
 * Pattern: Vitest + React Testing Library
 * Target: ≥85% coverage
 */

import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { AdminGameImportWizardClient } from '../client';

import { useGameImportWizardStore } from '@/stores/useGameImportWizardStore';

// Mock dependencies
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: vi.fn(),
    back: vi.fn(),
  }),
  usePathname: () => '/admin/games/import/wizard',
}));

vi.mock('@/components/auth/AuthProvider', () => ({
  useAuthUser: vi.fn(),
}));

vi.mock('@/components/layout', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

const { useAuthUser } = await import('@/components/auth/AuthProvider');

const mockUser = {
  id: 'user-123',
  email: 'admin@test.com',
  role: 'Admin',
};

beforeEach(() => {
  vi.clearAllMocks();

  // Default: authenticated admin user
  (useAuthUser as ReturnType<typeof vi.fn>).mockReturnValue({
    user: mockUser,
    loading: false,
  });

  // Reset store to initial state
  useGameImportWizardStore.setState({
    currentStep: 1,
    uploadedPdf: null,
    extractedMetadata: null,
    selectedBggId: null,
    bggGameData: null,
    enrichedData: null,
    isProcessing: false,
    error: null,
  });
});

describe('AdminGameImportWizardClient', () => {
  describe('Authentication States', () => {
    it('shows loading spinner while auth is loading', () => {
      (useAuthUser as ReturnType<typeof vi.fn>).mockReturnValue({
        user: null,
        loading: true,
      });

      render(<AdminGameImportWizardClient />);

      expect(screen.getByText('Loading...')).toBeInTheDocument();
    });

    it('shows sign-in prompt when not authenticated', () => {
      (useAuthUser as ReturnType<typeof vi.fn>).mockReturnValue({
        user: null,
        loading: false,
      });

      render(<AdminGameImportWizardClient />);

      expect(screen.getByText('Authentication Required')).toBeInTheDocument();
      expect(screen.getByText('Please sign in to access the admin wizard.')).toBeInTheDocument();
      expect(screen.getByRole('link', { name: /sign in/i })).toHaveAttribute(
        'href',
        '/auth/signin'
      );
    });

    it('renders wizard when authenticated', () => {
      render(<AdminGameImportWizardClient />);

      expect(screen.getByText('Game Import Wizard')).toBeInTheDocument();
      expect(screen.getByText(/import a game from pdf/i)).toBeInTheDocument();
    });
  });

  describe('Initial Render', () => {
    it('displays all wizard steps in step indicator', () => {
      render(<AdminGameImportWizardClient />);

      expect(screen.getByText('1. Upload PDF')).toBeInTheDocument();
      expect(screen.getByText('2. Metadata')).toBeInTheDocument();
      expect(screen.getByText('3. BGG Match')).toBeInTheDocument();
      expect(screen.getByText('4. Finalize')).toBeInTheDocument();
    });

    it('highlights step 1 as active on initial render', () => {
      render(<AdminGameImportWizardClient />);

      const step1Indicator = screen.getByText('📄').closest('div');
      expect(step1Indicator).toHaveClass('bg-primary');
    });

    it('displays wizard state summary', () => {
      render(<AdminGameImportWizardClient />);

      expect(screen.getByText('Wizard State')).toBeInTheDocument();
      expect(screen.getByText(/step:/i)).toBeInTheDocument();
      expect(screen.getByText(/pdf:/i)).toBeInTheDocument();
    });

    it('shows reset button', () => {
      render(<AdminGameImportWizardClient />);

      expect(screen.getByRole('button', { name: /reset/i })).toBeInTheDocument();
    });
  });

  describe('Step Navigation', () => {
    it('Previous button is disabled on step 1', () => {
      render(<AdminGameImportWizardClient />);

      const prevButton = screen.getByRole('button', { name: /previous/i });
      expect(prevButton).toBeDisabled();
    });

    it('Next button is disabled when step validation fails', () => {
      render(<AdminGameImportWizardClient />);

      const nextButton = screen.getByRole('button', { name: /next/i });
      expect(nextButton).toBeDisabled(); // No PDF uploaded
    });

    it('Next button is enabled when step validation passes', () => {
      // Upload PDF to satisfy step 1 validation
      useGameImportWizardStore.setState({
        uploadedPdf: { id: 'pdf-123', fileName: 'test.pdf' },
      });

      render(<AdminGameImportWizardClient />);

      const nextButton = screen.getByRole('button', { name: /next/i });
      expect(nextButton).not.toBeDisabled();
    });

    it('clicking Next advances to next step', async () => {
      useGameImportWizardStore.setState({
        uploadedPdf: { id: 'pdf-123', fileName: 'test.pdf' },
      });

      render(<AdminGameImportWizardClient />);

      const nextButton = screen.getByRole('button', { name: /next/i });
      await userEvent.click(nextButton);

      await waitFor(() => {
        expect(useGameImportWizardStore.getState().currentStep).toBe(2);
      });
    });

    it('clicking Previous goes back to previous step', async () => {
      useGameImportWizardStore.setState({
        currentStep: 2,
        uploadedPdf: { id: 'pdf-123', fileName: 'test.pdf' },
        extractedMetadata: { title: 'Test Game' },
      });

      render(<AdminGameImportWizardClient />);

      const prevButton = screen.getByRole('button', { name: /previous/i });
      await userEvent.click(prevButton);

      await waitFor(() => {
        expect(useGameImportWizardStore.getState().currentStep).toBe(1);
      });
    });

    it('Submit button appears on final step instead of Next', () => {
      useGameImportWizardStore.setState({
        currentStep: 4,
      });

      render(<AdminGameImportWizardClient />);

      expect(screen.queryByRole('button', { name: /next/i })).not.toBeInTheDocument();
      expect(screen.getByRole('button', { name: /submit & import/i })).toBeInTheDocument();
    });
  });

  describe('Store Integration', () => {
    it('displays uploaded PDF information when available', () => {
      useGameImportWizardStore.setState({
        uploadedPdf: { id: 'pdf-123', fileName: 'rulebook.pdf' },
      });

      render(<AdminGameImportWizardClient />);

      expect(screen.getByText(/uploaded:/i)).toBeInTheDocument();
      expect(screen.getByText('rulebook.pdf')).toBeInTheDocument();
    });

    it('displays extracted metadata on step 2', () => {
      useGameImportWizardStore.setState({
        currentStep: 2,
        extractedMetadata: {
          title: 'Test Game',
          minPlayers: 2,
          maxPlayers: 4,
          playTime: 60,
        },
      });

      render(<AdminGameImportWizardClient />);

      expect(screen.getByText(/title:/i)).toBeInTheDocument();
      expect(screen.getByText('Test Game')).toBeInTheDocument();
      expect(screen.getByText(/2-4/i)).toBeInTheDocument();
      expect(screen.getByText(/60 min/i)).toBeInTheDocument();
    });

    it('displays selected BGG ID on step 3', () => {
      useGameImportWizardStore.setState({
        currentStep: 3,
        selectedBggId: 12345,
      });

      render(<AdminGameImportWizardClient />);

      expect(screen.getByText(/selected bgg id:/i)).toBeInTheDocument();
      expect(screen.getByText('12345')).toBeInTheDocument();
    });

    it('displays enriched data on step 4', () => {
      useGameImportWizardStore.setState({
        currentStep: 4,
        enrichedData: {
          title: 'Final Game Title',
          minPlayers: 2,
          maxPlayers: 4,
          bggId: 12345,
        },
      });

      render(<AdminGameImportWizardClient />);

      expect(screen.getByText(/final title:/i)).toBeInTheDocument();
      expect(screen.getByText('Final Game Title')).toBeInTheDocument();
      expect(screen.getByText(/bgg id:/i)).toBeInTheDocument();
      expect(screen.getByText(/12345/i)).toBeInTheDocument();
    });
  });

  describe('Error Handling', () => {
    it('displays error message when present', () => {
      useGameImportWizardStore.setState({
        error: 'Test error message',
      });

      render(<AdminGameImportWizardClient />);

      expect(screen.getByText('Error')).toBeInTheDocument();
      expect(screen.getByText('Test error message')).toBeInTheDocument();
    });

    it('clears error when navigating between steps', async () => {
      useGameImportWizardStore.setState({
        currentStep: 2,
        uploadedPdf: { id: 'pdf-123', fileName: 'test.pdf' },
        extractedMetadata: { title: 'Test' },
        error: 'Previous error',
      });

      render(<AdminGameImportWizardClient />);

      const prevButton = screen.getByRole('button', { name: /previous/i });
      await userEvent.click(prevButton);

      await waitFor(() => {
        expect(useGameImportWizardStore.getState().error).toBeNull();
      });
    });
  });

  describe('Wizard Submission', () => {
    it('Submit button is disabled without enriched data', () => {
      useGameImportWizardStore.setState({
        currentStep: 4,
        uploadedPdf: { id: 'pdf-123', fileName: 'test.pdf' },
        enrichedData: null,
      });

      render(<AdminGameImportWizardClient />);

      const submitButton = screen.getByRole('button', { name: /submit & import/i });
      expect(submitButton).toBeDisabled();
    });

    it('Submit button is enabled with complete data', () => {
      useGameImportWizardStore.setState({
        currentStep: 4,
        uploadedPdf: { id: 'pdf-123', fileName: 'test.pdf' },
        enrichedData: { title: 'Test Game' },
      });

      render(<AdminGameImportWizardClient />);

      const submitButton = screen.getByRole('button', { name: /submit & import/i });
      expect(submitButton).not.toBeDisabled();
    });

    it('shows processing state during submission', () => {
      useGameImportWizardStore.setState({
        currentStep: 4,
        uploadedPdf: { id: 'pdf-123', fileName: 'test.pdf' },
        enrichedData: { title: 'Test Game' },
        isProcessing: true,
      });

      render(<AdminGameImportWizardClient />);

      expect(screen.getByText(/submitting/i)).toBeInTheDocument();
    });
  });

  describe('Reset Functionality', () => {
    it('clicking Reset resets wizard state', async () => {
      useGameImportWizardStore.setState({
        currentStep: 3,
        uploadedPdf: { id: 'pdf-123', fileName: 'test.pdf' },
        extractedMetadata: { title: 'Test' },
        selectedBggId: 12345,
      });

      render(<AdminGameImportWizardClient />);

      const resetButton = screen.getByRole('button', { name: /reset/i });
      await userEvent.click(resetButton);

      await waitFor(() => {
        const state = useGameImportWizardStore.getState();
        expect(state.currentStep).toBe(1);
        expect(state.uploadedPdf).toBeNull();
        expect(state.extractedMetadata).toBeNull();
        expect(state.selectedBggId).toBeNull();
      });
    });
  });

  describe('Wizard State Summary', () => {
    it('displays completed state checkmarks', () => {
      useGameImportWizardStore.setState({
        currentStep: 4,
        uploadedPdf: { id: 'pdf-123', fileName: 'test.pdf' },
        extractedMetadata: { title: 'Test' },
        selectedBggId: 12345,
        enrichedData: { title: 'Final' },
      });

      render(<AdminGameImportWizardClient />);

      const summary = screen.getByText('Wizard State').closest('div');
      expect(within(summary!).getByText(/✓.*test\.pdf/i)).toBeInTheDocument();
      expect(within(summary!).getByText(/✓.*extracted/i)).toBeInTheDocument();
      expect(within(summary!).getByText(/✓.*id 12345/i)).toBeInTheDocument();
      expect(within(summary!).getByText(/✓.*ready/i)).toBeInTheDocument();
    });

    it('displays incomplete state markers', () => {
      useGameImportWizardStore.setState({
        currentStep: 1,
        uploadedPdf: null,
        extractedMetadata: null,
        selectedBggId: null,
        enrichedData: null,
      });

      render(<AdminGameImportWizardClient />);

      const summary = screen.getByText('Wizard State').closest('div');
      expect(within(summary!).getByText(/✗.*not uploaded/i)).toBeInTheDocument();
      expect(within(summary!).getByText(/✗.*not extracted/i)).toBeInTheDocument();
      expect(within(summary!).getByText(/✗.*not selected/i)).toBeInTheDocument();
      expect(within(summary!).getByText(/✗.*not ready/i)).toBeInTheDocument();
    });
  });
});
