/**
 * Admin Game Import Wizard — 5-step PDF flow
 *
 * Tests:
 * - Auth states (loading, unauthenticated, authenticated)
 * - Step rendering and navigation
 * - Progress bar
 * - Reset functionality
 * - Error display
 */

import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import { AdminGameImportWizardClient } from '../client';
import { useGameImportWizardStore } from '@/stores/useGameImportWizardStore';

// ── Mocks ──────────────────────────────────────────────────────────────────────

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), back: vi.fn() }),
  usePathname: () => '/admin/shared-games/import',
}));

vi.mock('@/components/auth/AuthProvider', () => ({
  useAuthUser: vi.fn(),
}));

vi.mock('@/components/layout', () => ({
  toast: { success: vi.fn(), error: vi.fn(), warning: vi.fn() },
}));

// Mock heavy step components that are integration-tested separately
vi.mock('../steps/Step2MetadataReview', () => ({
  Step2MetadataReview: () => <div data-testid="step2-metadata-review">Step 2 — Metadata</div>,
}));
vi.mock('../steps/Step3PreviewConfirm', () => ({
  Step3PreviewConfirm: () => <div data-testid="step3-preview-confirm">Step 3 — Preview</div>,
}));
vi.mock('../steps/Step4CreationProgress', () => ({
  Step4CreationProgress: () => (
    <div data-testid="step4-creation-progress">Step 4 — Creating...</div>
  ),
}));
vi.mock('../steps/Step5RagTest', () => ({
  Step5RagTest: () => <div data-testid="step5-rag-test">Step 5 — RAG Test</div>,
}));
vi.mock('../steps/Step1UploadPdf', () => ({
  Step1UploadPdf: ({ onUploadComplete }: { onUploadComplete: (pdf: unknown) => void }) => (
    <div data-testid="step1-upload-pdf">
      <button
        onClick={() =>
          onUploadComplete({ pdfDocumentId: 'pdf-123', id: 'pdf-123', fileName: 'test.pdf' })
        }
      >
        Simulate Upload
      </button>
    </div>
  ),
}));

const { useAuthUser } = await import('@/components/auth/AuthProvider');

const mockUser = { id: 'user-123', email: 'admin@test.com', role: 'Admin' };

// ── Setup ──────────────────────────────────────────────────────────────────────

beforeEach(() => {
  vi.clearAllMocks();

  (useAuthUser as ReturnType<typeof vi.fn>).mockReturnValue({
    user: mockUser,
    loading: false,
  });

  useGameImportWizardStore.setState(
    useGameImportWizardStore.getInitialState?.() ?? {
      currentStep: 1,
      uploadedPdf: null,
      reviewedMetadata: null,
      coverImage: { mode: 'placeholder', imageUrl: null },
      importResult: null,
      ragTestHistory: [],
      isIndexingReady: false,
      isProcessing: false,
      error: null,
      extractedMetadata: null,
      selectedBggId: null,
      enrichedData: null,
      bggGameData: null,
    }
  );
});

// ── Tests ──────────────────────────────────────────────────────────────────────

