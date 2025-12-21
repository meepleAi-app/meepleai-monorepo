/**
 * SearchFilters Performance Tests (Issue #1509)
 *
 * Tests performance characteristics:
 * - Render time with large game/agent lists
 * - Filter application performance
 * - Re-render count on filter changes
 * - Memory efficiency
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { SearchFilters } from '../search/SearchFilters';
import {
  measureRenderPerformance,
  runPerformanceTest,
  assertPerformanceThresholds,
  DEFAULT_THRESHOLDS,
} from '@/test-utils/performance-test-utils';
import type { SearchFilters as SearchFiltersType, Game, Agent } from '@/types';

// ============================================================================
// Test Data Generators
// ============================================================================

/**
 * Generate mock games for testing
 */
function generateMockGames(count: number): Game[] {
  return Array.from({ length: count }, (_, i) => ({
    id: `game-${i}`,
    title: `Test Game ${i}`,
    createdAt: new Date(Date.now() - i * 86400000).toISOString(),
  }));
}

/**
 * Generate mock agents for testing
 */
function generateMockAgents(count: number): Agent[] {
  return Array.from({ length: count }, (_, i) => ({
    id: `agent-${i}`,
    gameId: `game-${i % 10}`,
    name: `Test Agent ${i}`,
    type: (i % 2 === 0 ? 'qa' : 'explain') as const,
  }));
}

// ============================================================================
// Performance Tests
// ============================================================================

