import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { GameNightDrawerContent } from '../GameNightDrawerContent';

// Mock cascade store
const pushDrawerMock = vi.fn();
vi.mock('@/lib/stores/cascade-navigation-store', () => ({
  useCascadeNavigationStore: (
    selector: (state: { pushDrawer: typeof pushDrawerMock }) => unknown
  ) => selector({ pushDrawer: pushDrawerMock }),
}));

const baseProps = {
  gameNightId: 'gn-1',
  title: 'Sabato a casa Marco',
  date: '2026-06-14T20:00:00Z',
  location: 'Roma',
  status: 'Published' as const,
  players: [
    { id: 'p1', userId: 'u1', name: 'Anna', isGuest: false },
    { id: 'p2', name: 'Bob Guest', isGuest: true },
  ],
  sessions: [],
};

describe('GameNightDrawerContent', () => {
  it('renders title, date, location', () => {
    render(<GameNightDrawerContent {...baseProps} />);
    expect(screen.getByText('Sabato a casa Marco')).toBeInTheDocument();
    expect(screen.getByText('Roma', { exact: false })).toBeInTheDocument();
  });

  it('renders status badge with correct label', () => {
    render(<GameNightDrawerContent {...baseProps} status="InProgress" />);
    expect(screen.getByTestId('status-badge')).toHaveTextContent('In corso');
  });

  it('renders all 5 status labels correctly', () => {
    const statuses = ['Draft', 'Published', 'InProgress', 'Completed', 'Cancelled'] as const;
    const labels = ['Bozza', 'Pianificata', 'In corso', 'Completata', 'Annullata'];
    statuses.forEach((s, i) => {
      const { unmount } = render(<GameNightDrawerContent {...baseProps} status={s} />);
      expect(screen.getByTestId('status-badge')).toHaveTextContent(labels[i]);
      unmount();
    });
  });

  it('renders player list with User badge for User-linked', () => {
    render(<GameNightDrawerContent {...baseProps} />);
    expect(screen.getByText('Anna')).toBeInTheDocument();
    expect(screen.getByText('✓ User')).toBeInTheDocument();
  });

  it('renders player list with Guest badge for guests', () => {
    render(<GameNightDrawerContent {...baseProps} />);
    expect(screen.getByText('Bob Guest')).toBeInTheDocument();
    expect(screen.getByText('Guest')).toBeInTheDocument();
  });

  it('player click pushes drawer for User-linked', () => {
    pushDrawerMock.mockClear();
    render(<GameNightDrawerContent {...baseProps} />);
    fireEvent.click(screen.getByTestId('player-row-p1'));
    expect(pushDrawerMock).toHaveBeenCalledWith('player', 'p1');
  });

  it('guest player click is disabled (no pushDrawer)', () => {
    pushDrawerMock.mockClear();
    render(<GameNightDrawerContent {...baseProps} />);
    fireEvent.click(screen.getByTestId('player-row-p2'));
    expect(pushDrawerMock).not.toHaveBeenCalled();
  });

  it('does NOT render sessions section when status=Published', () => {
    render(
      <GameNightDrawerContent
        {...baseProps}
        status="Published"
        sessions={[
          { id: 's1', playOrder: 1, gameTitle: 'Wingspan', status: 'InProgress' as const },
        ]}
      />
    );
    expect(screen.queryByText('Partite (1)')).not.toBeInTheDocument();
  });

  it('renders sessions sorted by playOrder when status=InProgress', () => {
    render(
      <GameNightDrawerContent
        {...baseProps}
        status="InProgress"
        sessions={[
          { id: 's2', playOrder: 2, gameTitle: 'Catan', status: 'Pending' as const },
          {
            id: 's1',
            playOrder: 1,
            gameTitle: 'Wingspan',
            status: 'Completed' as const,
            winnerName: 'Marco',
            scoreDisplay: '87 pt',
          },
        ]}
      />
    );
    const sessionRows = screen.getAllByText(/Session \d+/);
    expect(sessionRows[0]).toHaveTextContent('Session 1: Wingspan');
    expect(sessionRows[1]).toHaveTextContent('Session 2: Catan');
    expect(screen.getByText('MVP Marco', { exact: false })).toBeInTheDocument();
    expect(screen.getByText('87 pt', { exact: false })).toBeInTheDocument();
  });

  it('renders CTA Modifica RSVP / Cancel for Published status', () => {
    render(<GameNightDrawerContent {...baseProps} status="Published" />);
    expect(screen.getByText('Modifica RSVP')).toBeInTheDocument();
    expect(screen.getByText('Cancel')).toBeInTheDocument();
  });

  it('renders CTA Aggiungi session / Termina serata for InProgress status', () => {
    render(<GameNightDrawerContent {...baseProps} status="InProgress" />);
    expect(screen.getByText('Aggiungi session')).toBeInTheDocument();
    expect(screen.getByText('Termina serata')).toBeInTheDocument();
  });

  it('renders CTA Aggiungi note / Aggiungi foto for Completed status', () => {
    render(<GameNightDrawerContent {...baseProps} status="Completed" />);
    expect(screen.getByText('Aggiungi note')).toBeInTheDocument();
    expect(screen.getByText('Aggiungi foto')).toBeInTheDocument();
  });
});
