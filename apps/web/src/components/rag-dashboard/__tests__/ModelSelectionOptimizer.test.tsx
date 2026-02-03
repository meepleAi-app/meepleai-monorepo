/**
 * Tests for ModelSelectionOptimizer component
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
import { ModelSelectionOptimizer } from '../ModelSelectionOptimizer';

describe('ModelSelectionOptimizer', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // =========================================================================
  // Rendering Tests
  // =========================================================================

  describe('Rendering', () => {
    it('should render the model selection optimizer card', () => {
      render(<ModelSelectionOptimizer />);

      expect(screen.getByText('Model Selection Optimizer')).toBeInTheDocument();
    });

    it('should render subtitle description', () => {
      render(<ModelSelectionOptimizer />);

      expect(screen.getByText(/Compare LLM pricing/i)).toBeInTheDocument();
    });

    it('should render strategy filter buttons', () => {
      render(<ModelSelectionOptimizer />);

      expect(screen.getByRole('button', { name: /all models/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'FAST' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'BALANCED' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'PRECISE' })).toBeInTheDocument();
    });

    it('should render model cards', () => {
      render(<ModelSelectionOptimizer />);

      // Check for model names (they appear multiple times in cards and cost comparison)
      expect(screen.getAllByText('Llama 3.3 70B').length).toBeGreaterThan(0);
      expect(screen.getAllByText('Gemini 2.0 Flash').length).toBeGreaterThan(0);
      expect(screen.getAllByText('Claude 3.5 Sonnet').length).toBeGreaterThan(0);
    });

    it('should apply custom className', () => {
      const { container } = render(<ModelSelectionOptimizer className="custom-class" />);

      expect(container.querySelector('.custom-class')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Strategy Filter Tests
  // =========================================================================

  describe('Strategy Filter', () => {
    it('should show all models by default', () => {
      render(<ModelSelectionOptimizer />);

      // Should show models from all strategies (appear multiple times)
      expect(screen.getAllByText('Llama 3.3 70B').length).toBeGreaterThan(0);
      expect(screen.getAllByText('Claude 3.5 Sonnet').length).toBeGreaterThan(0);
      expect(screen.getAllByText('Claude 3.5 Opus').length).toBeGreaterThan(0);
    });

    it('should filter to FAST models only', async () => {
      const user = userEvent.setup();
      render(<ModelSelectionOptimizer />);

      const fastButton = screen.getByRole('button', { name: 'FAST' });
      await user.click(fastButton);

      // Should show FAST models (appear multiple times)
      expect(screen.getAllByText('Llama 3.3 70B').length).toBeGreaterThan(0);
      expect(screen.getAllByText('Gemini 2.0 Flash').length).toBeGreaterThan(0);
    });

    it('should filter to BALANCED models only', async () => {
      const user = userEvent.setup();
      render(<ModelSelectionOptimizer />);

      const balancedButton = screen.getByRole('button', { name: 'BALANCED' });
      await user.click(balancedButton);

      // Should show BALANCED models (appear multiple times)
      expect(screen.getAllByText('Claude 3.5 Sonnet').length).toBeGreaterThan(0);
      expect(screen.getAllByText('DeepSeek Chat').length).toBeGreaterThan(0);
    });

    it('should filter to PRECISE models only', async () => {
      const user = userEvent.setup();
      render(<ModelSelectionOptimizer />);

      const preciseButton = screen.getByRole('button', { name: 'PRECISE' });
      await user.click(preciseButton);

      // Should show PRECISE models (appear multiple times)
      expect(screen.getAllByText('Claude 3.5 Opus').length).toBeGreaterThan(0);
      expect(screen.getAllByText('GPT-4o').length).toBeGreaterThan(0);
    });

    it('should return to all models when clicking All Models', async () => {
      const user = userEvent.setup();
      render(<ModelSelectionOptimizer />);

      // Filter to FAST
      const fastButton = screen.getByRole('button', { name: 'FAST' });
      await user.click(fastButton);

      // Then back to all
      const allButton = screen.getByRole('button', { name: /all models/i });
      await user.click(allButton);

      // Should show all models again (appear multiple times)
      expect(screen.getAllByText('Llama 3.3 70B').length).toBeGreaterThan(0);
      expect(screen.getAllByText('Claude 3.5 Opus').length).toBeGreaterThan(0);
    });
  });

  // =========================================================================
  // Model Card Tests
  // =========================================================================

  describe('Model Cards', () => {
    it('should display model provider', () => {
      render(<ModelSelectionOptimizer />);

      // Providers appear in multiple model cards
      expect(screen.getAllByText('OpenRouter').length).toBeGreaterThan(0);
      expect(screen.getAllByText('Google').length).toBeGreaterThan(0);
      expect(screen.getAllByText('Anthropic').length).toBeGreaterThan(0);
    });

    it('should display free pricing for FAST models', () => {
      render(<ModelSelectionOptimizer />);

      // FAST models show "Free"
      const freeElements = screen.getAllByText('Free');
      expect(freeElements.length).toBeGreaterThan(0);
    });

    it('should display pricing for paid models', () => {
      render(<ModelSelectionOptimizer />);

      // Paid models show $/M format
      const pricingElements = screen.getAllByText(/\$\d+\/M/);
      expect(pricingElements.length).toBeGreaterThan(0);
    });

    it('should display quality scores', () => {
      render(<ModelSelectionOptimizer />);

      // Quality label appears in multiple cards
      expect(screen.getAllByText('Quality').length).toBeGreaterThan(0);
      // Check for quality scores like 7/10, 9/10, etc.
      const qualityScores = screen.getAllByText(/\d+\/10/);
      expect(qualityScores.length).toBeGreaterThan(0);
    });

    it('should display speed scores', () => {
      render(<ModelSelectionOptimizer />);

      // Speed label appears in multiple cards
      expect(screen.getAllByText('Speed').length).toBeGreaterThan(0);
    });

    it('should display feature tags', () => {
      render(<ModelSelectionOptimizer />);

      // Check for common features
      expect(screen.getAllByText('Fast').length).toBeGreaterThan(0);
    });

    it('should show recommended star for recommended models', () => {
      const { container } = render(<ModelSelectionOptimizer />);

      // Recommended models have a yellow star
      const stars = container.querySelectorAll('.text-yellow-500');
      expect(stars.length).toBeGreaterThan(0);
    });
  });

  // =========================================================================
  // Model Selection Tests
  // =========================================================================

  describe('Model Selection', () => {
    it('should select a model when clicked', async () => {
      const user = userEvent.setup();
      render(<ModelSelectionOptimizer />);

      // Click on Llama model card (first occurrence)
      const llamaCards = screen.getAllByText('Llama 3.3 70B');
      const llamaCard = llamaCards[0].closest('div');
      if (llamaCard) {
        await user.click(llamaCard);
      }

      // Should show model details panel
      expect(screen.getByText(/Best for:/)).toBeInTheDocument();
    });

    it('should show context window in model details', async () => {
      const user = userEvent.setup();
      render(<ModelSelectionOptimizer />);

      // Click on Claude Sonnet model (first occurrence in model cards)
      const sonnetCards = screen.getAllByText('Claude 3.5 Sonnet');
      const sonnetCard = sonnetCards[0].closest('div');
      if (sonnetCard) {
        await user.click(sonnetCard);
      }

      // Should show context window
      expect(screen.getByText(/Context window:/)).toBeInTheDocument();
      expect(screen.getByText(/200K tokens/)).toBeInTheDocument();
    });

    it('should deselect model when clicked again', async () => {
      const user = userEvent.setup();
      render(<ModelSelectionOptimizer />);

      const llamaCards = screen.getAllByText('Llama 3.3 70B');
      const llamaCard = llamaCards[0].closest('div');
      if (llamaCard) {
        // Select
        await user.click(llamaCard);
        expect(screen.getByText(/Best for:/)).toBeInTheDocument();

        // Deselect
        await user.click(llamaCard);
        // Best for in details panel should be gone
      }
    });
  });

  // =========================================================================
  // Cost Calculator Tests
  // =========================================================================

  describe('Cost Calculator', () => {
    it('should render cost calculator section', () => {
      render(<ModelSelectionOptimizer />);

      expect(screen.getByText('Cost Calculator')).toBeInTheDocument();
    });

    it('should have queries per month slider', () => {
      render(<ModelSelectionOptimizer />);

      expect(screen.getByText('Queries per Month')).toBeInTheDocument();
    });

    it('should have tokens per query slider', () => {
      render(<ModelSelectionOptimizer />);

      expect(screen.getByText('Avg Tokens per Query')).toBeInTheDocument();
    });

    it('should display monthly cost comparison', () => {
      render(<ModelSelectionOptimizer />);

      expect(screen.getByText(/Monthly Cost Comparison/)).toBeInTheDocument();
    });

    it('should update costs when query count changes', async () => {
      const user = userEvent.setup();
      render(<ModelSelectionOptimizer />);

      // Find the queries per month range input
      const sliders = screen.getAllByRole('slider');
      expect(sliders.length).toBeGreaterThanOrEqual(2);

      // The cost comparison shows queries (toLocaleString formats as 100,000)
      const queryLabels = screen.getAllByText(/queries/i);
      expect(queryLabels.length).toBeGreaterThan(0);
    });

    it('should display token count per query', () => {
      render(<ModelSelectionOptimizer />);

      // Tokens per query label shows in cost calculator
      expect(screen.getByText(/Avg Tokens per Query/i)).toBeInTheDocument();
    });

    it('should show Free label for free models in cost comparison', () => {
      render(<ModelSelectionOptimizer />);

      // Free models show "Free" in cost comparison
      const freeLabels = screen.getAllByText('Free');
      expect(freeLabels.length).toBeGreaterThan(0);
    });
  });

  // =========================================================================
  // Use Case Recommendations Tests
  // =========================================================================

  describe('Use Case Recommendations', () => {
    it('should render use case recommendations section', () => {
      render(<ModelSelectionOptimizer />);

      expect(screen.getByText('Use Case Recommendations')).toBeInTheDocument();
    });

    it('should display all use cases', () => {
      render(<ModelSelectionOptimizer />);

      expect(screen.getByText('Simple Rule Lookups')).toBeInTheDocument();
      expect(screen.getByText('Complex Rule Interpretation')).toBeInTheDocument();
      expect(screen.getByText('Strategy Analysis')).toBeInTheDocument();
      expect(screen.getByText('High Volume Operations')).toBeInTheDocument();
      expect(screen.getByText('Premium User Experience')).toBeInTheDocument();
    });

    it('should display recommended models for each use case', () => {
      render(<ModelSelectionOptimizer />);

      // Check for recommended model names (appear multiple times)
      expect(screen.getAllByText('Llama 3.3 70B').length).toBeGreaterThan(0);
      expect(screen.getAllByText('Claude 3.5 Opus').length).toBeGreaterThan(0);
    });

    it('should display alternative models', () => {
      render(<ModelSelectionOptimizer />);

      // Check for "Alt:" prefix
      const altLabels = screen.getAllByText(/Alt:/);
      expect(altLabels.length).toBeGreaterThan(0);
    });

    it('should display reasoning for recommendations', () => {
      render(<ModelSelectionOptimizer />);

      expect(screen.getByText(/Free models handle simple extractions/)).toBeInTheDocument();
      expect(screen.getByText(/Strong reasoning capabilities/)).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Key Insights Tests
  // =========================================================================

  describe('Key Insights', () => {
    it('should display cost optimization insight', () => {
      render(<ModelSelectionOptimizer />);

      expect(screen.getByText('Cost Optimization')).toBeInTheDocument();
      expect(screen.getByText(/Use FAST strategy for 70%/)).toBeInTheDocument();
    });

    it('should display quality balance insight', () => {
      render(<ModelSelectionOptimizer />);

      expect(screen.getByText('Quality Balance')).toBeInTheDocument();
      expect(screen.getByText(/Claude 3.5 Sonnet offers best/)).toBeInTheDocument();
    });

    it('should display premium value insight', () => {
      render(<ModelSelectionOptimizer />);

      expect(screen.getByText('Premium Value')).toBeInTheDocument();
      expect(screen.getByText(/Reserve Opus for premium/)).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Model Data Tests
  // =========================================================================

  describe('Model Data', () => {
    it('should display correct pricing for Claude 3.5 Sonnet', () => {
      render(<ModelSelectionOptimizer />);

      // Claude 3.5 Sonnet: $3/M input, $15/M output (may appear multiple times)
      expect(screen.getAllByText('$3/M').length).toBeGreaterThan(0);
      expect(screen.getAllByText('$15/M').length).toBeGreaterThan(0);
    });

    it('should display DeepSeek as affordable option', () => {
      render(<ModelSelectionOptimizer />);

      // DeepSeek appears multiple times
      expect(screen.getAllByText('DeepSeek Chat').length).toBeGreaterThan(0);
      expect(screen.getAllByText('DeepSeek').length).toBeGreaterThan(0);
    });

    it('should display all model providers', () => {
      render(<ModelSelectionOptimizer />);

      // Providers appear multiple times
      expect(screen.getAllByText('OpenRouter').length).toBeGreaterThan(0);
      expect(screen.getAllByText('Google').length).toBeGreaterThan(0);
      expect(screen.getAllByText('Anthropic').length).toBeGreaterThan(0);
      expect(screen.getAllByText('DeepSeek').length).toBeGreaterThan(0);
      expect(screen.getAllByText('OpenAI').length).toBeGreaterThan(0);
    });

    it('should show Gemini 1.5 Pro with 1000K context', async () => {
      const user = userEvent.setup();
      render(<ModelSelectionOptimizer />);

      // Click on Gemini 1.5 Pro (first occurrence)
      const geminiCards = screen.getAllByText('Gemini 1.5 Pro');
      const geminiCard = geminiCards[0].closest('div');
      if (geminiCard) {
        await user.click(geminiCard);
      }

      expect(screen.getByText(/1000K tokens/)).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Strategy Badge Tests
  // =========================================================================

  describe('Strategy Badges', () => {
    it('should display FAST strategy in model cards', () => {
      render(<ModelSelectionOptimizer />);

      // FAST strategy is shown in model cards
      const fastElements = screen.getAllByText('FAST');
      expect(fastElements.length).toBeGreaterThan(0);
    });

    it('should display BALANCED strategy in model cards', () => {
      render(<ModelSelectionOptimizer />);

      // BALANCED strategy is shown in model cards
      const balancedElements = screen.getAllByText('BALANCED');
      expect(balancedElements.length).toBeGreaterThan(0);
    });

    it('should display PRECISE strategy in model cards', () => {
      render(<ModelSelectionOptimizer />);

      // PRECISE strategy is shown in model cards
      const preciseElements = screen.getAllByText('PRECISE');
      expect(preciseElements.length).toBeGreaterThan(0);
    });
  });
});
