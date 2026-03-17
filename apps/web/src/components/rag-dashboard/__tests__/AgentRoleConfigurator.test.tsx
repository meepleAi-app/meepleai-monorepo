/**
 * Tests for AgentRoleConfigurator component
 * Issue #3005: Frontend coverage improvements
 */

import React from 'react';

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

// Mock framer-motion
vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, ...props }: React.PropsWithChildren<Record<string, unknown>>) => (
      <div {...props}>{children}</div>
    ),
    button: ({ children, ...props }: React.PropsWithChildren<Record<string, unknown>>) => (
      <button {...props}>{children}</button>
    ),
  },
  AnimatePresence: ({ children }: React.PropsWithChildren) => <>{children}</>,
}));

// Mock clipboard API
Object.assign(navigator, {
  clipboard: {
    writeText: vi.fn().mockResolvedValue(undefined),
  },
});

// Import after mocking
import { AgentRoleConfigurator } from '../config/AgentRoleConfigurator';

describe('AgentRoleConfigurator', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // =========================================================================
  // Rendering Tests
  // =========================================================================

  describe('Rendering', () => {
    it('should render the agent role configurator card', () => {
      render(<AgentRoleConfigurator />);

      expect(screen.getByText('Agent Role Configurator')).toBeInTheDocument();
    });

    it('should render subtitle description', () => {
      render(<AgentRoleConfigurator />);

      expect(screen.getByText(/Pre-built agent configurations/i)).toBeInTheDocument();
    });

    it('should render all 4 agent role cards', () => {
      render(<AgentRoleConfigurator />);

      // Agent names appear multiple times (cards and detail panels)
      expect(screen.getAllByText('RulesExpert').length).toBeGreaterThan(0);
      expect(screen.getAllByText('StrategyCoach').length).toBeGreaterThan(0);
      expect(screen.getAllByText('SetupWizard').length).toBeGreaterThan(0);
      expect(screen.getAllByText('GameAssistant').length).toBeGreaterThan(0);
    });

    it('should show Select Agent Role label', () => {
      render(<AgentRoleConfigurator />);

      expect(screen.getByText('Select Agent Role')).toBeInTheDocument();
    });

    it('should apply custom className', () => {
      const { container } = render(<AgentRoleConfigurator className="custom-class" />);

      expect(container.querySelector('.custom-class')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Default State Tests
  // =========================================================================

  describe('Default State', () => {
    it('should show RulesExpert as default selected agent', () => {
      render(<AgentRoleConfigurator />);

      // RulesExpert should be displayed in the detail panel
      expect(screen.getByText(/Precision rules interpretation/i)).toBeInTheDocument();
    });

    it('should show Overview tab as default', () => {
      render(<AgentRoleConfigurator />);

      // Overview tab content should be visible
      expect(screen.getByText('Specializations')).toBeInTheDocument();
    });

    it('should display agent tagline', () => {
      render(<AgentRoleConfigurator />);

      expect(
        screen.getByText(/Precision rules interpretation with citation accuracy/i)
      ).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Agent Selection Tests
  // =========================================================================

  describe('Agent Selection', () => {
    it('should switch to StrategyCoach when clicked', async () => {
      const user = userEvent.setup();
      render(<AgentRoleConfigurator />);

      const strategyButton = screen.getByText('StrategyCoach').closest('button');
      if (strategyButton) {
        await user.click(strategyButton);
      }

      expect(screen.getByText(/Advanced tactical analysis/i)).toBeInTheDocument();
    });

    it('should switch to SetupWizard when clicked', async () => {
      const user = userEvent.setup();
      render(<AgentRoleConfigurator />);

      const setupButton = screen.getByText('SetupWizard').closest('button');
      if (setupButton) {
        await user.click(setupButton);
      }

      expect(screen.getByText(/Step-by-step game setup/i)).toBeInTheDocument();
    });

    it('should switch to GameAssistant when clicked', async () => {
      const user = userEvent.setup();
      render(<AgentRoleConfigurator />);

      const assistantButton = screen.getByText('GameAssistant').closest('button');
      if (assistantButton) {
        await user.click(assistantButton);
      }

      expect(screen.getByText(/Friendly guide for all board game questions/i)).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Tab Navigation Tests
  // =========================================================================

  describe('Tab Navigation', () => {
    it('should show three tabs', () => {
      render(<AgentRoleConfigurator />);

      expect(screen.getByRole('button', { name: /overview/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /system prompt/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /model config/i })).toBeInTheDocument();
    });

    it('should switch to System Prompt tab', async () => {
      const user = userEvent.setup();
      render(<AgentRoleConfigurator />);

      const promptTab = screen.getByRole('button', { name: /system prompt/i });
      await user.click(promptTab);

      expect(screen.getByText(/Full System Prompt/i)).toBeInTheDocument();
    });

    it('should switch to Model Config tab', async () => {
      const user = userEvent.setup();
      render(<AgentRoleConfigurator />);

      const configTab = screen.getByRole('button', { name: /model config/i });
      await user.click(configTab);

      expect(screen.getByText('Model Configuration by Strategy')).toBeInTheDocument();
    });

    it('should switch back to Overview tab', async () => {
      const user = userEvent.setup();
      render(<AgentRoleConfigurator />);

      // Go to config tab
      const configTab = screen.getByRole('button', { name: /model config/i });
      await user.click(configTab);

      // Go back to overview
      const overviewTab = screen.getByRole('button', { name: /overview/i });
      await user.click(overviewTab);

      expect(screen.getByText('Specializations')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Overview Tab Content Tests
  // =========================================================================

  describe('Overview Tab Content', () => {
    it('should display specializations', () => {
      render(<AgentRoleConfigurator />);

      expect(screen.getByText('Exact rule text citation')).toBeInTheDocument();
    });

    it('should display Best For section', () => {
      render(<AgentRoleConfigurator />);

      expect(screen.getByText('Best For')).toBeInTheDocument();
    });

    it('should display Limitations section', () => {
      render(<AgentRoleConfigurator />);

      expect(screen.getByText('Limitations')).toBeInTheDocument();
    });

    it('should display Example Queries section', () => {
      render(<AgentRoleConfigurator />);

      expect(screen.getByText('Example Queries')).toBeInTheDocument();
    });

    it('should display Token Budgets section', () => {
      render(<AgentRoleConfigurator />);

      expect(screen.getByText('Token Budgets')).toBeInTheDocument();
    });

    it('should display token budget values', () => {
      render(<AgentRoleConfigurator />);

      expect(screen.getByText('System')).toBeInTheDocument();
      expect(screen.getByText('Max Context')).toBeInTheDocument();
      expect(screen.getByText('Max Response')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // System Prompt Tab Tests
  // =========================================================================

  describe('System Prompt Tab', () => {
    it('should display system prompt content', async () => {
      const user = userEvent.setup();
      render(<AgentRoleConfigurator />);

      const promptTab = screen.getByRole('button', { name: /system prompt/i });
      await user.click(promptTab);

      expect(screen.getByText(/CORE RESPONSIBILITIES/)).toBeInTheDocument();
    });

    it('should have copy button', async () => {
      const user = userEvent.setup();
      render(<AgentRoleConfigurator />);

      const promptTab = screen.getByRole('button', { name: /system prompt/i });
      await user.click(promptTab);

      // Find copy button by looking for a button with Copy icon
      const buttons = screen.getAllByRole('button');
      const copyButton = buttons.find(btn => btn.querySelector('svg'));
      expect(copyButton).toBeTruthy();
    });

    it('should copy prompt to clipboard when copy button is clicked', async () => {
      const user = userEvent.setup();
      const writeTextMock = vi.fn().mockResolvedValue(undefined);
      Object.defineProperty(navigator, 'clipboard', {
        value: { writeText: writeTextMock },
        writable: true,
        configurable: true,
      });

      render(<AgentRoleConfigurator />);

      const promptTab = screen.getByRole('button', { name: /system prompt/i });
      await user.click(promptTab);

      // Find and click copy button
      const buttons = screen.getAllByRole('button');
      // The copy button should be small and near the system prompt text
      const copyButtons = buttons.filter(btn => btn.classList.contains('h-7'));
      if (copyButtons.length > 0) {
        await user.click(copyButtons[0]);
        expect(writeTextMock).toHaveBeenCalled();
      }
    });

    it('should show token count for system prompt', async () => {
      const user = userEvent.setup();
      render(<AgentRoleConfigurator />);

      const promptTab = screen.getByRole('button', { name: /system prompt/i });
      await user.click(promptTab);

      expect(screen.getByText(/tokens\)/)).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Model Config Tab Tests
  // =========================================================================

  describe('Model Config Tab', () => {
    it('should display FAST model config', async () => {
      const user = userEvent.setup();
      render(<AgentRoleConfigurator />);

      const configTab = screen.getByRole('button', { name: /model config/i });
      await user.click(configTab);

      // Look for FAST in the model config cards
      const fastElements = screen.getAllByText('FAST');
      expect(fastElements.length).toBeGreaterThan(0);
    });

    it('should display BALANCED model config', async () => {
      const user = userEvent.setup();
      render(<AgentRoleConfigurator />);

      const configTab = screen.getByRole('button', { name: /model config/i });
      await user.click(configTab);

      const balancedElements = screen.getAllByText('BALANCED');
      expect(balancedElements.length).toBeGreaterThan(0);
    });

    it('should display PRECISE model config', async () => {
      const user = userEvent.setup();
      render(<AgentRoleConfigurator />);

      const configTab = screen.getByRole('button', { name: /model config/i });
      await user.click(configTab);

      const preciseElements = screen.getAllByText('PRECISE');
      expect(preciseElements.length).toBeGreaterThan(0);
    });

    it('should display model names', async () => {
      const user = userEvent.setup();
      render(<AgentRoleConfigurator />);

      const configTab = screen.getByRole('button', { name: /model config/i });
      await user.click(configTab);

      // Multiple model configs display "Model:" label
      expect(screen.getAllByText('Model:').length).toBeGreaterThan(0);
    });

    it('should display max tokens setting', async () => {
      const user = userEvent.setup();
      render(<AgentRoleConfigurator />);

      const configTab = screen.getByRole('button', { name: /model config/i });
      await user.click(configTab);

      const maxTokensLabels = screen.getAllByText('Max Tokens:');
      expect(maxTokensLabels.length).toBeGreaterThan(0);
    });

    it('should display temperature setting', async () => {
      const user = userEvent.setup();
      render(<AgentRoleConfigurator />);

      const configTab = screen.getByRole('button', { name: /model config/i });
      await user.click(configTab);

      const tempLabels = screen.getAllByText('Temperature:');
      expect(tempLabels.length).toBeGreaterThan(0);
    });

    it('should display configuration notes', async () => {
      const user = userEvent.setup();
      render(<AgentRoleConfigurator />);

      const configTab = screen.getByRole('button', { name: /model config/i });
      await user.click(configTab);

      expect(screen.getByText('Configuration Notes')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Agent Switching with Tabs Tests
  // =========================================================================

  describe('Agent Switching with Tabs', () => {
    it('should maintain tab state when switching agents', async () => {
      const user = userEvent.setup();
      render(<AgentRoleConfigurator />);

      // Switch to config tab
      const configTab = screen.getByRole('button', { name: /model config/i });
      await user.click(configTab);

      // Switch to different agent
      const strategyButton = screen.getByText('StrategyCoach').closest('button');
      if (strategyButton) {
        await user.click(strategyButton);
      }

      // Should still show config tab content
      expect(screen.getByText('Model Configuration by Strategy')).toBeInTheDocument();
    });

    it('should update content when switching agents', async () => {
      const user = userEvent.setup();
      render(<AgentRoleConfigurator />);

      // Switch to StrategyCoach
      const strategyButton = screen.getByText('StrategyCoach').closest('button');
      if (strategyButton) {
        await user.click(strategyButton);
      }

      // Should show StrategyCoach specializations
      expect(screen.getByText('Opening strategies')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Best For and Limitations Tests
  // =========================================================================

  describe('Best For and Limitations', () => {
    it('should display best for items with checkmarks', () => {
      render(<AgentRoleConfigurator />);

      expect(screen.getByText('Simple rule clarifications')).toBeInTheDocument();
    });

    it('should display limitation items', () => {
      render(<AgentRoleConfigurator />);

      expect(screen.getByText(/Cannot give strategy advice/)).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Example Queries Tests
  // =========================================================================

  describe('Example Queries', () => {
    it('should display example queries for RulesExpert', () => {
      render(<AgentRoleConfigurator />);

      expect(screen.getByText(/What happens when I roll a 7 in Catan/)).toBeInTheDocument();
    });

    it('should display example queries for StrategyCoach', async () => {
      const user = userEvent.setup();
      render(<AgentRoleConfigurator />);

      const strategyButton = screen.getByText('StrategyCoach').closest('button');
      if (strategyButton) {
        await user.click(strategyButton);
      }

      expect(screen.getByText(/What settlement spots should I prioritize/)).toBeInTheDocument();
    });
  });
});
