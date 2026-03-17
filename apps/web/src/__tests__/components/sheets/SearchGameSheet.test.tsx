import { render, screen, fireEvent } from '@testing-library/react';
import { SearchGameSheet } from '@/components/sheets/SearchGameSheet';
import { useLibrary } from '@/hooks/queries/useLibrary';
import { useSharedGames } from '@/hooks/queries/useSharedGames';
import { useRouter } from 'next/navigation';

vi.mock('next/navigation', () => ({
  useRouter: vi.fn(() => ({ push: vi.fn() })),
}));

vi.mock('@/hooks/queries/useLibrary');
vi.mock('@/hooks/queries/useSharedGames');

const defaultLibraryMock = {
  data: { items: [], page: 1, pageSize: 100, totalCount: 0 },
  isLoading: false,
  error: null,
};

const defaultCatalogMock = {
  data: undefined,
  isLoading: false,
  error: null,
};

describe('SearchGameSheet', () => {
  beforeEach(() => {
    vi.resetAllMocks();
    vi.mocked(useLibrary).mockReturnValue(defaultLibraryMock as any);
    vi.mocked(useSharedGames).mockReturnValue(defaultCatalogMock as any);
    vi.mocked(useRouter).mockReturnValue({ push: vi.fn() } as any);
  });

  it('renders when isOpen is true', () => {
    render(<SearchGameSheet isOpen={true} onClose={vi.fn()} />);
    expect(screen.getByRole('heading', { name: 'Cerca Gioco' })).toBeInTheDocument();
  });

  it('does not render sheet content when isOpen is false', () => {
    render(<SearchGameSheet isOpen={false} onClose={vi.fn()} />);
    expect(screen.queryByRole('heading', { name: 'Cerca Gioco' })).not.toBeInTheDocument();
  });

  it('shows search input', () => {
    render(<SearchGameSheet isOpen={true} onClose={vi.fn()} />);
    expect(screen.getByRole('searchbox')).toBeInTheDocument();
  });

  it('shows scope toggles', () => {
    render(<SearchGameSheet isOpen={true} onClose={vi.fn()} />);
    expect(screen.getByText('La mia collezione')).toBeInTheDocument();
    expect(screen.getByText('Catalogo shared')).toBeInTheDocument();
  });

  it('calls onClose when close button is clicked', () => {
    const onClose = vi.fn();
    render(<SearchGameSheet isOpen={true} onClose={onClose} />);
    fireEvent.click(screen.getByRole('button', { name: 'Chiudi' }));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('shows loading state when library is loading', () => {
    vi.mocked(useLibrary).mockReturnValue({ ...defaultLibraryMock, isLoading: true } as any);
    render(<SearchGameSheet isOpen={true} onClose={vi.fn()} />);
    // Loader icon is shown — we check for the spinner class
    const spinner = document.querySelector('.animate-spin');
    expect(spinner).toBeInTheDocument();
  });

  it('shows game results from library', () => {
    vi.mocked(useLibrary).mockReturnValue({
      data: {
        items: [
          {
            gameId: 'g1',
            gameTitle: 'Catan',
            gamePublisher: 'Kosmos',
            minPlayers: 3,
            maxPlayers: 4,
            kbCardCount: 2,
            hasKb: true,
            isFavorite: false,
          },
        ],
        page: 1,
        pageSize: 100,
        totalCount: 1,
      },
      isLoading: false,
      error: null,
    } as any);

    render(<SearchGameSheet isOpen={true} onClose={vi.fn()} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Kosmos')).toBeInTheDocument();
  });

  it('navigates to game detail and closes when result is clicked', () => {
    const push = vi.fn();
    const onClose = vi.fn();
    vi.mocked(useRouter).mockReturnValue({ push } as any);
    vi.mocked(useLibrary).mockReturnValue({
      data: {
        items: [
          {
            gameId: 'g1',
            gameTitle: 'Catan',
            gamePublisher: null,
            minPlayers: 3,
            maxPlayers: 4,
            kbCardCount: 0,
            hasKb: false,
            isFavorite: false,
          },
        ],
        page: 1,
        pageSize: 100,
        totalCount: 1,
      },
      isLoading: false,
      error: null,
    } as any);

    render(<SearchGameSheet isOpen={true} onClose={onClose} />);
    fireEvent.click(screen.getByText('Catan'));

    expect(push).toHaveBeenCalledWith('/library/g1');
    expect(onClose).toHaveBeenCalledTimes(1);
  });
});
