/**
 * @vitest-environment jsdom
 *
 * KbFeedbackPanel — Issue #1665 regression coverage.
 *
 * Bug: switching the outcome filter (or the gameId prop) used to keep the
 * stale `page` value, so a user paged past page 1 would land on an empty
 * page after filtering. Fix resets `page` to 1 in both cases.
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const mockGetAdminKbFeedback = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    knowledgeBase: {
      getAdminKbFeedback: (...args: unknown[]) => mockGetAdminKbFeedback(...args),
    },
  },
}));

import { KbFeedbackPanel } from '../kb-feedback-panel';

const GAME_A = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
const GAME_B = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';

function makeWrapper() {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  );
}

function pageOfLastCall(): number | undefined {
  const last = mockGetAdminKbFeedback.mock.calls.at(-1);
  return last?.[1]?.page as number | undefined;
}

function outcomeOfLastCall(): string | undefined {
  const last = mockGetAdminKbFeedback.mock.calls.at(-1);
  return last?.[1]?.outcome as string | undefined;
}

describe('KbFeedbackPanel — #1665 page reset on filter change', () => {
  beforeEach(() => {
    mockGetAdminKbFeedback.mockReset();
    mockGetAdminKbFeedback.mockResolvedValue({
      items: Array.from({ length: 20 }, (_, i) => ({
        id: `fb-${i}`,
        messageId: `msg-${String(i).padStart(8, '0')}`,
        outcome: 'helpful' as const,
        comment: null,
        createdAt: '2026-05-30T12:00:00Z',
      })),
      total: 80,
      page: 1,
      pageSize: 20,
    });
  });

  it('resets page to 1 when the outcome filter changes', async () => {
    const user = userEvent.setup();
    render(<KbFeedbackPanel gameId={GAME_A} />, { wrapper: makeWrapper() });

    await waitFor(() => expect(mockGetAdminKbFeedback).toHaveBeenCalled());

    // Walk to page 3 — re-find between clicks because re-render can swap refs.
    await user.click(await screen.findByRole('button', { name: /succ/i }));
    await waitFor(() => expect(pageOfLastCall()).toBe(2));
    await user.click(await screen.findByRole('button', { name: /succ/i }));
    await waitFor(() => expect(pageOfLastCall()).toBe(3));

    // Open the filter and pick "Utili"
    await user.click(screen.getByRole('combobox'));
    await user.click(await screen.findByRole('option', { name: /^utili$/i }));

    // Bug: would fetch with page=3. Fix: page resets to 1.
    await waitFor(() => {
      expect(outcomeOfLastCall()).toBe('helpful');
      expect(pageOfLastCall()).toBe(1);
    });
  });

  it('resets page to 1 when the gameId prop changes', async () => {
    const user = userEvent.setup();
    const { rerender } = render(<KbFeedbackPanel gameId={GAME_A} />, { wrapper: makeWrapper() });

    await waitFor(() => expect(mockGetAdminKbFeedback).toHaveBeenCalled());

    const next = await screen.findByRole('button', { name: /succ/i });
    await user.click(next);
    await waitFor(() => expect(pageOfLastCall()).toBe(2));

    rerender(<KbFeedbackPanel gameId={GAME_B} />);

    await waitFor(() => {
      const lastCall = mockGetAdminKbFeedback.mock.calls.at(-1);
      expect(lastCall?.[0]).toBe(GAME_B);
      expect(pageOfLastCall()).toBe(1);
    });
  });
});
