import React from 'react';

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { GameBackContent, GameBackData, GameBackActions } from '../GameBackContent';

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

const defaultData: GameBackData = {
  complexityRating: 3.2,
  playingTimeMinutes: 90,
  minPlayers: 2,
  maxPlayers: 4,
  averageRating: 7.8,
  timesPlayed: 12,
  kbDocuments: [
    { id: '1', fileName: 'rules.pdf', status: 'Ready' },
    { id: '2', fileName: 'faq.pdf', status: 'processing' },
  ],
  hasKb: true,
  kbCardCount: 5,
  lastPlayedLabel: '3 giorni fa',
  winRate: 65,
  entityLinkCount: 3,
  noteCount: 2,
};

const defaultActions: GameBackActions = {
  onChatAgent: vi.fn(),
  onToggleFavorite: vi.fn(),
  isFavorite: false,
  onViewLinks: vi.fn(),
};

function renderComponent(
  dataOverrides?: Partial<GameBackData>,
  actionsOverrides?: Partial<GameBackActions> | null,
  props?: { title?: string; subtitle?: string; detailHref?: string; entityColor?: string }
) {
  const data = { ...defaultData, ...dataOverrides };
  const actions =
    actionsOverrides === null ? undefined : { ...defaultActions, ...actionsOverrides };
  return render(
    <GameBackContent
      data={data}
      actions={actions}
      title={props?.title ?? 'Catan'}
      subtitle={props?.subtitle}
      detailHref={props?.detailHref ?? '/games/catan'}
      entityColor={props?.entityColor}
    />
  );
}

// ============================================================================
// GameBackContent — basic render
// ============================================================================

describe('GameBackContent', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders with all data provided', () => {
    renderComponent();
    expect(screen.getByTestId('game-back-content')).toBeInTheDocument();
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('falls back to "Statistiche" when no title', () => {
    renderComponent({}, {}, { title: '' });
    expect(screen.getByText('Statistiche')).toBeInTheDocument();
  });
});

// ============================================================================
// Enriched Header (Issue #336)
// ============================================================================

describe('Enriched Header', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows subtitle when provided', () => {
    renderComponent({}, {}, { title: 'Agricola', subtitle: 'Lookout Games' });
    expect(screen.getByText('Lookout Games')).toBeInTheDocument();
  });

  it('omits subtitle when not provided', () => {
    renderComponent({}, {}, { title: 'Agricola' });
    const title = screen.getByText('Agricola');
    const header = title.closest('div');
    // Only title h2 should be present, no subtitle <p>
    const paragraphs = header?.querySelectorAll('p');
    expect(paragraphs?.length ?? 0).toBe(0);
  });
});

// ============================================================================
// InfoChips
// ============================================================================

describe('InfoChips', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders player range chip', () => {
    renderComponent({ minPlayers: 2, maxPlayers: 4 });
    expect(screen.getByText('2-4')).toBeInTheDocument();
  });

  it('renders single player count when min equals max', () => {
    renderComponent({ minPlayers: 3, maxPlayers: 3, entityLinkCount: 0 });
    const infoChips = screen.getByTestId('info-chips');
    expect(infoChips).toHaveTextContent('3');
  });

  it('renders playing time chip', () => {
    renderComponent({ playingTimeMinutes: 90 });
    expect(screen.getByText('90m')).toBeInTheDocument();
  });

  it('renders 5 complexity dots', () => {
    renderComponent({ complexityRating: 3.2 });
    const dotsContainer = screen.getByTestId('complexity-dots');
    const dots = dotsContainer.querySelectorAll('span.rounded-full');
    expect(dots).toHaveLength(5);
  });

  it('renders filled dots matching rounded complexity', () => {
    renderComponent({ complexityRating: 3.2 });
    const dotsContainer = screen.getByTestId('complexity-dots');
    const dots = dotsContainer.querySelectorAll('span.rounded-full');
    const filled = Array.from(dots).filter(d => d.classList.contains('bg-current'));
    expect(filled).toHaveLength(3);
  });

  it('hides complexity chip when null', () => {
    renderComponent({ complexityRating: null });
    expect(screen.queryByTestId('complexity-dots')).not.toBeInTheDocument();
  });
});

