/**
 * EditGameDrawer tests.
 *
 * Issue #4090: the drawer used to fabricate 'https://placeholder.example/cover.png' to satisfy a
 * NotEmpty rule on the #2123 deprecation-tombstone columns, persisting a fake URL on every save.
 * The backend now accepts empty, so the payload must carry '' instead.
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { vi, describe, it, expect, beforeEach } from 'vitest';

import { EditGameDrawer, type EditGameTarget } from '../EditGameDrawer';

// ─── Mocks ───────────────────────────────────────────────────────────────────

const mockUpdate = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: {
      update: (...args: unknown[]) => mockUpdate(...args),
    },
  },
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

// ─── Helpers ─────────────────────────────────────────────────────────────────

const createWrapper = () => {
  const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  );
};

/** A game as it comes back post-#2123: the tombstone columns are empty. */
const gameWithoutCover: EditGameTarget = {
  id: '11111111-1111-1111-1111-111111111111',
  title: 'Wingspan',
  description: 'Bird-themed engine builder',
  yearPublished: 2019,
  minPlayers: 1,
  maxPlayers: 5,
  playingTimeMinutes: 60,
  minAge: 10,
  imageUrl: '',
  thumbnailUrl: '',
};

const renderDrawer = (game: EditGameTarget = gameWithoutCover) =>
  render(<EditGameDrawer open onOpenChange={vi.fn()} game={game} />, {
    wrapper: createWrapper(),
  });

/**
 * The save button is `disabled={!isDirty}`, so a pristine form cannot submit. Editing the title is
 * also the exact scenario the defect was reported from: an admin changes one field and saves.
 */
const submit = async () => {
  fireEvent.change(screen.getByLabelText(/titolo/i), {
    target: { value: 'Wingspan ITA' },
  });
  fireEvent.click(screen.getByTestId('edit-game-submit'));
  await waitFor(() => expect(mockUpdate).toHaveBeenCalled());
};

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('EditGameDrawer', () => {
  beforeEach(() => {
    mockUpdate.mockReset();
    mockUpdate.mockResolvedValue(undefined);
  });

  it('sends empty strings for the tombstone URL columns instead of a fabricated placeholder', async () => {
    renderDrawer();

    await submit();

    const [, payload] = mockUpdate.mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.imageUrl).toBe('');
    expect(payload.thumbnailUrl).toBe('');
    expect(JSON.stringify(payload)).not.toContain('placeholder.example');
  });

  it('still forwards a real cover URL when the game carries one', async () => {
    renderDrawer({
      ...gameWithoutCover,
      imageUrl: 'https://cdn.example.com/cover.webp',
      thumbnailUrl: 'https://cdn.example.com/thumb.webp',
    });

    await submit();

    const [, payload] = mockUpdate.mock.calls[0] as [string, Record<string, unknown>];
    expect(payload.imageUrl).toBe('https://cdn.example.com/cover.webp');
    expect(payload.thumbnailUrl).toBe('https://cdn.example.com/thumb.webp');
  });

  it('does not send taxonomy keys, so the backend leaves categories and mechanics alone', async () => {
    renderDrawer();

    await submit();

    const [, payload] = mockUpdate.mock.calls[0] as [string, Record<string, unknown>];
    expect(payload).not.toHaveProperty('categories');
    expect(payload).not.toHaveProperty('mechanics');
    expect(payload).not.toHaveProperty('designers');
    expect(payload).not.toHaveProperty('publishers');
  });
});
