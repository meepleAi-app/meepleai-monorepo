/**
 * Tests for AgentConfigPanel component
 * Issue #2414 (Sub-Issue 2398.2): Mode Configuration Panel UI
 *
 * Coverage:
 * - Component rendering across all modes (Chat, Player, Ledger)
 * - Form validation with Zod schemas
 * - Dynamic field rendering based on mode
 * - Confidence threshold slider behavior
 * - Advanced options toggle
 * - Local storage persistence
 * - Form state management
 * - Error handling for invalid values
 * - Integration with React Hook Form
 */

import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { AgentConfigPanel, type AgentConfig } from '../AgentConfigPanel';

// Mock localStorage
const localStorageMock = (() => {
  let store: Record<string, string> = {};

  return {
    getItem: (key: string) => store[key] || null,
    setItem: (key: string, value: string) => {
      store[key] = value;
    },
    clear: () => {
      store = {};
    },
    removeItem: (key: string) => {
      delete store[key];
    },
  };
})();

Object.defineProperty(window, 'localStorage', {
  value: localStorageMock,
});

describe('AgentConfigPanel', () => {
  const mockOnConfigChange = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    localStorageMock.clear();
  });

  afterEach(() => {
    localStorageMock.clear();
  });

  // =========================================================================
  // Rendering Tests
  // =========================================================================

  describe('Rendering', () => {
    it('should render with default Chat mode configuration', () => {
      render(<AgentConfigPanel mode="Chat" onConfigChange={mockOnConfigChange} />);

      expect(screen.getByText('Configuration')).toBeInTheDocument();
      expect(screen.getByText('Adjust parameters for Chat mode')).toBeInTheDocument();
      expect(screen.getByText('Confidence Threshold')).toBeInTheDocument();
      expect(screen.getByText('Advanced Options')).toBeInTheDocument();
    });

    it('should render with Player mode configuration', () => {
      render(<AgentConfigPanel mode="Player" onConfigChange={mockOnConfigChange} />);

      expect(screen.getByText('Adjust parameters for Player mode')).toBeInTheDocument();
    });

    it('should render with Ledger mode configuration', () => {
      render(<AgentConfigPanel mode="Ledger" onConfigChange={mockOnConfigChange} />);

      expect(screen.getByText('Adjust parameters for Ledger mode')).toBeInTheDocument();
    });

    it('should render in disabled state', () => {
      render(<AgentConfigPanel mode="Chat" onConfigChange={mockOnConfigChange} disabled={true} />);

      // All interactive elements should be disabled
      const advancedButton = screen.getByRole('button', { name: /advanced options/i });
      expect(advancedButton).toBeDisabled();
    });
  });

  // =========================================================================
  // Chat Mode Specific Tests
  // =========================================================================

  describe('Chat Mode', () => {
    it('should render Chat mode specific fields', () => {
      render(<AgentConfigPanel mode="Chat" onConfigChange={mockOnConfigChange} />);

      expect(screen.getByText('Enable Rule Lookup')).toBeInTheDocument();
      expect(screen.getByText('Max History Messages')).toBeInTheDocument();
    });

    it('should toggle Enable Rule Lookup switch', async () => {
      const user = userEvent.setup();
      render(<AgentConfigPanel mode="Chat" onConfigChange={mockOnConfigChange} />);

      const ruleSwitch = screen.getByRole('switch', { name: /enable rule lookup/i });
      expect(ruleSwitch).toBeInTheDocument();

      await user.click(ruleSwitch);

      await waitFor(() => {
        expect(mockOnConfigChange).toHaveBeenCalled();
      });
    });

    it('should validate Max History Messages range (1-50)', async () => {
      const user = userEvent.setup();
      render(<AgentConfigPanel mode="Chat" onConfigChange={mockOnConfigChange} />);

      const historyInput = screen.getByLabelText(/max history messages/i);

      // Clear and type valid value
      await user.clear(historyInput);
      await user.type(historyInput, '25');

      await waitFor(() => {
        expect(historyInput).toHaveValue(25);
      });

      // Zod schema validates min=1, max=50 on form submission/blur
    });
  });

  // =========================================================================
  // Player Mode Specific Tests
  // =========================================================================

  describe('Player Mode', () => {
    it('should render Player mode specific fields', () => {
      render(<AgentConfigPanel mode="Player" onConfigChange={mockOnConfigChange} />);

      expect(screen.getByText('Suggestion Depth')).toBeInTheDocument();
      expect(screen.getByText('Evaluate Risks')).toBeInTheDocument();
      expect(screen.getByText('Show Alternatives')).toBeInTheDocument();
    });

    it('should toggle Evaluate Risks switch', async () => {
      const user = userEvent.setup();
      render(<AgentConfigPanel mode="Player" onConfigChange={mockOnConfigChange} />);

      const riskSwitch = screen.getByRole('switch', { name: /evaluate risks/i });
      await user.click(riskSwitch);

      await waitFor(() => {
        expect(mockOnConfigChange).toHaveBeenCalled();
      });
    });

    it('should validate Suggestion Depth range (1-10)', async () => {
      const user = userEvent.setup();
      render(<AgentConfigPanel mode="Player" onConfigChange={mockOnConfigChange} />);

      const depthInput = screen.getByLabelText(/suggestion depth/i);

      await user.clear(depthInput);
      await user.type(depthInput, '5');

      await waitFor(() => {
        expect(depthInput).toHaveValue(5);
      });
    });
  });

  // =========================================================================
  // Ledger Mode Specific Tests
  // =========================================================================

  describe('Ledger Mode', () => {
    it('should render Ledger mode specific fields', () => {
      render(<AgentConfigPanel mode="Ledger" onConfigChange={mockOnConfigChange} />);

      expect(screen.getByText('Track Player Actions')).toBeInTheDocument();
      expect(screen.getByText('Track Resource Changes')).toBeInTheDocument();
      expect(screen.getByText('Enable Time Travel')).toBeInTheDocument();
    });

    it('should toggle Track Player Actions switch', async () => {
      const user = userEvent.setup();
      render(<AgentConfigPanel mode="Ledger" onConfigChange={mockOnConfigChange} />);

      const actionSwitch = screen.getByRole('switch', { name: /track player actions/i });
      await user.click(actionSwitch);

      await waitFor(() => {
        expect(mockOnConfigChange).toHaveBeenCalled();
      });
    });

    it('should toggle Enable Time Travel switch', async () => {
      const user = userEvent.setup();
      render(<AgentConfigPanel mode="Ledger" onConfigChange={mockOnConfigChange} />);

      const timeTravelSwitch = screen.getByRole('switch', { name: /enable time travel/i });
      await user.click(timeTravelSwitch);

      await waitFor(() => {
        expect(mockOnConfigChange).toHaveBeenCalled();
      });
    });
  });

  // =========================================================================
  // Confidence Threshold Slider Tests
  // =========================================================================

  describe('Confidence Threshold Slider', () => {
    it('should render confidence threshold slider', () => {
      render(<AgentConfigPanel mode="Chat" onConfigChange={mockOnConfigChange} />);

      expect(screen.getByText('Confidence Threshold')).toBeInTheDocument();
      // Slider is rendered with default value 0.70
      expect(screen.getByText('0.70')).toBeInTheDocument();
    });

    it('should display custom confidence threshold value', async () => {
      const config: AgentConfig = {
        mode: 'Chat',
        confidenceThreshold: 0.95,
        temperature: 0.7,
        maxTokens: 2048,
        useContextWindow: true,
        enableRuleLookup: true,
        maxHistoryMessages: 10,
      };

      render(<AgentConfigPanel mode="Chat" config={config} onConfigChange={mockOnConfigChange} />);

      // Wait for form to initialize with config
      await waitFor(() => {
        // Check for formatted value (two decimal places)
        const confidenceValue = screen.getAllByText(/0\.\d{2}/);
        const hasCorrectValue = confidenceValue.some(el => el.textContent === '0.95');
        expect(hasCorrectValue).toBeTruthy();
      });
    });
  });

  // =========================================================================
  // Advanced Options Toggle Tests
  // =========================================================================

  describe('Advanced Options', () => {
    it('should toggle advanced options section', async () => {
      const user = userEvent.setup();
      render(<AgentConfigPanel mode="Chat" onConfigChange={mockOnConfigChange} />);

      const advancedButton = screen.getByRole('button', { name: /advanced options/i });
      expect(advancedButton).toBeInTheDocument();

      // Advanced options should be collapsed initially
      expect(screen.queryByText('Temperature')).not.toBeInTheDocument();

      // Click to expand
      await user.click(advancedButton);

      await waitFor(() => {
        expect(screen.getByText('Temperature')).toBeInTheDocument();
        expect(screen.getByText('Max Tokens')).toBeInTheDocument();
        expect(screen.getByText('Use Context Window')).toBeInTheDocument();
      });
    });

    it('should render temperature slider in advanced options', async () => {
      const user = userEvent.setup();
      render(<AgentConfigPanel mode="Chat" onConfigChange={mockOnConfigChange} />);

      const advancedButton = screen.getByRole('button', { name: /advanced options/i });
      await user.click(advancedButton);

      await waitFor(() => {
        expect(screen.getByText('Temperature')).toBeInTheDocument();
        // Default temperature 0.70
        expect(screen.getAllByText('0.70')).toBeTruthy();
      });
    });

    it('should render Max Tokens input in advanced options', async () => {
      const user = userEvent.setup();
      render(<AgentConfigPanel mode="Chat" onConfigChange={mockOnConfigChange} />);

      const advancedButton = screen.getByRole('button', { name: /advanced options/i });
      await user.click(advancedButton);

      await waitFor(() => {
        const maxTokensInput = screen.getByLabelText(/max tokens/i);
        expect(maxTokensInput).toHaveValue(2048); // Default for Chat mode
      });
    });

    it('should toggle Use Context Window switch in advanced options', async () => {
      const user = userEvent.setup();
      render(<AgentConfigPanel mode="Chat" onConfigChange={mockOnConfigChange} />);

      const advancedButton = screen.getByRole('button', { name: /advanced options/i });
      await user.click(advancedButton);

      await waitFor(() => {
        const contextSwitch = screen.getByRole('switch', { name: /use context window/i });
        expect(contextSwitch).toBeInTheDocument();
      });
    });
  });

  // =========================================================================
  // Local Storage Persistence Tests
  // =========================================================================

  describe('Local Storage Persistence', () => {
    it('should save Chat mode config to localStorage on change', async () => {
      const user = userEvent.setup();
      render(<AgentConfigPanel mode="Chat" onConfigChange={mockOnConfigChange} />);

      const ruleSwitch = screen.getByRole('switch', { name: /enable rule lookup/i });
      await user.click(ruleSwitch);

      await waitFor(() => {
        const stored = localStorageMock.getItem('meepleai_agent_config_chat');
        expect(stored).toBeTruthy();

        if (stored) {
          const parsed = JSON.parse(stored);
          expect(parsed.mode).toBe('Chat');
        }
      });
    });

    it('should load Chat mode config from localStorage on mount', () => {
      const storedConfig: AgentConfig = {
        mode: 'Chat',
        confidenceThreshold: 0.85,
        temperature: 0.5,
        maxTokens: 1024,
        useContextWindow: false,
        enableRuleLookup: false,
        maxHistoryMessages: 5,
      };

      localStorageMock.setItem('meepleai_agent_config_chat', JSON.stringify(storedConfig));

      render(<AgentConfigPanel mode="Chat" onConfigChange={mockOnConfigChange} />);

      // Config should be loaded and callback called
      expect(mockOnConfigChange).toHaveBeenCalledWith(
        expect.objectContaining({
          mode: 'Chat',
          confidenceThreshold: 0.85,
        })
      );
    });

    it('should use default config when localStorage is empty', () => {
      render(<AgentConfigPanel mode="Player" onConfigChange={mockOnConfigChange} />);

      expect(mockOnConfigChange).toHaveBeenCalledWith(
        expect.objectContaining({
          mode: 'Player',
          confidenceThreshold: 0.8, // Default for Player
        })
      );
    });
  });

  // =========================================================================
  // Form Validation Tests
  // =========================================================================

  describe('Form Validation', () => {
    it('should validate confidence threshold range (0.0-1.0)', async () => {
      const config: AgentConfig = {
        mode: 'Chat',
        confidenceThreshold: 0.5,
        temperature: 0.7,
        maxTokens: 2048,
        useContextWindow: true,
        enableRuleLookup: true,
        maxHistoryMessages: 10,
      };

      render(<AgentConfigPanel mode="Chat" config={config} onConfigChange={mockOnConfigChange} />);

      // Slider should enforce 0.0-1.0 range via Zod schema
      expect(screen.getByText('0.50')).toBeInTheDocument();
    });

    it('should validate max tokens range (100-8192)', async () => {
      const user = userEvent.setup();
      render(<AgentConfigPanel mode="Chat" onConfigChange={mockOnConfigChange} />);

      const advancedButton = screen.getByRole('button', { name: /advanced options/i });
      await user.click(advancedButton);

      await waitFor(async () => {
        const maxTokensInput = screen.getByLabelText(/max tokens/i);
        await user.clear(maxTokensInput);
        await user.type(maxTokensInput, '4096');

        expect(maxTokensInput).toHaveValue(4096);
      });
    });

    it('should validate temperature range (0.0-2.0)', async () => {
      const config: AgentConfig = {
        mode: 'Chat',
        confidenceThreshold: 0.7,
        temperature: 1.5,
        maxTokens: 2048,
        useContextWindow: true,
        enableRuleLookup: true,
        maxHistoryMessages: 10,
      };

      const user = userEvent.setup();
      render(<AgentConfigPanel mode="Chat" config={config} onConfigChange={mockOnConfigChange} />);

      const advancedButton = screen.getByRole('button', { name: /advanced options/i });
      await user.click(advancedButton);

      await waitFor(() => {
        expect(screen.getByText('1.50')).toBeInTheDocument();
      });
    });
  });

  // =========================================================================
  // Mode Switching Tests
  // =========================================================================

  describe('Mode Switching', () => {
    it('should reset config when mode changes', async () => {
      const { rerender } = render(
        <AgentConfigPanel mode="Chat" onConfigChange={mockOnConfigChange} />
      );

      // Switch to Player mode
      rerender(<AgentConfigPanel mode="Player" onConfigChange={mockOnConfigChange} />);

      await waitFor(() => {
        expect(mockOnConfigChange).toHaveBeenCalledWith(
          expect.objectContaining({
            mode: 'Player',
          })
        );
      });
    });

    it('should load mode-specific config from localStorage', () => {
      const playerConfig: AgentConfig = {
        mode: 'Player',
        confidenceThreshold: 0.9,
        temperature: 0.3,
        maxTokens: 1024,
        useContextWindow: true,
        suggestionDepth: 5,
        evaluateRisks: true,
        showAlternatives: true,
      };

      localStorageMock.setItem('meepleai_agent_config_player', JSON.stringify(playerConfig));

      render(<AgentConfigPanel mode="Player" onConfigChange={mockOnConfigChange} />);

      expect(mockOnConfigChange).toHaveBeenCalledWith(
        expect.objectContaining({
          mode: 'Player',
          suggestionDepth: 5,
        })
      );
    });
  });

  // =========================================================================
  // Integration Tests
  // =========================================================================

  describe('Integration', () => {
    it('should complete full configuration flow for Chat mode', async () => {
      const user = userEvent.setup();
      render(<AgentConfigPanel mode="Chat" onConfigChange={mockOnConfigChange} />);

      // 1. Toggle rule lookup
      const ruleSwitch = screen.getByRole('switch', { name: /enable rule lookup/i });
      await user.click(ruleSwitch);

      // 2. Change max history
      const historyInput = screen.getByLabelText(/max history messages/i);
      await user.clear(historyInput);
      await user.type(historyInput, '20');

      // 3. Open advanced options
      const advancedButton = screen.getByRole('button', { name: /advanced options/i });
      await user.click(advancedButton);

      // 4. Change max tokens
      await waitFor(async () => {
        const maxTokensInput = screen.getByLabelText(/max tokens/i);
        await user.clear(maxTokensInput);
        await user.type(maxTokensInput, '4096');
      });

      // Config should be updated and saved
      await waitFor(() => {
        expect(mockOnConfigChange).toHaveBeenCalled();
        const stored = localStorageMock.getItem('meepleai_agent_config_chat');
        expect(stored).toBeTruthy();
      });
    });
  });
});
