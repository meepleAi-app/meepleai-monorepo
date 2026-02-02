/**
 * Tests for QuerySimulator component
 * Issue #3005: Frontend coverage improvements
 */

import React from 'react';

import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
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
import { QuerySimulator } from '../QuerySimulator';

describe('QuerySimulator', () => {
  // =========================================================================
  // Rendering Tests
  // =========================================================================

  describe('Rendering', () => {
    it('should render the query simulator card', () => {
      render(<QuerySimulator />);

      expect(screen.getByText('Query Simulator')).toBeInTheDocument();
    });

    it('should render user tier selector', () => {
      render(<QuerySimulator />);

      expect(screen.getByText('User Tier:')).toBeInTheDocument();
    });

    it('should render all tier buttons', () => {
      render(<QuerySimulator />);

      // Anonymous shows with emoji prefix "🚫 Anonymous"
      expect(screen.getByRole('button', { name: /Anonymous/ })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'User' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Editor' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Admin' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Premium' })).toBeInTheDocument();
    });

    it('should render query input with placeholder', () => {
      render(<QuerySimulator />);

      expect(screen.getByPlaceholderText(/how many food tokens/i)).toBeInTheDocument();
    });

    it('should render analyze button', () => {
      render(<QuerySimulator />);

      expect(screen.getByRole('button', { name: /analyze/i })).toBeInTheDocument();
    });

    it('should render example queries', () => {
      render(<QuerySimulator />);

      expect(screen.getByText('How many players in Wingspan?')).toBeInTheDocument();
      expect(screen.getByText('Best strategy for Catan with 3 ore?')).toBeInTheDocument();
      expect(screen.getByText('Explain worker placement mechanics')).toBeInTheDocument();
      expect(screen.getByText('Setup guide for Terraforming Mars')).toBeInTheDocument();
    });

    it('should apply custom className', () => {
      const { container } = render(<QuerySimulator className="custom-class" />);

      expect(container.querySelector('.custom-class')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Tier Selection Tests
  // =========================================================================

  describe('Tier Selection', () => {
    it('should have User tier selected by default', () => {
      render(<QuerySimulator />);

      const userButton = screen.getByRole('button', { name: 'User' });
      expect(userButton).toHaveClass('bg-primary');
    });

    it('should change tier when clicking different tier button', async () => {
      render(<QuerySimulator />);

      const adminButton = screen.getByRole('button', { name: 'Admin' });
      await act(async () => {
        fireEvent.click(adminButton);
      });

      expect(adminButton).toHaveClass('bg-primary');
    });

    it('should update tier styling when selected', async () => {
      render(<QuerySimulator />);

      const editorButton = screen.getByRole('button', { name: 'Editor' });
      await act(async () => {
        fireEvent.click(editorButton);
      });

      expect(editorButton).toHaveClass('text-primary-foreground');
    });
  });

  // =========================================================================
  // Query Input Tests
  // =========================================================================

  describe('Query Input', () => {
    it('should update input value when typing', async () => {
      render(<QuerySimulator />);

      const input = screen.getByPlaceholderText(/how many food tokens/i);
      await act(async () => {
        fireEvent.change(input, { target: { value: 'What are the rules?' } });
      });

      expect(input).toHaveValue('What are the rules?');
    });

    it('should disable analyze button when query is empty', () => {
      render(<QuerySimulator />);

      const analyzeButton = screen.getByRole('button', { name: /analyze/i });
      expect(analyzeButton).toBeDisabled();
    });

    it('should enable analyze button when query has content', async () => {
      render(<QuerySimulator />);

      const input = screen.getByPlaceholderText(/how many food tokens/i);
      await act(async () => {
        fireEvent.change(input, { target: { value: 'Test query' } });
      });

      const analyzeButton = screen.getByRole('button', { name: /analyze/i });
      expect(analyzeButton).not.toBeDisabled();
    });
  });

  // =========================================================================
  // Example Query Tests
  // =========================================================================

  describe('Example Queries', () => {
    it('should fill input when clicking example query', async () => {
      render(<QuerySimulator />);

      const exampleButton = screen.getByRole('button', { name: 'How many players in Wingspan?' });
      await act(async () => {
        fireEvent.click(exampleButton);
      });

      const input = screen.getByPlaceholderText(/how many food tokens/i);
      expect(input).toHaveValue('How many players in Wingspan?');
    });

    it('should show Try these examples label', () => {
      render(<QuerySimulator />);

      expect(screen.getByText('Try these examples:')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Analysis Tests
  // =========================================================================

  describe('Analysis', () => {
    beforeEach(() => {
      vi.useFakeTimers();
    });

    afterEach(() => {
      vi.useRealTimers();
    });

    it('should show Analyzing... when button is clicked', async () => {
      render(<QuerySimulator />);

      const input = screen.getByPlaceholderText(/how many food tokens/i);
      await act(async () => {
        fireEvent.change(input, { target: { value: 'Test query' } });
      });

      const analyzeButton = screen.getByRole('button', { name: /analyze/i });
      await act(async () => {
        fireEvent.click(analyzeButton);
      });

      expect(screen.getByText('Analyzing...')).toBeInTheDocument();
    });

    it('should trigger analysis on Enter key', async () => {
      render(<QuerySimulator />);

      const input = screen.getByPlaceholderText(/how many food tokens/i);
      await act(async () => {
        fireEvent.change(input, { target: { value: 'Test query' } });
        fireEvent.keyDown(input, { key: 'Enter', code: 'Enter' });
      });

      expect(screen.getByText('Analyzing...')).toBeInTheDocument();
    });

    it('should show analysis results after delay', async () => {
      render(<QuerySimulator />);

      const input = screen.getByPlaceholderText(/how many food tokens/i);
      await act(async () => {
        fireEvent.change(input, { target: { value: 'What is the rule?' } });
      });

      const analyzeButton = screen.getByRole('button', { name: /analyze/i });
      await act(async () => {
        fireEvent.click(analyzeButton);
      });

      // Advance timers to complete analysis (component uses 800ms timeout)
      await act(async () => {
        vi.advanceTimersByTime(1000);
      });

      expect(screen.getByText('Template')).toBeInTheDocument();
    });

    it('should display analysis template result', async () => {
      render(<QuerySimulator />);

      const input = screen.getByPlaceholderText(/how many food tokens/i);
      await act(async () => {
        fireEvent.change(input, { target: { value: 'What is the rule for 7?' } });
      });

      const analyzeButton = screen.getByRole('button', { name: /analyze/i });
      await act(async () => {
        fireEvent.click(analyzeButton);
        vi.advanceTimersByTime(1000);
      });

      expect(screen.getByText('Template')).toBeInTheDocument();
      expect(screen.getByText(/rule lookup/i)).toBeInTheDocument();
    });

    it('should display complexity score', async () => {
      render(<QuerySimulator />);

      const input = screen.getByPlaceholderText(/how many food tokens/i);
      await act(async () => {
        fireEvent.change(input, { target: { value: 'Test query' } });
      });

      const analyzeButton = screen.getByRole('button', { name: /analyze/i });
      await act(async () => {
        fireEvent.click(analyzeButton);
        vi.advanceTimersByTime(1000);
      });

      expect(screen.getByText('Complexity')).toBeInTheDocument();
      expect(screen.getByText(/\/5/)).toBeInTheDocument();
    });

    it('should display strategy result', async () => {
      render(<QuerySimulator />);

      const input = screen.getByPlaceholderText(/how many food tokens/i);
      await act(async () => {
        fireEvent.change(input, { target: { value: 'Test query' } });
      });

      const analyzeButton = screen.getByRole('button', { name: /analyze/i });
      await act(async () => {
        fireEvent.click(analyzeButton);
        vi.advanceTimersByTime(1000);
      });

      expect(screen.getByText('Strategy')).toBeInTheDocument();
    });

    it('should display cache status', async () => {
      render(<QuerySimulator />);

      const input = screen.getByPlaceholderText(/how many food tokens/i);
      await act(async () => {
        fireEvent.change(input, { target: { value: 'Test query' } });
      });

      const analyzeButton = screen.getByRole('button', { name: /analyze/i });
      await act(async () => {
        fireEvent.click(analyzeButton);
        vi.advanceTimersByTime(1000);
      });

      expect(screen.getByText('Cache')).toBeInTheDocument();
    });

    it('should display performance metrics section', async () => {
      render(<QuerySimulator />);

      const input = screen.getByPlaceholderText(/how many food tokens/i);
      await act(async () => {
        fireEvent.change(input, { target: { value: 'Test query' } });
      });

      const analyzeButton = screen.getByRole('button', { name: /analyze/i });
      await act(async () => {
        fireEvent.click(analyzeButton);
        vi.advanceTimersByTime(1000);
      });

      expect(screen.getByText('Model')).toBeInTheDocument();
      expect(screen.getByText('Est. Tokens')).toBeInTheDocument();
      expect(screen.getByText('Est. Cost')).toBeInTheDocument();
    });

    it('should hide example queries after analysis', async () => {
      render(<QuerySimulator />);

      const input = screen.getByPlaceholderText(/how many food tokens/i);
      await act(async () => {
        fireEvent.change(input, { target: { value: 'Test query' } });
      });

      const analyzeButton = screen.getByRole('button', { name: /analyze/i });
      await act(async () => {
        fireEvent.click(analyzeButton);
        vi.advanceTimersByTime(1000);
      });

      expect(screen.queryByText('Try these examples:')).not.toBeInTheDocument();
    });
  });

  // =========================================================================
  // Template Classification Tests
  // =========================================================================

  describe('Template Classification', () => {
    beforeEach(() => {
      vi.useFakeTimers();
    });

    afterEach(() => {
      vi.useRealTimers();
    });

    it('should classify rule query correctly', async () => {
      render(<QuerySimulator />);

      const input = screen.getByPlaceholderText(/how many food tokens/i);
      await act(async () => {
        fireEvent.change(input, { target: { value: 'What is the rule for trading?' } });
      });

      const analyzeButton = screen.getByRole('button', { name: /analyze/i });
      await act(async () => {
        fireEvent.click(analyzeButton);
        vi.advanceTimersByTime(1000);
      });

      expect(screen.getByText(/rule lookup/i)).toBeInTheDocument();
    });

    it('should classify strategy query correctly', async () => {
      render(<QuerySimulator />);

      const input = screen.getByPlaceholderText(/how many food tokens/i);
      // Use query that matches strategy keywords but not rule_lookup keywords like "what is"
      await act(async () => {
        fireEvent.change(input, { target: { value: 'Best strategy to win this game?' } });
      });

      const analyzeButton = screen.getByRole('button', { name: /analyze/i });
      await act(async () => {
        fireEvent.click(analyzeButton);
        vi.advanceTimersByTime(1000);
      });

      expect(screen.getByText(/strategy advice/i)).toBeInTheDocument();
    });

    it('should classify setup query correctly', async () => {
      render(<QuerySimulator />);

      const input = screen.getByPlaceholderText(/how many food tokens/i);
      await act(async () => {
        fireEvent.change(input, { target: { value: 'How do I setup the game?' } });
      });

      const analyzeButton = screen.getByRole('button', { name: /analyze/i });
      await act(async () => {
        fireEvent.click(analyzeButton);
        vi.advanceTimersByTime(1000);
      });

      expect(screen.getByText(/setup guide/i)).toBeInTheDocument();
    });

    it('should classify educational query correctly', async () => {
      render(<QuerySimulator />);

      const input = screen.getByPlaceholderText(/how many food tokens/i);
      await act(async () => {
        fireEvent.change(input, { target: { value: 'Explain the trading mechanics' } });
      });

      const analyzeButton = screen.getByRole('button', { name: /analyze/i });
      await act(async () => {
        fireEvent.click(analyzeButton);
        vi.advanceTimersByTime(1000);
      });

      expect(screen.getByText(/educational/i)).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Strategy Selection Based on Tier Tests
  // =========================================================================

  describe('Strategy Selection', () => {
    beforeEach(() => {
      vi.useFakeTimers();
    });

    afterEach(() => {
      vi.useRealTimers();
    });

    it('should show No Access for Anonymous users', async () => {
      render(<QuerySimulator />);

      // Select Anonymous tier
      const anonymousButton = screen.getByRole('button', { name: /Anonymous/ });
      await act(async () => {
        fireEvent.click(anonymousButton);
      });

      // Anonymous users should see the "No Access" warning
      expect(screen.getByText(/NO ACCESS/)).toBeInTheDocument();

      // The analyze button should be disabled and show "No Access"
      const noAccessButton = screen.getByRole('button', { name: /no access/i });
      expect(noAccessButton).toBeDisabled();
    });

    it('should allow higher tier strategies for Admin users', async () => {
      render(<QuerySimulator />);

      // Select Admin tier
      const adminButton = screen.getByRole('button', { name: 'Admin' });
      await act(async () => {
        fireEvent.click(adminButton);
      });

      const input = screen.getByPlaceholderText(/how many food tokens/i);
      await act(async () => {
        fireEvent.change(input, { target: { value: 'Explain the complex strategy and analyze if this is optimal' } });
      });

      const analyzeButton = screen.getByRole('button', { name: /analyze/i });
      await act(async () => {
        fireEvent.click(analyzeButton);
        vi.advanceTimersByTime(1000);
      });

      // Should allow BALANCED or PRECISE for complex queries
      const strategyBadge = screen.getByText(/FAST|BALANCED|PRECISE/);
      expect(strategyBadge).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Edge Cases Tests
  // =========================================================================

  describe('Edge Cases', () => {
    beforeEach(() => {
      vi.useFakeTimers();
    });

    afterEach(() => {
      vi.useRealTimers();
    });

    it('should not analyze empty query', async () => {
      render(<QuerySimulator />);

      const input = screen.getByPlaceholderText(/how many food tokens/i);
      await act(async () => {
        fireEvent.change(input, { target: { value: '   ' } }); // Whitespace only
      });

      const analyzeButton = screen.getByRole('button', { name: /analyze/i });
      expect(analyzeButton).toBeDisabled();
    });

    it('should handle very long queries', async () => {
      render(<QuerySimulator />);

      const longQuery = 'This is a very long query '.repeat(10);
      const input = screen.getByPlaceholderText(/how many food tokens/i);
      await act(async () => {
        fireEvent.change(input, { target: { value: longQuery } });
      });

      const analyzeButton = screen.getByRole('button', { name: /analyze/i });
      await act(async () => {
        fireEvent.click(analyzeButton);
        vi.advanceTimersByTime(1000);
      });

      expect(screen.getByText('Template')).toBeInTheDocument();
    });
  });
});
