/**
 * #5076: the admin share-request queue. Every type is listed, a proposal by its private game's title, and a row
 * opens the review page.
 */

import { QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { createTestQueryClient } from '@/__tests__/utils/query-test-utils';
import { PaginatedAdminShareRequestsResponseSchema } from '@/lib/api/schemas/admin-share-requests.schemas';

const adminShareRequests = vi.hoisted(() => ({ getAll: vi.fn() }));

vi.mock('@/lib/api', () => ({ api: { adminShareRequests } }));

import { ShareRequestsList } from '../ShareRequestsList';

const PROPOSAL_ID = '11111111-1111-4111-8111-111111111111';

/** A list page as the backend sends it: a proposal has no shared game, so its sourceGameId is the empty GUID. */
const page = PaginatedAdminShareRequestsResponseSchema.parse({
  items: [
    {
      id: PROPOSAL_ID,
      status: 'Pending',
      contributionType: 'NewGameProposal',
      sourceGameId: '00000000-0000-0000-0000-000000000000',
      gameTitle: 'Tile Kingdoms',
      gameThumbnailUrl: null,
      bggId: null,
      userId: '55555555-5555-4555-8555-555555555555',
      userName: 'Ada',
      userAvatarUrl: null,
      userTotalContributions: 0,
      userNotes: null,
      attachedDocumentCount: 0,
      createdAt: '2026-11-19T10:00:00Z',
      isInReview: false,
      reviewingAdminId: null,
      reviewingAdminName: null,
      reviewStartedAt: null,
      reviewLockExpiresAt: null,
      canStartReview: true,
      targetSharedGameId: null,
      targetSharedGameTitle: null,
    },
  ],
  total: 1,
  page: 1,
  pageSize: 20,
});

function renderList(props: { oldestFirst?: boolean } = {}) {
  return render(
    <QueryClientProvider client={createTestQueryClient()}>
      <ShareRequestsList {...props} />
    </QueryClientProvider>
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  adminShareRequests.getAll.mockResolvedValue(page);
});

describe('ShareRequestsList', () => {
  it('lists every request, newest first, and links a row to its review', async () => {
    renderList();

    const link = await screen.findByRole('link', { name: /Tile Kingdoms/ });
    expect(link).toHaveAttribute('href', `/admin/share-requests/${PROPOSAL_ID}`);
    expect(screen.getByText('Proposta di gioco privato')).toBeInTheDocument();
    expect(screen.getByText('Ada')).toBeInTheDocument();
    expect(adminShareRequests.getAll).toHaveBeenCalledWith({
      page: 1,
      pageSize: 20,
      status: undefined,
      contributionType: undefined,
      searchTerm: undefined,
      sortBy: 'CreatedAt',
      sortDirection: 'Descending',
    });
  });

  it('lists the oldest first when the notification link asks for it', async () => {
    renderList({ oldestFirst: true });

    await screen.findByRole('link', { name: /Tile Kingdoms/ });
    expect(adminShareRequests.getAll).toHaveBeenCalledWith(
      expect.objectContaining({ sortDirection: 'Ascending' })
    );
  });

  it('searches by game or notes, what the backend matches', async () => {
    renderList();
    await screen.findByRole('link', { name: /Tile Kingdoms/ });

    // The backend matches the game title and the contributor's notes, not the contributor's name.
    await userEvent.type(screen.getByRole('searchbox', { name: 'Cerca per gioco o note' }), 'tile');

    await waitFor(() =>
      expect(adminShareRequests.getAll).toHaveBeenLastCalledWith(
        expect.objectContaining({ searchTerm: 'tile', page: 1 })
      )
    );
  });

  it('filters by the open statuses only, the ones the backend accepts', async () => {
    // The queue lists Pending, InReview and ChangesRequested; a filter on any other status is a 400.
    renderList();
    await screen.findByRole('link', { name: /Tile Kingdoms/ });

    await userEvent.click(screen.getByRole('combobox', { name: 'Stato' }));
    const options = (await screen.findAllByRole('option')).map(o => o.textContent);

    expect(options).toEqual([
      'Tutte le attive',
      'In attesa',
      'In revisione',
      'Modifiche richieste',
    ]);
  });

  it('says so when nothing matches', async () => {
    adminShareRequests.getAll.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 20 });
    renderList();

    expect(await screen.findByText('Nessuna richiesta con questi filtri.')).toBeInTheDocument();
  });
});
