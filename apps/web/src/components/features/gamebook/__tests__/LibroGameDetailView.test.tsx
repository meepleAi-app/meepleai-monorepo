import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, within, type RenderResult } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { axe, toHaveNoViolations } from 'jest-axe';
import type { ReactElement, ReactNode } from 'react';
import { IntlProvider } from 'react-intl';

import itMessages from '@/locales/it.json';
import type { LibraryGameDetail } from '@/hooks/queries/useLibrary';
import { GameBookRole, type GameBookDto } from '@/lib/api/gamebook';
import { LibroGameDetailView } from '../LibroGameDetailView';

expect.extend(toHaveNoViolations);

// GameBookList (mounted in the info tab) consumes `useTranslation`, so the view
// must render under an IntlProvider. Flatten the nested catalogue for react-intl.
function flatten(obj: Record<string, unknown>, prefix = ''): Record<string, string> {
  return Object.keys(obj).reduce(
    (acc, key) => {
      const full = prefix ? `${prefix}.${key}` : key;
      const value = obj[key];
      if (value && typeof value === 'object') {
        Object.assign(acc, flatten(value as Record<string, unknown>, full));
      } else {
        acc[full] = String(value);
      }
      return acc;
    },
    {} as Record<string, string>
  );
}
const FLAT_IT = flatten(itMessages as Record<string, unknown>);

function renderView(ui: ReactElement): RenderResult {
  function Wrapper({ children }: { children: ReactNode }) {
    return (
      <IntlProvider locale="it" messages={FLAT_IT} onError={() => {}}>
        {children}
      </IntlProvider>
    );
  }
  return render(ui, { wrapper: Wrapper });
}

function makeGameBook(overrides: Partial<GameBookDto> = {}): GameBookDto {
  return {
    id: 'gb-1',
    gameRefId: 'g1',
    gameRefKind: 0,
    ownerUserId: null,
    displayName: 'Manuale Base',
    roles: GameBookRole.RulesReference,
    paragraphScheme: 0,
    language: 'it',
    sequentialRead: false,
    kbSourceDocId: 'kb-1',
    physicalOnly: false,
    createdAt: '2026-07-01T00:00:00Z',
    ...overrides,
  };
}

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

// #2750 C1: the AI Chat tab opens the global chat panel via chat-panel-store.
const mockOpenChat = vi.hoisted(() => vi.fn());
vi.mock('@/lib/stores/chat-panel-store', () => ({
  useChatPanelStore: (selector: (s: { open: typeof mockOpenChat }) => unknown) =>
    selector({ open: mockOpenChat }),
}));

vi.mock('@/components/features/gamebook/NanolithCampaignCTA', () => ({
  NanolithCampaignCTA: ({ gameId, gameTitle }: { gameId: string; gameTitle: string }) => (
    <div data-testid="mock-nanolith-cta">
      CTA {gameId} {gameTitle}
    </div>
  ),
}));

// ─── Fixture helper ─────────────────────────────────────────────────────────

function makeLibraryGameDetail(overrides: Partial<LibraryGameDetail> = {}): LibraryGameDetail {
  return {
    libraryEntryId: 'l1',
    userId: 'u1',
    gameId: 'g1',
    addedAt: '2026-01-01T00:00:00Z',
    notes: null,
    isFavorite: false,
    currentState: 'owned',
    stateChangedAt: null,
    stateNotes: null,
    isAvailableForPlay: true,
    hasCustomPdf: false,
    hasRagAccess: false,
    gameTitle: 'Nanolith',
    gamePublisher: 'Kosmos',
    gameYearPublished: 2024,
    gameIconUrl: null,
    gameImageUrl: null,
    description: 'Test description',
    minPlayers: 2,
    maxPlayers: 4,
    playingTimeMinutes: 90,
    complexityRating: null,
    averageRating: 7.5,
    timesPlayed: 3,
    lastPlayed: null,
    winRate: null,
    avgDuration: null,
    ...overrides,
  };
}

// ─── Tests ──────────────────────────────────────────────────────────────────

