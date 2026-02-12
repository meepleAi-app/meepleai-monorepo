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

// Mock useUploadPdf hook (used by Step1UploadPdf)
vi.mock('@/hooks/queries/useUploadPdf', () => ({
  useUploadPdf: vi.fn(() => ({
    mutate: vi.fn(),
    isPending: false,
    progress: 0,
    error: null,
  })),
}));

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

      // WizardSteps uses aria-current for active step
      const step1Button = screen.getByLabelText(/step 1:/i);
      expect(step1Button).toHaveAttribute('aria-current', 'step');
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

      // Check wizard state summary shows PDF uploaded
      const summary = screen.getByText('Wizard State').closest('div');
      expect(within(summary!).getByText(/✓.*rulebook\.pdf/i)).toBeInTheDocument();
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

  describe('Wizard Navigation & Progress - Issue #4166', () => {
    describe('Progress Bar', () => {
      it('displays progress bar with correct percentage for step 1', () => {
        render(<AdminGameImportWizardClient />);

        expect(screen.getByText('Step 1 of 4')).toBeInTheDocument();
        expect(screen.getByText('0%')).toBeInTheDocument();

        const progressBar = screen.getByRole('progressbar');
        expect(progressBar).toHaveAttribute('aria-valuenow', '0');
        expect(progressBar).toHaveStyle({ width: '0%' });
      });

      it('displays progress bar with correct percentage for step 2', () => {
        useGameImportWizardStore.setState({ currentStep: 2 });

        render(<AdminGameImportWizardClient />);

        expect(screen.getByText('Step 2 of 4')).toBeInTheDocument();
        expect(screen.getByText('33%')).toBeInTheDocument();

        const progressBar = screen.getByRole('progressbar');
        expect(progressBar).toHaveAttribute('aria-valuenow', '33.33333333333333');
      });

      it('displays progress bar with correct percentage for step 3', () => {
        useGameImportWizardStore.setState({ currentStep: 3 });

        render(<AdminGameImportWizardClient />);

        expect(screen.getByText('Step 3 of 4')).toBeInTheDocument();
        expect(screen.getByText('67%')).toBeInTheDocument();

        const progressBar = screen.getByRole('progressbar');
        expect(progressBar).toHaveAttribute('aria-valuenow', '66.66666666666666');
      });

      it('displays progress bar with correct percentage for step 4', () => {
        useGameImportWizardStore.setState({ currentStep: 4 });

        render(<AdminGameImportWizardClient />);

        expect(screen.getByText('Step 4 of 4')).toBeInTheDocument();
        expect(screen.getByText('100%')).toBeInTheDocument();

        const progressBar = screen.getByRole('progressbar');
        expect(progressBar).toHaveAttribute('aria-valuenow', '100');
        expect(progressBar).toHaveStyle({ width: '100%' });
      });
    });

    describe('WizardSteps Integration', () => {
      it('renders WizardSteps component with all steps', () => {
        render(<AdminGameImportWizardClient />);

        // WizardSteps should render all step labels
        expect(screen.getByText('1. Upload PDF')).toBeInTheDocument();
        expect(screen.getByText('2. Metadata')).toBeInTheDocument();
        expect(screen.getByText('3. BGG Match')).toBeInTheDocument();
        expect(screen.getByText('4. Finalize')).toBeInTheDocument();
      });

      it('highlights current step in WizardSteps', () => {
        useGameImportWizardStore.setState({ currentStep: 2 });

        render(<AdminGameImportWizardClient />);

        // Step 2 should have active state (aria-current)
        const step2 = screen.getByLabelText(/step 2:/i);
        expect(step2).toHaveAttribute('aria-current', 'step');
      });

      it('shows completed checkmark for completed steps', () => {
        useGameImportWizardStore.setState({ currentStep: 3 });

        render(<AdminGameImportWizardClient />);

        // Steps 1 and 2 should have completed state (green background)
        const step1 = screen.getByLabelText(/step 1:/i);
        const step2 = screen.getByLabelText(/step 2:/i);

        // WizardSteps applies bg-green-600 to completed steps
        const step1Indicator = within(step1).getByText('1. Upload PDF').previousSibling;
        const step2Indicator = within(step2).getByText('2. Metadata').previousSibling;

        expect(step1Indicator).toHaveClass('bg-green-600');
        expect(step2Indicator).toHaveClass('bg-green-600');
      });
    });

    describe('Breadcrumb Navigation', () => {
      it('allows clicking on completed steps to navigate back', async () => {
        useGameImportWizardStore.setState({
          currentStep: 3,
          uploadedPdf: { id: 'pdf-123', fileName: 'test.pdf' },
          extractedMetadata: { title: 'Test' },
        });

        render(<AdminGameImportWizardClient />);

        // Click on step 1 (completed)
        const step1Button = screen.getByLabelText(/step 1:/i);
        await userEvent.click(step1Button);

        await waitFor(() => {
          expect(useGameImportWizardStore.getState().currentStep).toBe(1);
        });
      });

      it('allows clicking on active step (no effect)', async () => {
        useGameImportWizardStore.setState({ currentStep: 2 });

        render(<AdminGameImportWizardClient />);

        // Click on step 2 (active)
        const step2Button = screen.getByLabelText(/step 2:/i);
        await userEvent.click(step2Button);

        await waitFor(() => {
          expect(useGameImportWizardStore.getState().currentStep).toBe(2);
        });
      });

      it('does not allow clicking on future steps', async () => {
        useGameImportWizardStore.setState({ currentStep: 1 });

        render(<AdminGameImportWizardClient />);

        // Step 3 button should be disabled
        const step3Button = screen.getByLabelText(/step 3:/i);
        expect(step3Button).toBeDisabled();
      });
    });

    describe('Validation Gates', () => {
      it('prevents navigation to step 2 without uploaded PDF', () => {
        useGameImportWizardStore.setState({
          currentStep: 1,
          uploadedPdf: null,
        });

        render(<AdminGameImportWizardClient />);

        const nextButton = screen.getByRole('button', { name: /next/i });
        expect(nextButton).toBeDisabled();
      });

      it('allows navigation to step 2 with uploaded PDF', () => {
        useGameImportWizardStore.setState({
          currentStep: 1,
          uploadedPdf: { id: 'pdf-123', fileName: 'test.pdf' },
        });

        render(<AdminGameImportWizardClient />);

        const nextButton = screen.getByRole('button', { name: /next/i });
        expect(nextButton).not.toBeDisabled();
      });

      it('prevents navigation to step 3 without extracted metadata', () => {
        useGameImportWizardStore.setState({
          currentStep: 2,
          uploadedPdf: { id: 'pdf-123', fileName: 'test.pdf' },
          extractedMetadata: null,
        });

        render(<AdminGameImportWizardClient />);

        const nextButton = screen.getByRole('button', { name: /next/i });
        expect(nextButton).toBeDisabled();
      });

      it('prevents navigation to step 4 without BGG selection', () => {
        useGameImportWizardStore.setState({
          currentStep: 3,
          uploadedPdf: { id: 'pdf-123', fileName: 'test.pdf' },
          extractedMetadata: { title: 'Test' },
          selectedBggId: null,
        });

        render(<AdminGameImportWizardClient />);

        const nextButton = screen.getByRole('button', { name: /next/i });
        expect(nextButton).toBeDisabled();
      });
    });
  });
});
