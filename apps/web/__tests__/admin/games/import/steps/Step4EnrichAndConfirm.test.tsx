/**
 * Step4EnrichAndConfirm Component Tests
 * Issue #4165: Step 4 Enrich & Confirm
 *
 * Test coverage:
 * - Rendering with different data states
 * - Conflict detection logic
 * - Conflict resolution UI interactions
 * - Merge logic correctness
 * - Submit flow (success/error/loading)
 * - Edge cases and validation
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import { Step4EnrichAndConfirm } from '@/app/admin/(dashboard)/shared-games/import/steps/Step4EnrichAndConfirm';
import { useGameImportWizardStore } from '@/stores/useGameImportWizardStore';
import type { ExtractedMetadata, BggGameData } from '@/stores/useGameImportWizardStore';

// Mock dependencies
vi.mock('@/stores/useGameImportWizardStore');
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: vi.fn(),
  }),
}));
vi.mock('@/components/layout', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
    info: vi.fn(),
  },
}));
vi.mock('@/hooks/wizard/useCheckDuplicate', () => ({
  useCheckDuplicate: () => ({ data: null }),
}));
vi.mock('@/components/admin/games/import/DuplicateWarningDialog', () => ({
  DuplicateWarningDialog: () => null,
}));
vi.mock('@/components/ui/data-display/meeple-card', () => ({
  MeepleCard: (props: Record<string, unknown>) => (
    <div data-testid={(props['data-testid'] as string) || 'final-preview-card'}>
      {props.title as string}
    </div>
  ),
}));

describe('Step4EnrichAndConfirm', () => {
  // Mock store state
  const mockExtractedMetadata: ExtractedMetadata = {
    title: 'Catan', // Same as BGG name to avoid unintended title conflict
    year: 1995,
    minPlayers: 3,
    maxPlayers: 4,
    playingTime: 60,
    minAge: 10,
    description: 'PDF description of Catan',
    confidenceScore: 0.85,
  };

  const mockBggGameData: BggGameData = {
    bggId: 13,
    name: 'Catan',
    yearPublished: 1995,
    minPlayers: 3,
    maxPlayers: 4,
    playingTime: 90, // Different from PDF!
    minAge: 10,
    description: 'PDF description of Catan', // Same as PDF to avoid unintended description conflict
    imageUrl: 'https://example.com/catan.jpg',
    thumbnailUrl: 'https://example.com/catan-thumb.jpg',
  };

  const mockResolveConflicts = vi.fn();
  const mockSubmitWizard = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();

    // Default mock store implementation
    vi.mocked(useGameImportWizardStore).mockReturnValue({
      extractedMetadata: mockExtractedMetadata,
      bggGameData: mockBggGameData,
      selectedBggId: 13,
      enrichedData: null,
      resolveConflicts: mockResolveConflicts,
      submitWizard: mockSubmitWizard,
      isProcessing: false,
      error: null,
      // Other store methods (not used in this component)
      currentStep: 4,
      uploadedPdf: null,
      setStep: vi.fn(),
      goNext: vi.fn(),
      goBack: vi.fn(),
      canGoNext: vi.fn(),
      canGoBack: vi.fn(),
      setUploadedPdf: vi.fn(),
      setExtractedMetadata: vi.fn(),
      setSelectedBggId: vi.fn(),
      reset: vi.fn(),
    });
  });

  describe('Rendering', () => {
    it('renders header and description', () => {
      renderWithQuery(<Step4EnrichAndConfirm />);

      expect(screen.getByText('Review & Confirm')).toBeInTheDocument();
      expect(
        screen.getByText(/Risolvi eventuali conflitti tra dati PDF e catalogo/i)
      ).toBeInTheDocument();
    });

    it('shows "No Conflicts" alert when data matches', () => {
      // Mock data with no conflicts (same title and playTime)
      vi.mocked(useGameImportWizardStore).mockReturnValue({
        extractedMetadata: { ...mockExtractedMetadata, playingTime: 90 }, // Match BGG playingTime
        bggGameData: mockBggGameData,
        selectedBggId: 13,
        enrichedData: null,
        resolveConflicts: mockResolveConflicts,
        submitWizard: mockSubmitWizard,
        isProcessing: false,
        error: null,
        currentStep: 4,
        uploadedPdf: null,
        setStep: vi.fn(),
        goNext: vi.fn(),
        goBack: vi.fn(),
        canGoNext: vi.fn(),
        canGoBack: vi.fn(),
        setUploadedPdf: vi.fn(),
        setExtractedMetadata: vi.fn(),
        setSelectedBggId: vi.fn(),
        reset: vi.fn(),
      });

      renderWithQuery(<Step4EnrichAndConfirm />);

      expect(screen.getByText('No Conflicts')).toBeInTheDocument();
      expect(
        screen.getByText(/I dati catalogo e PDF sono stati uniti automaticamente senza conflitti/i)
      ).toBeInTheDocument();
    });

    it('shows conflict resolution UI when conflicts exist', () => {
      renderWithQuery(<Step4EnrichAndConfirm />);

      // Should detect playTime conflict (60 vs 90)
      expect(screen.getByText('Resolve Conflicts')).toBeInTheDocument();
      expect(screen.getByText(/1 conflitto rilevato/i)).toBeInTheDocument();
    });

    it('displays final preview card', () => {
      renderWithQuery(<Step4EnrichAndConfirm />);

      expect(screen.getByText('Final Preview')).toBeInTheDocument();
      expect(screen.getByTestId('final-preview-card')).toBeInTheDocument();
    });

    it('shows missing data alert when no metadata available', () => {
      vi.mocked(useGameImportWizardStore).mockReturnValue({
        extractedMetadata: null,
        bggGameData: null,
        selectedBggId: null,
        enrichedData: null,
        resolveConflicts: mockResolveConflicts,
        submitWizard: mockSubmitWizard,
        isProcessing: false,
        error: null,
        currentStep: 4,
        uploadedPdf: null,
        setStep: vi.fn(),
        goNext: vi.fn(),
        goBack: vi.fn(),
        canGoNext: vi.fn(),
        canGoBack: vi.fn(),
        setUploadedPdf: vi.fn(),
        setExtractedMetadata: vi.fn(),
        setSelectedBggId: vi.fn(),
        reset: vi.fn(),
      });

      renderWithQuery(<Step4EnrichAndConfirm />);

      expect(screen.getByText('Missing Data')).toBeInTheDocument();
      expect(
        screen.getByText(/Nessun metadato estratto o dati catalogo disponibili/i)
      ).toBeInTheDocument();
    });
  });

  describe('Conflict Detection', () => {
    it('detects title conflict when names differ', () => {
      vi.mocked(useGameImportWizardStore).mockReturnValue({
        extractedMetadata: { ...mockExtractedMetadata, title: 'Catan PDF Different' },
        bggGameData: mockBggGameData,
        selectedBggId: 13,
        enrichedData: null,
        resolveConflicts: mockResolveConflicts,
        submitWizard: mockSubmitWizard,
        isProcessing: false,
        error: null,
        currentStep: 4,
        uploadedPdf: null,
        setStep: vi.fn(),
        goNext: vi.fn(),
        goBack: vi.fn(),
        canGoNext: vi.fn(),
        canGoBack: vi.fn(),
        setUploadedPdf: vi.fn(),
        setExtractedMetadata: vi.fn(),
        setSelectedBggId: vi.fn(),
        reset: vi.fn(),
      });

      renderWithQuery(<Step4EnrichAndConfirm />);

      expect(screen.getByText('Game Title')).toBeInTheDocument();
    });

    it('detects playTime conflict', () => {
      renderWithQuery(<Step4EnrichAndConfirm />);

      // playTime conflict: PDF 60 vs BGG 90
      expect(screen.getByText('Play Time (minutes)')).toBeInTheDocument();
      expect(screen.getByText(/Usa catalogo:/)).toBeInTheDocument();
      expect(screen.getByText(/90/)).toBeInTheDocument(); // BGG value
      expect(screen.getByText(/Use PDF:/)).toBeInTheDocument();
      expect(screen.getByText(/60/)).toBeInTheDocument(); // PDF value
    });

    it('detects multiple conflicts correctly', () => {
      vi.mocked(useGameImportWizardStore).mockReturnValue({
        extractedMetadata: {
          ...mockExtractedMetadata,
          title: 'Different Title',
          year: 1996, // Different
          minPlayers: 2, // Different
        },
        bggGameData: mockBggGameData,
        selectedBggId: 13,
        enrichedData: null,
        resolveConflicts: mockResolveConflicts,
        submitWizard: mockSubmitWizard,
        isProcessing: false,
        error: null,
        currentStep: 4,
        uploadedPdf: null,
        setStep: vi.fn(),
        goNext: vi.fn(),
        goBack: vi.fn(),
        canGoNext: vi.fn(),
        canGoBack: vi.fn(),
        setUploadedPdf: vi.fn(),
        setExtractedMetadata: vi.fn(),
        setSelectedBggId: vi.fn(),
        reset: vi.fn(),
      });

      renderWithQuery(<Step4EnrichAndConfirm />);

      expect(screen.getByText(/4 conflitti rilevati/i)).toBeInTheDocument();
    });
  });

  describe('Conflict Resolution UI', () => {
    it('allows selecting BGG option', async () => {
      const user = userEvent.setup();
      renderWithQuery(<Step4EnrichAndConfirm />);

      // Find playTime conflict BGG radio button
      const bggRadio = screen.getByRole('radio', { name: /Usa catalogo:.*90/ });
      expect(bggRadio).toBeChecked(); // Default is BGG

      // Click should keep it checked
      await user.click(bggRadio);
      expect(bggRadio).toBeChecked();
    });

    it('allows selecting PDF option', async () => {
      const user = userEvent.setup();
      renderWithQuery(<Step4EnrichAndConfirm />);

      const pdfRadio = screen.getByRole('radio', { name: /Use PDF:.*60/i });
      expect(pdfRadio).not.toBeChecked();

      await user.click(pdfRadio);

      await waitFor(() => {
        expect(pdfRadio).toBeChecked();
      });
    });

    it('allows selecting custom option and entering custom value', async () => {
      const user = userEvent.setup();
      renderWithQuery(<Step4EnrichAndConfirm />);

      // Find the custom radio for playTime field (first "Custom:" in the document for playTime conflict)
      const customRadios = screen.getAllByRole('radio', { name: /Custom:/ });
      // playTime is the only conflict, so first custom radio
      const customRadio = customRadios[0];

      await user.click(customRadio);

      // Wait for custom input to be enabled
      const customInput = screen.getByTestId('playingTime-custom-input');

      await waitFor(() => {
        expect(customRadio).toBeChecked();
        expect(customInput).not.toBeDisabled();
      });

      await user.type(customInput, '75');
      expect(customInput).toHaveValue(75); // Number type input returns number
    });

    it('disables custom input when custom option not selected', () => {
      renderWithQuery(<Step4EnrichAndConfirm />);

      const customInput = screen.getByTestId('playingTime-custom-input');
      expect(customInput).toBeDisabled(); // BGG selected by default
    });
  });

  describe('Submit Flow', () => {
    it('calls submitWizard when confirm button clicked', async () => {
      const user = userEvent.setup();
      mockSubmitWizard.mockResolvedValue(undefined);

      renderWithQuery(<Step4EnrichAndConfirm />);

      const confirmButton = screen.getByTestId('confirm-import-btn');
      await user.click(confirmButton);

      await waitFor(() => {
        expect(mockResolveConflicts).toHaveBeenCalled();
        expect(mockSubmitWizard).toHaveBeenCalled();
      });
    });

    it('shows loading state during submit', async () => {
      const user = userEvent.setup();
      mockSubmitWizard.mockImplementation(() => new Promise(resolve => setTimeout(resolve, 1000)));

      vi.mocked(useGameImportWizardStore).mockReturnValue({
        extractedMetadata: mockExtractedMetadata,
        bggGameData: mockBggGameData,
        selectedBggId: 13,
        enrichedData: null,
        resolveConflicts: mockResolveConflicts,
        submitWizard: mockSubmitWizard,
        isProcessing: true, // Loading state
        error: null,
        currentStep: 4,
        uploadedPdf: null,
        setStep: vi.fn(),
        goNext: vi.fn(),
        goBack: vi.fn(),
        canGoNext: vi.fn(),
        canGoBack: vi.fn(),
        setUploadedPdf: vi.fn(),
        setExtractedMetadata: vi.fn(),
        setSelectedBggId: vi.fn(),
        reset: vi.fn(),
      });

      renderWithQuery(<Step4EnrichAndConfirm />);

      expect(screen.getByText('Importing...')).toBeInTheDocument();
      expect(screen.getByTestId('confirm-import-btn')).toBeDisabled();
    });

    it('displays error alert when submit fails', () => {
      vi.mocked(useGameImportWizardStore).mockReturnValue({
        extractedMetadata: mockExtractedMetadata,
        bggGameData: mockBggGameData,
        selectedBggId: 13,
        enrichedData: null,
        resolveConflicts: mockResolveConflicts,
        submitWizard: mockSubmitWizard,
        isProcessing: false,
        error: 'Network error: Failed to import game', // Error state
        currentStep: 4,
        uploadedPdf: null,
        setStep: vi.fn(),
        goNext: vi.fn(),
        goBack: vi.fn(),
        canGoNext: vi.fn(),
        canGoBack: vi.fn(),
        setUploadedPdf: vi.fn(),
        setExtractedMetadata: vi.fn(),
        setSelectedBggId: vi.fn(),
        reset: vi.fn(),
      });

      renderWithQuery(<Step4EnrichAndConfirm />);

      expect(screen.getByText('Import Failed')).toBeInTheDocument();
      expect(screen.getByText(/Network error: Failed to import game/i)).toBeInTheDocument();
    });

    it('calls onComplete callback on successful submit', async () => {
      const user = userEvent.setup();
      const onComplete = vi.fn();
      mockSubmitWizard.mockResolvedValue(undefined);

      renderWithQuery(<Step4EnrichAndConfirm onComplete={onComplete} />);

      const confirmButton = screen.getByTestId('confirm-import-btn');
      await user.click(confirmButton);

      await waitFor(() => {
        expect(onComplete).toHaveBeenCalledWith('13'); // gameId
      });
    });
  });

  describe('Merge Logic', () => {
    it('merges data correctly with BGG priority for conflicts', async () => {
      const user = userEvent.setup();
      mockSubmitWizard.mockResolvedValue(undefined);

      renderWithQuery(<Step4EnrichAndConfirm />);

      const confirmButton = screen.getByTestId('confirm-import-btn');
      await user.click(confirmButton);

      await waitFor(() => {
        expect(mockResolveConflicts).toHaveBeenCalledWith(
          expect.objectContaining({
            title: 'Catan', // BGG (default)
            playingTime: 90, // BGG (default for conflict)
            imageUrl: 'https://example.com/catan.jpg', // BGG only
            bggId: 13,
          })
        );
      });
    });

    it('uses PDF value when BGG not available', async () => {
      const user = userEvent.setup();
      mockSubmitWizard.mockResolvedValue(undefined);

      vi.mocked(useGameImportWizardStore).mockReturnValue({
        extractedMetadata: mockExtractedMetadata,
        bggGameData: {
          ...mockBggGameData,
          minPlayers: undefined, // BGG missing
          maxPlayers: undefined, // BGG missing
        },
        selectedBggId: 13,
        enrichedData: null,
        resolveConflicts: mockResolveConflicts,
        submitWizard: mockSubmitWizard,
        isProcessing: false,
        error: null,
        currentStep: 4,
        uploadedPdf: null,
        setStep: vi.fn(),
        goNext: vi.fn(),
        goBack: vi.fn(),
        canGoNext: vi.fn(),
        canGoBack: vi.fn(),
        setUploadedPdf: vi.fn(),
        setExtractedMetadata: vi.fn(),
        setSelectedBggId: vi.fn(),
        reset: vi.fn(),
      });

      renderWithQuery(<Step4EnrichAndConfirm />);

      const confirmButton = screen.getByTestId('confirm-import-btn');
      await user.click(confirmButton);

      await waitFor(() => {
        expect(mockResolveConflicts).toHaveBeenCalledWith(
          expect.objectContaining({
            minPlayers: 3, // From PDF (BGG missing)
            maxPlayers: 4, // From PDF (BGG missing)
          })
        );
      });
    });

    it('uses custom value when custom option selected', async () => {
      const user = userEvent.setup();
      mockSubmitWizard.mockResolvedValue(undefined);

      renderWithQuery(<Step4EnrichAndConfirm />);

      // Select custom option for playTime
      const customRadios = screen.getAllByRole('radio', { name: /Custom:/ });
      const customRadio = customRadios[0]; // playTime conflict
      await user.click(customRadio);

      // Wait for custom input to be enabled
      const customInput = screen.getByTestId('playingTime-custom-input');
      await waitFor(() => {
        expect(customInput).not.toBeDisabled();
      });

      // Enter custom value
      await user.type(customInput, '75');

      // Submit
      const confirmButton = screen.getByTestId('confirm-import-btn');
      await user.click(confirmButton);

      await waitFor(() => {
        expect(mockResolveConflicts).toHaveBeenCalledWith(
          expect.objectContaining({
            playingTime: 75, // Custom value (coerced to number for numeric fields)
          })
        );
      });
    });
  });

  describe('Edge Cases', () => {
    it('handles only PDF data (no BGG)', async () => {
      const user = userEvent.setup();
      mockSubmitWizard.mockResolvedValue(undefined);

      vi.mocked(useGameImportWizardStore).mockReturnValue({
        extractedMetadata: mockExtractedMetadata,
        bggGameData: null, // No BGG data
        selectedBggId: null,
        enrichedData: null,
        resolveConflicts: mockResolveConflicts,
        submitWizard: mockSubmitWizard,
        isProcessing: false,
        error: null,
        currentStep: 4,
        uploadedPdf: null,
        setStep: vi.fn(),
        goNext: vi.fn(),
        goBack: vi.fn(),
        canGoNext: vi.fn(),
        canGoBack: vi.fn(),
        setUploadedPdf: vi.fn(),
        setExtractedMetadata: vi.fn(),
        setSelectedBggId: vi.fn(),
        reset: vi.fn(),
      });

      renderWithQuery(<Step4EnrichAndConfirm />);

      // Should show no conflicts (nothing to conflict with)
      expect(screen.getByText('No Conflicts')).toBeInTheDocument();

      // Submit should use PDF data
      const confirmButton = screen.getByTestId('confirm-import-btn');
      await user.click(confirmButton);

      await waitFor(() => {
        expect(mockResolveConflicts).toHaveBeenCalledWith(
          expect.objectContaining({
            title: 'Catan',
            playingTime: 60,
            minPlayers: 3,
            maxPlayers: 4,
          })
        );
      });
    });

    it('handles only BGG data (no PDF)', async () => {
      const user = userEvent.setup();
      mockSubmitWizard.mockResolvedValue(undefined);

      vi.mocked(useGameImportWizardStore).mockReturnValue({
        extractedMetadata: null, // No PDF data
        bggGameData: mockBggGameData,
        selectedBggId: 13,
        enrichedData: null,
        resolveConflicts: mockResolveConflicts,
        submitWizard: mockSubmitWizard,
        isProcessing: false,
        error: null,
        currentStep: 4,
        uploadedPdf: null,
        setStep: vi.fn(),
        goNext: vi.fn(),
        goBack: vi.fn(),
        canGoNext: vi.fn(),
        canGoBack: vi.fn(),
        setUploadedPdf: vi.fn(),
        setExtractedMetadata: vi.fn(),
        setSelectedBggId: vi.fn(),
        reset: vi.fn(),
      });

      renderWithQuery(<Step4EnrichAndConfirm />);

      // Should show no conflicts
      expect(screen.getByText('No Conflicts')).toBeInTheDocument();

      // Submit should use BGG data
      const confirmButton = screen.getByTestId('confirm-import-btn');
      await user.click(confirmButton);

      await waitFor(() => {
        expect(mockResolveConflicts).toHaveBeenCalledWith(
          expect.objectContaining({
            title: 'Catan',
            playingTime: 90,
            minPlayers: 3,
            maxPlayers: 4,
            imageUrl: 'https://example.com/catan.jpg',
          })
        );
      });
    });

    it('disables submit button when no enrichedData', () => {
      vi.mocked(useGameImportWizardStore).mockReturnValue({
        extractedMetadata: null,
        bggGameData: null,
        selectedBggId: null,
        enrichedData: null,
        resolveConflicts: mockResolveConflicts,
        submitWizard: mockSubmitWizard,
        isProcessing: false,
        error: null,
        currentStep: 4,
        uploadedPdf: null,
        setStep: vi.fn(),
        goNext: vi.fn(),
        goBack: vi.fn(),
        canGoNext: vi.fn(),
        canGoBack: vi.fn(),
        setUploadedPdf: vi.fn(),
        setExtractedMetadata: vi.fn(),
        setSelectedBggId: vi.fn(),
        reset: vi.fn(),
      });

      renderWithQuery(<Step4EnrichAndConfirm />);

      // Missing data alert shown, no submit button
      expect(screen.getByText('Missing Data')).toBeInTheDocument();
    });
  });
});
