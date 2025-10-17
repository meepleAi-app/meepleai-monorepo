import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import SetupPage from '../../pages/setup';
import { api } from '../../lib/api';

// Mock the API client
jest.mock('../../lib/api', () => ({
  api: {
    get: jest.fn(),
    post: jest.fn()
  }
}));

const mockApi = api as jest.Mocked<typeof api>;

// Mock window.confirm
const originalConfirm = window.confirm;

// Test data fixtures
const mockAuthResponse = {
  user: {
    id: 'user-1',
    email: 'user@example.com',
    displayName: 'Test User',
    role: 'User'
  },
  expiresAt: new Date(Date.now() + 3600000).toISOString()
};

const mockGames = [
  { id: 'game-1', name: 'Chess' },
  { id: 'game-2', name: 'Tic-Tac-Toe' }
];

const mockSetupGuideResponse = {
  gameTitle: 'Chess',
  steps: [
    {
      stepNumber: 1,
      title: 'Place the board',
      instruction: 'Place the chessboard between the two players',
      references: [
        { text: 'Board setup instructions', source: 'chess-rules.pdf', page: 2, line: 5 }
      ],
      isOptional: false
    },
    {
      stepNumber: 2,
      title: 'Arrange pieces',
      instruction: 'Place all pieces in their starting positions',
      references: [],
      isOptional: false
    },
    {
      stepNumber: 3,
      title: 'Set timer (optional)',
      instruction: 'If playing with time controls, set the chess clock',
      references: [
        { text: 'Time control rules', source: 'chess-rules.pdf', page: 15, line: 10 }
      ],
      isOptional: true
    }
  ],
  estimatedSetupTimeMinutes: 5,
  promptTokens: 150,
  completionTokens: 200,
  totalTokens: 350,
  confidence: 0.95
};

// Helper to setup authenticated state
const setupAuthenticatedState = () => {
  mockApi.get.mockResolvedValueOnce(mockAuthResponse);
  mockApi.get.mockResolvedValueOnce(mockGames);
};

