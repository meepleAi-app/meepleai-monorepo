/**
 * #5076: the admin review of a share request. A proposal is approved through approve-game-proposal with one of
 * three actions (variant and merge on a catalog game the admin picks), a cover change with UpdateCover, the other
 * types with the generic approve.
 */

import { QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { createTestQueryClient } from '@/__tests__/utils/query-test-utils';
import {
  ShareRequestDetailsDtoSchema,
  type ShareRequestDetailsDto,
} from '@/lib/api/schemas/admin-share-requests.schemas';
import { PagedSharedGamesSchema } from '@/lib/api/schemas/shared-games.schemas';

const adminShareRequests = vi.hoisted(() => ({
  getById: vi.fn(),
  startReview: vi.fn(),
  releaseReview: vi.fn(),
  approve: vi.fn(),
  approveGameProposal: vi.fn(),
  reject: vi.fn(),
  requestChanges: vi.fn(),
}));
const sharedGames = vi.hoisted(() => ({ search: vi.fn() }));

vi.mock('@/lib/api', () => ({
  api: {
    adminShareRequests,
    sharedGames,
    auth: { getProfile: vi.fn().mockResolvedValue(null) },
  },
}));

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

import { ShareRequestDetail } from '../ShareRequestDetail';

const REQUEST_ID = '11111111-1111-4111-8111-111111111111';
const PRIVATE_GAME_ID = '22222222-2222-4222-8222-222222222222';
const CATALOG_GAME_ID = '33333333-3333-4333-8333-333333333333';
const ADMIN_ID = '44444444-4444-4444-8444-444444444444';

type Lock = 'none' | 'mine' | 'theirs';

/** A review lock that is still held: the panel stops offering decisions once it passes. */
const inTwentyMinutes = () => new Date(Date.now() + 20 * 60_000).toISOString();

const DOCUMENT = {
  documentId: '66666666-6666-4666-8666-666666666666',
  fileName: 'rules.pdf',
  contentType: 'application/pdf',
  fileSize: 2048,
  previewUrl: null,
  pageCount: null,
};

/** A details payload as the backend sends it, through the real schema. */
function details(
  contributionType: ShareRequestDetailsDto['contributionType'],
  lock: Lock,
  status: ShareRequestDetailsDto['status'] = lock === 'none' ? 'Pending' : 'InReview',
  {
    lockExpiresAt = inTwentyMinutes(),
    documents = 0,
    coverChange = null,
  }: {
    lockExpiresAt?: string;
    documents?: number;
    coverChange?: ShareRequestDetailsDto['coverChange'];
  } = {}
): ShareRequestDetailsDto {
  return ShareRequestDetailsDtoSchema.parse({
    id: REQUEST_ID,
    status,
    contributionType,
    sourceGame: {
      id: PRIVATE_GAME_ID,
      title: 'Tile Kingdoms',
      yearPublished: 2021,
      description: 'A tile game.',
      thumbnailUrl: null,
      bggId: null,
      minPlayers: 2,
      maxPlayers: 4,
      playingTime: 45,
      complexity: null,
      categories: [],
      mechanisms: [],
    },
    targetSharedGame: null,
    contributor: {
      userId: '55555555-5555-4555-8555-555555555555',
      userName: 'Ada',
      avatarUrl: null,
      joinedAt: '2026-01-01T00:00:00Z',
      totalContributions: 0,
      approvedContributions: 1,
      approvalRate: 0.5,
      badges: [],
    },
    userNotes: 'Please add it',
    // A distinct uuid for each, through the real schema: the last group differs by index.
    attachedDocuments: Array.from({ length: documents }, (_, i) => ({
      ...DOCUMENT,
      documentId: `${DOCUMENT.documentId.slice(0, -1)}${i}`,
    })),
    history: [],
    lockStatus: {
      isLocked: lock !== 'none',
      isLockedByCurrentAdmin: lock === 'mine',
      lockedByAdminId: lock === 'none' ? null : ADMIN_ID,
      lockedByAdminName: lock === 'theirs' ? 'Grace' : lock === 'mine' ? 'Me' : null,
      lockExpiresAt: lock === 'none' ? null : lockExpiresAt,
    },
    createdAt: '2026-11-19T10:00:00Z',
    resolvedAt: status === 'Approved' ? '2026-11-19T11:00:00Z' : null,
    coverChange,
  });
}

function renderDetail(request: ShareRequestDetailsDto) {
  adminShareRequests.getById.mockResolvedValue(request);
  const queryClient = createTestQueryClient();
  return render(
    <QueryClientProvider client={queryClient}>
      <ShareRequestDetail id={REQUEST_ID} />
    </QueryClientProvider>
  );
}

const approval = {
  shareRequestId: REQUEST_ID,
  status: 'Approved',
  targetSharedGameId: CATALOG_GAME_ID,
  resolvedAt: '2026-11-19T11:00:00Z',
};

beforeEach(() => {
  vi.clearAllMocks();
  adminShareRequests.approveGameProposal.mockResolvedValue(approval);
  adminShareRequests.approve.mockResolvedValue(undefined);
  adminShareRequests.reject.mockResolvedValue(undefined);
  sharedGames.search.mockResolvedValue(
    PagedSharedGamesSchema.parse({
      items: [
        {
          id: CATALOG_GAME_ID,
          bggId: null,
          title: 'Carcassonne',
          yearPublished: 2000,
          description: 'Tiles.',
          minPlayers: 2,
          maxPlayers: 5,
          playingTimeMinutes: 45,
          minAge: 7,
          complexityRating: null,
          averageRating: null,
          imageUrl: '',
          thumbnailUrl: '',
          status: 'Published',
          createdAt: '2026-01-01T00:00:00Z',
          modifiedAt: null,
        },
      ],
      total: 1,
      page: 1,
      pageSize: 8,
    })
  );
});

describe('ShareRequestDetail', () => {
  it('shows the proposed private game with its year and players, and starts the review', async () => {
    adminShareRequests.startReview.mockResolvedValue({
      shareRequestId: REQUEST_ID,
      lockExpiresAt: inTwentyMinutes(),
      requestDetails: details('NewGameProposal', 'mine'),
    });
    renderDetail(details('NewGameProposal', 'none'));

    expect(await screen.findByRole('heading', { name: 'Tile Kingdoms' })).toBeInTheDocument();
    expect(screen.getByText('Gioco proposto')).toBeInTheDocument();
    expect(screen.getByText('2021')).toBeInTheDocument();
    expect(screen.getByText('2–4')).toBeInTheDocument();
    expect(screen.getByText('A tile game.')).toBeInTheDocument();
    // No decision before the review lock.
    expect(screen.queryByRole('button', { name: 'Approva la proposta' })).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Avvia la revisione' }));

    expect(adminShareRequests.startReview).toHaveBeenCalledWith(REQUEST_ID);
    expect(await screen.findByRole('button', { name: 'Approva la proposta' })).toBeInTheDocument();
  });

  it('approves a proposal as a new game through approve-game-proposal, without a target', async () => {
    renderDetail(details('NewGameProposal', 'mine'));

    await userEvent.click(await screen.findByRole('button', { name: 'Approva la proposta' }));

    await waitFor(() =>
      expect(adminShareRequests.approveGameProposal).toHaveBeenCalledWith(REQUEST_ID, {
        approvalAction: 'ApproveAsNew',
        targetSharedGameId: null,
        adminNotes: undefined,
      })
    );
    expect(adminShareRequests.approve).not.toHaveBeenCalled();
  });

  it.each([
    ['Approva come variante', 'ApproveAsVariant'],
    ['Unisci a un gioco esistente', 'MergeKnowledgeBase'],
  ] as const)('%s needs a catalog game, and sends it with %s', async (label, action) => {
    renderDetail(details('NewGameProposal', 'mine'));

    await userEvent.click(await screen.findByRole('radio', { name: new RegExp(label) }));
    const submit = screen.getByRole('button', { name: 'Approva la proposta' });
    expect(submit).toBeDisabled();

    await userEvent.type(screen.getByRole('searchbox', { name: 'Gioco del catalogo' }), 'Carc');
    await userEvent.click(await screen.findByRole('button', { name: /Carcassonne/ }));
    await userEvent.type(screen.getByLabelText(/Note dell'admin/), 'Second edition');

    expect(sharedGames.search).toHaveBeenCalledWith({ searchTerm: 'Carc', pageSize: 8 });
    expect(submit).toBeEnabled();
    await userEvent.click(submit);

    await waitFor(() =>
      expect(adminShareRequests.approveGameProposal).toHaveBeenCalledWith(REQUEST_ID, {
        approvalAction: action,
        targetSharedGameId: CATALOG_GAME_ID,
        adminNotes: 'Second edition',
      })
    );
  });

  it('approves a cover change with UpdateCover', async () => {
    renderDetail(details('CoverChange', 'mine'));

    await userEvent.click(await screen.findByRole('button', { name: 'Approva la copertina' }));

    await waitFor(() =>
      expect(adminShareRequests.approveGameProposal).toHaveBeenCalledWith(REQUEST_ID, {
        approvalAction: 'UpdateCover',
        targetSharedGameId: null,
      })
    );
    expect(adminShareRequests.approve).not.toHaveBeenCalled();
  });

  // #5084: a cover change was approved or rejected without seeing it.
  it('shows the proposed cover next to the current one, before the review starts', async () => {
    renderDetail(
      details('CoverChange', 'none', 'Pending', {
        coverChange: {
          proposedCoverUrl: 'https://r2.test/proposed.webp',
          currentCoverUrl: 'https://r2.test/current.webp',
          proposedFromPage: 3,
        },
      })
    );

    const comparison = await screen.findByTestId('cover-comparison');
    expect(
      within(comparison).getByRole('img', { name: 'Copertina attuale del gioco' })
    ).toHaveAttribute('src', 'https://r2.test/current.webp');
    expect(within(comparison).getByRole('img', { name: 'Copertina proposta' })).toHaveAttribute(
      'src',
      'https://r2.test/proposed.webp'
    );
    expect(comparison).toHaveTextContent('Proposta, dalla pagina 3 del PDF');
    expect(screen.getByRole('button', { name: 'Avvia la revisione' })).toBeInTheDocument();
  });

  it('says what is missing when a cover cannot be shown', async () => {
    renderDetail(
      details('CoverChange', 'mine', 'InReview', {
        coverChange: { proposedCoverUrl: null, currentCoverUrl: null, proposedFromPage: 1 },
      })
    );

    const comparison = await screen.findByTestId('cover-comparison');
    expect(within(comparison).queryByRole('img')).not.toBeInTheDocument();
    expect(comparison).toHaveTextContent('Copertina attuale non disponibile.');
    expect(comparison).toHaveTextContent('Anteprima non disponibile.');
  });

  it('shows no cover comparison for another kind of request', async () => {
    renderDetail(details('AdditionalContent', 'mine'));

    await screen.findByRole('button', { name: 'Approva' });
    expect(screen.queryByTestId('cover-comparison')).not.toBeInTheDocument();
  });

  it('approves the other types with the generic approve', async () => {
    renderDetail(details('NewGame', 'mine'));

    await userEvent.click(await screen.findByRole('button', { name: 'Approva' }));

    await waitFor(() =>
      expect(adminShareRequests.approve).toHaveBeenCalledWith(REQUEST_ID, { adminNotes: undefined })
    );
    expect(adminShareRequests.approveGameProposal).not.toHaveBeenCalled();
    expect(screen.queryByRole('radio')).not.toBeInTheDocument();
  });

  it('rejects only with a reason of at least ten characters', async () => {
    renderDetail(details('NewGameProposal', 'mine'));

    const reject = await screen.findByRole('button', { name: 'Rifiuta' });
    await userEvent.type(screen.getByLabelText('Motivo del rifiuto'), 'Too short');
    expect(reject).toBeDisabled();
    await userEvent.type(screen.getByLabelText('Motivo del rifiuto'), ' and vague');
    await userEvent.click(reject);

    await waitFor(() =>
      expect(adminShareRequests.reject).toHaveBeenCalledWith(REQUEST_ID, {
        reason: 'Too short and vague',
      })
    );
  });

  it('approves a request with attached documents, and says they are copied', async () => {
    // #5109, decision of 2026-11-23: the backend copies them into the game the request contributes to. The panel
    // sends no target: the destination is the request's.
    renderDetail(details('NewGame', 'mine', 'InReview', { documents: 1 }));

    expect(await screen.findByTestId('approve-copies-documents')).toHaveTextContent(
      'il documento allegato viene copiato'
    );
    await userEvent.click(screen.getByRole('button', { name: 'Approva' }));

    await waitFor(() =>
      expect(adminShareRequests.approve).toHaveBeenCalledWith(REQUEST_ID, { adminNotes: undefined })
    );
  });

  it('counts the documents it copies when there are several', async () => {
    renderDetail(details('NewGame', 'mine', 'InReview', { documents: 3 }));

    expect(await screen.findByTestId('approve-copies-documents')).toHaveTextContent(
      'i 3 documenti allegati vengono copiati'
    );
  });

  it('stops offering decisions once the review lock has expired', async () => {
    // The backend reports the lock as held until its release job runs, and refuses every decision meanwhile.
    renderDetail(
      details('NewGameProposal', 'mine', 'InReview', {
        lockExpiresAt: new Date(Date.now() - 60_000).toISOString(),
      })
    );

    expect(await screen.findByTestId('review-expired')).toHaveTextContent('è scaduta');
    expect(screen.queryByRole('button', { name: 'Approva la proposta' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Rifiuta' })).not.toBeInTheDocument();

    adminShareRequests.getById.mockClear();
    await userEvent.click(screen.getByRole('button', { name: 'Aggiorna' }));
    await waitFor(() => expect(adminShareRequests.getById).toHaveBeenCalledWith(REQUEST_ID));
  });

  it('offers no decision while another admin holds the review', async () => {
    renderDetail(details('NewGameProposal', 'theirs'));

    expect(await screen.findByText(/In revisione da Grace/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Avvia la revisione' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Approva la proposta' })).not.toBeInTheDocument();
  });

  it('offers no decision on a closed request', async () => {
    renderDetail(details('NewGameProposal', 'none', 'Approved'));

    expect(await screen.findByText(/Richiesta chiusa il/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Avvia la revisione' })).not.toBeInTheDocument();
  });
});