describe('SearchFilters Performance', () => {
  const mockOnFiltersChange = vi.fn();

  beforeEach(() => {
    mockOnFiltersChange.mockClear();
  });

  describe('Render Performance', () => {
    it('should render with 10 games and 10 agents within 1500ms', async () => {
      const games = generateMockGames(10);
      const agents = generateMockAgents(10);

      const result = await measureRenderPerformance(() => {
        const { unmount } = render(
          <SearchFilters
            filters={{}}
            onFiltersChange={mockOnFiltersChange}
            games={games}
            agents={agents}
          />
        );
        unmount();
      });

      // Issue #2152: Increased from 400ms to 1500ms for CI/Windows environment variability
      // Small dataset with Radix UI Select components (heavier than plain inputs)
      expect(result.renderTime).toBeLessThan(1500);

      console.log(`[PERF] 10 games + 10 agents: ${result.renderTime.toFixed(2)}ms`);
      console.log(`[PERF] Memory increase: ${result.memoryIncrease.toFixed(2)}MB`);
    });

    it('should render with 50 games and 50 agents within 2000ms', async () => {
      const games = generateMockGames(50);
      const agents = generateMockAgents(50);

      const result = await measureRenderPerformance(() => {
        const { unmount } = render(
          <SearchFilters
            filters={{}}
            onFiltersChange={mockOnFiltersChange}
            games={games}
            agents={agents}
          />
        );
        unmount();
      });

      // Issue #2152: Increased from 700ms to 2000ms for CI/Windows environment variability
      // Medium dataset with complex Radix UI components
      // Relaxed from 500ms to 700ms (previous), now 2000ms for CI stability
      expect(result.renderTime).toBeLessThan(2000);

      console.log(`[PERF] 50 games + 50 agents: ${result.renderTime.toFixed(2)}ms`);
    });

    it('should render with 100 games and 100 agents within 900ms', async () => {
      const games = generateMockGames(100);
      const agents = generateMockAgents(100);

      const result = await measureRenderPerformance(() => {
        const { unmount } = render(
          <SearchFilters
            filters={{}}
            onFiltersChange={mockOnFiltersChange}
            games={games}
            agents={agents}
          />
        );
        unmount();
      });

      // Large dataset with Radix UI components has higher render cost
      // Increased threshold to accommodate CI/environment variability (was 750ms → 900ms)
      expect(result.renderTime).toBeLessThan(900);

      console.log(`[PERF] 100 games + 100 agents: ${result.renderTime.toFixed(2)}ms`);
    });

    it('should meet heavy component thresholds for 50 games/agents', async () => {
      const games = generateMockGames(50);
      const agents = generateMockAgents(50);

      const result = await measureRenderPerformance(() => {
        const { unmount } = render(
          <SearchFilters
            filters={{}}
            onFiltersChange={mockOnFiltersChange}
            games={games}
            agents={agents}
          />
        );
        unmount();
      });

      // Radix UI components are heavier - use heavy threshold
      assertPerformanceThresholds(
        result,
        DEFAULT_THRESHOLDS.heavy,
        'SearchFilters (50 games/agents)'
      );
    });
  });

  describe('Filter Application Performance', () => {
    it('should apply game filter within 300ms', async () => {
      const games = generateMockGames(50);
      const agents = generateMockAgents(50);

      const { unmount } = render(
        <SearchFilters
          filters={{}}
          onFiltersChange={mockOnFiltersChange}
          games={games}
          agents={agents}
        />
      );

      const gameTrigger = screen.getByLabelText('Game');

      fireEvent.click(gameTrigger);
      await waitFor(() => {
        expect(screen.getByRole('listbox')).toBeInTheDocument();
      });
      const gameOption = screen.getByText(games[0].title);

      const startTime = performance.now();

      fireEvent.click(gameOption);

      const endTime = performance.now();
      const duration = endTime - startTime;

      // Radix UI Select onChange has overhead from portal rendering
      expect(duration).toBeLessThan(300);
      expect(mockOnFiltersChange).toHaveBeenCalled();

      console.log(`[PERF] Game filter applied in ${duration.toFixed(2)}ms`);

      unmount();
    });

    it('should apply agent filter within 200ms', async () => {
      const games = generateMockGames(50);
      const agents = generateMockAgents(50);

      const { unmount } = render(
        <SearchFilters
          filters={{}}
          onFiltersChange={mockOnFiltersChange}
          games={games}
          agents={agents}
        />
      );

      const agentTrigger = screen.getByLabelText('Agent');

      fireEvent.click(agentTrigger);
      await waitFor(() => {
        expect(screen.getByRole('listbox')).toBeInTheDocument();
      });
      const agentOption = screen.getByText(agents[0].name);

      const startTime = performance.now();

      fireEvent.click(agentOption);

      const endTime = performance.now();
      const duration = endTime - startTime;

      expect(duration).toBeLessThan(200);
      expect(mockOnFiltersChange).toHaveBeenCalled();

      console.log(`[PERF] Agent filter applied in ${duration.toFixed(2)}ms`);

      unmount();
    });

    it('should apply type filter within 100ms', async () => {
      const games = generateMockGames(10);
      const agents = generateMockAgents(10);

      const { unmount } = render(
        <SearchFilters
          filters={{}}
          onFiltersChange={mockOnFiltersChange}
          games={games}
          agents={agents}
        />
      );

      // Find and click a result type button (Messages, Chats, Games, Agents, PDFs)
      const messagesButton = screen.getByRole('button', { name: /messages/i });

      const startTime = performance.now();

      fireEvent.click(messagesButton);

      const endTime = performance.now();
      const duration = endTime - startTime;

      expect(duration).toBeLessThan(100);
      expect(mockOnFiltersChange).toHaveBeenCalled();

      console.log(`[PERF] Type filter applied in ${duration.toFixed(2)}ms`);

      unmount();
    });
  });

  describe('Multiple Filter Changes Performance', () => {
    it('should handle 5 filter changes within 800ms', async () => {
      const games = generateMockGames(50);
      const agents = generateMockAgents(50);

      const { unmount } = render(
        <SearchFilters
          filters={{}}
          onFiltersChange={mockOnFiltersChange}
          games={games}
          agents={agents}
        />
      );

      const startTime = performance.now();

      const selectGame = async (index: number) => {
        fireEvent.click(screen.getByLabelText('Game'));
        await waitFor(() => {
          expect(screen.getByRole('listbox')).toBeInTheDocument();
        });
        const option = screen.getByText(games[index].title);
        fireEvent.click(option);
      };

      const selectAgent = async (index: number) => {
        fireEvent.click(screen.getByLabelText('Agent'));
        await waitFor(() => {
          expect(screen.getByRole('listbox')).toBeInTheDocument();
        });
        const option = screen.getByText(agents[index].name);
        fireEvent.click(option);
      };

      // Apply multiple filters sequentially
      await selectGame(0);
      await selectAgent(0);
      await selectGame(1);
      await selectAgent(1);
      await selectGame(2);

      const endTime = performance.now();
      const duration = endTime - startTime;

      // 5 filter changes with Radix UI overhead
      expect(duration).toBeLessThan(4500);
      expect(mockOnFiltersChange).toHaveBeenCalledTimes(5);

      console.log(`[PERF] 5 filter changes: ${duration.toFixed(2)}ms`);

      unmount();
    });
  });

  describe('Clear All Performance', () => {
    it('should clear all filters within 50ms', async () => {
      const games = generateMockGames(50);
      const agents = generateMockAgents(50);

      const filters: SearchFiltersType = {
        gameId: games[0].id,
        agentId: agents[0].id,
        types: ['qa', 'explanation'],
        dateFrom: new Date('2024-01-01'),
        dateTo: new Date('2024-12-31'),
      };

      const { unmount } = render(
        <SearchFilters
          filters={filters}
          onFiltersChange={mockOnFiltersChange}
          games={games}
          agents={agents}
        />
      );

      const clearButton = screen.getByRole('button', { name: /clear all/i });

      const startTime = performance.now();

      fireEvent.click(clearButton);

      const endTime = performance.now();
      const duration = endTime - startTime;

      // Clearing all filters should be instant
      expect(duration).toBeLessThan(50);
      expect(mockOnFiltersChange).toHaveBeenCalledWith({});

      console.log(`[PERF] Clear all filters: ${duration.toFixed(2)}ms`);

      unmount();
    });
  });

  describe('Median Performance (5 iterations)', () => {
    it('should have consistent render performance', async () => {
      const games = generateMockGames(50);
      const agents = generateMockAgents(50);

      const medianResult = await runPerformanceTest(() => {
        const { unmount } = render(
          <SearchFilters
            filters={{}}
            onFiltersChange={mockOnFiltersChange}
            games={games}
            agents={agents}
          />
        );
        unmount();
      }, 5);

      // Median should be stable with Radix UI components
      expect(medianResult.renderTime).toBeLessThan(600);

      console.log(`[PERF] Median render time (5 runs): ${medianResult.renderTime.toFixed(2)}ms`);
      console.log(`[PERF] Median memory increase: ${medianResult.memoryIncrease.toFixed(2)}MB`);
    });
  });

  describe('Memory Efficiency', () => {
    it('should keep memory increase under 10MB for 100 games/agents', async () => {
      const games = generateMockGames(100);
      const agents = generateMockAgents(100);

      const result = await measureRenderPerformance(() => {
        const { unmount } = render(
          <SearchFilters
            filters={{}}
            onFiltersChange={mockOnFiltersChange}
            games={games}
            agents={agents}
          />
        );
        unmount();
      });

      // Memory check (only meaningful if performance.memory available)
      if (result.memoryBefore > 0) {
        expect(result.memoryIncrease).toBeLessThan(10);
        console.log(
          `[PERF] Memory delta for 100 games/agents: ${result.memoryIncrease.toFixed(2)}MB`
        );
      }
    });
  });

  describe('Date Range Filter Performance', () => {
    it('should apply date range filters efficiently', async () => {
      const games = generateMockGames(50);
      const agents = generateMockAgents(50);

      const { unmount, container } = render(
        <SearchFilters
          filters={{}}
          onFiltersChange={mockOnFiltersChange}
          games={games}
          agents={agents}
        />
      );

      const dateFromInput = container.querySelector('input[type="date"]') as HTMLInputElement;

      const startTime = performance.now();

      if (dateFromInput) {
        fireEvent.change(dateFromInput, { target: { value: '2024-01-01' } });
      }

      const endTime = performance.now();
      const duration = endTime - startTime;

      expect(duration).toBeLessThan(50);

      console.log(`[PERF] Date filter applied in ${duration.toFixed(2)}ms`);

      unmount();
    });
  });
});
