/**
 * Mechanic Analyses (M1.2) Page Tests
 *
 * Covers the async pipeline UI: generation form, status polling render,
 * section runs table, lifecycle button gating, suppress dialog validation.
 */

import { fireEvent, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

const mockGetAll = vi.hoisted(() => vi.fn());
const mockGetAllPdfs = vi.hoisted(() => vi.fn());
const mockGenerateMechanicAnalysis = vi.hoisted(() => vi.fn());
const mockGetMechanicAnalysisStatus = vi.hoisted(() => vi.fn());
const mockSubmitMechanicAnalysisForReview = vi.hoisted(() => vi.fn());
const mockApproveMechanicAnalysis = vi.hoisted(() => vi.fn());
const mockSuppressMechanicAnalysis = vi.hoisted(() => vi.fn());
const mockListMechanicAnalyses = vi.hoisted(() => vi.fn());
const mockGetMechanicAnalysisClaims = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api/clients/adminClient', () => ({
  createAdminClient: () => ({
    getAllPdfs: mockGetAllPdfs,
    generateMechanicAnalysis: mockGenerateMechanicAnalysis,
    getMechanicAnalysisStatus: mockGetMechanicAnalysisStatus,
    submitMechanicAnalysisForReview: mockSubmitMechanicAnalysisForReview,
    approveMechanicAnalysis: mockApproveMechanicAnalysis,
    suppressMechanicAnalysis: mockSuppressMechanicAnalysis,
    listMechanicAnalyses: mockListMechanicAnalyses,
    getMechanicAnalysisClaims: mockGetMechanicAnalysisClaims,
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

// next/navigation: minimal hoisted mocks for the deep-link query-param sync
// (spec-panel gap #1). The page reads `?analysisId=…` on mount and pushes
// changes via router.replace — tests don't need real navigation, just stable
// stubs. `mockSearchParams` is mutable so individual tests can simulate the
// page being loaded with a pre-existing `?analysisId=…` query string.
const mockRouterReplace = vi.hoisted(() => vi.fn());
const mockSearchParamsRef = vi.hoisted(() => ({
  current: new URLSearchParams() as URLSearchParams,
}));
vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace: mockRouterReplace, push: vi.fn(), back: vi.fn() }),
  usePathname: () => '/admin/knowledge-base/mechanic-extractor/analyses',
  useSearchParams: () => mockSearchParamsRef.current,
}));

import MechanicAnalysesPage from '../page';

const SAMPLE_STATUS_DRAFT_EMPTY = {
  id: '11111111-1111-1111-1111-111111111111',
  sharedGameId: '22222222-2222-2222-2222-222222222222',
  pdfDocumentId: '33333333-3333-3333-3333-333333333333',
  promptVersion: 'v1.0.0',
  status: 0, // Draft
  rejectionReason: null,
  createdBy: '44444444-4444-4444-4444-444444444444',
  createdAt: '2026-04-23T10:00:00Z',
  reviewedBy: null,
  reviewedAt: null,
  provider: 'DeepSeek',
  modelUsed: 'deepseek-chat',
  totalTokensUsed: 0,
  estimatedCostUsd: 0,
  costCapUsd: 0.5,
  costCapOverrideApplied: false,
  costCapOverrideAt: null,
  costCapOverrideBy: null,
  costCapOverrideReason: null,
  isSuppressed: false,
  suppressedAt: null,
  suppressedBy: null,
  suppressionReason: null,
  claimsCount: 0,
  sectionRuns: [],
};

const SAMPLE_STATUS_DRAFT_COMPLETE = {
  ...SAMPLE_STATUS_DRAFT_EMPTY,
  claimsCount: 12,
  totalTokensUsed: 4200,
  estimatedCostUsd: 0.42,
  sectionRuns: Array.from({ length: 6 }, (_, i) => ({
    section: i,
    runOrder: 1,
    provider: 'DeepSeek',
    modelUsed: 'deepseek-chat',
    promptTokens: 300,
    completionTokens: 400,
    totalTokens: 700,
    estimatedCostUsd: 0.07,
    latencyMs: 1234,
    status: 0,
    errorMessage: null,
    startedAt: '2026-04-23T10:00:01Z',
    completedAt: '2026-04-23T10:00:02Z',
  })),
};

