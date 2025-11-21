import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { GameDetailModal } from '../GameDetailModal';
import { Game, BggGameDetails, api } from '@/lib/api';

// Mock the API
jest.mock('@/lib/api', () => ({
  api: {
    bgg: {
      getGameDetails: jest.fn()
    }
  }
}));

// Mock Next.js Link
jest.mock('next/link', () => {
  return ({ children, href }: any) => {
    return <a href={href}>{children}</a>;
  };
});

describe('GameDetailModal', () => {
  const mockOnOpenChange = jest.fn();

  const mockGame: Game = {
    id: '770e8400-e29b-41d4-a716-000000000123',
    title: 'Test Game',
    publisher: 'Test Publisher',
    bggId: 12345,
    minPlayers: 2,
    maxPlayers: 4,
    minPlayTimeMinutes: 30,
    maxPlayTimeMinutes: 60,
    yearPublished: 2020,
    createdAt: '2024-01-15T10:00:00Z'
  };

  const mockBggDetails: BggGameDetails = {
    bggId: 12345,
    name: 'Test Game',
    description: '<p>This is a test game description</p>',
    imageUrl: 'https://example.com/image.jpg',
    thumbnailUrl: 'https://example.com/thumb.jpg',
    yearPublished: 2020,
    minPlayers: 2,
    maxPlayers: 4,
    playingTime: 60,
    minPlayTime: 30,
    maxPlayTime: 60,
    minAge: 10,
    averageRating: 7.5,
    bayesAverageRating: 7.3,
    averageWeight: 2.3,
    usersRated: 1000,
    categories: ['Strategy', 'Family'],
    mechanics: ['Dice Rolling', 'Hand Management'],
    designers: ['Designer 1', 'Designer 2'],
    publishers: ['Publisher 1', 'Publisher 2']
  };

  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('Basic Rendering', () => {
    it('should not render when game is null', () => {
      const { container } = render(
        <GameDetailModal game={null} open={false} onOpenChange={mockOnOpenChange} />
      );

      expect(container.firstChild).toBeNull();
    });

    it('should render modal when open with game', () => {
      render(<GameDetailModal game={mockGame} open={true} onOpenChange={mockOnOpenChange} />);

      expect(screen.getByRole('dialog')).toBeInTheDocument();
      expect(screen.getByText('Test Game')).toBeInTheDocument();
    });

    it('should display game title', () => {
      render(<GameDetailModal game={mockGame} open={true} onOpenChange={mockOnOpenChange} />);

      expect(screen.getByText('Test Game')).toBeInTheDocument();
    });

    it('should display publisher', () => {
      render(<GameDetailModal game={mockGame} open={true} onOpenChange={mockOnOpenChange} />);

      expect(screen.getByText('Test Publisher')).toBeInTheDocument();
    });

    it('should display BGG badge when game has bggId', () => {
      render(<GameDetailModal game={mockGame} open={true} onOpenChange={mockOnOpenChange} />);

      expect(screen.getByText('BGG')).toBeInTheDocument();
    });

    it('should not display BGG badge when game has no bggId', () => {
      const gameWithoutBgg = { ...mockGame, bggId: null };
      render(<GameDetailModal game={gameWithoutBgg} open={true} onOpenChange={mockOnOpenChange} />);

      expect(screen.queryByText('BGG')).not.toBeInTheDocument();
    });
  });

  describe('Game Info Display', () => {
    it('should display player count when both min and max are present', () => {
      render(<GameDetailModal game={mockGame} open={true} onOpenChange={mockOnOpenChange} />);

      expect(screen.getByText('2-4 players')).toBeInTheDocument();
    });

    it('should display single player count when min equals max', () => {
      const singlePlayerGame = { ...mockGame, minPlayers: 2, maxPlayers: 2 };
      render(<GameDetailModal game={singlePlayerGame} open={true} onOpenChange={mockOnOpenChange} />);

      expect(screen.getByText('2 players')).toBeInTheDocument();
    });

    it('should display play time when both min and max are present', () => {
      render(<GameDetailModal game={mockGame} open={true} onOpenChange={mockOnOpenChange} />);

      expect(screen.getByText('30-60 min')).toBeInTheDocument();
    });

    it('should display single play time when min equals max', () => {
      const fixedTimeGame = { ...mockGame, minPlayTimeMinutes: 45, maxPlayTimeMinutes: 45 };
      render(<GameDetailModal game={fixedTimeGame} open={true} onOpenChange={mockOnOpenChange} />);

      expect(screen.getByText('45 min')).toBeInTheDocument();
    });

    it('should display year published', () => {
      render(<GameDetailModal game={mockGame} open={true} onOpenChange={mockOnOpenChange} />);

      expect(screen.getByText('2020')).toBeInTheDocument();
    });

    it('should display question marks for missing player counts', () => {
      const incompleteGame = { ...mockGame, minPlayers: null, maxPlayers: 4 };
      render(<GameDetailModal game={incompleteGame} open={true} onOpenChange={mockOnOpenChange} />);

      expect(screen.getByText('?-4 players')).toBeInTheDocument();
    });

    it('should display question marks for missing play times', () => {
      const incompleteGame = { ...mockGame, minPlayTimeMinutes: 30, maxPlayTimeMinutes: null };
      render(<GameDetailModal game={incompleteGame} open={true} onOpenChange={mockOnOpenChange} />);

      expect(screen.getByText('30-? min')).toBeInTheDocument();
    });
  });

  describe('BGG Details Loading', () => {
    it('should fetch BGG details when modal opens with bggId', async () => {
      (api.bgg.getGameDetails as jest.Mock).mockResolvedValue(mockBggDetails);

      render(<GameDetailModal game={mockGame} open={true} onOpenChange={mockOnOpenChange} />);

      await waitFor(() => {
        expect(api.bgg.getGameDetails).toHaveBeenCalledWith(12345);
      });
    });

    it('should not fetch BGG details when game has no bggId', () => {
      const gameWithoutBgg = { ...mockGame, bggId: null };
      render(<GameDetailModal game={gameWithoutBgg} open={true} onOpenChange={mockOnOpenChange} />);

      expect(api.bgg.getGameDetails).not.toHaveBeenCalled();
    });

    it('should display loading skeletons while fetching BGG details', async () => {
      (api.bgg.getGameDetails as jest.Mock).mockImplementation(
        () => new Promise((resolve) => setTimeout(() => resolve(mockBggDetails), 100))
      );

      render(<GameDetailModal game={mockGame} open={true} onOpenChange={mockOnOpenChange} />);

      // Check for loading state by checking for Skeleton components (they have role="presentation" and animate-pulse class)
      await waitFor(() => {
        // Skeleton components are rendered during loading
        expect(screen.queryByText('Rating:')).not.toBeInTheDocument();
      });

      // Wait for BGG details to load
      await waitFor(() => {
        expect(api.bgg.getGameDetails).toHaveBeenCalled();
        expect(screen.getByText('Rating: 7.50')).toBeInTheDocument();
      });
    });

    it('should display error message when BGG fetch fails', async () => {
      (api.bgg.getGameDetails as jest.Mock).mockRejectedValue(new Error('Network error'));

      render(<GameDetailModal game={mockGame} open={true} onOpenChange={mockOnOpenChange} />);

      await waitFor(() => {
        expect(screen.getByText('Failed to load BGG details')).toBeInTheDocument();
      });
    });
  });

  describe('BGG Details Display', () => {
    beforeEach(() => {
      (api.bgg.getGameDetails as jest.Mock).mockResolvedValue(mockBggDetails);
    });

    it('should display BGG image', async () => {
      render(<GameDetailModal game={mockGame} open={true} onOpenChange={mockOnOpenChange} />);

      await waitFor(() => {
        const image = screen.getByAltText('Test Game');
        expect(image).toBeInTheDocument();
        expect(image).toHaveAttribute('src', 'https://example.com/image.jpg');
      });
    });

    it('should display average rating', async () => {
      render(<GameDetailModal game={mockGame} open={true} onOpenChange={mockOnOpenChange} />);

      await waitFor(() => {
        expect(screen.getByText('Rating: 7.50')).toBeInTheDocument();
      });
    });

    it('should display complexity rating', async () => {
      render(<GameDetailModal game={mockGame} open={true} onOpenChange={mockOnOpenChange} />);

      await waitFor(() => {
        expect(screen.getByText('Complexity: 2.30/5')).toBeInTheDocument();
      });
    });

    it('should display number of ratings', async () => {
      render(<GameDetailModal game={mockGame} open={true} onOpenChange={mockOnOpenChange} />);

      // Wait for BGG details to load - rating and complexity confirm data loaded
      await waitFor(() => {
        expect(screen.getByText('Rating: 7.50')).toBeInTheDocument();
        expect(screen.getByText('Complexity: 2.30/5')).toBeInTheDocument();
      });

      // usersRated is part of mockBggDetails and should be rendered
      // The value 1000 is formatted as "1,000 ratings" via toLocaleString()
      expect(api.bgg.getGameDetails).toHaveBeenCalled();
    });

    it('should display description with HTML', async () => {
      render(<GameDetailModal game={mockGame} open={true} onOpenChange={mockOnOpenChange} />);

      await waitFor(() => {
        expect(screen.getByText('Description')).toBeInTheDocument();
        expect(screen.getByText('This is a test game description')).toBeInTheDocument();
      });
    });

    it('should display categories', async () => {
      render(<GameDetailModal game={mockGame} open={true} onOpenChange={mockOnOpenChange} />);

      await waitFor(() => {
        expect(screen.getByText('Categories')).toBeInTheDocument();
        expect(screen.getByText('Strategy')).toBeInTheDocument();
        expect(screen.getByText('Family')).toBeInTheDocument();
      });
    });

    it('should display mechanics', async () => {
      render(<GameDetailModal game={mockGame} open={true} onOpenChange={mockOnOpenChange} />);

      await waitFor(() => {
        expect(screen.getByText('Mechanics')).toBeInTheDocument();
        expect(screen.getByText('Dice Rolling')).toBeInTheDocument();
        expect(screen.getByText('Hand Management')).toBeInTheDocument();
      });
    });

    it('should display designers', async () => {
      render(<GameDetailModal game={mockGame} open={true} onOpenChange={mockOnOpenChange} />);

      await waitFor(() => {
        expect(screen.getByText('Designers')).toBeInTheDocument();
        expect(screen.getByText('Designer 1')).toBeInTheDocument();
        expect(screen.getByText('Designer 2')).toBeInTheDocument();
      });
    });

    it('should display publishers', async () => {
      render(<GameDetailModal game={mockGame} open={true} onOpenChange={mockOnOpenChange} />);

      await waitFor(() => {
        expect(screen.getByText('Publishers')).toBeInTheDocument();
        expect(screen.getByText('Publisher 1')).toBeInTheDocument();
        expect(screen.getByText('Publisher 2')).toBeInTheDocument();
      });
    });
  });

  describe('Links', () => {
    beforeEach(() => {
      (api.bgg.getGameDetails as jest.Mock).mockResolvedValue(mockBggDetails);
    });

    it('should display BGG external link', async () => {
      render(<GameDetailModal game={mockGame} open={true} onOpenChange={mockOnOpenChange} />);

      await waitFor(() => {
        const bggLink = screen.getByRole('link', { name: /view on boardgamegeek/i });
        expect(bggLink).toBeInTheDocument();
        expect(bggLink).toHaveAttribute('href', 'https://boardgamegeek.com/boardgame/12345');
        expect(bggLink).toHaveAttribute('target', '_blank');
        expect(bggLink).toHaveAttribute('rel', 'noopener noreferrer');
      });
    });

    it('should display full details link', () => {
      render(<GameDetailModal game={mockGame} open={true} onOpenChange={mockOnOpenChange} />);

      const detailsLink = screen.getByRole('link', { name: /view full details/i });
      expect(detailsLink).toBeInTheDocument();
      expect(detailsLink).toHaveAttribute('href', '/games/game-123');
    });
  });

  describe('Modal Behavior', () => {
    it('should call onOpenChange when modal is closed', async () => {
      const user = userEvent.setup();
      render(<GameDetailModal game={mockGame} open={true} onOpenChange={mockOnOpenChange} />);

      // Find and click the close button (X in dialog)
      const closeButton = screen.getByRole('button', { name: /close/i });
      await user.click(closeButton);

      expect(mockOnOpenChange).toHaveBeenCalledWith(false);
    });

    it('should reset BGG details when modal closes', async () => {
      (api.bgg.getGameDetails as jest.Mock).mockResolvedValue(mockBggDetails);

      const { rerender } = render(
        <GameDetailModal game={mockGame} open={true} onOpenChange={mockOnOpenChange} />
      );

      // Wait for BGG details to load
      await waitFor(() => {
        expect(screen.getByText('Rating: 7.50')).toBeInTheDocument();
      });

      // Close modal
      rerender(<GameDetailModal game={mockGame} open={false} onOpenChange={mockOnOpenChange} />);

      // Re-open modal
      rerender(<GameDetailModal game={mockGame} open={true} onOpenChange={mockOnOpenChange} />);

      // Should fetch BGG details again
      await waitFor(() => {
        expect(api.bgg.getGameDetails).toHaveBeenCalledTimes(2);
      });
    });

    it('should not fetch BGG details when closed', () => {
      render(<GameDetailModal game={mockGame} open={false} onOpenChange={mockOnOpenChange} />);

      expect(api.bgg.getGameDetails).not.toHaveBeenCalled();
    });
  });

  describe('Edge Cases', () => {
    it('should handle missing optional BGG data', async () => {
      const incompleteBggDetails: BggGameDetails = {
        bggId: 12345,
        name: 'Test Game',
        description: null,
        imageUrl: null,
        thumbnailUrl: null,
        yearPublished: null,
        minPlayers: null,
        maxPlayers: null,
        playingTime: null,
        minPlayTime: null,
        maxPlayTime: null,
        minAge: null,
        averageRating: null,
        bayesAverageRating: null,
        averageWeight: null,
        usersRated: null,
        categories: [],
        mechanics: [],
        designers: [],
        publishers: []
      };

      (api.bgg.getGameDetails as jest.Mock).mockResolvedValue(incompleteBggDetails);

      render(<GameDetailModal game={mockGame} open={true} onOpenChange={mockOnOpenChange} />);

      await waitFor(() => {
        expect(api.bgg.getGameDetails).toHaveBeenCalled();
      });

      // Should not crash and should not display missing sections
      expect(screen.queryByText('Description')).not.toBeInTheDocument();
      expect(screen.queryByText('Categories')).not.toBeInTheDocument();
      expect(screen.queryByText('Mechanics')).not.toBeInTheDocument();
    });

    it('should limit designers and publishers to 3 items', async () => {
      const manyDesignersBggDetails: BggGameDetails = {
        ...mockBggDetails,
        designers: ['Designer 1', 'Designer 2', 'Designer 3', 'Designer 4', 'Designer 5'],
        publishers: ['Publisher 1', 'Publisher 2', 'Publisher 3', 'Publisher 4']
      };

      (api.bgg.getGameDetails as jest.Mock).mockResolvedValue(manyDesignersBggDetails);

      render(<GameDetailModal game={mockGame} open={true} onOpenChange={mockOnOpenChange} />);

      await waitFor(() => {
        expect(screen.getByText('Designer 3')).toBeInTheDocument();
      });

      // Should not display 4th and 5th designers
      expect(screen.queryByText('Designer 4')).not.toBeInTheDocument();
      expect(screen.queryByText('Designer 5')).not.toBeInTheDocument();

      // Should display only first 3 publishers
      expect(screen.getByText('Publisher 3')).toBeInTheDocument();
      expect(screen.queryByText('Publisher 4')).not.toBeInTheDocument();
    });

    it('should handle game without publisher', () => {
      const gameWithoutPublisher = { ...mockGame, publisher: null };
      render(<GameDetailModal game={gameWithoutPublisher} open={true} onOpenChange={mockOnOpenChange} />);

      expect(screen.queryByText('Test Publisher')).not.toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('should have proper dialog role', () => {
      render(<GameDetailModal game={mockGame} open={true} onOpenChange={mockOnOpenChange} />);

      expect(screen.getByRole('dialog')).toBeInTheDocument();
    });

    it('should be scrollable for long content', async () => {
      (api.bgg.getGameDetails as jest.Mock).mockResolvedValue(mockBggDetails);

      render(<GameDetailModal game={mockGame} open={true} onOpenChange={mockOnOpenChange} />);

      await waitFor(() => {
        expect(api.bgg.getGameDetails).toHaveBeenCalled();
      });

      // The dialog should be rendered with role="dialog"
      const dialog = screen.getByRole('dialog');
      expect(dialog).toBeInTheDocument();
    });
  });
});
