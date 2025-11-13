import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { GamePicker } from '@/components/game/GamePicker';

describe('GamePicker', () => {
  const mockGames = [
    { id: '1', name: 'Gloomhaven', createdAt: '2024-01-01' },
    { id: '2', name: 'Wingspan', createdAt: '2024-01-02' },
    { id: '3', name: 'Catan', createdAt: '2024-01-03' }
  ];

  const mockProps = {
    games: mockGames,
    selectedGameId: null,
    onGameSelect: jest.fn(),
    onGameCreate: jest.fn(),
    loading: false
  };

  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('Rendering', () => {
    it('renders game selection dropdown', () => {
      render(<GamePicker {...mockProps} />);

      expect(screen.getByLabelText(/Select Game/i)).toBeInTheDocument();
    });

    it('renders create game form', () => {
      render(<GamePicker {...mockProps} />);

      expect(screen.getByLabelText(/Create New Game/i)).toBeInTheDocument();
      expect(screen.getByPlaceholderText(/e.g., Gloomhaven/i)).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Create/i })).toBeInTheDocument();
    });

    it('displays selected game alert when game is selected', () => {
      render(<GamePicker {...mockProps} selectedGameId="1" />);

      expect(screen.getByText(/Selected:/i)).toBeInTheDocument();
      expect(screen.getByText(/Gloomhaven/i)).toBeInTheDocument();
    });

    it('does not display alert when no game is selected', () => {
      render(<GamePicker {...mockProps} selectedGameId={null} />);

      expect(screen.queryByText(/Selected:/i)).not.toBeInTheDocument();
    });
  });

  describe('Game Selection', () => {
    it('calls onGameSelect when game is selected from dropdown', async () => {
      const user = userEvent.setup();
      render(<GamePicker {...mockProps} />);

      const select = screen.getByRole('combobox', { name: /Select Game/i });
      await user.click(select);

      const option = screen.getByRole('option', { name: 'Wingspan' });
      await user.click(option);

      expect(mockProps.onGameSelect).toHaveBeenCalledWith('2');
    });

    it('displays all games in dropdown', async () => {
      const user = userEvent.setup();
      render(<GamePicker {...mockProps} />);

      const select = screen.getByRole('combobox');
      await user.click(select);

      expect(screen.getByRole('option', { name: 'Gloomhaven' })).toBeInTheDocument();
      expect(screen.getByRole('option', { name: 'Wingspan' })).toBeInTheDocument();
      expect(screen.getByRole('option', { name: 'Catan' })).toBeInTheDocument();
    });
  });

  describe('Game Creation', () => {
    it('creates new game when form is submitted', async () => {
      const user = userEvent.setup();
      mockProps.onGameCreate.mockResolvedValue(undefined);

      render(<GamePicker {...mockProps} />);

      const input = screen.getByPlaceholderText(/e.g., Gloomhaven/i);
      const createButton = screen.getByRole('button', { name: /Create/i });

      await user.type(input, 'Azul');
      await user.click(createButton);

      expect(mockProps.onGameCreate).toHaveBeenCalledWith('Azul');
    });

    it('shows loading state while creating game', async () => {
      const user = userEvent.setup();
      let resolveCreate: () => void;
      const createPromise = new Promise<void>((resolve) => {
        resolveCreate = resolve;
      });
      mockProps.onGameCreate.mockReturnValue(createPromise);

      render(<GamePicker {...mockProps} />);

      const input = screen.getByPlaceholderText(/e.g., Gloomhaven/i);
      const createButton = screen.getByRole('button', { name: /Create/i });

      await user.type(input, 'Azul');
      await user.click(createButton);

      expect(createButton).toBeDisabled();
      expect(screen.getByRole('button', { name: /Create/i })).toBeDisabled();

      resolveCreate!();
      await waitFor(() => {
        expect(createButton).not.toBeDisabled();
      });
    });

    it('clears input after successful creation', async () => {
      const user = userEvent.setup();
      mockProps.onGameCreate.mockResolvedValue(undefined);

      render(<GamePicker {...mockProps} />);

      const input = screen.getByPlaceholderText(/e.g., Gloomhaven/i);
      await user.type(input, 'Azul');
      await user.click(screen.getByRole('button', { name: /Create/i }));

      await waitFor(() => {
        expect(input).toHaveValue('');
      });
    });

    it('displays error when creation fails', async () => {
      const user = userEvent.setup();
      mockProps.onGameCreate.mockRejectedValue(new Error('Creation failed'));

      render(<GamePicker {...mockProps} />);

      const input = screen.getByPlaceholderText(/e.g., Gloomhaven/i);
      await user.type(input, 'Azul');
      await user.click(screen.getByRole('button', { name: /Create/i }));

      await waitFor(() => {
        expect(screen.getByText(/Creation failed/i)).toBeInTheDocument();
      });
    });
  });

  describe('Validation', () => {
    it('disables create button when input is empty', () => {
      render(<GamePicker {...mockProps} />);

      const createButton = screen.getByRole('button', { name: /Create/i });
      expect(createButton).toBeDisabled();
    });

    it('enables create button when input has value', async () => {
      const user = userEvent.setup();
      render(<GamePicker {...mockProps} />);

      const input = screen.getByPlaceholderText(/e.g., Gloomhaven/i);
      await user.type(input, 'Azul');

      const createButton = screen.getByRole('button', { name: /Create/i });
      expect(createButton).not.toBeDisabled();
    });

    it('shows error for empty game name', async () => {
      const user = userEvent.setup();
      render(<GamePicker {...mockProps} />);

      const input = screen.getByPlaceholderText(/e.g., Gloomhaven/i);
      await user.type(input, '   '); // Only whitespace
      await user.click(screen.getByRole('button', { name: /Create/i }));

      await waitFor(() => {
        expect(screen.getByText(/Game name cannot be empty/i)).toBeInTheDocument();
      });
    });

    it('shows error for game name too short', async () => {
      const user = userEvent.setup();
      render(<GamePicker {...mockProps} />);

      const input = screen.getByPlaceholderText(/e.g., Gloomhaven/i);
      await user.type(input, 'A');
      await user.click(screen.getByRole('button', { name: /Create/i }));

      await waitFor(() => {
        expect(screen.getByText(/at least 2 characters/i)).toBeInTheDocument();
      });
    });

    it('clears error when user starts typing', async () => {
      const user = userEvent.setup();
      mockProps.onGameCreate.mockRejectedValue(new Error('Failed'));

      render(<GamePicker {...mockProps} />);

      const input = screen.getByPlaceholderText(/e.g., Gloomhaven/i);
      await user.type(input, 'A');
      await user.click(screen.getByRole('button', { name: /Create/i }));

      await waitFor(() => {
        expect(screen.getByText(/at least 2 characters/i)).toBeInTheDocument();
      });

      await user.type(input, 'zul');

      expect(screen.queryByText(/at least 2 characters/i)).not.toBeInTheDocument();
    });
  });

  describe('Loading States', () => {
    it('disables select dropdown when loading', () => {
      render(<GamePicker {...mockProps} loading={true} />);

      const select = screen.getByRole('combobox');
      expect(select).toBeDisabled();
    });

    it('disables create form when creating', async () => {
      const user = userEvent.setup();
      let resolveCreate: () => void;
      const createPromise = new Promise<void>((resolve) => {
        resolveCreate = resolve;
      });
      mockProps.onGameCreate.mockReturnValue(createPromise);

      render(<GamePicker {...mockProps} />);

      const input = screen.getByPlaceholderText(/e.g., Gloomhaven/i);
      await user.type(input, 'Azul');
      await user.click(screen.getByRole('button', { name: /Create/i }));

      expect(input).toBeDisabled();

      resolveCreate!();
    });
  });

  describe('Edge Cases', () => {
    it('handles empty games array', () => {
      render(<GamePicker {...mockProps} games={[]} />);

      expect(screen.getByLabelText(/Select Game/i)).toBeInTheDocument();
    });

    it('handles null selectedGameId', () => {
      render(<GamePicker {...mockProps} selectedGameId={null} />);

      expect(screen.queryByText(/Selected:/i)).not.toBeInTheDocument();
    });

    it('trims whitespace from game name before submission', async () => {
      const user = userEvent.setup();
      mockProps.onGameCreate.mockResolvedValue(undefined);

      render(<GamePicker {...mockProps} />);

      const input = screen.getByPlaceholderText(/e.g., Gloomhaven/i);
      await user.type(input, '  Azul  ');
      await user.click(screen.getByRole('button', { name: /Create/i }));

      expect(mockProps.onGameCreate).toHaveBeenCalledWith('Azul');
    });
  });
});
