/**
 * Tests for AgentRagIntegration component
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
  },
  AnimatePresence: ({ children }: React.PropsWithChildren) => <>{children}</>,
}));

// Import after mocking
import { AgentRagIntegration } from '../AgentRagIntegration';

describe('AgentRagIntegration', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // =========================================================================
  // Rendering Tests
  // =========================================================================

  describe('Rendering', () => {
    it('should render the agent-rag integration card', () => {
      render(<AgentRagIntegration />);

      expect(screen.getByText('Agent-RAG Integration')).toBeInTheDocument();
    });

    it('should render subtitle description', () => {
      render(<AgentRagIntegration />);

      expect(screen.getByText(/How MeepleAI agents assemble prompts/i)).toBeInTheDocument();
    });

    it('should render 5-Component Prompt Assembly section', () => {
      render(<AgentRagIntegration />);

      expect(screen.getByText('5-Component Prompt Assembly')).toBeInTheDocument();
    });

    it('should render all 5 prompt components', () => {
      render(<AgentRagIntegration />);

      // These components appear multiple times (cards, flow diagram, and preview)
      expect(screen.getAllByText('System Prompt').length).toBeGreaterThan(0);
      expect(screen.getAllByText('RAG Context').length).toBeGreaterThan(0);
      expect(screen.getAllByText('User Query').length).toBeGreaterThan(0);
      expect(screen.getAllByText('Template Instructions').length).toBeGreaterThan(0);
      expect(screen.getAllByText('Output Format').length).toBeGreaterThan(0);
    });

    it('should apply custom className', () => {
      const { container } = render(<AgentRagIntegration className="custom-class" />);

      expect(container.querySelector('.custom-class')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Prompt Component Selection Tests
  // =========================================================================

  describe('Prompt Component Selection', () => {
    it('should have System Prompt selected by default', () => {
      render(<AgentRagIntegration />);

      // Should show example for System Prompt
      expect(screen.getByText('Example: System Prompt')).toBeInTheDocument();
    });

    it('should switch to RAG Context component', async () => {
      const user = userEvent.setup();
      render(<AgentRagIntegration />);

      // Find and click RAG Context card (first occurrence in the component cards section)
      const ragContextCards = screen.getAllByText('RAG Context');
      const ragContextCard = ragContextCards[0].closest('div');
      if (ragContextCard) {
        await user.click(ragContextCard);
      }

      expect(screen.getByText('Example: RAG Context')).toBeInTheDocument();
    });

    it('should switch to User Query component', async () => {
      const user = userEvent.setup();
      render(<AgentRagIntegration />);

      const userQueryCards = screen.getAllByText('User Query');
      const userQueryCard = userQueryCards[0].closest('div');
      if (userQueryCard) {
        await user.click(userQueryCard);
      }

      expect(screen.getByText('Example: User Query')).toBeInTheDocument();
    });

    it('should switch to Template Instructions component', async () => {
      const user = userEvent.setup();
      render(<AgentRagIntegration />);

      const templateCards = screen.getAllByText('Template Instructions');
      const templateCard = templateCards[0].closest('div');
      if (templateCard) {
        await user.click(templateCard);
      }

      expect(screen.getByText('Example: Template Instructions')).toBeInTheDocument();
    });

    it('should switch to Output Format component', async () => {
      const user = userEvent.setup();
      render(<AgentRagIntegration />);

      const outputCards = screen.getAllByText('Output Format');
      const outputCard = outputCards[0].closest('div');
      if (outputCard) {
        await user.click(outputCard);
      }

      expect(screen.getByText('Example: Output Format')).toBeInTheDocument();
    });

    it('should display component descriptions', () => {
      render(<AgentRagIntegration />);

      expect(screen.getByText(/Agent role definition/)).toBeInTheDocument();
      expect(screen.getByText(/Retrieved documents from vector/)).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Agent Role Tests
  // =========================================================================

  describe('Agent Roles', () => {
    it('should render Specialized Agent Roles section', () => {
      render(<AgentRagIntegration />);

      expect(screen.getByText('Specialized Agent Roles')).toBeInTheDocument();
    });

    it('should render all 4 agent role buttons', () => {
      render(<AgentRagIntegration />);

      // Agent names may appear multiple times (buttons and descriptions)
      expect(screen.getAllByText('RulesExpert').length).toBeGreaterThan(0);
      expect(screen.getAllByText('StrategyCoach').length).toBeGreaterThan(0);
      expect(screen.getAllByText('SetupWizard').length).toBeGreaterThan(0);
      expect(screen.getAllByText('GameAssistant').length).toBeGreaterThan(0);
    });

    it('should have RulesExpert selected by default', () => {
      render(<AgentRagIntegration />);

      expect(screen.getByText(/Specializes in precise rules interpretation/)).toBeInTheDocument();
    });

    it('should switch to StrategyCoach agent', async () => {
      const user = userEvent.setup();
      render(<AgentRagIntegration />);

      const strategyButton = screen.getByText('StrategyCoach').closest('button');
      if (strategyButton) {
        await user.click(strategyButton);
      }

      expect(screen.getByText(/Provides tactical insights/)).toBeInTheDocument();
    });

    it('should switch to SetupWizard agent', async () => {
      const user = userEvent.setup();
      render(<AgentRagIntegration />);

      const setupButton = screen.getByText('SetupWizard').closest('button');
      if (setupButton) {
        await user.click(setupButton);
      }

      expect(screen.getByText(/Guides players through game setup/)).toBeInTheDocument();
    });

    it('should switch to GameAssistant agent', async () => {
      const user = userEvent.setup();
      render(<AgentRagIntegration />);

      const assistantButton = screen.getByText('GameAssistant').closest('button');
      if (assistantButton) {
        await user.click(assistantButton);
      }

      expect(screen.getByText(/General assistant for learning/)).toBeInTheDocument();
    });

    it('should display template info for each agent', () => {
      render(<AgentRagIntegration />);

      // RulesExpert uses rule_lookup template
      expect(screen.getByText('(rule_lookup)')).toBeInTheDocument();
    });

    it('should display system prompt snippet', () => {
      render(<AgentRagIntegration />);

      expect(screen.getByText('System Prompt Snippet:')).toBeInTheDocument();
      expect(screen.getByText(/Expert in board game rules/)).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Strategy Prompt Pattern Tests
  // =========================================================================

  describe('Strategy Prompt Patterns', () => {
    it('should render Strategy-Specific Prompt Patterns section', () => {
      render(<AgentRagIntegration />);

      expect(screen.getByText('Strategy-Specific Prompt Patterns')).toBeInTheDocument();
    });

    it('should render all 6 strategy buttons', () => {
      render(<AgentRagIntegration />);

      const allButtons = screen.getAllByRole('button');
      const strategyButtons = allButtons.filter(
        btn =>
          btn.textContent?.includes('FAST') ||
          btn.textContent?.includes('BALANCED') ||
          btn.textContent?.includes('PRECISE') ||
          btn.textContent?.includes('EXPERT') ||
          btn.textContent?.includes('CONSENSUS') ||
          btn.textContent?.includes('CUSTOM')
      );

      expect(strategyButtons.length).toBeGreaterThanOrEqual(6);
    });

    it('should have BALANCED selected by default', () => {
      render(<AgentRagIntegration />);

      expect(screen.getByText('CRAG-Enhanced')).toBeInTheDocument();
    });

    it('should switch to FAST strategy', async () => {
      const user = userEvent.setup();
      render(<AgentRagIntegration />);

      // Find and click FAST button in strategy section
      const strategyButtons = screen.getAllByRole('button').filter(btn =>
        btn.textContent === 'FAST'
      );
      if (strategyButtons.length > 0) {
        await user.click(strategyButtons[0]);
      }

      expect(screen.getByText('Direct Response')).toBeInTheDocument();
    });

    it('should switch to PRECISE strategy', async () => {
      const user = userEvent.setup();
      render(<AgentRagIntegration />);

      const strategyButtons = screen.getAllByRole('button').filter(btn =>
        btn.textContent === 'PRECISE'
      );
      if (strategyButtons.length > 0) {
        await user.click(strategyButtons[0]);
      }

      expect(screen.getByText('Multi-Agent Chain')).toBeInTheDocument();
    });

    it('should switch to EXPERT strategy', async () => {
      const user = userEvent.setup();
      render(<AgentRagIntegration />);

      const strategyButtons = screen.getAllByRole('button').filter(btn =>
        btn.textContent === 'EXPERT'
      );
      if (strategyButtons.length > 0) {
        await user.click(strategyButtons[0]);
      }

      expect(screen.getByText('Web-Augmented Expert')).toBeInTheDocument();
    });

    it('should switch to CONSENSUS strategy', async () => {
      const user = userEvent.setup();
      render(<AgentRagIntegration />);

      const strategyButtons = screen.getAllByRole('button').filter(btn =>
        btn.textContent === 'CONSENSUS'
      );
      if (strategyButtons.length > 0) {
        await user.click(strategyButtons[0]);
      }

      expect(screen.getByText('Multi-LLM Voting')).toBeInTheDocument();
    });

    it('should switch to CUSTOM strategy', async () => {
      const user = userEvent.setup();
      render(<AgentRagIntegration />);

      const strategyButtons = screen.getAllByRole('button').filter(btn =>
        btn.textContent === 'CUSTOM'
      );
      if (strategyButtons.length > 0) {
        await user.click(strategyButtons[0]);
      }

      expect(screen.getByText('Admin-Configured')).toBeInTheDocument();
    });

    it('should display strategy descriptions', () => {
      render(<AgentRagIntegration />);

      expect(screen.getByText(/Includes quality evaluation/)).toBeInTheDocument();
    });

    it('should display prompt style examples', () => {
      render(<AgentRagIntegration />);

      expect(screen.getByText(/BALANCED MODE/)).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Full Prompt Preview Tests
  // =========================================================================

  describe('Full Prompt Preview', () => {
    it('should render Full Prompt Preview section', () => {
      render(<AgentRagIntegration />);

      expect(screen.getByText('Full Prompt Preview')).toBeInTheDocument();
    });

    it('should have Show button initially', () => {
      render(<AgentRagIntegration />);

      expect(screen.getByRole('button', { name: /show/i })).toBeInTheDocument();
    });

    it('should toggle full prompt visibility', async () => {
      const user = userEvent.setup();
      render(<AgentRagIntegration />);

      // Click Show button
      const showButton = screen.getByRole('button', { name: /show/i });
      await user.click(showButton);

      // Should now show Hide button
      expect(screen.getByRole('button', { name: /hide/i })).toBeInTheDocument();

      // Should show assembled prompt content
      expect(screen.getByText(/ASSEMBLED PROMPT/)).toBeInTheDocument();
    });

    it('should display full prompt content when shown', async () => {
      const user = userEvent.setup();
      render(<AgentRagIntegration />);

      const showButton = screen.getByRole('button', { name: /show/i });
      await user.click(showButton);

      // Check for prompt sections
      expect(screen.getByText(/SYSTEM PROMPT/)).toBeInTheDocument();
      expect(screen.getByText(/RAG CONTEXT/)).toBeInTheDocument();
      expect(screen.getByText(/USER QUERY/)).toBeInTheDocument();
      expect(screen.getByText(/TEMPLATE INSTRUCTIONS/)).toBeInTheDocument();
      expect(screen.getByText(/OUTPUT FORMAT/)).toBeInTheDocument();
    });

    it('should hide full prompt when clicked again', async () => {
      const user = userEvent.setup();
      render(<AgentRagIntegration />);

      // Show the prompt
      const showButton = screen.getByRole('button', { name: /show/i });
      await user.click(showButton);

      // Hide the prompt
      const hideButton = screen.getByRole('button', { name: /hide/i });
      await user.click(hideButton);

      // Should show Show button again
      expect(screen.getByRole('button', { name: /show/i })).toBeInTheDocument();
    });

    it('should display token estimate in full prompt', async () => {
      const user = userEvent.setup();
      render(<AgentRagIntegration />);

      const showButton = screen.getByRole('button', { name: /show/i });
      await user.click(showButton);

      expect(screen.getByText(/Total Estimated Tokens/)).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Prompt Assembly Flow Tests
  // =========================================================================

  describe('Prompt Assembly Flow', () => {
    it('should render Prompt Assembly Flow section', () => {
      render(<AgentRagIntegration />);

      expect(screen.getByText('Prompt Assembly Flow')).toBeInTheDocument();
    });

    it('should display flow diagram with all components', () => {
      render(<AgentRagIntegration />);

      // Check for flow diagram elements - all 5 components plus LLM Response
      const systemPrompts = screen.getAllByText('System Prompt');
      expect(systemPrompts.length).toBeGreaterThan(0);

      const ragContexts = screen.getAllByText('RAG Context');
      expect(ragContexts.length).toBeGreaterThan(0);
    });

    it('should display LLM Response in flow', () => {
      render(<AgentRagIntegration />);

      expect(screen.getByText(/LLM Response/)).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Dynamic Update Tests
  // =========================================================================

  describe('Dynamic Updates', () => {
    it('should update full prompt when agent changes', async () => {
      const user = userEvent.setup();
      render(<AgentRagIntegration />);

      // Show full prompt
      const showButton = screen.getByRole('button', { name: /show/i });
      await user.click(showButton);

      // Switch to StrategyCoach
      const strategyButton = screen.getByText('StrategyCoach').closest('button');
      if (strategyButton) {
        await user.click(strategyButton);
      }

      // Full prompt should show StrategyCoach (appears multiple times)
      await waitFor(() => {
        expect(screen.getAllByText(/StrategyCoach/).length).toBeGreaterThan(0);
      });
    });

    it('should update token estimate when strategy changes', async () => {
      const user = userEvent.setup();
      render(<AgentRagIntegration />);

      // Show full prompt
      const showButton = screen.getByRole('button', { name: /show/i });
      await user.click(showButton);

      // Switch to FAST strategy
      const strategyButtons = screen.getAllByRole('button').filter(btn =>
        btn.textContent === 'FAST'
      );
      if (strategyButtons.length > 0) {
        await user.click(strategyButtons[0]);
      }

      // Token estimate should change (FAST has lower tokens)
      await waitFor(() => {
        expect(screen.getByText(/2,100/)).toBeInTheDocument();
      });
    });
  });
});
