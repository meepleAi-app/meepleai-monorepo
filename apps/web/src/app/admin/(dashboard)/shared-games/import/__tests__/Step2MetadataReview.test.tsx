/**
 * Step 2 of the import wizard — #4459: what the extraction returns is the backend's wire format.
 *
 * The client mock below returns `GameMetadataDto` as the API serializes it (camelCase: `year`,
 * `playingTime`, nullable `title`), and the real `useExtractMetadata` hook maps it for the form.
 * The schema itself is covered in `sharedGamesClient.test.ts`.
 */

import type { ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { useGameImportWizardStore } from '@/stores/useGameImportWizardStore';

import { Step2MetadataReview } from '../steps/Step2MetadataReview';

const mockExtractMetadataByPdfId = vi.fn();
vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: {
      extractMetadataByPdfId: (...args: unknown[]) => mockExtractMetadataByPdfId(...args),
    },
  },
}));

vi.mock('../components/CoverImagePicker', () => ({
  CoverImagePicker: () => <div data-testid="cover-image-picker" />,
}));

vi.mock('@/components/ui/data-display/meeple-card', () => ({
  MeepleCard: () => <div data-testid="meeple-card" />,
}));

const emptyExtraction = {
  title: null,
  year: null,
  minPlayers: null,
  maxPlayers: null,
  playingTime: null,
  minAge: null,
  description: null,
  publishers: null,
  designers: null,
  categories: null,
  mechanics: null,
  confidenceScore: 0,
};

function renderStep2() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
  return render(<Step2MetadataReview />, { wrapper });
}

beforeEach(() => {
  vi.clearAllMocks();
  useGameImportWizardStore.getState().reset();
  useGameImportWizardStore.setState({
    importTitle: 'Catan',
    uploadedPdf: { pdfDocumentId: 'pdf-1', fileName: 'rules.pdf', id: 'pdf-1' },
  });
});

describe('Step2MetadataReview (#4459)', () => {
  it('shows the form with the step-1 title when nothing was extracted', async () => {
    mockExtractMetadataByPdfId.mockResolvedValue(emptyExtraction);

    renderStep2();

    expect(await screen.findByText('Nessun dato estratto')).toBeInTheDocument();
    expect(screen.getByLabelText(/^Titolo/)).toHaveValue('Catan');
    expect(screen.queryByText(/Confidenza estrazione/)).not.toBeInTheDocument();
    expect(screen.queryByText('Estrazione non disponibile')).not.toBeInTheDocument();
  });

  it('fills year and playing time from the names the backend sends', async () => {
    mockExtractMetadataByPdfId.mockResolvedValue({
      ...emptyExtraction,
      title: 'Wingspan',
      year: 2019,
      playingTime: 70,
      confidenceScore: 0.82,
    });

    renderStep2();

    await waitFor(() => expect(screen.getByLabelText('Anno di pubblicazione')).toHaveValue(2019));
    expect(screen.getByLabelText('Durata (min)')).toHaveValue(70);
    // The title typed at step 1 still wins over the extracted one (#4401).
    expect(screen.getByLabelText(/^Titolo/)).toHaveValue('Catan');
    expect(screen.queryByText('Nessun dato estratto')).not.toBeInTheDocument();
  });

  it('keeps the technical error out of the page and prefills the step-1 title', async () => {
    mockExtractMetadataByPdfId.mockRejectedValue(
      new Error('Schema validation failed: Response validation failed for /api/v1/admin/…')
    );

    renderStep2();

    expect(await screen.findByText('Estrazione non disponibile')).toBeInTheDocument();
    expect(screen.queryByText(/Schema validation failed/)).not.toBeInTheDocument();
    expect(screen.getByLabelText(/^Titolo/)).toHaveValue('Catan');
  });
});
