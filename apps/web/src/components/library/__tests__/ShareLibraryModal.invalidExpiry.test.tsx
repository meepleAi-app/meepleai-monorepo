/**
 * ShareLibraryModal — an expiry that is not a date (#4840).
 *
 * The field is a `datetime-local` and lets the year run past four digits. `handleCreateOrUpdate` converted the raw
 * string with `new Date(expiresAt).toISOString()` BEFORE its `try`: the click ended in an unhandled rejection, with
 * no message and nothing saved.
 */

import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { toast } from '@/components/layout/Toast';

import { ShareLibraryModal } from '../ShareLibraryModal';

const mockCreateMutation = { mutateAsync: vi.fn(), isPending: false };
const mockUpdateMutation = { mutateAsync: vi.fn(), isPending: false };
const mockRevokeMutation = { mutateAsync: vi.fn(), isPending: false };

vi.mock('@/hooks/queries', () => ({
  useLibraryShareLink: () => ({ data: null, isLoading: false }),
  useCreateShareLink: () => mockCreateMutation,
  useUpdateShareLink: () => mockUpdateMutation,
  useRevokeShareLink: () => mockRevokeMutation,
}));

vi.mock('@/components/layout/Toast', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

/** Hands the input to React with the string a browser would, which jsdom would otherwise blank. */
const typeRaw = (input: HTMLInputElement, value: string): void => {
  Object.defineProperty(input, 'value', { configurable: true, get: () => value });
  fireEvent.change(input);
};

describe('ShareLibraryModal — an expiry that is not a date (#4840)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockCreateMutation.mutateAsync.mockResolvedValue({});
  });

  it('does not save, and says why, when the expiry has a year of five digits', async () => {
    render(<ShareLibraryModal isOpen onClose={vi.fn()} />);
    typeRaw(document.getElementById('expires-at') as HTMLInputElement, '12345-10-30T20:00');

    fireEvent.click(screen.getByRole('button', { name: /crea link/i }));

    await waitFor(() =>
      expect(toast.error).toHaveBeenCalledWith('La data di scadenza non è valida.')
    );
    expect(mockCreateMutation.mutateAsync).not.toHaveBeenCalled();
  });

  it('still saves a real expiry, as the instant it names', async () => {
    // The other direction: refusing what is not a date must not refuse what is.
    render(<ShareLibraryModal isOpen onClose={vi.fn()} />);
    fireEvent.change(document.getElementById('expires-at') as HTMLInputElement, {
      target: { value: '2099-12-31T20:00' },
    });

    fireEvent.click(screen.getByRole('button', { name: /crea link/i }));

    await waitFor(() => expect(mockCreateMutation.mutateAsync).toHaveBeenCalledTimes(1));
    expect(mockCreateMutation.mutateAsync.mock.calls[0][0].expiresAt).toBe(
      new Date(2099, 11, 31, 20, 0).toISOString()
    );
    expect(toast.error).not.toHaveBeenCalled();
  });

  it('still saves with no expiry at all', async () => {
    render(<ShareLibraryModal isOpen onClose={vi.fn()} />);

    fireEvent.click(screen.getByRole('button', { name: /crea link/i }));

    await waitFor(() => expect(mockCreateMutation.mutateAsync).toHaveBeenCalledTimes(1));
    expect(mockCreateMutation.mutateAsync.mock.calls[0][0].expiresAt).toBeNull();
  });
});