// ============================================================================
// Stats Row (Issue #336)
// ============================================================================

describe('Stats Row', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders stats row when timesPlayed is provided', () => {
    renderComponent({ timesPlayed: 12 });
    expect(screen.getByTestId('stats-row')).toBeInTheDocument();
    expect(screen.getByText('12')).toBeInTheDocument();
    expect(screen.getByText('Partite')).toBeInTheDocument();
  });

  it('renders stats row with timesPlayed=0 (falsy but not null)', () => {
    renderComponent({ timesPlayed: 0 });
    expect(screen.getByTestId('stats-row')).toBeInTheDocument();
    expect(screen.getByText('0')).toBeInTheDocument();
  });

  it('renders win rate formatted as percentage', () => {
    renderComponent({ winRate: 67 });
    expect(screen.getByText('67%')).toBeInTheDocument();
    expect(screen.getByText('Vittorie')).toBeInTheDocument();
  });

  it('renders winRate 0 as "0%"', () => {
    renderComponent({ winRate: 0 });
    expect(screen.getByTestId('stats-row')).toBeInTheDocument();
    expect(screen.getByText('0%')).toBeInTheDocument();
  });

  it('renders total play time as hours when >= 60 minutes', () => {
    renderComponent({ totalPlayTimeMinutes: 180 });
    expect(screen.getByText('3h')).toBeInTheDocument();
    expect(screen.getByText('Tempo')).toBeInTheDocument();
  });

  it('renders total play time as minutes when < 60', () => {
    renderComponent({ totalPlayTimeMinutes: 45 });
    expect(screen.getByText('45m')).toBeInTheDocument();
  });

  it('renders totalPlayTimeMinutes=0 as "0m"', () => {
    renderComponent({ totalPlayTimeMinutes: 0, timesPlayed: undefined, winRate: undefined, lastPlayedLabel: undefined });
    expect(screen.getByTestId('stats-row')).toBeInTheDocument();
    expect(screen.getByText('0m')).toBeInTheDocument();
  });

  it('renders lastPlayedLabel in stats row', () => {
    renderComponent({ lastPlayedLabel: '2 giorni fa', timesPlayed: 5 });
    expect(screen.getByText('2 giorni fa')).toBeInTheDocument();
  });

  it('renders stats row with only lastPlayedLabel (no numeric stats)', () => {
    renderComponent({ timesPlayed: undefined, winRate: undefined, totalPlayTimeMinutes: undefined, lastPlayedLabel: 'Ieri' });
    expect(screen.getByTestId('stats-row')).toBeInTheDocument();
    expect(screen.getByText('Ieri')).toBeInTheDocument();
  });

  it('hides stats row when all stats are undefined/null', () => {
    renderComponent({
      timesPlayed: undefined,
      winRate: undefined,
      totalPlayTimeMinutes: undefined,
      lastPlayedLabel: undefined,
    });
    expect(screen.queryByTestId('stats-row')).not.toBeInTheDocument();
  });

  it('shows separators between multiple stats', () => {
    renderComponent({ timesPlayed: 5, winRate: 60, totalPlayTimeMinutes: undefined, lastPlayedLabel: undefined });
    const statsRow = screen.getByTestId('stats-row');
    expect(statsRow.querySelectorAll('[data-separator]')).toHaveLength(1);
  });
});

// ============================================================================
// KB Summary (Issue #336)
// ============================================================================

