/**
 * @vitest-environment jsdom
 *
 * KbFeedbackPanel — #1665 regression coverage and #4469 (feedback per profile version, no game UUID).
 *
 * #1665: switching a filter used to keep the stale `page` value, so a user paged past page 1 would
 * land on an empty page after filtering. Fix resets `page` to 1.
 */

import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import type {
  AdminKbFeedbackGame,
  AdminKbFeedbackItem,
  AdminKbFeedbackVersion,
} from '@/lib/api/schemas/knowledge-base.schemas';

const mockGetAdminKbFeedback = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    knowledgeBase: {
      getAdminKbFeedback: (...args: unknown[]) => mockGetAdminKbFeedback(...args),
    },
  },
}));

import { KbFeedbackPanel } from '../kb-feedback-panel';

const V4 = '44444444-4444-4444-4444-444444444444';
const V3 = '33333333-3333-3333-3333-333333333333';
const CATAN = 'cccccccc-cccc-cccc-cccc-cccccccccccc';

const VERSIONS: AdminKbFeedbackVersion[] = [
  {
    profileVersionId: V4,
    versionNumber: 4,
    status: 'Published',
    publishedAt: null,
    helpful: 3,
    notHelpful: 1,
  },
  {
    profileVersionId: V3,
    versionNumber: 3,
    status: 'Archived',
    publishedAt: null,
    helpful: 0,
    notHelpful: 2,
  },
  {
    profileVersionId: null,
    versionNumber: null,
    status: null,
    publishedAt: null,
    helpful: 1,
    notHelpful: 0,
  },
];
const GAMES: AdminKbFeedbackGame[] = [{ gameId: CATAN, gameName: 'Catan' }];

function makeItem(i: number, overrides: Partial<AdminKbFeedbackItem> = {}): AdminKbFeedbackItem {
  return {
    id: `fb-${i}`,
    userId: 'u',
    gameId: CATAN,
    gameName: 'Catan',
    chatSessionId: 't',
    messageId: `msg-${String(i).padStart(8, '0')}`,
    profileVersionId: V4,
    versionNumber: 4,
    outcome: 'helpful',
    comment: null,
    createdAt: '2026-05-30T12:00:00Z',
    answerExcerpt: `Risposta ${i}`,
    ...overrides,
  };
}

function makeWrapper() {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  );
}

function lastParams(): Record<string, unknown> | undefined {
  return mockGetAdminKbFeedback.mock.calls.at(-1)?.[0] as Record<string, unknown> | undefined;
}

function renderPanel(version: string | null = null, onVersionChange = vi.fn()) {
  return render(
    <KbFeedbackPanel
      versions={VERSIONS}
      games={GAMES}
      version={version}
      onVersionChange={onVersionChange}
    />,
    { wrapper: makeWrapper() }
  );
}

describe('KbFeedbackPanel — #1665 page reset on filter change', () => {
  beforeEach(() => {
    mockGetAdminKbFeedback.mockReset();
    mockGetAdminKbFeedback.mockResolvedValue({
      items: Array.from({ length: 20 }, (_, i) => makeItem(i)),
      total: 80,
    });
  });

  it('resets page to 1 when the outcome filter changes', async () => {
    const user = userEvent.setup();
    renderPanel();

    await waitFor(() => expect(mockGetAdminKbFeedback).toHaveBeenCalled());

    // Walk to page 3 — re-find between clicks because re-render can swap refs.
    await user.click(await screen.findByRole('button', { name: /succ/i }));
    await waitFor(() => expect(lastParams()?.page).toBe(2));
    await user.click(await screen.findByRole('button', { name: /succ/i }));
    await waitFor(() => expect(lastParams()?.page).toBe(3));

    await user.click(screen.getByRole('combobox', { name: 'Esito' }));
    await user.click(await screen.findByRole('option', { name: /^utili$/i }));

    // Bug: would fetch with page=3. Fix: page resets to 1.
    await waitFor(() => {
      expect(lastParams()?.outcome).toBe('helpful');
      expect(lastParams()?.page).toBe(1);
    });
  });

  it('resets page to 1 when the version changes from outside, e.g. the summary', async () => {
    const user = userEvent.setup();
    const { rerender } = renderPanel();

    await user.click(await screen.findByRole('button', { name: /succ/i }));
    await waitFor(() => expect(lastParams()?.page).toBe(2));

    rerender(
      <KbFeedbackPanel versions={VERSIONS} games={GAMES} version={V3} onVersionChange={vi.fn()} />
    );

    await waitFor(() => {
      expect(lastParams()?.version).toBe(V3);
      expect(lastParams()?.page).toBe(1);
    });
  });
});

describe('KbFeedbackPanel — #4469 per version, without a game UUID', () => {
  beforeEach(() => {
    mockGetAdminKbFeedback.mockReset();
    mockGetAdminKbFeedback.mockResolvedValue({
      items: [
        makeItem(1, { outcome: 'not_helpful', comment: 'Non risponde alla domanda' }),
        makeItem(2, { profileVersionId: null, versionNumber: null, answerExcerpt: null }),
      ],
      total: 2,
    });
  });

  it('loads every feedback without asking for a game', async () => {
    renderPanel();

    await waitFor(() => expect(mockGetAdminKbFeedback).toHaveBeenCalled());
    expect(lastParams()).toMatchObject({ version: undefined, gameId: undefined, page: 1 });
  });

  it('shows the game, the version and the answer being judged', async () => {
    renderPanel();

    const items = await screen.findAllByRole('article');
    expect(items[0]).toHaveTextContent('Catan');
    expect(items[0]).toHaveTextContent('v4');
    expect(items[0]).toHaveTextContent('Risposta 1');
    expect(items[0]).toHaveTextContent('Non risponde alla domanda');
    expect(items[1]).toHaveTextContent('Senza versione');
    expect(items[1]).toHaveTextContent('Risposta non più disponibile');
  });

  it('picks the game by name', async () => {
    const user = userEvent.setup();
    renderPanel();
    await waitFor(() => expect(mockGetAdminKbFeedback).toHaveBeenCalled());

    await user.click(screen.getByRole('combobox', { name: 'Gioco' }));
    await user.click(await screen.findByRole('option', { name: 'Catan' }));

    await waitFor(() => expect(lastParams()?.gameId).toBe(CATAN));
  });

  it('asks the page to change the version, including the one without a version', async () => {
    const user = userEvent.setup();
    const onVersionChange = vi.fn();
    renderPanel(null, onVersionChange);

    await user.click(screen.getByRole('combobox', { name: 'Versione del profilo' }));
    await user.click(await screen.findByRole('option', { name: 'Senza versione' }));

    expect(onVersionChange).toHaveBeenCalledWith('none');
  });

  it('sends the version it is given', async () => {
    renderPanel('none');

    await waitFor(() => expect(lastParams()?.version).toBe('none'));
  });
});