describe('AdminGameImportWizardClient', () => {
  describe('Authentication States', () => {
    it('shows loading spinner while auth is loading', () => {
      (useAuthUser as ReturnType<typeof vi.fn>).mockReturnValue({ user: null, loading: true });

      renderWithQuery(<AdminGameImportWizardClient />);

      expect(screen.getByText(/caricamento/i)).toBeInTheDocument();
    });

    it('shows sign-in prompt when not authenticated', () => {
      (useAuthUser as ReturnType<typeof vi.fn>).mockReturnValue({ user: null, loading: false });

      renderWithQuery(<AdminGameImportWizardClient />);

      expect(screen.getByText(/autenticazione richiesta/i)).toBeInTheDocument();
      expect(screen.getByRole('link', { name: /accedi/i })).toHaveAttribute('href', '/login');
    });

    it('renders wizard when authenticated', () => {
      renderWithQuery(<AdminGameImportWizardClient />);

      expect(screen.getByText('Importa gioco da PDF')).toBeInTheDocument();
    });
  });

  describe('Initial Render (Step 1)', () => {
    it('displays all 5 wizard step labels', () => {
      renderWithQuery(<AdminGameImportWizardClient />);

      // Labels appear in both the WizardSteps breadcrumb and the card header — use getAllByText
      expect(screen.getAllByText('1. Upload PDF').length).toBeGreaterThanOrEqual(1);
      expect(screen.getAllByText('2. Metadati').length).toBeGreaterThanOrEqual(1);
      expect(screen.getAllByText('3. Anteprima').length).toBeGreaterThanOrEqual(1);
      expect(screen.getAllByText('4. Creazione').length).toBeGreaterThanOrEqual(1);
      expect(screen.getAllByText('5. RAG Test').length).toBeGreaterThanOrEqual(1);
    });

    it('shows Step 1 content on initial render', () => {
      renderWithQuery(<AdminGameImportWizardClient />);

      expect(screen.getByTestId('step1-upload-pdf')).toBeInTheDocument();
    });

    it('shows step counter "Step 1 di 5"', () => {
      renderWithQuery(<AdminGameImportWizardClient />);

      expect(screen.getByText('Step 1 di 5')).toBeInTheDocument();
    });

    it('shows reset button', () => {
      renderWithQuery(<AdminGameImportWizardClient />);

      expect(screen.getByRole('button', { name: /ricomincia/i })).toBeInTheDocument();
    });

    it('shows 0% progress bar on step 1', () => {
      renderWithQuery(<AdminGameImportWizardClient />);

      expect(screen.getByText('0%')).toBeInTheDocument();
      const bar = screen.getByRole('progressbar');
      expect(bar).toHaveAttribute('aria-valuenow', '0');
    });
  });

  describe('Navigation', () => {
    it('Back button is disabled on step 1', () => {
      renderWithQuery(<AdminGameImportWizardClient />);

      const back = screen.getByRole('button', { name: /indietro/i });
      expect(back).toBeDisabled();
    });

    it('Next button is disabled without PDF', () => {
      renderWithQuery(<AdminGameImportWizardClient />);

      const next = screen.getByRole('button', { name: /avanti/i });
      expect(next).toBeDisabled();
    });

    it('Next is enabled after PDF upload', async () => {
      renderWithQuery(<AdminGameImportWizardClient />);

      await userEvent.click(screen.getByRole('button', { name: /simulate upload/i }));

      await waitFor(() => {
        const next = screen.getByRole('button', { name: /avanti/i });
        expect(next).not.toBeDisabled();
      });
    });

    it('clicking Next advances to step 2', async () => {
      useGameImportWizardStore.setState({
        uploadedPdf: { pdfDocumentId: 'pdf-123', id: 'pdf-123', fileName: 'test.pdf' },
      });

      renderWithQuery(<AdminGameImportWizardClient />);

      await userEvent.click(screen.getByRole('button', { name: /avanti/i }));

      await waitFor(() => {
        expect(useGameImportWizardStore.getState().currentStep).toBe(2);
      });
    });

    it('Back button goes back from step 2 to step 1', async () => {
      useGameImportWizardStore.setState({
        currentStep: 2,
        uploadedPdf: { pdfDocumentId: 'pdf-123', id: 'pdf-123', fileName: 'test.pdf' },
      });

      renderWithQuery(<AdminGameImportWizardClient />);

      await userEvent.click(screen.getByRole('button', { name: /indietro/i }));

      await waitFor(() => {
        expect(useGameImportWizardStore.getState().currentStep).toBe(1);
      });
    });

    it('Nav buttons hidden on step 4 (saga manages itself)', () => {
      useGameImportWizardStore.setState({ currentStep: 4 });

      renderWithQuery(<AdminGameImportWizardClient />);

      expect(screen.queryByRole('button', { name: /avanti/i })).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: /indietro/i })).not.toBeInTheDocument();
    });

    it('On step 5 shows "Importa un altro gioco" button instead of nav', () => {
      useGameImportWizardStore.setState({ currentStep: 5 });

      renderWithQuery(<AdminGameImportWizardClient />);

      expect(screen.queryByRole('button', { name: /avanti/i })).not.toBeInTheDocument();
      expect(screen.getByRole('button', { name: /importa un altro gioco/i })).toBeInTheDocument();
    });

    it('Step 3 Next button shows "Crea gioco →"', () => {
      useGameImportWizardStore.setState({
        currentStep: 3,
        reviewedMetadata: { title: 'Test Game' },
        uploadedPdf: { pdfDocumentId: 'pdf-123', id: 'pdf-123', fileName: 'test.pdf' },
      });

      renderWithQuery(<AdminGameImportWizardClient />);

      expect(screen.getByRole('button', { name: /crea gioco/i })).toBeInTheDocument();
    });
  });

  describe('Progress Bar', () => {
    it('shows 25% on step 2 (1/4 steps completed)', () => {
      useGameImportWizardStore.setState({ currentStep: 2 });

      renderWithQuery(<AdminGameImportWizardClient />);

      expect(screen.getByText('Step 2 di 5')).toBeInTheDocument();
    });

    it('shows 100% on step 5', () => {
      useGameImportWizardStore.setState({ currentStep: 5 });

      renderWithQuery(<AdminGameImportWizardClient />);

      expect(screen.getByText('100%')).toBeInTheDocument();
      const bar = screen.getByRole('progressbar');
      expect(bar).toHaveAttribute('aria-valuenow', '100');
    });
  });

  describe('Step Content', () => {
    it('renders Step2 on step 2', () => {
      useGameImportWizardStore.setState({ currentStep: 2 });
      renderWithQuery(<AdminGameImportWizardClient />);
      expect(screen.getByTestId('step2-metadata-review')).toBeInTheDocument();
    });

    it('renders Step3 on step 3', () => {
      useGameImportWizardStore.setState({ currentStep: 3 });
      renderWithQuery(<AdminGameImportWizardClient />);
      expect(screen.getByTestId('step3-preview-confirm')).toBeInTheDocument();
    });

    it('renders Step4 on step 4', () => {
      useGameImportWizardStore.setState({ currentStep: 4 });
      renderWithQuery(<AdminGameImportWizardClient />);
      expect(screen.getByTestId('step4-creation-progress')).toBeInTheDocument();
    });

    it('renders Step5 on step 5', () => {
      useGameImportWizardStore.setState({ currentStep: 5 });
      renderWithQuery(<AdminGameImportWizardClient />);
      expect(screen.getByTestId('step5-rag-test')).toBeInTheDocument();
    });
  });

  describe('Error Display', () => {
    it('shows error message on non-step-4 steps', () => {
      useGameImportWizardStore.setState({ currentStep: 2, error: 'Qualcosa è andato storto' });

      renderWithQuery(<AdminGameImportWizardClient />);

      expect(screen.getByText('Qualcosa è andato storto')).toBeInTheDocument();
    });

    it('does not show global error on step 4 (saga handles its own errors)', () => {
      useGameImportWizardStore.setState({ currentStep: 4, error: 'Saga error' });

      renderWithQuery(<AdminGameImportWizardClient />);

      expect(screen.queryByText('Saga error')).not.toBeInTheDocument();
    });
  });

  describe('Reset', () => {
    it('clicking Ricomincia resets wizard to step 1', async () => {
      useGameImportWizardStore.setState({
        currentStep: 3,
        uploadedPdf: { pdfDocumentId: 'pdf-123', id: 'pdf-123', fileName: 'test.pdf' },
      });

      renderWithQuery(<AdminGameImportWizardClient />);

      await userEvent.click(screen.getByRole('button', { name: /ricomincia/i }));

      await waitFor(() => {
        expect(useGameImportWizardStore.getState().currentStep).toBe(1);
        expect(useGameImportWizardStore.getState().uploadedPdf).toBeNull();
      });
    });
  });
});