describe('KB Summary', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders condensed KB summary when hasKb is true', () => {
    renderComponent({ hasKb: true, kbCardCount: 5, kbDocuments: [
      { id: '1', fileName: 'rules.pdf', status: 'Ready' },
      { id: '2', fileName: 'faq.pdf', status: 'Ready' },
      { id: '3', fileName: 'errata.pdf', status: 'Ready' },
    ]});
    expect(screen.getByTestId('kb-summary')).toBeInTheDocument();
    expect(screen.getByText(/3 documenti/)).toBeInTheDocument();
  });

  it('shows green status when all KB docs are ready', () => {
    renderComponent({ hasKb: true, kbDocuments: [
      { id: '1', fileName: 'rules.pdf', status: 'Ready' },
    ]});
    const badge = screen.getByTestId('kb-status-badge');
    expect(badge).toHaveTextContent('Pronta');
  });

  it('shows amber status when some KB docs are processing', () => {
    renderComponent({ hasKb: true, kbDocuments: [
      { id: '1', fileName: 'rules.pdf', status: 'Ready' },
      { id: '2', fileName: 'faq.pdf', status: 'Processing' },
    ]});
    const badge = screen.getByTestId('kb-status-badge');
    expect(badge).toHaveTextContent('In elaborazione');
  });

  it('hides KB summary when hasKb is false', () => {
    renderComponent({ hasKb: false, kbDocuments: [] });
    expect(screen.queryByTestId('kb-summary')).not.toBeInTheDocument();
  });

  it('hides KB summary when kbDocuments is empty', () => {
    renderComponent({ hasKb: true, kbDocuments: [] });
    expect(screen.queryByTestId('kb-summary')).not.toBeInTheDocument();
  });
});

// ============================================================================
// Tag Pills (Issue #336)
// ============================================================================

describe('Tag Pills', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders category and mechanic tags as pills', () => {
    renderComponent({
      categories: ['Strategia', 'Famiglia'],
      mechanics: ['Worker Placement'],
    });
    expect(screen.getByTestId('tag-pills')).toBeInTheDocument();
    expect(screen.getByText('Strategia')).toBeInTheDocument();
    expect(screen.getByText('Famiglia')).toBeInTheDocument();
    expect(screen.getByText('Worker Placement')).toBeInTheDocument();
  });

  it('shows max 6 tags with +N overflow pill', () => {
    renderComponent({
      categories: ['Cat1', 'Cat2', 'Cat3', 'Cat4'],
      mechanics: ['Mech1', 'Mech2', 'Mech3'],
    });
    // 7 total, should show 6 + "+1"
    expect(screen.getByText('+1')).toBeInTheDocument();
  });

  it('shows all tags when 6 or fewer', () => {
    renderComponent({
      categories: ['Cat1', 'Cat2'],
      mechanics: ['Mech1'],
    });
    expect(screen.queryByText(/^\+\d+$/)).not.toBeInTheDocument();
  });

  it('renders with only categories (no mechanics)', () => {
    renderComponent({ categories: ['Strategia'], mechanics: undefined });
    expect(screen.getByTestId('tag-pills')).toBeInTheDocument();
    expect(screen.getByText('Strategia')).toBeInTheDocument();
  });

  it('hides when both categories and mechanics are empty', () => {
    renderComponent({ categories: [], mechanics: [] });
    expect(screen.queryByTestId('tag-pills')).not.toBeInTheDocument();
  });

  it('hides when both categories and mechanics are undefined', () => {
    renderComponent({ categories: undefined, mechanics: undefined });
    expect(screen.queryByTestId('tag-pills')).not.toBeInTheDocument();
  });

  it('overflow pill has singular aria-label for +1', () => {
    renderComponent({
      categories: ['C1', 'C2', 'C3', 'C4', 'C5', 'C6', 'C7'],
    });
    const overflow = screen.getByText('+1');
    expect(overflow).toHaveAttribute('aria-label', '1 altro tag');
  });

  it('overflow pill has plural aria-label for +2 or more', () => {
    renderComponent({
      categories: ['C1', 'C2', 'C3', 'C4', 'C5', 'C6', 'C7', 'C8'],
    });
    const overflow = screen.getByText('+2');
    expect(overflow).toHaveAttribute('aria-label', '2 altri tag');
  });
});