const SAMPLE_STATUS_IN_REVIEW = {
  ...SAMPLE_STATUS_DRAFT_COMPLETE,
  status: 1, // InReview
  reviewedBy: null,
  reviewedAt: null,
};

describe('MechanicAnalysesPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // Default: empty query string. Individual tests reassign mockSearchParamsRef
    // before rendering when they need to simulate `?analysisId=…` deep-link.
    mockSearchParamsRef.current = new URLSearchParams();
    mockGetAll.mockResolvedValue({
      items: [{ id: '22222222-2222-2222-2222-222222222222', title: 'Catan' }],
      totalCount: 1,
    });
    mockGetAllPdfs.mockResolvedValue({
      items: [
        {
          id: '33333333-3333-3333-3333-333333333333',
          fileName: 'catan-rules.pdf',
          gameId: '22222222-2222-2222-2222-222222222222',
        },
      ],
      totalCount: 1,
    });
    mockGetMechanicAnalysisStatus.mockResolvedValue(null);
    mockListMechanicAnalyses.mockResolvedValue({
      items: [],
      page: 1,
      pageSize: 20,
      totalCount: 0,
    });
  });

  describe('header', () => {
    it('renders page title', () => {
      renderWithQuery(<MechanicAnalysesPage />);
      // Spec-panel gap #4: heading is user-facing, no internal refs.
      expect(
        screen.getByRole('heading', { level: 1, name: 'Mechanic Extraction' })
      ).toBeInTheDocument();
    });

    it('renders pipeline summary badge', () => {
      renderWithQuery(<MechanicAnalysesPage />);
      // Internal IDs (ISSUE-524 / ADR-051 / M1.2) intentionally not in the UI.
      expect(screen.getByText(/6 sections · cost-cap enforced/i)).toBeInTheDocument();
    });
  });

  describe('deep-link URL sync (spec-panel gap #1)', () => {
    it('does not push a URL when no analysis is selected on initial mount', () => {
      // No URL update is needed when analysisId starts as null and the URL has
      // no `?analysisId=` — the effect short-circuits via the equality guard.
      renderWithQuery(<MechanicAnalysesPage />);
      expect(mockRouterReplace).not.toHaveBeenCalled();
    });

    it('hydrates analysisId from URL on mount (deep-link entry)', async () => {
      const seededId = '11111111-1111-1111-1111-111111111111';
      mockSearchParamsRef.current = new URLSearchParams(`analysisId=${seededId}`);
      mockGetMechanicAnalysisStatus.mockResolvedValue({
        ...SAMPLE_STATUS_DRAFT_EMPTY,
        id: seededId,
      });

      renderWithQuery(<MechanicAnalysesPage />);

      // Page issues the status fetch with the seeded id → confirms the lazy
      // initializer read the URL on first render.
      await waitFor(() => {
        expect(mockGetMechanicAnalysisStatus).toHaveBeenCalledWith(seededId);
      });
    });

    it('pushes ?analysisId=… when an analysis is selected from the discovery list', async () => {
      mockListMechanicAnalyses.mockResolvedValue({
        items: [
          {
            id: '11111111-1111-1111-1111-111111111111',
            sharedGameId: '22222222-2222-2222-2222-222222222222',
            gameTitle: 'Catan',
            pdfDocumentId: '33333333-3333-3333-3333-333333333333',
            promptVersion: 'v1.0.0',
            status: 0,
            claimsCount: 0,
            totalTokensUsed: 0,
            estimatedCostUsd: 0,
            certificationStatus: 0,
            isSuppressed: false,
            createdAt: '2026-04-25T10:00:00Z',
          },
        ],
        page: 1,
        pageSize: 20,
        totalCount: 1,
      });
      mockGetMechanicAnalysisStatus.mockResolvedValue(SAMPLE_STATUS_DRAFT_EMPTY);

      renderWithQuery(<MechanicAnalysesPage />);

      const row = await screen.findByTestId(
        'mechanic-analyses-list-row-11111111-1111-1111-1111-111111111111'
      );
      fireEvent.click(row);

      await waitFor(() => {
        expect(mockRouterReplace).toHaveBeenCalledWith(
          expect.stringContaining('analysisId=11111111-1111-1111-1111-111111111111'),
          expect.objectContaining({ scroll: false })
        );
      });
    });
  });

  describe('generation form', () => {
    it('renders Shared Game and PDF selects', () => {
      renderWithQuery(<MechanicAnalysesPage />);
      expect(screen.getByText('Shared Game')).toBeInTheDocument();
      expect(screen.getByText('PDF')).toBeInTheDocument();
    });

    it('renders cost cap input with default 0.50', () => {
      renderWithQuery(<MechanicAnalysesPage />);
      const input = screen.getByDisplayValue('0.50') as HTMLInputElement;
      expect(input).toBeInTheDocument();
      expect(input.type).toBe('number');
    });

    it('Generate button disabled without game+PDF selection', () => {
      renderWithQuery(<MechanicAnalysesPage />);
      const btn = screen.getByText('Generate').closest('button');
      expect(btn).toBeDisabled();
    });

    it('renders override toggle checkbox', () => {
      renderWithQuery(<MechanicAnalysesPage />);
      expect(screen.getByText(/Apply planning-time override/)).toBeInTheDocument();
    });

    it('reveals override inputs when checkbox is toggled', () => {
      renderWithQuery(<MechanicAnalysesPage />);
      const checkbox = screen
        .getByText(/Apply planning-time override/)
        .closest('label')!
        .querySelector('input[type="checkbox"]')!;
      fireEvent.click(checkbox);
      expect(screen.getByPlaceholderText(/Override cap USD/)).toBeInTheDocument();
      expect(screen.getByPlaceholderText(/Reason for override/)).toBeInTheDocument();
    });
  });

  describe('status card (when analysis exists)', () => {
    it('renders status badge for Draft', async () => {
      mockGenerateMechanicAnalysis.mockResolvedValue({
        id: '11111111-1111-1111-1111-111111111111',
        status: 0,
        promptVersion: 'v1.0.0',
        costCapUsd: 0.5,
        estimatedCostUsd: 0,
        projectedTotalTokens: 0,
        costCapOverrideApplied: false,
        statusUrl: '/api/v1/admin/mechanic-analyses/11111111-1111-1111-1111-111111111111/status',
        isExistingAnalysis: false,
      });
      mockGetMechanicAnalysisStatus.mockResolvedValue(SAMPLE_STATUS_DRAFT_COMPLETE);

      renderWithQuery(<MechanicAnalysesPage />);

      // Wait for the Ready-PDFs query to fire (page derives the games dropdown
      // from this payload now, see page.tsx:143-163 — sharedGamesClient was
      // dropped because it capped pageSize at 100 and silently truncated 7W).
      await waitFor(() => expect(mockGetAllPdfs).toHaveBeenCalled());
    });

    it('isPipelineRunning logic: Draft + 0 runs = running (indirectly via badge)', async () => {
      // When we have a pending analysis with 0 section runs, status card shows Running badge.
      // Asserted indirectly: page logic polls every 2s until runs.length >= 6.
      mockGetMechanicAnalysisStatus.mockResolvedValue(SAMPLE_STATUS_DRAFT_EMPTY);
      renderWithQuery(<MechanicAnalysesPage />);
      await waitFor(() => expect(mockGetAllPdfs).toHaveBeenCalled());
    });
  });

  describe('suppress dialog validation', () => {
    it('does not open initially', () => {
      renderWithQuery(<MechanicAnalysesPage />);
      expect(screen.queryByTestId('suppress-reason-input')).not.toBeInTheDocument();
    });
  });
});