describe('SetupPage', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockApi.get.mockReset();
    mockApi.post.mockReset();
    window.confirm = originalConfirm;
  });

  afterEach(() => {
    window.confirm = originalConfirm;
  });

  // =============================================================================
  // AUTHENTICATION TESTS
  // =============================================================================

  describe('Authentication', () => {
    it('shows unauthenticated state when user is not logged in', async () => {
      mockApi.get.mockResolvedValueOnce(null);

      render(<SetupPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/auth/me'));

      expect(screen.getByRole('heading', { name: 'Login Required' })).toBeInTheDocument();
      expect(screen.getByText('You must be logged in to use the setup guide.')).toBeInTheDocument();
      expect(screen.getByRole('link', { name: 'Go to Login' })).toBeInTheDocument();
    });

    it('shows authenticated interface when user is logged in', async () => {
      setupAuthenticatedState();

      render(<SetupPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/auth/me'));

      expect(screen.getByRole('heading', { name: 'Game Setup Guide' })).toBeInTheDocument();
      expect(screen.queryByText('Login Required')).not.toBeInTheDocument();
    });

    it('handles authentication check failure gracefully', async () => {
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
      mockApi.get.mockRejectedValueOnce(new Error('Auth failed'));

      render(<SetupPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/auth/me'));

      expect(screen.getByRole('heading', { name: 'Login Required' })).toBeInTheDocument();

      consoleErrorSpy.mockRestore();
    });
  });

  // =============================================================================
  // DATA LOADING TESTS
  // =============================================================================

  describe('Data Loading', () => {
    it('loads games after authentication', async () => {
      setupAuthenticatedState();

      render(<SetupPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/games'));

      const gameSelect = screen.getByLabelText('Game:');
      expect(screen.getByText('Chess')).toBeInTheDocument();
      expect(screen.getByText('Tic-Tac-Toe')).toBeInTheDocument();
    });

    it('auto-selects first game when games are loaded', async () => {
      setupAuthenticatedState();

      render(<SetupPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/games'));

      const gameSelect = screen.getByLabelText('Game:') as HTMLSelectElement;
      expect(gameSelect.value).toBe('game-1');
    });

    it('shows loading indicator while loading games', async () => {
      mockApi.get.mockResolvedValueOnce(mockAuthResponse);
      mockApi.get.mockImplementation(() => new Promise(() => {})); // Never resolves

      render(<SetupPage />);

      await waitFor(() => {
        const gameSelect = screen.getByLabelText('Game:') as HTMLSelectElement;
        expect(gameSelect.disabled).toBe(true);
      });
    });

    it('shows empty state when no games are available', async () => {
      mockApi.get.mockResolvedValueOnce(mockAuthResponse);
      mockApi.get.mockResolvedValueOnce([]);

      render(<SetupPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/games'));

      const gameSelect = screen.getByLabelText('Game:');
      expect(screen.getByText('Select a game...')).toBeInTheDocument();
      expect(screen.queryByText('Chess')).not.toBeInTheDocument();
    });

    it('handles games loading error gracefully', async () => {
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
      mockApi.get.mockResolvedValueOnce(mockAuthResponse);
      mockApi.get.mockRejectedValueOnce(new Error('Games API failed'));

      render(<SetupPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/games'));

      expect(screen.getByText('Error loading games.')).toBeInTheDocument();

      consoleErrorSpy.mockRestore();
    });
  });

  // =============================================================================
  // SETUP GUIDE GENERATION TESTS
  // =============================================================================

  describe('Setup Guide Generation', () => {
    it('generates setup guide when button is clicked', async () => {
      setupAuthenticatedState();
      mockApi.post.mockResolvedValueOnce(mockSetupGuideResponse);

      render(<SetupPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/games'));

      const user = userEvent.setup();
      const generateButton = screen.getByRole('button', { name: 'Generate Setup Guide' });

      await user.click(generateButton);

      await waitFor(() => {
        expect(mockApi.post).toHaveBeenCalledWith('/api/v1/agents/setup', {
          gameId: 'game-1',
          chatId: null
        });
      });

      expect(screen.getByText('Progress: 0 / 3 steps')).toBeInTheDocument();
    });

    it('shows loading state while generating guide', async () => {
      setupAuthenticatedState();
      mockApi.post.mockImplementation(() => new Promise(() => {})); // Never resolves

      render(<SetupPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/games'));

      const user = userEvent.setup();
      const generateButton = screen.getByRole('button', { name: 'Generate Setup Guide' });

      await user.click(generateButton);

      await waitFor(() => {
        expect(screen.getByText('Generating your setup guide...')).toBeInTheDocument();
        expect(screen.getByRole('button', { name: 'Generating...' })).toBeInTheDocument();
      });
    });

    it('disables generate button when no game is selected', async () => {
      setupAuthenticatedState();

      render(<SetupPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/games'));

      const user = userEvent.setup();
      const gameSelect = screen.getByLabelText('Game:');

      await user.selectOptions(gameSelect, '');

      const generateButton = screen.getByRole('button', { name: 'Generate Setup Guide' }) as HTMLButtonElement;
      expect(generateButton.disabled).toBe(true);
    });

    it('handles setup guide generation error gracefully', async () => {
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
      setupAuthenticatedState();
      mockApi.post.mockRejectedValueOnce(new Error('Generation failed'));

      render(<SetupPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/games'));

      const user = userEvent.setup();
      const generateButton = screen.getByRole('button', { name: 'Generate Setup Guide' });

      await user.click(generateButton);

      await waitFor(() => {
        expect(screen.getByText('Error generating setup guide. Please try again.')).toBeInTheDocument();
      });

      consoleErrorSpy.mockRestore();
    });
  });

  // =============================================================================
  // SETUP STEPS INTERACTION TESTS
  // =============================================================================

  describe('Setup Steps Interaction', () => {
    beforeEach(async () => {
      setupAuthenticatedState();
      mockApi.post.mockResolvedValueOnce(mockSetupGuideResponse);
    });

    it('displays all setup steps', async () => {
      render(<SetupPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/games'));

      const user = userEvent.setup();
      await user.click(screen.getByRole('button', { name: 'Generate Setup Guide' }));

      await waitFor(() => {
        expect(screen.getByText('1. Place the board')).toBeInTheDocument();
        expect(screen.getByText('2. Arrange pieces')).toBeInTheDocument();
        expect(screen.getByText('3. Set timer (optional)')).toBeInTheDocument();
      });
    });

    it('marks optional steps with badge', async () => {
      render(<SetupPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/games'));

      const user = userEvent.setup();
      await user.click(screen.getByRole('button', { name: 'Generate Setup Guide' }));

      await waitFor(() => {
        const optionalBadges = screen.getAllByText('OPTIONAL');
        expect(optionalBadges.length).toBe(1);
      });
    });

    it('toggles step completion when checkbox is clicked', async () => {
      render(<SetupPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/games'));

      const user = userEvent.setup();
      await user.click(screen.getByRole('button', { name: 'Generate Setup Guide' }));

      await waitFor(() => {
        expect(screen.getByText('Progress: 0 / 3 steps')).toBeInTheDocument();
      });

      const checkboxes = screen.getAllByRole('checkbox');
      await user.click(checkboxes[0]);

      await waitFor(() => {
        expect(screen.getByText('Progress: 1 / 3 steps')).toBeInTheDocument();
      });

      await user.click(checkboxes[0]);

      await waitFor(() => {
        expect(screen.getByText('Progress: 0 / 3 steps')).toBeInTheDocument();
      });
    });

    it('updates progress percentage when steps are completed', async () => {
      render(<SetupPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/games'));

      const user = userEvent.setup();
      await user.click(screen.getByRole('button', { name: 'Generate Setup Guide' }));

      await waitFor(() => {
        expect(screen.getByText('0%')).toBeInTheDocument();
      });

      const checkboxes = screen.getAllByRole('checkbox');
      await user.click(checkboxes[0]);

      await waitFor(() => {
        // 1/3 = 33%
        expect(screen.getByText('33%')).toBeInTheDocument();
      });

      await user.click(checkboxes[1]);
      await user.click(checkboxes[2]);

      await waitFor(() => {
        expect(screen.getByText('100%')).toBeInTheDocument();
      });
    });

    it('shows completion message when all steps are done', async () => {
      render(<SetupPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/games'));

      const user = userEvent.setup();
      await user.click(screen.getByRole('button', { name: 'Generate Setup Guide' }));

      await waitFor(() => {
        expect(screen.getByText('Progress: 0 / 3 steps')).toBeInTheDocument();
      });

      const checkboxes = screen.getAllByRole('checkbox');
      await user.click(checkboxes[0]);
      await user.click(checkboxes[1]);
      await user.click(checkboxes[2]);

      await waitFor(() => {
        expect(screen.getByText(/Setup Complete!/i)).toBeInTheDocument();
        expect(screen.getByText(/Your game is ready to play/i)).toBeInTheDocument();
      });
    });

    it('resets progress when reset button is clicked', async () => {
      window.confirm = jest.fn(() => true);

      render(<SetupPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/games'));

      const user = userEvent.setup();
      await user.click(screen.getByRole('button', { name: 'Generate Setup Guide' }));

      await waitFor(() => {
        expect(screen.getByText('Progress: 0 / 3 steps')).toBeInTheDocument();
      });

      const checkboxes = screen.getAllByRole('checkbox');
      await user.click(checkboxes[0]);
      await user.click(checkboxes[1]);

      await waitFor(() => {
        expect(screen.getByText('Progress: 2 / 3 steps')).toBeInTheDocument();
      });

      const resetButton = screen.getByRole('button', { name: 'Reset Progress' });
      await user.click(resetButton);

      expect(window.confirm).toHaveBeenCalledWith('Are you sure you want to reset all progress?');

      await waitFor(() => {
        expect(screen.getByText('Progress: 0 / 3 steps')).toBeInTheDocument();
      });

      // All checkboxes should be unchecked
      const updatedCheckboxes = screen.getAllByRole('checkbox');
      updatedCheckboxes.forEach(checkbox => {
        expect(checkbox).not.toBeChecked();
      });
    });

    it('does not reset progress when user cancels confirmation', async () => {
      window.confirm = jest.fn(() => false);

      render(<SetupPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/games'));

      const user = userEvent.setup();
      await user.click(screen.getByRole('button', { name: 'Generate Setup Guide' }));

      await waitFor(() => {
        expect(screen.getByText('Progress: 0 / 3 steps')).toBeInTheDocument();
      });

      const checkboxes = screen.getAllByRole('checkbox');
      await user.click(checkboxes[0]);

      await waitFor(() => {
        expect(screen.getByText('Progress: 1 / 3 steps')).toBeInTheDocument();
      });

      const resetButton = screen.getByRole('button', { name: 'Reset Progress' });
      await user.click(resetButton);

      expect(window.confirm).toHaveBeenCalled();

      // Progress should remain unchanged
      expect(screen.getByText('Progress: 1 / 3 steps')).toBeInTheDocument();
    });
  });

  // =============================================================================
  // REFERENCES/CITATIONS TESTS
  // =============================================================================

  describe('References/Citations', () => {
    beforeEach(async () => {
      setupAuthenticatedState();
      mockApi.post.mockResolvedValueOnce(mockSetupGuideResponse);
    });

    it('shows references button for steps with citations', async () => {
      render(<SetupPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/games'));

      const user = userEvent.setup();
      await user.click(screen.getByRole('button', { name: 'Generate Setup Guide' }));

      await waitFor(() => {
        const referenceButtons = screen.getAllByText(/View \d+ Reference/);
        expect(referenceButtons.length).toBe(2); // Steps 1 and 3 have references
      });
    });

    it('does not show references button for steps without citations', async () => {
      render(<SetupPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/games'));

      const user = userEvent.setup();
      await user.click(screen.getByRole('button', { name: 'Generate Setup Guide' }));

      await waitFor(() => {
        expect(screen.getByText('2. Arrange pieces')).toBeInTheDocument();
      });

      // Step 2 should not have a references button (no references in mock data)
      const stepCard = screen.getByText('Place all pieces in their starting positions').closest('div');
      expect(stepCard).toBeInTheDocument();
    });

    it('opens citation modal when reference button is clicked', async () => {
      render(<SetupPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/games'));

      const user = userEvent.setup();
      await user.click(screen.getByRole('button', { name: 'Generate Setup Guide' }));

      await waitFor(() => {
        expect(screen.getAllByText('View 1 Reference').length).toBeGreaterThan(0);
      });

      // Click first reference button (from step 1)
      const referenceButtons = screen.getAllByText('View 1 Reference');
      await user.click(referenceButtons[0]);

      await waitFor(() => {
        expect(screen.getByRole('heading', { name: 'References' })).toBeInTheDocument();
        expect(screen.getByText('Board setup instructions')).toBeInTheDocument();
        expect(screen.getByText('chess-rules.pdf')).toBeInTheDocument();
        expect(screen.getByText('Page 2')).toBeInTheDocument();
      });
    });

    it('closes citation modal when close button is clicked', async () => {
      render(<SetupPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/games'));

      const user = userEvent.setup();
      await user.click(screen.getByRole('button', { name: 'Generate Setup Guide' }));

      await waitFor(() => {
        expect(screen.getAllByText('View 1 Reference').length).toBeGreaterThan(0);
      });

      const referenceButtons = screen.getAllByText('View 1 Reference');
      await user.click(referenceButtons[0]);

      await waitFor(() => {
        expect(screen.getByRole('heading', { name: 'References' })).toBeInTheDocument();
      });

      // Close button has title="Close" but no accessible name, so query by title
      const closeButton = screen.getByTitle('Close');
      await user.click(closeButton);

      await waitFor(() => {
        expect(screen.queryByRole('heading', { name: 'References' })).not.toBeInTheDocument();
      });
    });

    it('closes citation modal when clicking outside', async () => {
      render(<SetupPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/games'));

      const user = userEvent.setup();
      await user.click(screen.getByRole('button', { name: 'Generate Setup Guide' }));

      await waitFor(() => {
        expect(screen.getAllByText('View 1 Reference').length).toBeGreaterThan(0);
      });

      const referenceButtons = screen.getAllByText('View 1 Reference');
      await user.click(referenceButtons[0]);

      await waitFor(() => {
        expect(screen.getByRole('heading', { name: 'References' })).toBeInTheDocument();
      });

      // Click on backdrop (outside the modal content)
      // The structure is: backdrop > content > header-container > heading
      // So we need 3 levels up from the heading to get to the backdrop
      const backdrop = screen.getByRole('heading', { name: 'References' }).parentElement?.parentElement?.parentElement;
      if (backdrop) {
        await user.click(backdrop);

        await waitFor(() => {
          expect(screen.queryByRole('heading', { name: 'References' })).not.toBeInTheDocument();
        });
      }
    });
  });

  // =============================================================================
  // UI STATE TESTS
  // =============================================================================

  describe('UI State', () => {
    it('shows empty state before generating guide', async () => {
      setupAuthenticatedState();

      render(<SetupPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/games'));

      expect(screen.getByText('No Setup Guide Yet')).toBeInTheDocument();
      expect(screen.getByText(/Select a game and click "Generate Setup Guide"/i)).toBeInTheDocument();
    });

    it('displays game title and estimated time', async () => {
      setupAuthenticatedState();
      mockApi.post.mockResolvedValueOnce(mockSetupGuideResponse);

      render(<SetupPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/games'));

      const user = userEvent.setup();
      await user.click(screen.getByRole('button', { name: 'Generate Setup Guide' }));

      await waitFor(() => {
        // Look for heading specifically, not the dropdown option
        expect(screen.getByRole('heading', { name: 'Chess' })).toBeInTheDocument();
        expect(screen.getByText('Estimated setup time: 5 minutes')).toBeInTheDocument();
      });
    });

    it('displays AI confidence score when available', async () => {
      setupAuthenticatedState();
      mockApi.post.mockResolvedValueOnce(mockSetupGuideResponse);

      render(<SetupPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/games'));

      const user = userEvent.setup();
      await user.click(screen.getByRole('button', { name: 'Generate Setup Guide' }));

      await waitFor(() => {
        expect(screen.getByText('AI Confidence: 95%')).toBeInTheDocument();
      });
    });

    it('disables reset button when no progress has been made', async () => {
      setupAuthenticatedState();
      mockApi.post.mockResolvedValueOnce(mockSetupGuideResponse);

      render(<SetupPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/games'));

      const user = userEvent.setup();
      await user.click(screen.getByRole('button', { name: 'Generate Setup Guide' }));

      await waitFor(() => {
        const resetButton = screen.getByRole('button', { name: 'Reset Progress' }) as HTMLButtonElement;
        expect(resetButton.disabled).toBe(true);
      });
    });

    it('enables reset button when progress has been made', async () => {
      setupAuthenticatedState();
      mockApi.post.mockResolvedValueOnce(mockSetupGuideResponse);

      render(<SetupPage />);

      await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/api/v1/games'));

      const user = userEvent.setup();
      await user.click(screen.getByRole('button', { name: 'Generate Setup Guide' }));

      await waitFor(() => {
        expect(screen.getByText('Progress: 0 / 3 steps')).toBeInTheDocument();
      });

      const checkboxes = screen.getAllByRole('checkbox');
      await user.click(checkboxes[0]);

      await waitFor(() => {
        const resetButton = screen.getByRole('button', { name: 'Reset Progress' }) as HTMLButtonElement;
        expect(resetButton.disabled).toBe(false);
      });
    });
  });
});
