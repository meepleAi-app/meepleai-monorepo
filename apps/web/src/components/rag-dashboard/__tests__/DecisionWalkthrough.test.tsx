/**
 * Tests for DecisionWalkthrough component
 * Issue #3005: Frontend coverage improvements
 */

import React from 'react';

import { render, screen, waitFor, fireEvent, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

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
import { DecisionWalkthrough } from '../DecisionWalkthrough';

describe('DecisionWalkthrough', () => {
  // =========================================================================
  // Rendering Tests
  // =========================================================================

  describe('Rendering', () => {
    it('should render the decision walkthrough card', () => {
      render(<DecisionWalkthrough />);

      expect(screen.getByText('Decision Walkthrough')).toBeInTheDocument();
    });

    it('should render subtitle description', () => {
      render(<DecisionWalkthrough />);

      expect(screen.getByText(/Step-by-step visualization/i)).toBeInTheDocument();
    });

    it('should render scenario selection buttons', () => {
      render(<DecisionWalkthrough />);

      expect(screen.getByText('Rule Query')).toBeInTheDocument();
      expect(screen.getByText('Strategy Query')).toBeInTheDocument();
    });

    it('should render control buttons', () => {
      render(<DecisionWalkthrough />);

      expect(screen.getByRole('button', { name: /start/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /skip to end/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /reset/i })).toBeInTheDocument();
    });

    it('should apply custom className', () => {
      const { container } = render(<DecisionWalkthrough className="custom-class" />);

      expect(container.querySelector('.custom-class')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Default State Tests
  // =========================================================================

  describe('Default State', () => {
    it('should show Rule Query scenario selected by default', () => {
      render(<DecisionWalkthrough />);

      const ruleButton = screen.getByText('Rule Query');
      expect(ruleButton.closest('button')).toHaveClass('bg-primary/10');
    });

    it('should display default query', () => {
      render(<DecisionWalkthrough />);

      expect(screen.getByText(/What happens when I roll a 7 in Catan/)).toBeInTheDocument();
    });

    it('should display user tier', () => {
      render(<DecisionWalkthrough />);

      expect(screen.getByText('User')).toBeInTheDocument();
    });

    it('should display strategy badge', () => {
      render(<DecisionWalkthrough />);

      expect(screen.getByText('BALANCED')).toBeInTheDocument();
    });

    it('should display total tokens', () => {
      render(<DecisionWalkthrough />);

      expect(screen.getByText('3350')).toBeInTheDocument();
    });

    it('should display total latency', () => {
      render(<DecisionWalkthrough />);

      expect(screen.getByText('1850ms')).toBeInTheDocument();
    });

    it('should show all 6 step cards', () => {
      render(<DecisionWalkthrough />);

      expect(screen.getByText('Query Classification')).toBeInTheDocument();
      expect(screen.getByText('Cache Lookup')).toBeInTheDocument();
      expect(screen.getByText('Document Retrieval')).toBeInTheDocument();
      expect(screen.getByText('Quality Evaluation')).toBeInTheDocument();
      expect(screen.getByText('Response Generation')).toBeInTheDocument();
      expect(screen.getByText('Response Validation')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Scenario Selection Tests
  // =========================================================================

  describe('Scenario Selection', () => {
    it('should switch to Strategy Query scenario', () => {
      render(<DecisionWalkthrough />);

      const strategyButton = screen.getByText('Strategy Query');
      fireEvent.click(strategyButton);

      expect(screen.getByText(/Should I build a settlement or buy a development card/)).toBeInTheDocument();
    });

    it('should update user tier when scenario changes', () => {
      render(<DecisionWalkthrough />);

      const strategyButton = screen.getByText('Strategy Query');
      fireEvent.click(strategyButton);

      expect(screen.getByText('Editor')).toBeInTheDocument();
    });

    it('should update strategy badge when scenario changes', () => {
      render(<DecisionWalkthrough />);

      const strategyButton = screen.getByText('Strategy Query');
      fireEvent.click(strategyButton);

      expect(screen.getByText('PRECISE')).toBeInTheDocument();
    });

    it('should reset walkthrough when scenario changes', () => {
      render(<DecisionWalkthrough />);

      // Start the walkthrough
      const startButton = screen.getByRole('button', { name: /start/i });
      fireEvent.click(startButton);

      // Change scenario
      const strategyButton = screen.getByText('Strategy Query');
      fireEvent.click(strategyButton);

      // Should reset to start state
      expect(screen.getByRole('button', { name: /start/i })).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Playback Controls Tests
  // =========================================================================

  describe('Playback Controls', () => {
    it('should show Start button initially', () => {
      render(<DecisionWalkthrough />);

      expect(screen.getByRole('button', { name: /start/i })).toBeInTheDocument();
    });

    it('should change to Pause button when playing', () => {
      render(<DecisionWalkthrough />);

      const startButton = screen.getByRole('button', { name: /start/i });
      fireEvent.click(startButton);

      expect(screen.getByRole('button', { name: /pause/i })).toBeInTheDocument();
    });

    it('should change to Resume button when paused', () => {
      render(<DecisionWalkthrough />);

      // Start
      const startButton = screen.getByRole('button', { name: /start/i });
      fireEvent.click(startButton);

      // Pause
      const pauseButton = screen.getByRole('button', { name: /pause/i });
      fireEvent.click(pauseButton);

      expect(screen.getByRole('button', { name: /resume/i })).toBeInTheDocument();
    });

    it('should reset walkthrough when Reset is clicked', () => {
      vi.useFakeTimers();
      render(<DecisionWalkthrough />);

      // Start
      const startButton = screen.getByRole('button', { name: /start/i });
      fireEvent.click(startButton);

      vi.advanceTimersByTime(2500);

      // Reset
      const resetButton = screen.getByRole('button', { name: /reset/i });
      fireEvent.click(resetButton);

      expect(screen.getByRole('button', { name: /start/i })).toBeInTheDocument();
      vi.useRealTimers();
    });

    it('should skip to end when Skip to End is clicked', async () => {
      render(<DecisionWalkthrough />);

      const skipButton = screen.getByRole('button', { name: /skip to end/i });
      fireEvent.click(skipButton);

      await waitFor(() => {
        expect(screen.getByText('Final Response')).toBeInTheDocument();
      });
    });
  });

  // =========================================================================
  // Auto-Advance Tests
  // =========================================================================

  describe('Auto-Advance', () => {
    beforeEach(() => {
      vi.useFakeTimers();
    });

    afterEach(() => {
      vi.useRealTimers();
    });

    it('should advance to first step on Start', async () => {
      render(<DecisionWalkthrough />);

      const startButton = screen.getByRole('button', { name: /start/i });
      await act(async () => {
        fireEvent.click(startButton);
      });

      // First step should be active (show details)
      expect(screen.getByText(/Keyword match:/)).toBeInTheDocument();
    });

    it('should advance to next step after delay', async () => {
      render(<DecisionWalkthrough />);

      const startButton = screen.getByRole('button', { name: /start/i });
      await act(async () => {
        fireEvent.click(startButton);
      });

      // Advance time to next step
      await act(async () => {
        vi.advanceTimersByTime(2500);
      });

      // Second step details should be visible
      expect(screen.getByText(/Exact match:/)).toBeInTheDocument();
    });

    it('should show final response after all steps complete', async () => {
      render(<DecisionWalkthrough />);

      const startButton = screen.getByRole('button', { name: /start/i });
      await act(async () => {
        fireEvent.click(startButton);
      });

      // Component has 6 steps (0-5), timer is 2000ms per step
      // Each timer schedules the next one, so we need to advance and flush repeatedly
      // Advance through all 6 steps with sufficient time between each
      for (let i = 0; i < 7; i++) {
        await act(async () => {
          vi.advanceTimersByTime(2100); // Just over the 2000ms timer interval
        });
      }

      // After all steps, final response should be shown
      expect(screen.getByText('Final Response')).toBeInTheDocument();
    });

    it('should stop auto-advance when paused', async () => {
      render(<DecisionWalkthrough />);

      // Start
      const startButton = screen.getByRole('button', { name: /start/i });
      fireEvent.click(startButton);

      // Pause
      const pauseButton = screen.getByRole('button', { name: /pause/i });
      fireEvent.click(pauseButton);

      // Advance time
      vi.advanceTimersByTime(5000);

      // Should still be on first step (showing first step details)
      expect(screen.getByText(/Keyword match:/)).toBeInTheDocument();
      // Should NOT show second step details
      expect(screen.queryByText(/Exact match:/)).not.toBeInTheDocument();
    });
  });

  // =========================================================================
  // Step Card Tests
  // =========================================================================

  describe('Step Cards', () => {
    it('should display layer names for each step', () => {
      render(<DecisionWalkthrough />);

      expect(screen.getByText('L1: Intelligent Routing')).toBeInTheDocument();
      expect(screen.getByText('L2: Semantic Cache')).toBeInTheDocument();
      expect(screen.getByText('L3: Modular Retrieval')).toBeInTheDocument();
      expect(screen.getByText('L4: CRAG Evaluation')).toBeInTheDocument();
      expect(screen.getByText('L5: Adaptive Generation')).toBeInTheDocument();
      expect(screen.getByText('L6: Self-Validation')).toBeInTheDocument();
    });

    it('should display step descriptions', () => {
      render(<DecisionWalkthrough />);

      expect(screen.getByText(/Analyzing query complexity/)).toBeInTheDocument();
      expect(screen.getByText(/Checking memory and semantic cache/)).toBeInTheDocument();
    });

    it('should display tokens for each step', () => {
      render(<DecisionWalkthrough />);

      // Look for token values
      expect(screen.getByText('320')).toBeInTheDocument();
      expect(screen.getByText('310')).toBeInTheDocument();
    });

    it('should display latency for each step', () => {
      render(<DecisionWalkthrough />);

      // Some latency values may appear multiple times
      const latency150Elements = screen.getAllByText('150ms');
      expect(latency150Elements.length).toBeGreaterThan(0);
      expect(screen.getByText('50ms')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Active Step Details Tests
  // =========================================================================

  describe('Active Step Details', () => {
    it('should show decision details for active step', async () => {
      render(<DecisionWalkthrough />);

      const startButton = screen.getByRole('button', { name: /start/i });
      fireEvent.click(startButton);

      await waitFor(() => {
        expect(screen.getByText(/Decision:/)).toBeInTheDocument();
      });
    });

    it('should show detail bullets for active step', async () => {
      render(<DecisionWalkthrough />);

      const startButton = screen.getByRole('button', { name: /start/i });
      fireEvent.click(startButton);

      await waitFor(() => {
        expect(screen.getByText(/Complexity score: 2\/5/)).toBeInTheDocument();
      });
    });
  });

  // =========================================================================
  // Progress Bar Tests
  // =========================================================================

  describe('Progress Bar', () => {
    it('should have progress bar element', () => {
      const { container } = render(<DecisionWalkthrough />);

      const progressBar = container.querySelector('.h-2.bg-muted');
      expect(progressBar).toBeInTheDocument();
    });

    it('should show progress as steps complete', async () => {
      const { container } = render(<DecisionWalkthrough />);

      const startButton = screen.getByRole('button', { name: /start/i });
      fireEvent.click(startButton);

      // Check that inner progress bar exists
      const progressInner = container.querySelector('.h-full.bg-primary');
      expect(progressInner).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Final Response Tests
  // =========================================================================

  describe('Final Response', () => {
    it('should display final response after completion', async () => {
      render(<DecisionWalkthrough />);

      const skipButton = screen.getByRole('button', { name: /skip to end/i });
      fireEvent.click(skipButton);

      await waitFor(() => {
        expect(screen.getByText('Final Response')).toBeInTheDocument();
      });
    });

    it('should display rule quote in final response', async () => {
      render(<DecisionWalkthrough />);

      const skipButton = screen.getByRole('button', { name: /skip to end/i });
      fireEvent.click(skipButton);

      await waitFor(() => {
        expect(screen.getByText(/Rule Quote/)).toBeInTheDocument();
      });
    });

    it('should display citation in final response', async () => {
      render(<DecisionWalkthrough />);

      const skipButton = screen.getByRole('button', { name: /skip to end/i });
      fireEvent.click(skipButton);

      await waitFor(() => {
        expect(screen.getByText(/Citation:/)).toBeInTheDocument();
      });
    });
  });

  // =========================================================================
  // Strategy Scenario Tests
  // =========================================================================

  describe('Strategy Scenario', () => {
    it('should display strategy query content', () => {
      render(<DecisionWalkthrough />);

      const strategyButton = screen.getByText('Strategy Query');
      fireEvent.click(strategyButton);

      expect(screen.getByText(/Should I build a settlement/)).toBeInTheDocument();
    });

    it('should display strategy template', () => {
      render(<DecisionWalkthrough />);

      const strategyButton = screen.getByText('Strategy Query');
      fireEvent.click(strategyButton);

      expect(screen.getByText('strategy_advice')).toBeInTheDocument();
    });

    it('should display higher token count for strategy', () => {
      render(<DecisionWalkthrough />);

      const strategyButton = screen.getByText('Strategy Query');
      fireEvent.click(strategyButton);

      expect(screen.getByText('12900')).toBeInTheDocument();
    });

    it('should show strategy final response', async () => {
      render(<DecisionWalkthrough />);

      // Switch to strategy scenario
      const strategyButton = screen.getByText('Strategy Query');
      fireEvent.click(strategyButton);

      // Skip to end
      const skipButton = screen.getByRole('button', { name: /skip to end/i });
      fireEvent.click(skipButton);

      await waitFor(() => {
        expect(screen.getByText(/Analysis:/)).toBeInTheDocument();
        expect(screen.getByText(/Recommendation:/)).toBeInTheDocument();
      });
    });
  });
});