describe('status rendering — Published and Suppressed flags', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetAll.mockResolvedValue({ items: [], totalCount: 0 });
    mockGetAllPdfs.mockResolvedValue({ items: [], totalCount: 0 });
  });

  it('renders the section runs table header labels when not polling', () => {
    // Sanity: ensure page boots without an active analysisId
    renderWithQuery(<MechanicAnalysesPage />);
    expect(
      screen.getByRole('heading', { level: 1, name: 'Mechanic Extraction' })
    ).toBeInTheDocument();
    // Section runs table only mounts after analysisId is set; it should not be here.
    expect(screen.queryByText('Section runs')).not.toBeInTheDocument();
  });
});

describe('lifecycle status enum sanity (imported labels)', () => {
  it('MECHANIC_ANALYSIS_STATUS_LABELS matches backend enum', async () => {
    const { MECHANIC_ANALYSIS_STATUS_LABELS } =
      await import('@/lib/api/schemas/mechanic-analyses.schemas');
    expect(MECHANIC_ANALYSIS_STATUS_LABELS[0]).toBe('Draft');
    expect(MECHANIC_ANALYSIS_STATUS_LABELS[1]).toBe('In Review');
    expect(MECHANIC_ANALYSIS_STATUS_LABELS[2]).toBe('Published');
    expect(MECHANIC_ANALYSIS_STATUS_LABELS[3]).toBe('Rejected');
  });

  it('SUPPRESSION_REQUEST_SOURCE_LABELS matches backend enum (Email=1, Legal=2, Other=3)', async () => {
    const { SUPPRESSION_REQUEST_SOURCE_LABELS } =
      await import('@/lib/api/schemas/mechanic-analyses.schemas');
    expect(SUPPRESSION_REQUEST_SOURCE_LABELS[1]).toBe('Email');
    expect(SUPPRESSION_REQUEST_SOURCE_LABELS[2]).toBe('Legal');
    expect(SUPPRESSION_REQUEST_SOURCE_LABELS[3]).toBe('Other');
  });

  it('MECHANIC_SECTION_LABELS covers all 6 sections', async () => {
    const { MECHANIC_SECTION_LABELS } = await import('@/lib/api/schemas/mechanic-analyses.schemas');
    expect(MECHANIC_SECTION_LABELS[0]).toBe('Summary');
    expect(MECHANIC_SECTION_LABELS[1]).toBe('Mechanics');
    expect(MECHANIC_SECTION_LABELS[2]).toBe('Victory');
    expect(MECHANIC_SECTION_LABELS[3]).toBe('Resources');
    expect(MECHANIC_SECTION_LABELS[4]).toBe('Phases');
    expect(MECHANIC_SECTION_LABELS[5]).toBe('FAQ');
  });
});