// ============================================================================
// Navigation Links (Issue #336)
// ============================================================================

describe('Navigation Links', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders session link when handler and count provided', () => {
    const onViewSessions = vi.fn();
    renderComponent({ sessionCount: 7, entityLinkCount: 0 }, { onViewSessions, onViewLinks: undefined });
    expect(screen.getByText('Sessioni')).toBeInTheDocument();
    expect(screen.getByText('7')).toBeInTheDocument();
  });

  it('renders note link when handler and count provided', () => {
    const onViewNotes = vi.fn();
    renderComponent({ noteCount: 2 }, { onViewNotes });
    expect(screen.getByText('Note')).toBeInTheDocument();
    expect(screen.getByText('2')).toBeInTheDocument();
  });

  it('renders entity link when handler and count provided', () => {
    const onViewLinks = vi.fn();
    renderComponent({ entityLinkCount: 5 }, { onViewLinks });
    expect(screen.getByText('Collegamenti')).toBeInTheDocument();
    expect(screen.getByText('5')).toBeInTheDocument();
  });

  it('calls handler on click with stopPropagation', () => {
    const onViewSessions = vi.fn();
    renderComponent({ sessionCount: 3 }, { onViewSessions });
    const btn = screen.getByText('Sessioni').closest('button')!;
    const event = new MouseEvent('click', { bubbles: true });
    const stopProp = vi.spyOn(event, 'stopPropagation');
    fireEvent(btn, event);
    expect(onViewSessions).toHaveBeenCalledTimes(1);
    expect(stopProp).toHaveBeenCalled();
  });

  it('hides nav links when no handler has a count > 0', () => {
    renderComponent(
      { sessionCount: 0, noteCount: 0, entityLinkCount: 0 },
      { onViewSessions: vi.fn(), onViewNotes: vi.fn(), onViewLinks: vi.fn() }
    );
    expect(screen.queryByTestId('nav-links')).not.toBeInTheDocument();
  });

  it('hides nav links when no handlers provided', () => {
    renderComponent(
      { sessionCount: 5, noteCount: 2 },
      { onViewSessions: undefined, onViewNotes: undefined, onViewLinks: undefined }
    );
    expect(screen.queryByTestId('nav-links')).not.toBeInTheDocument();
  });

  it('nav link buttons are keyboard accessible', () => {
    const onViewSessions = vi.fn();
    renderComponent({ sessionCount: 1 }, { onViewSessions });
    const btn = screen.getByText('Sessioni').closest('button')!;
    expect(btn.tagName).toBe('BUTTON');
    expect(btn).not.toBeDisabled();
  });
});

// ============================================================================
// Compact Footer (Issue #336)
// ============================================================================

