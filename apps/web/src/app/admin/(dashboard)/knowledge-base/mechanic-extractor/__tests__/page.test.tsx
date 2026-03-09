/**
 * Mechanic Extractor Page Tests
 * Tests for the Variant C copyright-compliant mechanic extraction workflow.
 */

import { screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

const mockGetAll = vi.hoisted(() => vi.fn());
const mockGetAllPdfs = vi.hoisted(() => vi.fn());
const mockGetMechanicDraft = vi.hoisted(() => vi.fn());
const mockSaveMechanicDraft = vi.hoisted(() => vi.fn());
const mockAiAssistMechanicDraft = vi.hoisted(() => vi.fn());
const mockAcceptMechanicDraft = vi.hoisted(() => vi.fn());
const mockFinalizeMechanicAnalysis = vi.hoisted(() => vi.fn());
const mockSetNavConfig = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api/clients/adminClient', () => ({
  createAdminClient: () => ({
    getAllPdfs: mockGetAllPdfs,
    getMechanicDraft: mockGetMechanicDraft,
    saveMechanicDraft: mockSaveMechanicDraft,
    aiAssistMechanicDraft: mockAiAssistMechanicDraft,
    acceptMechanicDraft: mockAcceptMechanicDraft,
    finalizeMechanicAnalysis: mockFinalizeMechanicAnalysis,
  }),
}));

vi.mock('@/lib/api/clients/sharedGamesClient', () => ({
  createSharedGamesClient: () => ({
    getAll: mockGetAll,
  }),
}));

vi.mock('@/lib/api/core/httpClient', () => ({
  HttpClient: vi.fn(() => ({})),
}));

vi.mock('@/hooks/useSetNavConfig', () => ({
  useSetNavConfig: () => mockSetNavConfig,
}));

import MechanicExtractorPage from '../page';

describe('MechanicExtractorPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetAll.mockResolvedValue({ items: [], totalCount: 0 });
    mockGetAllPdfs.mockResolvedValue({ items: [], totalCount: 0 });
    mockGetMechanicDraft.mockResolvedValue(null);
  });

  describe('initial render (no game selected)', () => {
    it('renders page title', () => {
      renderWithQuery(<MechanicExtractorPage />);
      expect(screen.getByText('Mechanic Extractor')).toBeInTheDocument();
    });

    it('renders page description', () => {
      renderWithQuery(<MechanicExtractorPage />);
      expect(screen.getByText(/copyright-compliant human\+AI workflow/)).toBeInTheDocument();
    });

    it('renders Variant C copyright badge', () => {
      renderWithQuery(<MechanicExtractorPage />);
      expect(screen.getByText(/Variant C: AI reads notes only, never the PDF/)).toBeInTheDocument();
    });

    it('renders game selection label', () => {
      renderWithQuery(<MechanicExtractorPage />);
      expect(screen.getByText('Select Game')).toBeInTheDocument();
    });

    it('renders PDF selection label', () => {
      renderWithQuery(<MechanicExtractorPage />);
      expect(screen.getByText('Select PDF')).toBeInTheDocument();
    });

    it('renders Save button (disabled without selection)', () => {
      renderWithQuery(<MechanicExtractorPage />);
      const saveButton = screen.getByText('Save').closest('button');
      expect(saveButton).toBeInTheDocument();
      expect(saveButton).toBeDisabled();
    });

    it('does NOT render section tabs before game+PDF selection', () => {
      renderWithQuery(<MechanicExtractorPage />);
      // Section tabs are behind the selectedGameId && selectedPdfId guard
      expect(screen.queryByText('Summary')).not.toBeInTheDocument();
      expect(screen.queryByText('Mechanics')).not.toBeInTheDocument();
    });

    it('does NOT render editor before game+PDF selection', () => {
      renderWithQuery(<MechanicExtractorPage />);
      expect(screen.queryByText('Your Notes')).not.toBeInTheDocument();
    });

    it('sets nav config on mount', () => {
      renderWithQuery(<MechanicExtractorPage />);
      expect(mockSetNavConfig).toHaveBeenCalledWith(
        expect.objectContaining({
          miniNav: expect.arrayContaining([
            expect.objectContaining({
              id: 'extractor',
              label: 'Mechanic Extractor',
            }),
          ]),
        })
      );
    });
  });
});
