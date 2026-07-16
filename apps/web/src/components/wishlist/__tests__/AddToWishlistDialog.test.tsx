/**
 * @vitest-environment jsdom
 */

import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { IntlProvider } from 'react-intl';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { flattenMessages } from '@/locales';
import itMessages from '@/locales/it.json';

import { AddToWishlistDialog } from '../AddToWishlistDialog';

import type { WishlistItemDto } from '@/lib/api/schemas/wishlist.schemas';
import type { ReactElement } from 'react';

const MESSAGES = flattenMessages(itMessages as unknown as Record<string, unknown>);

// ============================================================================
// Mocks
// ============================================================================

const mockAddMutate = vi.fn();
const mockUpdateMutate = vi.fn();
const mockToastSuccess = vi.fn();

vi.mock('@/hooks/queries/useWishlist', () => ({
  useAddToWishlist: () => ({ mutate: mockAddMutate, isPending: false }),
  useUpdateWishlistItem: () => ({ mutate: mockUpdateMutate, isPending: false }),
}));

const LIBRARY_ITEMS = [
  { gameId: 'game-catan', gameTitle: 'Catan' },
  { gameId: 'game-brass', gameTitle: 'Brass: Birmingham' },
];

vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibrary: () => ({ data: { items: LIBRARY_ITEMS }, isLoading: false }),
}));

vi.mock('@/components/layout/Toast', () => ({
  toast: {
    success: (...args: unknown[]) => mockToastSuccess(...args),
  },
}));

// ============================================================================
// Helpers
// ============================================================================

function renderWithIntl(ui: ReactElement) {
  return render(
    <IntlProvider locale="it" messages={MESSAGES}>
      {ui}
    </IntlProvider>
  );
}

/**
 * jsdom does not dispatch a native `submit` event on submit-button click, so submit the
 * form directly. Radix `Dialog` renders its content into a portal on `document.body`
 * (outside RTL's render `container`), so query from `document.body`.
 */
function submitForm() {
  const form = document.body.querySelector('form');
  expect(form).not.toBeNull();
  fireEvent.submit(form as HTMLFormElement);
}

function buildItem(overrides: Partial<WishlistItemDto> = {}): WishlistItemDto {
  return {
    id: '00000000-0000-0000-0000-000000000001',
    userId: '00000000-0000-0000-0000-000000000002',
    gameId: 'game-brass',
    gameName: 'Brass: Birmingham',
    priority: 'high',
    targetPrice: 42,
    notes: 'Acquisto entro Q3',
    addedAt: '2026-01-01',
    updatedAt: null,
    visibility: 'private',
    ...overrides,
  };
}

const GAME_COMBO_NAME = 'Cerca gioco da aggiungere';
const PRICE_ARIA_NAME = 'Prezzo massimo che sei disposto a spendere';
const NOTES_PLACEHOLDER = 'es. acquisto entro Q3, voglio aspettare la nuova edizione, ecc.';