describe('Compact Footer', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders filled heart when isFavorite is true', () => {
    renderComponent({}, { isFavorite: true });
    const favBtn = screen.getByTestId('favorite-toggle');
    expect(favBtn.querySelector('[data-filled="true"]')).toBeInTheDocument();
  });

  it('renders outline heart when isFavorite is false', () => {
    renderComponent({}, { isFavorite: false });
    const favBtn = screen.getByTestId('favorite-toggle');
    expect(favBtn.querySelector('[data-filled="false"]')).toBeInTheDocument();
  });

  it('heart toggle has aria-pressed attribute', () => {
    renderComponent({}, { isFavorite: true });
    const favBtn = screen.getByTestId('favorite-toggle');
    expect(favBtn).toHaveAttribute('aria-pressed', 'true');
  });

  it('heart toggle responds to click', () => {
    const onToggleFavorite = vi.fn();
    renderComponent({}, { onToggleFavorite, isFavorite: false });
    fireEvent.click(screen.getByTestId('favorite-toggle'));
    expect(onToggleFavorite).toHaveBeenCalledTimes(1);
  });

  it('shows BGG weight when provided', () => {
    renderComponent({ bggWeight: 3.2 });
    expect(screen.getByText('BGG 3.2')).toBeInTheDocument();
  });

  it('shows best player count when provided', () => {
    renderComponent({ bestPlayerCount: 4 });
    expect(screen.getByText('Best 4p')).toBeInTheDocument();
  });

  it('hides meta pills when not provided', () => {
    renderComponent({ bggWeight: undefined, bestPlayerCount: undefined });
    expect(screen.queryByText(/BGG/)).not.toBeInTheDocument();
    expect(screen.queryByText(/Best/)).not.toBeInTheDocument();
  });

  it('"Dettaglio →" link is always present', () => {
    renderComponent({}, {}, { detailHref: '/games/catan' });
    const link = screen.getByTestId('game-detail-link');
    expect(link).toHaveAttribute('href', '/games/catan');
    expect(link).toHaveTextContent('Dettaglio →');
  });

  it('rejects unsafe href', () => {
    renderComponent({}, {}, { detailHref: '//evil.com' });
    expect(screen.queryByTestId('game-detail-link')).not.toBeInTheDocument();
  });

  it('rejects javascript href', () => {
    renderComponent({}, {}, { detailHref: 'javascript:alert(1)' });
    expect(screen.queryByTestId('game-detail-link')).not.toBeInTheDocument();
  });
});

// ============================================================================
// Graceful Degradation (Issue #336)
// ============================================================================

describe('Graceful Degradation', () => {
  it('renders only header + footer when no optional data', () => {
    renderComponent(
      {
        complexityRating: null,
        playingTimeMinutes: null,
        minPlayers: null,
        maxPlayers: null,
        averageRating: null,
        timesPlayed: undefined,
        winRate: undefined,
        totalPlayTimeMinutes: undefined,
        lastPlayedLabel: undefined,
        categories: undefined,
        mechanics: undefined,
        bggWeight: undefined,
        bestPlayerCount: undefined,
        hasKb: false,
        kbDocuments: [],
        sessionCount: undefined,
        noteCount: 0,
        entityLinkCount: 0,
      },
      null,
      { title: 'Bare Game', detailHref: '/games/bare' }
    );

    // Header always present
    expect(screen.getByText('Bare Game')).toBeInTheDocument();
    // Footer always present
    expect(screen.getByTestId('game-detail-link')).toBeInTheDocument();
    // All optional sections hidden
    expect(screen.queryByTestId('stats-row')).not.toBeInTheDocument();
    expect(screen.queryByTestId('kb-summary')).not.toBeInTheDocument();
    expect(screen.queryByTestId('tag-pills')).not.toBeInTheDocument();
    expect(screen.queryByTestId('nav-links')).not.toBeInTheDocument();
  });

  it('renders all sections when all data provided', () => {
    renderComponent(
      {
        timesPlayed: 10,
        winRate: 55,
        totalPlayTimeMinutes: 600,
        categories: ['Strategia'],
        mechanics: ['Worker Placement'],
        hasKb: true,
        kbDocuments: [{ id: '1', fileName: 'rules.pdf', status: 'Ready' }],
        sessionCount: 3,
        bggWeight: 3.5,
        bestPlayerCount: 4,
        noteCount: 2,
      },
      {
        onViewSessions: vi.fn(),
        onViewNotes: vi.fn(),
      }
    );

    expect(screen.getByTestId('stats-row')).toBeInTheDocument();
    expect(screen.getByTestId('kb-summary')).toBeInTheDocument();
    expect(screen.getByTestId('tag-pills')).toBeInTheDocument();
    expect(screen.getByTestId('nav-links')).toBeInTheDocument();
  });
});
