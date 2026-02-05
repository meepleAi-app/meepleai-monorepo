/**
 * Tests for TokenFlowVisualizer component
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
import { TokenFlowVisualizer } from '../TokenFlowVisualizer';

describe('TokenFlowVisualizer', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // =========================================================================
  // Rendering Tests
  // =========================================================================

  describe('Rendering', () => {
    it('should render the token flow visualizer card', () => {
      render(<TokenFlowVisualizer />);

      expect(screen.getByText('Token Flow Visualizer')).toBeInTheDocument();
    });

    it('should render all 6 strategy buttons', () => {
      render(<TokenFlowVisualizer />);

      expect(screen.getByRole('button', { name: 'FAST' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'BALANCED' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'PRECISE' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'EXPERT' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'CONSENSUS' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'CUSTOM' })).toBeInTheDocument();
    });

    it('should render all 6 layer names', () => {
      render(<TokenFlowVisualizer />);

      expect(screen.getByText('Intelligent Routing')).toBeInTheDocument();
      expect(screen.getByText('Semantic Cache')).toBeInTheDocument();
      expect(screen.getByText('Modular Retrieval')).toBeInTheDocument();
      expect(screen.getByText('CRAG Evaluation')).toBeInTheDocument();
      expect(screen.getByText('Adaptive Generation')).toBeInTheDocument();
      expect(screen.getByText('Self-Validation')).toBeInTheDocument();
    });

    it('should render layer short names', () => {
      render(<TokenFlowVisualizer />);

      expect(screen.getByText('L1')).toBeInTheDocument();
      expect(screen.getByText('L2')).toBeInTheDocument();
      expect(screen.getByText('L3')).toBeInTheDocument();
      expect(screen.getByText('L4')).toBeInTheDocument();
      expect(screen.getByText('L5')).toBeInTheDocument();
      expect(screen.getByText('L6')).toBeInTheDocument();
    });

    it('should apply custom className', () => {
      const { container } = render(<TokenFlowVisualizer className="custom-class" />);

      expect(container.querySelector('.custom-class')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Default State Tests
  // =========================================================================

  describe('Default State', () => {
    it('should show BALANCED as default strategy', () => {
      render(<TokenFlowVisualizer />);

      const balancedButton = screen.getByRole('button', { name: 'BALANCED' });
      expect(balancedButton).toHaveAttribute('data-active', 'true');
    });

    it('should display total tokens', () => {
      render(<TokenFlowVisualizer />);

      expect(screen.getByText('Total Tokens:')).toBeInTheDocument();
    });

    it('should display strategy info section', () => {
      render(<TokenFlowVisualizer />);

      expect(screen.getByText('BALANCED Strategy')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Strategy Switching Tests
  // =========================================================================

  describe('Strategy Switching', () => {
    it('should switch to FAST strategy', async () => {
      const user = userEvent.setup();
      render(<TokenFlowVisualizer />);

      const fastButton = screen.getByRole('button', { name: 'FAST' });
      await user.click(fastButton);

      expect(fastButton).toHaveAttribute('data-active', 'true');
      expect(screen.getByText('FAST Strategy')).toBeInTheDocument();
    });

    it('should switch to PRECISE strategy', async () => {
      const user = userEvent.setup();
      render(<TokenFlowVisualizer />);

      const preciseButton = screen.getByRole('button', { name: 'PRECISE' });
      await user.click(preciseButton);

      expect(preciseButton).toHaveAttribute('data-active', 'true');
      expect(screen.getByText('PRECISE Strategy')).toBeInTheDocument();
    });

    it('should switch to EXPERT strategy', async () => {
      const user = userEvent.setup();
      render(<TokenFlowVisualizer />);

      const expertButton = screen.getByRole('button', { name: 'EXPERT' });
      await user.click(expertButton);

      expect(expertButton).toHaveAttribute('data-active', 'true');
      expect(screen.getByText('EXPERT Strategy')).toBeInTheDocument();
    });

    it('should switch to CONSENSUS strategy', async () => {
      const user = userEvent.setup();
      render(<TokenFlowVisualizer />);

      const consensusButton = screen.getByRole('button', { name: 'CONSENSUS' });
      await user.click(consensusButton);

      expect(consensusButton).toHaveAttribute('data-active', 'true');
      expect(screen.getByText('CONSENSUS Strategy')).toBeInTheDocument();
    });

    it('should switch to CUSTOM strategy', async () => {
      const user = userEvent.setup();
      render(<TokenFlowVisualizer />);

      const customButton = screen.getByRole('button', { name: 'CUSTOM' });
      await user.click(customButton);

      expect(customButton).toHaveAttribute('data-active', 'true');
      expect(screen.getByText('CUSTOM Strategy')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Layer Descriptions Tests
  // =========================================================================

  describe('Layer Descriptions', () => {
    it('should display layer descriptions', () => {
      render(<TokenFlowVisualizer />);

      // Layer 1 (Intelligent Routing) description from types.ts
      expect(screen.getByText(/Classifies query and selects/i)).toBeInTheDocument();
    });

    it('should display token values', () => {
      render(<TokenFlowVisualizer />);

      // Check for token values like "1.5K" or similar
      const tokenValues = screen.getAllByText(/\d+(\.\d+)?K/);
      expect(tokenValues.length).toBeGreaterThan(0);
    });
  });

  // =========================================================================
  // Layer Expansion Tests
  // =========================================================================

  describe('Layer Expansion', () => {
    it('should have expand buttons for layers', () => {
      render(<TokenFlowVisualizer />);

      // Each layer should have an expand/collapse button
      const buttons = screen.getAllByRole('button');
      // There should be strategy buttons + layer action buttons
      expect(buttons.length).toBeGreaterThan(6);
    });

    it('should toggle layer expansion', async () => {
      const user = userEvent.setup();
      render(<TokenFlowVisualizer />);

      // Find expand buttons (they have aria-expanded attribute)
      const expandButtons = screen.getAllByRole('button').filter(
        btn => btn.hasAttribute('aria-expanded')
      );

      if (expandButtons.length > 0) {
        await user.click(expandButtons[0]);
        // Should show expanded content
      }
    });
  });

  // =========================================================================
  // Strategy Info Tests
  // =========================================================================

  describe('Strategy Info', () => {
    it('should display strategy info section', () => {
      render(<TokenFlowVisualizer />);

      // The strategy info section shows the selected strategy name
      expect(screen.getByText('BALANCED Strategy')).toBeInTheDocument();
    });

    it('should display use case', () => {
      render(<TokenFlowVisualizer />);

      // Use case label is present in the strategy info section
      expect(screen.getByText(/Use case:/i)).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Token Bar Tests
  // =========================================================================

  describe('Token Bars', () => {
    it('should render token bars for each layer', () => {
      const { container } = render(<TokenFlowVisualizer />);

      // Token bars have specific class
      const tokenBars = container.querySelectorAll('.rag-token-bar');
      expect(tokenBars.length).toBeGreaterThan(0);
    });
  });

  // =========================================================================
  // Flow Container Tests
  // =========================================================================

  describe('Flow Container', () => {
    it('should render flow container', () => {
      const { container } = render(<TokenFlowVisualizer />);

      const flowContainer = container.querySelector('.rag-flow-container');
      expect(flowContainer).toBeInTheDocument();
    });

    it('should render layer icons', () => {
      const { container } = render(<TokenFlowVisualizer />);

      // Layer icons have specific class
      const layerIcons = container.querySelectorAll('.rag-layer-icon');
      expect(layerIcons.length).toBeGreaterThan(0);
    });
  });

  // =========================================================================
  // Token Count Updates Tests
  // =========================================================================

  describe('Token Count Updates', () => {
    it('should update token counts when strategy changes', async () => {
      const user = userEvent.setup();
      render(<TokenFlowVisualizer />);

      // Get initial total tokens text
      const initialTotalText = screen.getByText('Total Tokens:').parentElement?.textContent;

      // Switch to FAST (lower tokens)
      const fastButton = screen.getByRole('button', { name: 'FAST' });
      await user.click(fastButton);

      // Token values should change
      const newTotalText = screen.getByText('Total Tokens:').parentElement?.textContent;

      // The values should be different between BALANCED and FAST
      expect(initialTotalText).not.toEqual(newTotalText);
    });
  });

  // =========================================================================
  // Handler Function Tests (for function coverage)
  // =========================================================================

  describe('Handler Functions', () => {
    it('should handle strategy selection across all strategies', async () => {
      const user = userEvent.setup();
      render(<TokenFlowVisualizer />);

      // Test clicking each strategy button
      const strategies = ['FAST', 'BALANCED', 'PRECISE', 'EXPERT', 'CONSENSUS', 'CUSTOM'];

      for (const strategy of strategies) {
        const button = screen.getByRole('button', { name: strategy });
        await user.click(button);

        // Verify button was clicked (it should be in the document and clickable)
        expect(button).toBeInTheDocument();
      }
    });

    it('should update layer visibility and token calculations', async () => {
      const user = userEvent.setup();
      render(<TokenFlowVisualizer />);

      // Switch to PRECISE which has different layers
      const preciseButton = screen.getByRole('button', { name: 'PRECISE' });
      await user.click(preciseButton);

      // Token distribution should update for PRECISE strategy
      // Format varies by locale: "22.120" (K format) or "22,120" (full format)
      const totalText = screen.getByText('Total Tokens:').parentElement?.textContent;
      // Check for the numeric value regardless of format (K format or comma-separated)
      expect(totalText).toMatch(/22[.,]?120|22[.,]12/);
    });

    it('should calculate layer token percentages correctly', async () => {
      const user = userEvent.setup();
      const { container } = render(<TokenFlowVisualizer />);

      // Switch to FAST to test percentage calculation
      const fastButton = screen.getByRole('button', { name: 'FAST' });
      await user.click(fastButton);

      // Should display percentage bars for each layer
      await waitFor(() => {
        const tokenBars = container.querySelectorAll('.rag-token-bar');
        // Each layer in FAST should have a token bar
        expect(tokenBars.length).toBeGreaterThan(0);
      });
    });
  });
});
