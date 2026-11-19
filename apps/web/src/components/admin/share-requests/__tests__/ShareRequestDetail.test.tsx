/**
 * #5076: the admin review of a share request. A proposal is approved through approve-game-proposal with one of
 * three actions (variant and merge on a catalog game the admin picks), a cover change with UpdateCover, the other
 * types with the generic approve.
 */

import { QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
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

/** A details payload as the backend sends it, through the real schema. */
function details(
  contributionType: ShareRequestDetailsDto['contributionType'],
  lock: Lock,
  status: ShareRequestDetailsDto['status'] = lock === 'none' ? 'Pending' : 'InReview'
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
    attachedDocuments: [],
    history: [],
    lockStatus: {
      isLocked: lock !== 'none',
      isLockedByCurrentAdmin: lock === 'mine',
      lockedByAdminId: lock === 'none' ? null : ADMIN_ID,
      lockedByAdminName: lock === 'theirs' ? 'Grace' : lock === 'mine' ? 'Me' : null,
      lockExpiresAt: lock === 'none' ? null : '2026-11-19T12:30:00Z',
    },
    createdAt: '2026-11-19T10:00:00Z',
    resolvedAt: status === 'Approved' ? '2026-11-19T11:00:00Z' : null,
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
      lockExpiresAt: '2026-11-19T12:30:00Z',
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
