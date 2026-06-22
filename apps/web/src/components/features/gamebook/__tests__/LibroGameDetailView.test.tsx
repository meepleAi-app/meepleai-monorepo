import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { axe, toHaveNoViolations } from 'jest-axe';

import type { LibraryGameDetail } from '@/hooks/queries/useLibrary';
import { LibroGameDetailView } from '../LibroGameDetailView';

expect.extend(toHaveNoViolations);

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
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
  it('renders default tab "info" with InfoPanel', () => {
    const gameDetail = makeLibraryGameDetail();
    render(<LibroGameDetailView gameDetail={gameDetail} />);

    expect(screen.getByText('Descrizione')).toBeVisible();
    expect(screen.getByText('Test description')).toBeVisible();
    expect(screen.getByText('Knowledge base')).toBeVisible();
  });

  it('renders all 4 MetaStat cells with formatted values', () => {
    const gameDetail = makeLibraryGameDetail({
      minPlayers: 2,
      maxPlayers: 4,
      playingTimeMinutes: 90,
      averageRating: 7.5,
      gameYearPublished: 2024,
    });
    render(<LibroGameDetailView gameDetail={gameDetail} />);

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
    render(<LibroGameDetailView gameDetail={gameDetail} />);

    expect(screen.getByLabelText('KB 3')).toBeVisible();
    expect(screen.getByLabelText('chat 0')).toBeVisible();
    expect(screen.getByLabelText('Tutor 1')).toBeVisible();
    expect(screen.getByLabelText('giocatori 0')).toBeVisible();
    expect(screen.getByLabelText('partite 5')).toBeVisible();
  });

  it('switches tabs and shows placeholder text', async () => {
    const gameDetail = makeLibraryGameDetail();
    render(<LibroGameDetailView gameDetail={gameDetail} />);
    const user = userEvent.setup();

    // Click AI Chat tab
    await user.click(screen.getByRole('tab', { name: 'AI Chat' }));
    expect(screen.getByText(/Pannello/i)).toBeVisible();
    // Verify placeholder panel is visible by checking both the label and complete text together
    const chatPanel = screen.getByRole('tabpanel');
    expect(chatPanel).toHaveTextContent('Pannello');
    expect(chatPanel).toHaveTextContent('AI Chat');
    expect(chatPanel).toHaveTextContent(/in arrivo con la prossima iter/);

    // Click Toolbox tab
    await user.click(screen.getByRole('tab', { name: 'Toolbox' }));
    const toolboxPanel = screen.getByRole('tabpanel');
    expect(toolboxPanel).toHaveTextContent('Toolbox');
    expect(toolboxPanel).toHaveTextContent(/in arrivo con la prossima iter/);

    // Click Toolkit tab
    await user.click(screen.getByRole('tab', { name: 'Toolkit' }));
    const toolkitPanel = screen.getByRole('tabpanel');
    expect(toolkitPanel).toHaveTextContent('Toolkit');
    expect(toolkitPanel).toHaveTextContent(/in arrivo con la prossima iter/);
  });

  it("KB badge variant: kbStatus='indexing'", () => {
    const gameDetail = makeLibraryGameDetail({ kbStatus: 'indexing' });
    render(<LibroGameDetailView gameDetail={gameDetail} />);

    expect(screen.getByText('Indicizzazione in corso…')).toBeVisible();
    expect(screen.getByText(/pipeline OCR\/embedding/)).toBeVisible();
  });

  it("KB badge variant: kbStatus='error'", () => {
    const gameDetail = makeLibraryGameDetail({ kbStatus: 'error' });
    render(<LibroGameDetailView gameDetail={gameDetail} />);

    // The source uses smart quote in "l'indicizzazione" — use regex to match
    expect(screen.getByText(/Errore durante l.indicizzazione/)).toBeVisible();
    expect(screen.getByText(/hanno fallito il processing/)).toBeVisible();
  });

  it('jest-axe smoke — no a11y violations on default render', async () => {
    const gameDetail = makeLibraryGameDetail();
    const { container } = render(<LibroGameDetailView gameDetail={gameDetail} />);

    // T12: heading-order rule re-enabled — all consumers now pass headingLevel prop
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
