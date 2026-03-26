/**
 * Step2MetadataExtraction Component Tests - Issue #4163
 *
 * Test coverage:
 * - Loading state during AI extraction
 * - Extracted metadata display
 * - Confidence badge color coding
 * - Editable fields and manual correction
 * - Store integration
 * - Error handling
 *
 * Target: ≥85% coverage
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { Step2MetadataExtraction } from '@/app/admin/(dashboard)/shared-games/import/steps/Step2MetadataExtraction';
import type { ExtractedMetadata } from '@/stores/useGameImportWizardStore';

// Mock useGameImportWizardStore
const mockSetExtractedMetadata = vi.fn();
const mockWizardStore = {
  uploadedPdf: { id: 'doc-123', fileName: 'test.pdf' },
  extractedMetadata: null,
  setExtractedMetadata: mockSetExtractedMetadata,
};

vi.mock('@/stores/useGameImportWizardStore', () => ({
  useGameImportWizardStore: vi.fn(() => mockWizardStore),
}));

// Mock useExtractMetadata hook
const mockMutate = vi.fn();
const mockUseExtractMetadata = {
  mutate: mockMutate,
  isPending: false,
  error: null,
};

vi.mock('@/hooks/queries/useExtractMetadata', () => ({
  useExtractMetadata: vi.fn(() => mockUseExtractMetadata),
}));

// Test data
const mockExtractedMetadata: ExtractedMetadata = {
  title: 'Catan',
  year: 1995,
  minPlayers: 3,
  maxPlayers: 4,
  playingTime: 60,
  minAge: 10,
  description: 'A game about settling an island',
  confidenceScore: 0.85, // High confidence (green badge) — 0.0-1.0 range
};

const mockLowConfidenceMetadata: ExtractedMetadata = {
  ...mockExtractedMetadata,
  confidenceScore: 0.45, // Low confidence (red badge) — 0.0-1.0 range
};

beforeEach(() => {
  vi.clearAllMocks();
  mockWizardStore.extractedMetadata = null;
  mockWizardStore.uploadedPdf = { id: 'doc-123', fileName: 'test.pdf' };
  mockUseExtractMetadata.isPending = false;
  mockUseExtractMetadata.error = null;
});

describe('Step2MetadataExtraction', () => {
  describe('Loading State', () => {
    it('shows loading skeleton during AI extraction', () => {
      mockUseExtractMetadata.isPending = true;

      render(<Step2MetadataExtraction />);

      expect(screen.getByText('Extracting Metadata...')).toBeInTheDocument();
      expect(screen.getByText(/ai is analyzing the pdf/i)).toBeInTheDocument();
    });

    it('displays loading spinner during extraction', () => {
      mockUseExtractMetadata.isPending = true;

      const { container } = render(<Step2MetadataExtraction />);

      // Check for spinner (Loader2 icon with animate-spin class)
      const spinner = container.querySelector('.animate-spin');
      expect(spinner).toBeInTheDocument();
    });
  });

  describe('Auto-trigger Extraction', () => {
    it('triggers extraction on mount when no metadata exists', () => {
      mockWizardStore.extractedMetadata = null;

      render(<Step2MetadataExtraction />);

      expect(mockMutate).toHaveBeenCalledWith(
        { documentId: 'doc-123' },
        expect.objectContaining({
          onSuccess: expect.any(Function),
        })
      );
    });

    it('does not trigger extraction if metadata already exists', () => {
      mockWizardStore.extractedMetadata = mockExtractedMetadata;

      render(<Step2MetadataExtraction />);

      expect(mockMutate).not.toHaveBeenCalled();
    });

    it('does not trigger extraction if no uploaded PDF', () => {
      mockWizardStore.uploadedPdf = null;

      render(<Step2MetadataExtraction />);

      expect(mockMutate).not.toHaveBeenCalled();
    });
  });

  describe('Metadata Display', () => {
    beforeEach(() => {
      mockWizardStore.extractedMetadata = mockExtractedMetadata;
    });

    it('displays all extracted metadata fields', () => {
      render(<Step2MetadataExtraction />);

      expect(screen.getByDisplayValue('Catan')).toBeInTheDocument();
      expect(screen.getByDisplayValue('1995')).toBeInTheDocument();
      expect(screen.getByDisplayValue('3')).toBeInTheDocument(); // minPlayers
      expect(screen.getByDisplayValue('4')).toBeInTheDocument(); // maxPlayers
      expect(screen.getByDisplayValue('60')).toBeInTheDocument();
      expect(screen.getByDisplayValue('10')).toBeInTheDocument();
      expect(screen.getByDisplayValue('A game about settling an island')).toBeInTheDocument();
    });

    it('shows field labels correctly', () => {
      render(<Step2MetadataExtraction />);

      expect(screen.getByText('Game Title')).toBeInTheDocument();
      expect(screen.getByText('Publication Year')).toBeInTheDocument();
      expect(screen.getByText('Minimum Age')).toBeInTheDocument();
      expect(screen.getByText('Min Players')).toBeInTheDocument();
      expect(screen.getByText('Max Players')).toBeInTheDocument();
      expect(screen.getByText('Playing Time (minutes)')).toBeInTheDocument();
      expect(screen.getByText('Description')).toBeInTheDocument();
    });

    it('marks title as required field', () => {
      render(<Step2MetadataExtraction />);

      expect(screen.getByText('Game Title')).toBeInTheDocument();
      // Required asterisk
      expect(screen.getByText('*')).toBeInTheDocument();
    });
  });

  describe('Confidence Badge', () => {
    it('displays high confidence badge (green) for ≥80%', () => {
      mockWizardStore.extractedMetadata = { ...mockExtractedMetadata, confidenceScore: 0.85 };

      render(<Step2MetadataExtraction />);

      expect(screen.getByTestId('confidence-badge-high')).toBeInTheDocument();
      expect(screen.getByText(/alta confidenza/i)).toBeInTheDocument();
      expect(screen.getByText(/85%/)).toBeInTheDocument();
    });

    it('displays medium confidence badge (yellow) for 50-79%', () => {
      mockWizardStore.extractedMetadata = { ...mockExtractedMetadata, confidenceScore: 0.65 };

      render(<Step2MetadataExtraction />);

      expect(screen.getByTestId('confidence-badge-medium')).toBeInTheDocument();
      expect(screen.getByText(/media confidenza/i)).toBeInTheDocument();
      expect(screen.getByText(/65%/)).toBeInTheDocument();
    });

    it('displays low confidence badge (red) for <50%', () => {
      mockWizardStore.extractedMetadata = mockLowConfidenceMetadata;

      render(<Step2MetadataExtraction />);

      expect(screen.getByTestId('confidence-badge-low')).toBeInTheDocument();
      expect(screen.getByText(/bassa confidenza/i)).toBeInTheDocument();
      // Multiple occurrences: badge + warning alert
      expect(screen.getAllByText(/45%/).length).toBeGreaterThan(0);
    });

    it('shows warning alert for low confidence', () => {
      mockWizardStore.extractedMetadata = mockLowConfidenceMetadata;

      render(<Step2MetadataExtraction />);

      expect(screen.getByText('Low Confidence Extraction')).toBeInTheDocument();
      expect(screen.getByText(/please review all fields carefully/i)).toBeInTheDocument();
    });
  });

  describe('Editable Fields', () => {
    beforeEach(() => {
      mockWizardStore.extractedMetadata = mockExtractedMetadata;
    });

    it('allows editing title field', async () => {
      const user = userEvent.setup();
      render(<Step2MetadataExtraction />);

      const titleInput = screen.getByLabelText(/game title/i) as HTMLInputElement;
      await user.clear(titleInput);
      await user.type(titleInput, 'Settlers of Catan');

      expect(titleInput.value).toBe('Settlers of Catan');
    });

    it('allows editing year field', async () => {
      const user = userEvent.setup();
      render(<Step2MetadataExtraction />);

      const yearInput = screen.getByLabelText(/publication year/i) as HTMLInputElement;
      await user.clear(yearInput);
      await user.type(yearInput, '1996');

      expect(yearInput.value).toBe('1996');
    });

    it('allows editing player count fields', async () => {
      const user = userEvent.setup();
      render(<Step2MetadataExtraction />);

      const minPlayersInput = screen.getByLabelText(/min players/i) as HTMLInputElement;
      const maxPlayersInput = screen.getByLabelText(/max players/i) as HTMLInputElement;

      await user.clear(minPlayersInput);
      await user.type(minPlayersInput, '2');

      await user.clear(maxPlayersInput);
      await user.type(maxPlayersInput, '6');

      expect(minPlayersInput.value).toBe('2');
      expect(maxPlayersInput.value).toBe('6');
    });

    it('allows editing description field', async () => {
      const user = userEvent.setup();
      render(<Step2MetadataExtraction />);

      const descriptionTextarea = screen.getByLabelText(/description/i) as HTMLTextAreaElement;
      await user.clear(descriptionTextarea);
      await user.type(descriptionTextarea, 'New description');

      expect(descriptionTextarea.value).toBe('New description');
    });

    it('shows save button when fields are edited', async () => {
      const user = userEvent.setup();
      render(<Step2MetadataExtraction />);

      // Initially no save button
      expect(screen.queryByRole('button', { name: /save changes/i })).not.toBeInTheDocument();

      // Edit a field
      const titleInput = screen.getByLabelText(/game title/i) as HTMLInputElement;
      await user.type(titleInput, ' Extended');

      // Save button appears
      await waitFor(() => {
        expect(screen.getByRole('button', { name: /save changes/i })).toBeInTheDocument();
      });
    });

    it('calls setExtractedMetadata when save button clicked', async () => {
      const user = userEvent.setup();
      render(<Step2MetadataExtraction />);

      // Edit a field
      const titleInput = screen.getByLabelText(/game title/i) as HTMLInputElement;
      await user.clear(titleInput);
      await user.type(titleInput, 'Modified Title');

      // Click save button
      const saveButton = await screen.findByRole('button', { name: /save changes/i });
      await user.click(saveButton);

      await waitFor(() => {
        expect(mockSetExtractedMetadata).toHaveBeenCalledWith(
          expect.objectContaining({
            title: 'Modified Title',
          })
        );
      });
    });

    it('calls onComplete callback when save clicked', async () => {
      const user = userEvent.setup();
      const onComplete = vi.fn();

      render(<Step2MetadataExtraction onComplete={onComplete} />);

      // Edit a field
      const titleInput = screen.getByLabelText(/game title/i) as HTMLInputElement;
      await user.type(titleInput, ' Extended');

      // Click save
      const saveButton = await screen.findByRole('button', { name: /save changes/i });
      await user.click(saveButton);

      await waitFor(() => {
        expect(onComplete).toHaveBeenCalledWith(
          expect.objectContaining({
            title: expect.stringContaining('Catan Extended'),
          })
        );
      });
    });
  });

  describe('Error Handling', () => {
    it('displays extraction error message', () => {
      mockUseExtractMetadata.error = new Error('AI service unavailable');

      render(<Step2MetadataExtraction />);

      expect(screen.getByText('Extraction Failed')).toBeInTheDocument();
      expect(screen.getByText('AI service unavailable')).toBeInTheDocument();
    });

    it('shows retry button on extraction failure', () => {
      mockUseExtractMetadata.error = new Error('Extraction failed');

      render(<Step2MetadataExtraction />);

      expect(screen.getByRole('button', { name: /retry extraction/i })).toBeInTheDocument();
    });

    it('retries extraction when retry button clicked', async () => {
      const user = userEvent.setup();
      mockUseExtractMetadata.error = new Error('Extraction failed');

      render(<Step2MetadataExtraction />);

      const retryButton = screen.getByRole('button', { name: /retry extraction/i });
      await user.click(retryButton);

      expect(mockMutate).toHaveBeenCalledWith({ documentId: 'doc-123' });
    });
  });

  describe('Store Integration', () => {
    it('calls setExtractedMetadata on successful extraction', async () => {
      // Mock successful extraction
      mockMutate.mockImplementation((input, options) => {
        options?.onSuccess?.(mockExtractedMetadata);
      });

      render(<Step2MetadataExtraction />);

      await waitFor(() => {
        expect(mockSetExtractedMetadata).toHaveBeenCalledWith(mockExtractedMetadata);
      });
    });

    it('calls onComplete callback on successful extraction', async () => {
      const onComplete = vi.fn();

      mockMutate.mockImplementation((input, options) => {
        options?.onSuccess?.(mockExtractedMetadata);
      });

      render(<Step2MetadataExtraction onComplete={onComplete} />);

      await waitFor(() => {
        expect(onComplete).toHaveBeenCalledWith(mockExtractedMetadata);
      });
    });
  });

  describe('Form Validation', () => {
    beforeEach(() => {
      mockWizardStore.extractedMetadata = mockExtractedMetadata;
    });

    it('validates number inputs min/max constraints', () => {
      render(<Step2MetadataExtraction />);

      const yearInput = screen.getByLabelText(/publication year/i) as HTMLInputElement;
      const minAgeInput = screen.getByLabelText(/minimum age/i) as HTMLInputElement;

      // Year constraints
      expect(yearInput).toHaveAttribute('min', '1900');
      expect(yearInput).toHaveAttribute('max', '2100');

      // Age constraints
      expect(minAgeInput).toHaveAttribute('min', '0');
      expect(minAgeInput).toHaveAttribute('max', '99');
    });

    it('marks title as required field', () => {
      render(<Step2MetadataExtraction />);

      const titleInput = screen.getByLabelText(/game title/i) as HTMLInputElement;
      expect(titleInput).toBeRequired();
    });
  });

  describe('UI States', () => {
    it('does not show save button initially when no edits', () => {
      mockWizardStore.extractedMetadata = mockExtractedMetadata;

      render(<Step2MetadataExtraction />);

      expect(screen.queryByRole('button', { name: /save changes/i })).not.toBeInTheDocument();
    });

    it('shows unsaved changes warning when fields edited', async () => {
      const user = userEvent.setup();
      mockWizardStore.extractedMetadata = mockExtractedMetadata;

      render(<Step2MetadataExtraction />);

      const titleInput = screen.getByLabelText(/game title/i) as HTMLInputElement;
      await user.type(titleInput, ' Extended');

      await waitFor(() => {
        expect(screen.getByText(/you have unsaved changes/i)).toBeInTheDocument();
      });
    });
  });
});
