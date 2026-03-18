import { render, screen, fireEvent } from '@testing-library/react';
import { SessionSheet } from '@/components/sheets/SessionSheet';
import { useActiveSessions } from '@/hooks/queries/useActiveSessions';
import { useLibrary } from '@/hooks/queries/useLibrary';
import { useRouter } from 'next/navigation';

vi.mock('next/navigation', () => ({
  useRouter: vi.fn(() => ({ push: vi.fn() })),
}));

vi.mock('@/hooks/queries/useActiveSessions');
vi.mock('@/hooks/queries/useLibrary');

const mockSession = {
  id: 's1-0000-0000-0000-000000000001',
  gameId: 'g1',
  status: 'InProgress',
  startedAt: '2026-03-16T10:00:00Z',
  completedAt: null,
  playerCount: 3,
  players: [],
  winnerName: null,
  notes: null,
  durationMinutes: 30,
};

const noSessionsMock = {
  data: { sessions: [], total: 0, page: 1, pageSize: 10 },
  isLoading: false,
  error: null,
};

const withSessionsMock = {
  data: { sessions: [mockSession], total: 1, page: 1, pageSize: 10 },
  isLoading: false,
  error: null,
};

const defaultLibraryMock = {
  data: { items: [], page: 1, pageSize: 100, totalCount: 0 },
  isLoading: false,
  error: null,
};

describe('SessionSheet', () => {
  beforeEach(() => {
    vi.resetAllMocks();
    vi.mocked(useActiveSessions).mockReturnValue(noSessionsMock as any);
    vi.mocked(useLibrary).mockReturnValue(defaultLibraryMock as any);
    vi.mocked(useRouter).mockReturnValue({ push: vi.fn() } as any);
  });

  it('renders when isOpen is true', () => {
    render(<SessionSheet isOpen={true} onClose={vi.fn()} />);
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it('does not render sheet content when isOpen is false', () => {
    render(<SessionSheet isOpen={false} onClose={vi.fn()} />);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('shows session list when sessions exist', () => {
    vi.mocked(useActiveSessions).mockReturnValue(withSessionsMock as any);
    render(<SessionSheet isOpen={true} onClose={vi.fn()} />);
    expect(screen.getByRole('heading', { name: 'Sessioni Attive' })).toBeInTheDocument();
    expect(screen.getByText('Attiva')).toBeInTheDocument();
    expect(screen.getByText('3 giocatori')).toBeInTheDocument();
  });

  it('shows creation form when no sessions exist', () => {
    render(<SessionSheet isOpen={true} onClose={vi.fn()} />);
    expect(screen.getByRole('heading', { name: 'Nuova Sessione' })).toBeInTheDocument();
    expect(screen.getByText('Generica')).toBeInTheDocument();
    expect(screen.getByText('Specifica')).toBeInTheDocument();
  });

  it('calls onClose when close button is clicked', () => {
    const onClose = vi.fn();
    render(<SessionSheet isOpen={true} onClose={onClose} />);
    fireEvent.click(screen.getByRole('button', { name: 'Chiudi' }));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('shows Nuova sessione button in session list state', () => {
    vi.mocked(useActiveSessions).mockReturnValue(withSessionsMock as any);
    render(<SessionSheet isOpen={true} onClose={vi.fn()} />);
    expect(screen.getByRole('button', { name: /Nuova sessione/i })).toBeInTheDocument();
  });

  it('switches to creation form when Nuova sessione is clicked', () => {
    vi.mocked(useActiveSessions).mockReturnValue(withSessionsMock as any);
    render(<SessionSheet isOpen={true} onClose={vi.fn()} />);
    fireEvent.click(screen.getByRole('button', { name: /Nuova sessione/i }));
    expect(screen.getByRole('heading', { name: 'Nuova Sessione' })).toBeInTheDocument();
  });

  it('navigates to session page and closes when session row is clicked', () => {
    const push = vi.fn();
    const onClose = vi.fn();
    vi.mocked(useRouter).mockReturnValue({ push } as any);
    vi.mocked(useActiveSessions).mockReturnValue(withSessionsMock as any);

    render(<SessionSheet isOpen={true} onClose={onClose} />);
    fireEvent.click(screen.getByText('Sessione di gioco'));

    expect(push).toHaveBeenCalledWith('/sessions/s1-0000-0000-0000-000000000001');
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('shows paused badge for paused sessions', () => {
    vi.mocked(useActiveSessions).mockReturnValue({
      ...withSessionsMock,
      data: {
        sessions: [{ ...mockSession, status: 'Paused' }],
        total: 1,
        page: 1,
        pageSize: 10,
      },
    } as any);
    render(<SessionSheet isOpen={true} onClose={vi.fn()} />);
    expect(screen.getByText('In Pausa')).toBeInTheDocument();
  });
});
