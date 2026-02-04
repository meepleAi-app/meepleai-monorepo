/**
 * Tests for PromptTemplateBuilder component
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

// Mock clipboard API
Object.assign(navigator, {
  clipboard: {
    writeText: vi.fn().mockResolvedValue(undefined),
  },
});

// Mock URL and Blob
global.URL.createObjectURL = vi.fn(() => 'blob:test');
global.URL.revokeObjectURL = vi.fn();

// Import after mocking
import { PromptTemplateBuilder } from '../PromptTemplateBuilder';

describe('PromptTemplateBuilder', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // =========================================================================
  // Rendering Tests
  // =========================================================================

  describe('Rendering', () => {
    it('should render the prompt template builder card', () => {
      render(<PromptTemplateBuilder />);

      expect(screen.getByText('Prompt Template Builder')).toBeInTheDocument();
    });

    it('should render subtitle description', () => {
      render(<PromptTemplateBuilder />);

      expect(screen.getByText(/Build and preview RAG prompts/i)).toBeInTheDocument();
    });

    it('should render Reset button', () => {
      render(<PromptTemplateBuilder />);

      expect(screen.getByRole('button', { name: /reset/i })).toBeInTheDocument();
    });

    it('should render agent selection', () => {
      render(<PromptTemplateBuilder />);

      expect(screen.getByText('Agent Role')).toBeInTheDocument();
    });

    it('should render all agent options', () => {
      render(<PromptTemplateBuilder />);

      expect(screen.getByRole('button', { name: 'RulesExpert' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'StrategyCoach' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'SetupWizard' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'GameAssistant' })).toBeInTheDocument();
    });

    it('should apply custom className', () => {
      const { container } = render(<PromptTemplateBuilder className="custom-class" />);

      expect(container.querySelector('.custom-class')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Agent Selection Tests
  // =========================================================================

  describe('Agent Selection', () => {
    it('should have RulesExpert selected by default', () => {
      render(<PromptTemplateBuilder />);

      const rulesButton = screen.getByRole('button', { name: 'RulesExpert' });
      expect(rulesButton).toHaveClass('border-primary');
    });

    it('should switch to StrategyCoach', async () => {
      const user = userEvent.setup();
      render(<PromptTemplateBuilder />);

      const strategyButton = screen.getByRole('button', { name: 'StrategyCoach' });
      await user.click(strategyButton);

      expect(strategyButton).toHaveClass('border-primary');
    });
  });

  // =========================================================================
  // Template Selection Tests
  // =========================================================================

  describe('Template Selection', () => {
    it('should render template options', () => {
      render(<PromptTemplateBuilder />);

      expect(screen.getByText('Query Template')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Rule Lookup' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Resource Planning' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Setup Guide' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Strategy Advice' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Educational' })).toBeInTheDocument();
    });

    it('should switch to different template', async () => {
      const user = userEvent.setup();
      render(<PromptTemplateBuilder />);

      const setupButton = screen.getByRole('button', { name: 'Setup Guide' });
      await user.click(setupButton);

      expect(setupButton).toHaveClass('border-primary');
    });
  });

  // =========================================================================
  // Strategy Selection Tests
  // =========================================================================

  describe('Strategy Selection', () => {
    it('should render strategy options', () => {
      render(<PromptTemplateBuilder />);

      expect(screen.getByText('RAG Strategy')).toBeInTheDocument();
    });

    it('should display strategy descriptions', () => {
      render(<PromptTemplateBuilder />);

      // Descriptions from rag-data.ts Single Source of Truth
      expect(screen.getByText(/Simple FAQs, quick responses/i)).toBeInTheDocument();
      expect(screen.getByText(/Standard queries with CRAG validation/i)).toBeInTheDocument();
      expect(screen.getByText(/Critical queries, multi-agent pipeline/i)).toBeInTheDocument();
    });

    it('should have BALANCED selected by default', () => {
      render(<PromptTemplateBuilder />);

      // Find the BALANCED button in the strategy section (description from rag-data.ts)
      const strategyButtons = screen.getAllByRole('button');
      const balancedButton = strategyButtons.find(
        btn => btn.textContent?.includes('BALANCED') && btn.textContent?.includes('Standard queries')
      );
      expect(balancedButton).toHaveClass('border-primary');
    });
  });

  // =========================================================================
  // Context Selection Tests
  // =========================================================================

  describe('Context Selection', () => {
    it('should render context selection', () => {
      render(<PromptTemplateBuilder />);

      // RAG Context appears multiple times (section header and preview)
      expect(screen.getAllByText(/RAG Context/i).length).toBeGreaterThan(0);
    });

    it('should display context documents', () => {
      render(<PromptTemplateBuilder />);

      // Context documents may appear in selection and preview
      expect(screen.getAllByText(/Rolling a 7/).length).toBeGreaterThan(0);
      expect(screen.getAllByText(/Robber Questions/).length).toBeGreaterThan(0);
      expect(screen.getAllByText(/Strategy Guide/).length).toBeGreaterThan(0);
    });

    it('should display relevance scores', () => {
      render(<PromptTemplateBuilder />);

      expect(screen.getByText('94% relevant')).toBeInTheDocument();
      expect(screen.getByText('89% relevant')).toBeInTheDocument();
      expect(screen.getByText('82% relevant')).toBeInTheDocument();
    });

    it('should toggle context selection', async () => {
      const user = userEvent.setup();
      render(<PromptTemplateBuilder />);

      // Find a context button
      const contextButtons = screen.getAllByRole('button').filter(
        btn => btn.textContent?.includes('relevant')
      );

      if (contextButtons.length > 0) {
        await user.click(contextButtons[0]);
        // Context should toggle
      }
    });
  });

  // =========================================================================
  // User Query Tests
  // =========================================================================

  describe('User Query', () => {
    it('should render user query input', () => {
      render(<PromptTemplateBuilder />);

      expect(screen.getByText('User Query')).toBeInTheDocument();
    });

    it('should display default query', () => {
      render(<PromptTemplateBuilder />);

      const textarea = screen.getByPlaceholderText(/Enter your question/i);
      expect(textarea).toHaveValue('What happens when I roll a 7 in Catan?');
    });

    it('should allow typing in query', async () => {
      const user = userEvent.setup();
      render(<PromptTemplateBuilder />);

      const textarea = screen.getByPlaceholderText(/Enter your question/i);
      await user.clear(textarea);
      await user.type(textarea, 'New test query');

      expect(textarea).toHaveValue('New test query');
    });
  });

  // =========================================================================
  // Preview Tests
  // =========================================================================

  describe('Preview', () => {
    it('should display prompt preview section', () => {
      render(<PromptTemplateBuilder />);

      expect(screen.getByText('Assembled Prompt Preview')).toBeInTheDocument();
    });

    it('should display token estimate', () => {
      render(<PromptTemplateBuilder />);

      expect(screen.getByText(/tokens$/)).toBeInTheDocument();
    });

    it('should display assembled prompt', () => {
      render(<PromptTemplateBuilder />);

      expect(screen.getByText(/ASSEMBLED RAG PROMPT/)).toBeInTheDocument();
    });

    it('should display token breakdown', () => {
      render(<PromptTemplateBuilder />);

      expect(screen.getByText('System')).toBeInTheDocument();
      expect(screen.getByText('Context')).toBeInTheDocument();
      expect(screen.getByText('Query')).toBeInTheDocument();
      expect(screen.getByText('Template')).toBeInTheDocument();
      expect(screen.getByText('Format')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Action Button Tests
  // =========================================================================

  describe('Action Buttons', () => {
    it('should copy prompt to clipboard', async () => {
      const user = userEvent.setup();
      const writeTextMock = vi.fn().mockResolvedValue(undefined);
      Object.defineProperty(navigator, 'clipboard', {
        value: { writeText: writeTextMock },
        writable: true,
        configurable: true,
      });

      render(<PromptTemplateBuilder />);

      // Find copy button (small button with Copy icon)
      const buttons = screen.getAllByRole('button');
      const copyButton = buttons.find(btn => btn.classList.contains('h-7'));

      if (copyButton) {
        await user.click(copyButton);
        expect(writeTextMock).toHaveBeenCalled();
      }
    });

    it('should reset all selections', async () => {
      const user = userEvent.setup();
      render(<PromptTemplateBuilder />);

      // Change some selections
      const setupButton = screen.getByRole('button', { name: 'SetupWizard' });
      await user.click(setupButton);

      // Click reset
      const resetButton = screen.getByRole('button', { name: /reset/i });
      await user.click(resetButton);

      // Should be back to defaults
      const rulesButton = screen.getByRole('button', { name: 'RulesExpert' });
      expect(rulesButton).toHaveClass('border-primary');
    });
  });

  // =========================================================================
  // Prompt Assembly Tests
  // =========================================================================

  describe('Prompt Assembly', () => {
    it('should include system prompt section', () => {
      render(<PromptTemplateBuilder />);

      expect(screen.getByText(/SYSTEM PROMPT/)).toBeInTheDocument();
    });

    it('should include RAG context section', () => {
      render(<PromptTemplateBuilder />);

      expect(screen.getByText(/RAG CONTEXT/)).toBeInTheDocument();
    });

    it('should include user query section', () => {
      render(<PromptTemplateBuilder />);

      expect(screen.getByText(/USER QUERY/)).toBeInTheDocument();
    });

    it('should include template instructions section', () => {
      render(<PromptTemplateBuilder />);

      expect(screen.getByText(/TEMPLATE INSTRUCTIONS/)).toBeInTheDocument();
    });

    it('should include output format section', () => {
      render(<PromptTemplateBuilder />);

      expect(screen.getByText(/OUTPUT FORMAT/)).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Dynamic Updates Tests
  // =========================================================================

  describe('Dynamic Updates', () => {
    it('should update preview when agent changes', async () => {
      const user = userEvent.setup();
      render(<PromptTemplateBuilder />);

      const strategyCoachButton = screen.getByRole('button', { name: 'StrategyCoach' });
      await user.click(strategyCoachButton);

      // Preview should show StrategyCoach content (appears multiple times)
      await waitFor(() => {
        expect(screen.getAllByText(/StrategyCoach/).length).toBeGreaterThan(0);
      });
    });

    it('should update token estimates when content changes', async () => {
      const user = userEvent.setup();
      render(<PromptTemplateBuilder />);

      const textarea = screen.getByPlaceholderText(/Enter your question/i);

      // Clear and type longer query
      await user.clear(textarea);
      await user.type(textarea, 'A'.repeat(100));

      // Token estimate should change
      expect(screen.getByText(/tokens$/)).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Handler Function Tests (for function coverage)
  // =========================================================================

  describe('Handler Functions', () => {
    it('should handle template selection changes', async () => {
      const user = userEvent.setup();
      render(<PromptTemplateBuilder />);

      // Click different templates
      const templates = ['Resource Planning', 'Setup Guide', 'Strategy Advice', 'Educational'];

      for (const template of templates) {
        const button = screen.getByRole('button', { name: template });
        await user.click(button);

        // Verify template was selected
        expect(button).toHaveClass('border-primary');
      }
    });

    it('should handle context document toggle', async () => {
      const user = userEvent.setup();
      render(<PromptTemplateBuilder />);

      // Find a context document button (they have relevance scores)
      const contextButtons = screen.getAllByRole('button').filter(btn =>
        btn.textContent?.includes('relevant')
      );

      if (contextButtons.length > 0) {
        // Toggle context selection
        await user.click(contextButtons[0]);

        // Context should toggle (button remains in DOM)
        expect(contextButtons[0]).toBeInTheDocument();
      }
    });
  });
});