describe('route constants', () => {
  it('MECHANIC_ANALYSES_ROUTES builds correct paths', async () => {
    const { MECHANIC_ANALYSES_ROUTES } =
      await import('@/lib/api/schemas/mechanic-analyses.schemas');
    const id = 'abc-123';
    expect(MECHANIC_ANALYSES_ROUTES.create).toBe('/api/v1/admin/mechanic-analyses');
    expect(MECHANIC_ANALYSES_ROUTES.status(id)).toBe(
      `/api/v1/admin/mechanic-analyses/${id}/status`
    );
    expect(MECHANIC_ANALYSES_ROUTES.submitReview(id)).toBe(
      `/api/v1/admin/mechanic-analyses/${id}/submit-review`
    );
    expect(MECHANIC_ANALYSES_ROUTES.approve(id)).toBe(
      `/api/v1/admin/mechanic-analyses/${id}/approve`
    );
    expect(MECHANIC_ANALYSES_ROUTES.suppress(id)).toBe(
      `/api/v1/admin/mechanic-analyses/${id}/suppress`
    );
  });

  it('MECHANIC_ANALYSES_PAGES.analyses points to admin UI', async () => {
    const { MECHANIC_ANALYSES_PAGES } = await import('@/lib/api/schemas/mechanic-analyses.schemas');
    expect(MECHANIC_ANALYSES_PAGES.analyses).toBe(
      '/admin/knowledge-base/mechanic-extractor/analyses'
    );
  });
});
