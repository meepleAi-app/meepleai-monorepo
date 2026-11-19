/**
 * #5081: Edit, Propose and Delete are reachable from a private game's card. The card passed them as `actions`
 * without `showQuickActions`, so since #1877 (2026-06-04) the grid drew only a decorative "⋯" and none of them was
 * reachable; the proposal of #5077 opened only in tests that mounted its modal directly. These start from the card.
 */

import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';
import { PaginatedPrivateGamesResponseSchema } from '@/lib/api/schemas/private-games.schemas';

const library = vi.hoisted(() => ({
  getPrivateGames: vi.fn(),
  updatePrivateGame: vi.fn(),
  deletePrivateGame: vi.fn(),
}));
const push = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    library,
    shareRequests: { proposePrivateGame: vi.fn() },
    auth: { getProfile: vi.fn().mockResolvedValue(null) },
  },
}));
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push, replace: vi.fn(), prefetch: vi.fn() }),
  usePathname: () => '/library/private',
  useSearchParams: () => new URLSearchParams(),
}));
// The onboarding journey above the grid needs the session; these tests are about the cards.
vi.mock('@/components/library/JourneyProgress', () => ({ JourneyProgress: () => null }));
vi.mock('@/hooks/queries/usePdfProcessingStatus', () => ({
  usePdfProcessingStatus: () => ({ data: undefined }),
}));

import PrivateGamesClient from '../PrivateGamesClient';

const GAME_ID = '11111111-1111-4111-8111-111111111111';

/** One private game, as the backend sends it, through the real schema. */
const page = PaginatedPrivateGamesResponseSchema.parse({
  items: [
    {
      id: GAME_ID,
      ownerId: '22222222-2222-4222-8222-222222222222',
      source: 'Manual',
      title: 'Tile Kingdoms',
      minPlayers: 2,
      maxPlayers: 4,
      yearPublished: 2021,
      description: 'A tile game.',
      createdAt: '2026-11-19T10:00:00Z',
      canProposeToCatalog: true,
    },
  ],
  page: 1,
  pageSize: 12,
  totalCount: 1,
  totalPages: 1,
  hasNextPage: false,
  hasPreviousPage: false,
});

async function renderLibrary() {
  renderWithQuery(<PrivateGamesClient />);
  const card = await screen.findByTestId(`game-card-${GAME_ID}`);
  const menu = screen.getByRole('button', { name: 'Actions for "Tile Kingdoms"' });
  return { card, menu };
}

async function choose(item: string) {
  const { card, menu } = await renderLibrary();
  // The menu is a control of its own, not nested in the clickable card.
  expect(card.contains(menu)).toBe(false);
  await userEvent.click(menu);
  await userEvent.click(await screen.findByRole('menuitem', { name: item }));
  return { card, menu };
}

beforeEach(() => {
  vi.clearAllMocks();
  library.getPrivateGames.mockResolvedValue(page);
  library.deletePrivateGame.mockResolvedValue(undefined);
});

describe('PrivateGamesClient card actions (#5081)', () => {
  it('opens the proposal to the catalog from the card, without opening the game', async () => {
    await choose('Propose to catalog');

    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText('Propose to Shared Catalog')).toBeInTheDocument();
    expect(push).not.toHaveBeenCalled();
  });

  it('opens the edit form from the card, and gives the focus back to the menu when it closes', async () => {
    const { menu } = await choose('Edit');
    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveTextContent('Edit Game');
    // The dialog keeps the focus: the closing menu does not take it back to its button.
    await waitFor(() => expect(dialog.contains(document.activeElement)).toBe(true));

    // The dialog is controlled and has no trigger: without the menu's button to go back to, focus fell on <body>.
    await userEvent.keyboard('{Escape}');

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    await waitFor(() => expect(menu).toHaveFocus());
    // A dialog opened while the modal menu was still closing restored the menu's `pointer-events: none` on close.
    expect(document.body).not.toHaveStyle({ pointerEvents: 'none' });
    expect(push).not.toHaveBeenCalled();
  });

  it('deletes the game after the confirmation opened from the card', async () => {
    await choose('Delete');

    const confirm = await screen.findByRole('alertdialog');
    expect(confirm).toHaveTextContent('Delete Game');
    await userEvent.click(within(confirm).getByTestId('confirm-delete-btn'));

    await waitFor(() => expect(library.deletePrivateGame).toHaveBeenCalledWith(GAME_ID));
  });

  it('keeps the page underneath out of reach while the menu is open', async () => {
    // A non-modal menu let the click that closes it reach the card, which opens the game.
    const { menu } = await renderLibrary();
    await userEvent.click(menu);
    await screen.findByRole('menuitem', { name: 'Edit' });

    expect(document.body).toHaveStyle({ pointerEvents: 'none' });
  });

  it('still opens the game when the card itself is clicked', async () => {
    const { card } = await renderLibrary();

    await userEvent.click(card);

    expect(push).toHaveBeenCalledWith(`/library/private/${GAME_ID}`);
  });
});