describe('AddToWishlistDialog', () => {
  beforeEach(() => {
    mockAddMutate.mockReset();
    mockUpdateMutate.mockReset();
    mockToastSuccess.mockReset();
  });

  describe('mode=add', () => {
    it('calls useAddToWishlist with {gameId, priority, targetPrice, notes} on submit', async () => {
      const user = userEvent.setup();
      renderWithIntl(<AddToWishlistDialog mode="add" open onOpenChange={() => {}} />);

      // Pick game via combobox
      const combo = screen.getByRole('combobox', { name: GAME_COMBO_NAME });
      await user.type(combo, 'Catan');
      await user.click(screen.getByRole('option', { name: 'Catan' }));

      // Switch priority to "Alta" (default is "Media")
      await user.click(screen.getByRole('radio', { name: 'Alta' }));

      // Target price
      const priceInput = screen.getByRole('spinbutton', { name: PRICE_ARIA_NAME });
      await user.type(priceInput, '29.99');

      // Notes
      const notesInput = screen.getByPlaceholderText(NOTES_PLACEHOLDER);
      await user.type(notesInput, 'Da comprare presto');

      submitForm();

      expect(mockAddMutate).toHaveBeenCalledTimes(1);
      const [payload] = mockAddMutate.mock.calls[0];
      expect(payload).toEqual({
        gameId: 'game-catan',
        priority: 'high',
        targetPrice: 29.99,
        notes: 'Da comprare presto',
      });
    });

    it('disables submit until a game is selected', () => {
      renderWithIntl(<AddToWishlistDialog mode="add" open onOpenChange={() => {}} />);
      expect(screen.getByRole('button', { name: 'Aggiungi' })).toBeDisabled();
    });

    it('enables submit once a game is pre-filled (priority defaults to "medium")', () => {
      renderWithIntl(
        <AddToWishlistDialog mode="add" open onOpenChange={() => {}} prefillGameId="game-catan" />
      );
      expect(screen.getByRole('button', { name: 'Aggiungi' })).toBeEnabled();
      expect(screen.getByRole('radio', { name: 'Media' })).toHaveAttribute('aria-checked', 'true');
    });

    it('updates the notes counter as the user types', async () => {
      const user = userEvent.setup();
      renderWithIntl(
        <AddToWishlistDialog mode="add" open onOpenChange={() => {}} prefillGameId="game-catan" />
      );

      const notesInput = screen.getByPlaceholderText(NOTES_PLACEHOLDER);
      await user.type(notesInput, 'Hello');

      expect(screen.getByText('5 / 200')).toBeInTheDocument();
    });
  });

  describe('mode=edit', () => {
    it('prefills the locked game chip, priority, price and notes from item', () => {
      const item = buildItem();
      renderWithIntl(<AddToWishlistDialog mode="edit" item={item} open onOpenChange={() => {}} />);

      expect(screen.getByText('Brass: Birmingham')).toBeInTheDocument();
      // Game is locked in edit mode — no remove button on the chip
      expect(
        screen.queryByRole('button', { name: 'Rimuovi gioco selezionato' })
      ).not.toBeInTheDocument();

      expect(screen.getByRole('radio', { name: 'Alta' })).toHaveAttribute('aria-checked', 'true');

      const priceInput = screen.getByRole('spinbutton', { name: PRICE_ARIA_NAME });
      expect(priceInput).toHaveValue(42);

      const notesInput = screen.getByPlaceholderText(NOTES_PLACEHOLDER);
      expect(notesInput).toHaveValue('Acquisto entro Q3');
    });

    it('sends clearTargetPrice: true when a previously-set target price is cleared', async () => {
      const user = userEvent.setup();
      const item = buildItem();
      renderWithIntl(<AddToWishlistDialog mode="edit" item={item} open onOpenChange={() => {}} />);

      const priceInput = screen.getByRole('spinbutton', { name: PRICE_ARIA_NAME });
      await user.clear(priceInput);

      submitForm();

      expect(mockUpdateMutate).toHaveBeenCalledTimes(1);
      const [payload] = mockUpdateMutate.mock.calls[0];
      expect(payload).toEqual({
        id: item.id,
        data: {
          priority: 'high',
          clearTargetPrice: true,
          notes: 'Acquisto entro Q3',
        },
      });
    });

    it('sends clearNotes: true when previously-set notes are cleared', async () => {
      const user = userEvent.setup();
      const item = buildItem();
      renderWithIntl(<AddToWishlistDialog mode="edit" item={item} open onOpenChange={() => {}} />);

      const notesInput = screen.getByPlaceholderText(NOTES_PLACEHOLDER);
      await user.clear(notesInput);

      submitForm();

      expect(mockUpdateMutate).toHaveBeenCalledTimes(1);
      const [payload] = mockUpdateMutate.mock.calls[0];
      expect(payload).toEqual({
        id: item.id,
        data: {
          priority: 'high',
          targetPrice: 42,
          clearNotes: true,
        },
      });
    });
  });
});
