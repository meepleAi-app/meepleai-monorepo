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
  nextGameNight: 'Sab 21:00',
  entityLinkCount: 3,
  noteCount: 2,
};

const defaultActions: GameBackActions = {
  onChatAgent: vi.fn(),
  onToggleFavorite: vi.fn(),
  isFavorite: false,
  onNewSession: vi.fn(),
  onAddToGameNight: vi.fn(),
  onViewLinks: vi.fn(),
};

function renderComponent(
  dataOverrides?: Partial<GameBackData>,
  actionsOverrides?: Partial<GameBackActions> | null,
  props?: { title?: string; detailHref?: string; entityColor?: string }
) {
  const data = { ...defaultData, ...dataOverrides };
  const actions =
    actionsOverrides === null ? undefined : { ...defaultActions, ...actionsOverrides };
  return render(
    <GameBackContent
      data={data}
      actions={actions}
      title={props?.title ?? 'Catan'}
      detailHref={props?.detailHref ?? '/games/catan'}
      entityColor={props?.entityColor}
      {...props}
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
    renderComponent({}, {}, { title: undefined });
    expect(screen.getByText('Statistiche')).toBeInTheDocument();
  });
});

// ============================================================================
// Enriched Header
// ============================================================================

describe('Enriched Header', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows "Mai giocato" when lastPlayedLabel is undefined', () => {
    renderComponent({ lastPlayedLabel: undefined });
    expect(screen.getByText(/Mai giocato/)).toBeInTheDocument();
  });

  it('shows lastPlayedLabel when provided', () => {
    renderComponent({ lastPlayedLabel: '3 giorni fa' });
    expect(screen.getByText(/3 giorni fa/)).toBeInTheDocument();
  });

  it('shows win rate when provided', () => {
    renderComponent({ winRate: 65 });
    expect(screen.getByText(/Win rate 65%/)).toBeInTheDocument();
  });

  it('omits win rate when not provided', () => {
    renderComponent({ winRate: undefined });
    expect(screen.queryByText(/Win rate/)).not.toBeInTheDocument();
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
// ContextualActions
// ============================================================================

describe('ContextualActions', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('fires onNewSession with stopPropagation', () => {
    const onNewSession = vi.fn();
    renderComponent({}, { onNewSession });
    const btn = screen.getByText('Nuova Sessione').closest('button')!;
    const event = new MouseEvent('click', { bubbles: true });
    const stopProp = vi.spyOn(event, 'stopPropagation');
    fireEvent(btn, event);
    expect(onNewSession).toHaveBeenCalledTimes(1);
    expect(stopProp).toHaveBeenCalled();
  });

  it('shows "Nessuna partita" for timesPlayed=0', () => {
    renderComponent({ timesPlayed: 0 });
    expect(screen.getByText('Nessuna partita')).toBeInTheDocument();
  });

  it('shows play count context', () => {
    renderComponent({ timesPlayed: 12 });
    expect(screen.getByText('12 partite giocate')).toBeInTheDocument();
  });

  it('shows Espansioni with "N collegate" context when entityLinkCount > 0', () => {
    renderComponent({ entityLinkCount: 3 });
    expect(screen.getByText('Espansioni')).toBeInTheDocument();
    expect(screen.getByText('3 collegate')).toBeInTheDocument();
  });

  it('hides Espansioni when entityLinkCount is 0', () => {
    renderComponent({ entityLinkCount: 0 });
    expect(screen.queryByText('Espansioni')).not.toBeInTheDocument();
  });

  it('shows Aggiungi a serata with nextGameNight context', () => {
    renderComponent({ nextGameNight: 'Ven 20:30' }, { onAddToGameNight: vi.fn() });
    expect(screen.getByText('Aggiungi a serata')).toBeInTheDocument();
    expect(screen.getByText('Ven 20:30')).toBeInTheDocument();
  });

  it('hides Aggiungi a serata when onAddToGameNight is undefined', () => {
    renderComponent({}, { onAddToGameNight: undefined });
    expect(screen.queryByText('Aggiungi a serata')).not.toBeInTheDocument();
  });
});

// ============================================================================
// KB Context in Chiedi all'AI action
// ============================================================================

describe('KB Context in ContextualAction', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows "KB pronta · N doc" as context when hasKb is true', () => {
    renderComponent({ hasKb: true, kbCardCount: 5 });
    expect(screen.getByText('KB pronta · 5 doc')).toBeInTheDocument();
  });

  it('shows "KB in elaborazione" when hasKb is false and docs are processing', () => {
    renderComponent({
      hasKb: false,
      kbCardCount: 0,
      kbDocuments: [{ id: '1', fileName: 'rules.pdf', status: 'Processing' }],
    });
    expect(screen.getByText('KB in elaborazione')).toBeInTheDocument();
  });

  it('shows "Nessuna KB" when hasKb is false and no processing docs', () => {
    renderComponent({ hasKb: false, kbCardCount: 0, kbDocuments: [] });
    expect(screen.getByText('Nessuna KB')).toBeInTheDocument();
  });
});

// ============================================================================
// Compact Footer
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

  it('shows noteCount when > 0', () => {
    renderComponent({ noteCount: 2 });
    const noteIndicator = screen.getByTestId('note-count-indicator');
    expect(noteIndicator).toHaveTextContent('2');
  });

  it('hides noteCount indicator when 0', () => {
    renderComponent({ noteCount: 0 });
    // StickyNote icon should not appear in footer when noteCount is 0
    expect(screen.queryByTestId('note-count-indicator')).not.toBeInTheDocument();
  });

  it('renders detail link', () => {
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
