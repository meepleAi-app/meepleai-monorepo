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
const mockAddReset = vi.fn();
const mockUpdateReset = vi.fn();
let mockAddIsError = false;
let mockUpdateIsError = false;
let mockAddIsPending = false;
let mockUpdateIsPending = false;

vi.mock('@/hooks/queries/useWishlist', () => ({
  useAddToWishlist: () => ({
    mutate: mockAddMutate,
    isPending: mockAddIsPending,
    isError: mockAddIsError,
    reset: mockAddReset,
  }),
  useUpdateWishlistItem: () => ({
    mutate: mockUpdateMutate,
    isPending: mockUpdateIsPending,
    isError: mockUpdateIsError,
    reset: mockUpdateReset,
  }),
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
    mockAddReset.mockReset();
    mockUpdateReset.mockReset();
    mockAddIsError = false;
    mockUpdateIsError = false;
    mockAddIsPending = false;
    mockUpdateIsPending = false;
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

    it('enables submit once a game is pre-filled (priority defaults to "high")', () => {
      renderWithIntl(
        <AddToWishlistDialog mode="add" open onOpenChange={() => {}} prefillGameId="game-catan" />
      );
      expect(screen.getByRole('button', { name: 'Aggiungi' })).toBeEnabled();
      expect(screen.getByRole('radio', { name: 'Alta' })).toHaveAttribute('aria-checked', 'true');
    });

    it('defaults priority to "medium" when no prefillGameId is provided', () => {
      renderWithIntl(<AddToWishlistDialog mode="add" open onOpenChange={() => {}} />);
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

    it('selects a game via ArrowDown + Enter (keyboard-only, no mouse)', async () => {
      const user = userEvent.setup();
      renderWithIntl(<AddToWishlistDialog mode="add" open onOpenChange={() => {}} />);

      const combo = screen.getByRole('combobox', { name: GAME_COMBO_NAME });
      await user.type(combo, 'Catan');

      // Highlight the only match and commit it — no click involved.
      await user.keyboard('{ArrowDown}');
      const option = screen.getByRole('option', { name: 'Catan' });
      expect(option).toHaveAttribute('aria-selected', 'true');
      expect(combo).toHaveAttribute('aria-activedescendant', option.id);

      await user.keyboard('{Enter}');

      // Combobox is replaced by the locked/removable selected-game chip.
      expect(screen.queryByRole('combobox')).not.toBeInTheDocument();
      expect(screen.getByText('Catan')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Aggiungi' })).toBeEnabled();
    });

    it('closes the listbox on Escape without selecting a game', async () => {
      const user = userEvent.setup();
      renderWithIntl(<AddToWishlistDialog mode="add" open onOpenChange={() => {}} />);

      const combo = screen.getByRole('combobox', { name: GAME_COMBO_NAME });
      await user.type(combo, 'Catan');
      await user.keyboard('{ArrowDown}');
      expect(screen.getByRole('listbox')).toBeInTheDocument();

      await user.keyboard('{Escape}');

      expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Aggiungi' })).toBeDisabled();
    });

    it('scopes Escape to the combobox: closing the listbox does not close the dialog', async () => {
      const user = userEvent.setup();
      const onOpenChange = vi.fn();
      renderWithIntl(<AddToWishlistDialog mode="add" open onOpenChange={onOpenChange} />);

      const combo = screen.getByRole('combobox', { name: GAME_COMBO_NAME });
      await user.type(combo, 'Catan');
      await user.keyboard('{ArrowDown}');
      expect(screen.getByRole('listbox')).toBeInTheDocument();

      await user.keyboard('{Escape}');

      // Listbox is gone, but the Escape keydown must not have closed the
      // surrounding Radix `Dialog` — it stays open.
      expect(screen.queryByRole('listbox')).not.toBeInTheDocument();
      expect(onOpenChange).not.toHaveBeenCalled();
      expect(screen.getByRole('button', { name: 'Aggiungi' })).toBeInTheDocument();
    });

    it('lets a second Escape (listbox already closed) propagate and close the dialog', async () => {
      const user = userEvent.setup();
      const onOpenChange = vi.fn();
      renderWithIntl(<AddToWishlistDialog mode="add" open onOpenChange={onOpenChange} />);

      const combo = screen.getByRole('combobox', { name: GAME_COMBO_NAME });
      await user.type(combo, 'Catan');
      await user.keyboard('{ArrowDown}');
      await user.keyboard('{Escape}'); // closes the listbox only
      expect(onOpenChange).not.toHaveBeenCalled();

      await user.keyboard('{Escape}'); // listbox already closed: propagates to the Dialog

      expect(onOpenChange).toHaveBeenCalledWith(false);
    });

    it('shows a success toast and closes the dialog when the add mutation succeeds', () => {
      const onOpenChange = vi.fn();
      const onSuccess = vi.fn();
      mockAddMutate.mockImplementation(
        (_payload: unknown, options?: { onSuccess?: () => void }) => {
          options?.onSuccess?.();
        }
      );

      renderWithIntl(
        <AddToWishlistDialog
          mode="add"
          open
          onOpenChange={onOpenChange}
          prefillGameId="game-catan"
          onSuccess={onSuccess}
        />
      );

      submitForm();

      expect(mockAddMutate).toHaveBeenCalledTimes(1);
      expect(mockToastSuccess).toHaveBeenCalledTimes(1);
      expect(mockToastSuccess).toHaveBeenCalledWith('Aggiunto');
      expect(onOpenChange).toHaveBeenCalledWith(false);
      expect(onSuccess).toHaveBeenCalledTimes(1);
    });

    it('shows an inline error banner with a retry button when the add mutation fails', () => {
      mockAddIsError = true;
      renderWithIntl(
        <AddToWishlistDialog mode="add" open onOpenChange={() => {}} prefillGameId="game-catan" />
      );

      const alert = screen.getByRole('alert');
      expect(alert).toHaveTextContent('Errore aggiungendo alla wishlist.');
      expect(screen.getByRole('button', { name: 'Riprova' })).toBeInTheDocument();
    });

    it('does not show the error banner when the mutation has not failed', () => {
      renderWithIntl(
        <AddToWishlistDialog mode="add" open onOpenChange={() => {}} prefillGameId="game-catan" />
      );

      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    });

    it('re-invokes the add mutation when the retry button is clicked', async () => {
      const user = userEvent.setup();
      mockAddIsError = true;
      renderWithIntl(
        <AddToWishlistDialog mode="add" open onOpenChange={() => {}} prefillGameId="game-catan" />
      );

      await user.click(screen.getByRole('button', { name: 'Riprova' }));

      expect(mockAddMutate).toHaveBeenCalledTimes(1);
      expect(mockAddReset).toHaveBeenCalled();
    });

    it('disables the retry button while the add mutation is pending', () => {
      mockAddIsError = true;
      mockAddIsPending = true;
      renderWithIntl(
        <AddToWishlistDialog mode="add" open onOpenChange={() => {}} prefillGameId="game-catan" />
      );

      expect(screen.getByRole('button', { name: 'Riprova' })).toBeDisabled();
    });

    it('calls reset on the active mutation when the dialog is cancelled (closed)', async () => {
      const user = userEvent.setup();
      const onOpenChange = vi.fn();
      renderWithIntl(
        <AddToWishlistDialog
          mode="add"
          open
          onOpenChange={onOpenChange}
          prefillGameId="game-catan"
        />
      );

      await user.click(screen.getByRole('button', { name: 'Annulla' }));

      expect(onOpenChange).toHaveBeenCalledWith(false);
      expect(mockAddReset).toHaveBeenCalledTimes(1);
    });

    it('shows no leftover error banner after closing and reopening the dialog', () => {
      mockAddIsError = true;
      const { rerender } = renderWithIntl(
        <AddToWishlistDialog mode="add" open onOpenChange={() => {}} prefillGameId="game-catan" />
      );
      expect(screen.getByRole('alert')).toBeInTheDocument();

      // Simulate the mutation's own reset() clearing isError (as the real
      // useMutation hook would after reset() is called on close), then reopen.
      mockAddIsError = false;
      rerender(
        <IntlProvider locale="it" messages={MESSAGES}>
          <AddToWishlistDialog mode="add" open onOpenChange={() => {}} prefillGameId="game-catan" />
        </IntlProvider>
      );

      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
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

    it('shows a success toast and closes the dialog when the update mutation succeeds', () => {
      const onOpenChange = vi.fn();
      const onSuccess = vi.fn();
      const item = buildItem();
      mockUpdateMutate.mockImplementation(
        (_payload: unknown, options?: { onSuccess?: () => void }) => {
          options?.onSuccess?.();
        }
      );

      renderWithIntl(
        <AddToWishlistDialog
          mode="edit"
          item={item}
          open
          onOpenChange={onOpenChange}
          onSuccess={onSuccess}
        />
      );

      submitForm();

      expect(mockUpdateMutate).toHaveBeenCalledTimes(1);
      expect(mockToastSuccess).toHaveBeenCalledTimes(1);
      expect(mockToastSuccess).toHaveBeenCalledWith('Aggiunto');
      expect(onOpenChange).toHaveBeenCalledWith(false);
      expect(onSuccess).toHaveBeenCalledTimes(1);
    });

    it('shows the error banner (from the update mutation, not add) when the update fails', () => {
      mockUpdateIsError = true;
      const item = buildItem();
      renderWithIntl(<AddToWishlistDialog mode="edit" item={item} open onOpenChange={() => {}} />);

      expect(screen.getByRole('alert')).toHaveTextContent('Errore aggiungendo alla wishlist.');
    });

    it('re-invokes the update mutation when the retry button is clicked', async () => {
      const user = userEvent.setup();
      mockUpdateIsError = true;
      const item = buildItem();
      renderWithIntl(<AddToWishlistDialog mode="edit" item={item} open onOpenChange={() => {}} />);

      await user.click(screen.getByRole('button', { name: 'Riprova' }));

      expect(mockUpdateMutate).toHaveBeenCalledTimes(1);
      expect(mockAddMutate).not.toHaveBeenCalled();
      expect(mockUpdateReset).toHaveBeenCalled();
    });
  });
});
