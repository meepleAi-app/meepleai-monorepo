/**
 * AddExpansionSheet Component Tests
 * Note: BGG search was removed (restricted to admin only). The component
 * now searches the shared catalog (api.sharedGames.search).
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';

// Mock the api module — must be before any imports that use it
vi.mock('@/lib/api', () => ({
  api: {
    sharedGames: {
      search: vi.fn(),
    },
    library: {
      addPrivateGame: vi.fn(),
      createEntityLink: vi.fn(),
    },
  },
}));

import { api } from '@/lib/api';
import { AddExpansionSheet } from '../AddExpansionSheet';

const mockSearch = vi.mocked(api.sharedGames.search);
const mockAddPrivateGame = vi.mocked(api.library.addPrivateGame);
const mockCreateEntityLink = vi.mocked(api.library.createEntityLink);

const defaultProps = {
  open: true,
  onClose: vi.fn(),
  onExpansionAdded: vi.fn(),
  baseGameId: 'game-1',
  baseGameTitle: 'Catan',
};

const makeCatalogResult = (overrides = {}) => ({
  id: 'shared-1',
  title: 'Catan: Seafarers',
  yearPublished: 1997,
  thumbnailUrl: null,
  ...overrides,
});

const makeCatalogResponse = (items = [makeCatalogResult()]) => ({
  items,
  page: 1,
  pageSize: 20,
  totalCount: items.length,
  totalPages: 1,
  hasNextPage: false,
  hasPreviousPage: false,
});

const makePrivateGameDto = (overrides = {}) => ({
  id: 'exp-1',
  ownerId: 'user-1',
  source: 'Manual' as const,
  bggId: null,
  title: 'Catan: Seafarers',
  minPlayers: 1,
  maxPlayers: 4,
  yearPublished: 1997,
  description: null,
  playingTimeMinutes: null,
  minAge: null,
  complexityRating: null,
  imageUrl: null,
  thumbnailUrl: null,
  createdAt: new Date().toISOString(),
  updatedAt: null,
  bggSyncedAt: null,
  canProposeToCatalog: false,
  agentDefinitionId: null,
  ...overrides,
});

const makeEntityLinkDto = () => ({
  id: 'link-1',
  sourceEntityType: 'Game' as const,
  sourceEntityId: 'exp-1',
  targetEntityType: 'Game' as const,
  targetEntityId: 'game-1',
  linkType: 'ExpansionOf' as const,
  isBidirectional: false,
  scope: 'User' as const,
  ownerUserId: 'user-1',
  metadata: null,
  isAdminApproved: false,
  isBggImported: false,
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
  isOwner: true,
});

describe('AddExpansionSheet', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders with search input and title', () => {
    render(<AddExpansionSheet {...defaultProps} />);

    expect(screen.getByText('Aggiungi espansione')).toBeInTheDocument();
    expect(screen.getByTestId('search-input')).toBeInTheDocument();
    expect(screen.getByPlaceholderText('Cerca espansione nel catalogo...')).toBeInTheDocument();
    // Shows the base game title
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('pre-fills search with base game title', () => {
    render(<AddExpansionSheet {...defaultProps} baseGameTitle="Wingspan" />);

    const input = screen.getByTestId('search-input') as HTMLInputElement;
    expect(input.value).toBe('Wingspan');
  });

  it('searches catalog and shows results after debounce', async () => {
    mockSearch.mockResolvedValue(makeCatalogResponse());

    render(<AddExpansionSheet {...defaultProps} />);

    const input = screen.getByTestId('search-input');
    fireEvent.change(input, { target: { value: 'Seafarers' } });

    // Advance debounce timer
    vi.advanceTimersByTime(400);

    await waitFor(() => {
      expect(mockSearch).toHaveBeenCalledWith(expect.objectContaining({ searchTerm: 'Seafarers' }));
    });

    await waitFor(() => {
      expect(screen.getByTestId('search-results')).toBeInTheDocument();
      expect(screen.getByText('Catan: Seafarers')).toBeInTheDocument();
    });
  });

  it('shows year when available in results', async () => {
    mockSearch.mockResolvedValue(makeCatalogResponse([makeCatalogResult({ yearPublished: 1997 })]));

    render(<AddExpansionSheet {...defaultProps} />);

    const input = screen.getByTestId('search-input');
    fireEvent.change(input, { target: { value: 'Seafarers' } });
    vi.advanceTimersByTime(400);

    await waitFor(() => {
      expect(screen.getByText('1997')).toBeInTheDocument();
    });
  });

  it('creates private game + entity link on selection and add', async () => {
    mockSearch.mockResolvedValue(makeCatalogResponse());
    mockAddPrivateGame.mockResolvedValue(makePrivateGameDto());
    mockCreateEntityLink.mockResolvedValue(makeEntityLinkDto());

    render(<AddExpansionSheet {...defaultProps} />);

    // Trigger search
    const input = screen.getByTestId('search-input');
    fireEvent.change(input, { target: { value: 'Seafarers' } });
    vi.advanceTimersByTime(400);

    await waitFor(() => {
      expect(screen.getByText('Catan: Seafarers')).toBeInTheDocument();
    });

    // Select result
    fireEvent.click(screen.getByText('Catan: Seafarers'));

    // Click "Aggiungi"
    const addButton = await screen.findByTestId('add-button');
    fireEvent.click(addButton);

    await waitFor(() => {
      expect(mockAddPrivateGame).toHaveBeenCalledWith({
        source: 'Manual',
        title: 'Catan: Seafarers',
        minPlayers: 1,
        maxPlayers: 4,
        yearPublished: 1997,
      });
    });

    await waitFor(() => {
      expect(mockCreateEntityLink).toHaveBeenCalledWith({
        sourceEntityType: 'Game',
        sourceEntityId: 'exp-1',
        targetEntityType: 'Game',
        targetEntityId: 'game-1',
        linkType: 'ExpansionOf',
      });
    });
  });

  it('shows success state after adding', async () => {
    mockSearch.mockResolvedValue(makeCatalogResponse());
    mockAddPrivateGame.mockResolvedValue(makePrivateGameDto());
    mockCreateEntityLink.mockResolvedValue(makeEntityLinkDto());

    render(<AddExpansionSheet {...defaultProps} />);

    const input = screen.getByTestId('search-input');
    fireEvent.change(input, { target: { value: 'Seafarers' } });
    vi.advanceTimersByTime(400);

    await waitFor(() => {
      expect(screen.getByText('Catan: Seafarers')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByText('Catan: Seafarers'));

    const addButton = await screen.findByTestId('add-button');
    fireEvent.click(addButton);

    await waitFor(() => {
      expect(screen.getByTestId('success-state')).toBeInTheDocument();
      expect(screen.getByText('Espansione aggiunta!')).toBeInTheDocument();
    });
  });

  it('calls onExpansionAdded callback on success', async () => {
    mockSearch.mockResolvedValue(makeCatalogResponse());
    mockAddPrivateGame.mockResolvedValue(makePrivateGameDto());
    mockCreateEntityLink.mockResolvedValue(makeEntityLinkDto());

    const onExpansionAdded = vi.fn();
    render(<AddExpansionSheet {...defaultProps} onExpansionAdded={onExpansionAdded} />);

    const input = screen.getByTestId('search-input');
    fireEvent.change(input, { target: { value: 'Seafarers' } });
    vi.advanceTimersByTime(400);

    await waitFor(() => {
      expect(screen.getByText('Catan: Seafarers')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByText('Catan: Seafarers'));
    const addButton = await screen.findByTestId('add-button');
    fireEvent.click(addButton);

    await waitFor(() => {
      expect(onExpansionAdded).toHaveBeenCalledTimes(1);
    });
  });

  it('shows error message when addPrivateGame fails', async () => {
    mockSearch.mockResolvedValue(makeCatalogResponse());
    mockAddPrivateGame.mockRejectedValue(new Error('Server error'));

    render(<AddExpansionSheet {...defaultProps} />);

    const input = screen.getByTestId('search-input');
    fireEvent.change(input, { target: { value: 'Seafarers' } });
    vi.advanceTimersByTime(400);

    await waitFor(() => {
      expect(screen.getByText('Catan: Seafarers')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByText('Catan: Seafarers'));
    const addButton = await screen.findByTestId('add-button');
    fireEvent.click(addButton);

    await waitFor(() => {
      expect(screen.getByRole('alert')).toBeInTheDocument();
    });
  });

  it('does not fire a second catalog search before debounce window expires after new input', async () => {
    mockSearch.mockResolvedValue(makeCatalogResponse());
    render(<AddExpansionSheet {...defaultProps} />);

    // Let the initial mount search complete
    vi.advanceTimersByTime(400);
    await waitFor(() => expect(mockSearch).toHaveBeenCalledTimes(1));

    // Now type new input — the debounce timer resets
    const input = screen.getByTestId('search-input');
    fireEvent.change(input, { target: { value: 'Cat' } });

    vi.advanceTimersByTime(200); // Less than 400ms — debounce not yet expired

    expect(mockSearch).toHaveBeenCalledTimes(1); // Still only the initial call
  });

  it('calls onClose when the close button is clicked', () => {
    const onClose = vi.fn();
    render(<AddExpansionSheet {...defaultProps} onClose={onClose} />);

    fireEvent.click(screen.getByText('Chiudi', { selector: '.sr-only' }).parentElement!);

    expect(onClose).toHaveBeenCalled();
  });

  it('does not render content when closed', () => {
    render(<AddExpansionSheet {...defaultProps} open={false} />);
    expect(screen.queryByText('Aggiungi espansione')).not.toBeInTheDocument();
  });
});