describe('LibroGameDetailView', () => {
  beforeEach(() => {
    mockOpenChat.mockClear();
  });

  it('renders default tab "info" with InfoPanel', () => {
    const gameDetail = makeLibraryGameDetail();
    renderView(<LibroGameDetailView gameDetail={gameDetail} />);

    expect(screen.getByText('Descrizione')).toBeVisible();
    expect(screen.getByText('Test description')).toBeVisible();
    expect(screen.getByText('Knowledge base')).toBeVisible();
  });

  it('SI-6: renders the 1..N GameBook list in the info tab', () => {
    const gameDetail = makeLibraryGameDetail();
    const books = [
      makeGameBook({ id: 'a', displayName: 'Manuale Base' }),
      makeGameBook({ id: 'b', displayName: 'Storybook', roles: GameBookRole.Narrative }),
    ];
    renderView(<LibroGameDetailView gameDetail={gameDetail} books={books} />);

    expect(screen.getByRole('heading', { name: 'Libri' })).toBeVisible();
    const list = screen.getByTestId('game-book-list');
    expect(within(list).getByText('Manuale Base')).toBeVisible();
    expect(within(list).getByText('Storybook')).toBeVisible();
  });

  it('SI-6: shows the graceful empty state when the game has no books', () => {
    const gameDetail = makeLibraryGameDetail();
    renderView(<LibroGameDetailView gameDetail={gameDetail} books={[]} />);

    expect(screen.getByTestId('game-book-list-empty')).toBeVisible();
  });

  it('renders all 4 MetaStat cells with formatted values', () => {
    const gameDetail = makeLibraryGameDetail({
      minPlayers: 2,
      maxPlayers: 4,
      playingTimeMinutes: 90,
      averageRating: 7.5,
      gameYearPublished: 2024,
    });
    renderView(<LibroGameDetailView gameDetail={gameDetail} />);

    expect(screen.getByText('2–4')).toBeVisible();
    expect(screen.getByText('1–2h')).toBeVisible();
    expect(screen.getByText('7.5')).toBeVisible();
    expect(screen.getByText('2024')).toBeVisible();

    // MetaStat labels are inside the meta grid div
    const metaLabels = screen.getAllByText('giocatori');
    expect(metaLabels[0]).toBeVisible();
    expect(screen.getByText('durata')).toBeVisible();
    expect(screen.getByText('BGG')).toBeVisible();
    expect(screen.getByText('anno')).toBeVisible();
  });

  it('renders 5 Pip chips with correct aria-labels', () => {
    const gameDetail = makeLibraryGameDetail({
      chunkCount: 3,
      sessionsCount: 5,
      hasRagAccess: true,
      timesPlayed: 2,
    });
    renderView(<LibroGameDetailView gameDetail={gameDetail} />);

    expect(screen.getByLabelText('KB 3')).toBeVisible();
    expect(screen.getByLabelText('chat 0')).toBeVisible();
    expect(screen.getByLabelText('Tutor 1')).toBeVisible();
    expect(screen.getByLabelText('giocatori 0')).toBeVisible();
    expect(screen.getByLabelText('partite 5')).toBeVisible();
  });

  // #2750 C1: the 3 non-Info tabs are wired to real surfaces (no more "in arrivo" placeholder).

  it('C1: AI Chat tab opens the global chat panel with the game context', async () => {
    const gameDetail = makeLibraryGameDetail({
      gameId: 'g1',
      gameTitle: 'Nanolith',
      chunkCount: 4,
    });
    renderView(<LibroGameDetailView gameDetail={gameDetail} />);
    const user = userEvent.setup();

    await user.click(screen.getByRole('tab', { name: 'AI Chat' }));
    const chatPanel = screen.getByRole('tabpanel');
    expect(chatPanel).not.toHaveTextContent(/in arrivo con la prossima iter/);

    await user.click(within(chatPanel).getByRole('button', { name: /chat/i }));
    expect(mockOpenChat).toHaveBeenCalledTimes(1);
    expect(mockOpenChat).toHaveBeenCalledWith(
      expect.objectContaining({ id: 'g1', name: 'Nanolith' })
    );
  });

  it('C1: Toolbox tab links to the game toolbox sub-page', async () => {
    const gameDetail = makeLibraryGameDetail({ gameId: 'g1' });
    renderView(<LibroGameDetailView gameDetail={gameDetail} />);
    const user = userEvent.setup();

    await user.click(screen.getByRole('tab', { name: 'Toolbox' }));
    const panel = screen.getByRole('tabpanel');
    expect(panel).not.toHaveTextContent(/in arrivo con la prossima iter/);
    const link = within(panel).getByRole('link');
    expect(link).toHaveAttribute('href', '/library/g1/toolbox');
  });

  it('C1: Toolkit tab links to the game toolkit sub-page', async () => {
    const gameDetail = makeLibraryGameDetail({ gameId: 'g1' });
    renderView(<LibroGameDetailView gameDetail={gameDetail} />);
    const user = userEvent.setup();

    await user.click(screen.getByRole('tab', { name: 'Toolkit' }));
    const panel = screen.getByRole('tabpanel');
    expect(panel).not.toHaveTextContent(/in arrivo con la prossima iter/);
    const link = within(panel).getByRole('link');
    expect(link).toHaveAttribute('href', '/library/g1/toolkit');
  });

  it("KB badge variant: kbStatus='indexing'", () => {
    const gameDetail = makeLibraryGameDetail({ kbStatus: 'indexing' });
    renderView(<LibroGameDetailView gameDetail={gameDetail} />);

    expect(screen.getByText('Indicizzazione in corso…')).toBeVisible();
    expect(screen.getByText(/pipeline OCR\/embedding/)).toBeVisible();
  });

  it("KB badge variant: kbStatus='error'", () => {
    const gameDetail = makeLibraryGameDetail({ kbStatus: 'error' });
    renderView(<LibroGameDetailView gameDetail={gameDetail} />);

    // The source uses smart quote in "l'indicizzazione" — use regex to match
    expect(screen.getByText(/Errore durante l.indicizzazione/)).toBeVisible();
    expect(screen.getByText(/hanno fallito il processing/)).toBeVisible();
  });

  it('jest-axe smoke — no a11y violations on default render', async () => {
    const gameDetail = makeLibraryGameDetail();
    const { container } = renderView(<LibroGameDetailView gameDetail={gameDetail} />);

    // T12: heading-order rule re-enabled — all consumers now pass headingLevel prop
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
