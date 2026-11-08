/**
 * @vitest-environment jsdom
 *
 * /admin/knowledge-base/feedback — #4469 (ADR-095 fetta 7a). La pagina chiedeva l'UUID del gioco,
 * digitato a mano, e senza quello non mostrava nulla. Ora si apre con il riepilogo per versione e la
 * lista, e un clic su una versione la filtra.
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const mockGetSummary = vi.fn();
const mockGetFeedback = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    knowledgeBase: {
      getAdminKbFeedbackSummary: (...args: unknown[]) => mockGetSummary(...args),
      getAdminKbFeedback: (...args: unknown[]) => mockGetFeedback(...args),
    },
  },
}));

import KbFeedbackPage from '../page';

const V4 = '44444444-4444-4444-4444-444444444444';

function makeWrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  );
}

describe('KbFeedbackPage', () => {
  beforeEach(() => {
    mockGetSummary.mockReset();
    mockGetFeedback.mockReset();
    mockGetSummary.mockResolvedValue({
      versions: [
        {
          profileVersionId: V4,
          versionNumber: 4,
          status: 'Published',
          publishedAt: null,
          helpful: 3,
          notHelpful: 1,
        },
        {
          profileVersionId: null,
          versionNumber: null,
          status: null,
          publishedAt: null,
          helpful: 1,
          notHelpful: 0,
        },
      ],
      games: [{ gameId: 'g', gameName: 'Catan' }],
    });
    mockGetFeedback.mockResolvedValue({ items: [], total: 0 });
  });

  it('shows the summary and the list without asking for a game UUID', async () => {
    render(<KbFeedbackPage />, { wrapper: makeWrapper() });

    expect(await screen.findByRole('region', { name: /per versione/i })).toBeInTheDocument();
    expect(screen.queryByPlaceholderText(/uuid/i)).not.toBeInTheDocument();
    await waitFor(() => expect(mockGetFeedback).toHaveBeenCalled());
    expect(mockGetFeedback.mock.calls.at(-1)?.[0]).toMatchObject({ version: undefined });
  });

  it('says so when the summary cannot be loaded, instead of a blank page', async () => {
    mockGetSummary.mockRejectedValue(new Error('500'));

    render(<KbFeedbackPage />, { wrapper: makeWrapper() });

    expect(await screen.findByRole('alert')).toHaveTextContent(/non è stato possibile/i);
  });

  it('filters the list by the version clicked in the summary', async () => {
    const user = userEvent.setup();
    render(<KbFeedbackPage />, { wrapper: makeWrapper() });

    await user.click(await screen.findByRole('button', { name: /v4/ }));

    await waitFor(() =>
      expect(mockGetFeedback.mock.calls.at(-1)?.[0]).toMatchObject({ version: V4 })
    );
  });
});
