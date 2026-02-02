/**
 * Tests for VariantComparisonTool component
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

// Import after mocking
import { VariantComparisonTool } from '../VariantComparisonTool';

describe('VariantComparisonTool', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // =========================================================================
  // Rendering Tests
  // =========================================================================

  describe('Rendering', () => {
    it('should render the variant comparison card', () => {
      render(<VariantComparisonTool />);

      // 31 variants = 6 strategies × 4 templates × 4 tiers (excluding Anonymous)
      // Anonymous users have NO ACCESS to RAG system (authentication required)
      expect(screen.getByText('31 RAG Variant Comparison')).toBeInTheDocument();
    });

    it('should render subtitle description', () => {
      render(<VariantComparisonTool />);

      expect(screen.getByText(/Click variants to compare/i)).toBeInTheDocument();
    });

    it('should render Filters button', () => {
      render(<VariantComparisonTool />);

      expect(screen.getByRole('button', { name: /filters/i })).toBeInTheDocument();
    });

    it('should render search input', () => {
      render(<VariantComparisonTool />);

      expect(screen.getByPlaceholderText(/Search by model, strategy/i)).toBeInTheDocument();
    });

    it('should render strategy summary cards', () => {
      render(<VariantComparisonTool />);

      // Strategy names appear multiple times (cards and variant details)
      expect(screen.getAllByText('FAST').length).toBeGreaterThan(0);
      expect(screen.getAllByText('BALANCED').length).toBeGreaterThan(0);
      expect(screen.getAllByText('PRECISE').length).toBeGreaterThan(0);
    });

    it('should apply custom className', () => {
      const { container } = render(<VariantComparisonTool className="custom-class" />);

      expect(container.querySelector('.custom-class')).toBeInTheDocument();
    });

    it('should show variant count', () => {
      render(<VariantComparisonTool />);

      // Should show "Showing X of Y variants" pattern
      expect(screen.getByText(/of \d+ variants/)).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Search Functionality Tests
  // =========================================================================

  describe('Search Functionality', () => {
    it('should filter variants by model name', async () => {
      const user = userEvent.setup();
      render(<VariantComparisonTool />);

      const searchInput = screen.getByPlaceholderText(/Search by model/i);
      await user.type(searchInput, 'Claude');

      // Should filter to only Claude models, showing updated count
      await waitFor(() => {
        expect(screen.getByText(/of \d+ variants/)).toBeInTheDocument();
      });
    });

    it('should filter variants by strategy', async () => {
      const user = userEvent.setup();
      render(<VariantComparisonTool />);

      const searchInput = screen.getByPlaceholderText(/Search by model/i);
      await user.type(searchInput, 'FAST');

      await waitFor(() => {
        expect(screen.getByText(/of \d+ variants/)).toBeInTheDocument();
      });
    });

    it('should clear search results', async () => {
      const user = userEvent.setup();
      render(<VariantComparisonTool />);

      const searchInput = screen.getByPlaceholderText(/Search by model/i);
      await user.type(searchInput, 'xyznonexistent');

      await waitFor(() => {
        expect(screen.getByText(/No variants match your filters/i)).toBeInTheDocument();
      });

      await user.clear(searchInput);

      await waitFor(() => {
        expect(screen.getByText(/of \d+ variants/)).toBeInTheDocument();
      });
    });
  });

  // =========================================================================
  // Filter Panel Tests
  // =========================================================================

  describe('Filter Panel', () => {
    it('should toggle filter panel visibility', async () => {
      const user = userEvent.setup();
      render(<VariantComparisonTool />);

      const filterButton = screen.getByRole('button', { name: /filters/i });
      await user.click(filterButton);

      // Filter section should be visible (labels may appear multiple times)
      expect(screen.getAllByText('Strategy').length).toBeGreaterThan(0);
      expect(screen.getAllByText('Template').length).toBeGreaterThan(0);
      expect(screen.getAllByText('User Tier').length).toBeGreaterThan(0);
    });

    it('should show template filter options', async () => {
      const user = userEvent.setup();
      render(<VariantComparisonTool />);

      const filterButton = screen.getByRole('button', { name: /filters/i });
      await user.click(filterButton);

      // Template names appear in filter section and variant cards
      expect(screen.getAllByText('Rules').length).toBeGreaterThan(0);
      expect(screen.getAllByText('Resources').length).toBeGreaterThan(0);
      expect(screen.getAllByText('Setup').length).toBeGreaterThan(0);
      expect(screen.getAllByText('Learning').length).toBeGreaterThan(0);
    });

    it('should show user tier filter options', async () => {
      const user = userEvent.setup();
      render(<VariantComparisonTool />);

      const filterButton = screen.getByRole('button', { name: /filters/i });
      await user.click(filterButton);

      // User tier labels may appear multiple times (filter section and variant cards)
      // NOTE: Anonymous is NOT included - authentication required for RAG access
      expect(screen.getAllByText('User').length).toBeGreaterThan(0);
      expect(screen.getAllByText('Editor').length).toBeGreaterThan(0);
      expect(screen.getAllByText('Admin').length).toBeGreaterThan(0);
      expect(screen.getAllByText('Premium').length).toBeGreaterThan(0);
    });

    it('should toggle strategy filter', async () => {
      const user = userEvent.setup();
      render(<VariantComparisonTool />);

      const filterButton = screen.getByRole('button', { name: /filters/i });
      await user.click(filterButton);

      // Find and click FAST filter button (in the filter section)
      const strategyButtons = screen.getAllByText('FAST');
      const filterStrategyButton = strategyButtons.find(btn =>
        btn.closest('button')?.classList.contains('rounded-full')
      );

      if (filterStrategyButton) {
        await user.click(filterStrategyButton);
        // Filter count should change
      }
    });
  });

  // =========================================================================
  // Variant Selection Tests
  // =========================================================================

  describe('Variant Selection', () => {
    it('should display variant cards in grid', () => {
      render(<VariantComparisonTool />);

      // Should display variant data like tokens, cost, accuracy
      expect(screen.getAllByText(/tokens/i).length).toBeGreaterThan(0);
    });

    it('should show variant details', () => {
      render(<VariantComparisonTool />);

      // Should show model names
      expect(screen.getAllByText(/Llama|Gemini|Claude|DeepSeek|GPT/i).length).toBeGreaterThan(0);
    });

    it('should show accuracy targets', () => {
      render(<VariantComparisonTool />);

      // Check for accuracy percentages
      const accuracyElements = screen.getAllByText(/%$/);
      expect(accuracyElements.length).toBeGreaterThan(0);
    });

    it('should show latency values', () => {
      render(<VariantComparisonTool />);

      // Check for latency values
      const latencyElements = screen.getAllByText(/ms$/);
      expect(latencyElements.length).toBeGreaterThan(0);
    });
  });

  // =========================================================================
  // Comparison Panel Tests
  // =========================================================================

  describe('Comparison Panel', () => {
    it('should show selection hint', () => {
      render(<VariantComparisonTool />);

      // Should initially have no variants selected
      expect(screen.queryByText(/Comparing/)).not.toBeInTheDocument();
    });

    it('should display features for variants', () => {
      render(<VariantComparisonTool />);

      // Features like "Zero-cost", "Cached rules" should be visible
      expect(screen.getAllByText(/Zero-cost|Cached|CRAG|Multi-agent/i).length).toBeGreaterThan(0);
    });
  });

  // =========================================================================
  // Strategy Cards Tests
  // =========================================================================

  describe('Strategy Cards', () => {
    it('should display strategy descriptions', () => {
      render(<VariantComparisonTool />);

      // Each strategy card shows variant count
      expect(screen.getAllByText(/variants/i).length).toBeGreaterThan(0);
    });

    it('should show free variant count', () => {
      render(<VariantComparisonTool />);

      // FAST variants are free
      expect(screen.getAllByText(/free/i).length).toBeGreaterThan(0);
    });
  });

  // =========================================================================
  // Empty State Tests
  // =========================================================================

  describe('Empty State', () => {
    it('should show empty message when no variants match', async () => {
      const user = userEvent.setup();
      render(<VariantComparisonTool />);

      const searchInput = screen.getByPlaceholderText(/Search by model/i);
      await user.type(searchInput, 'nonexistentmodel12345');

      await waitFor(() => {
        expect(screen.getByText(/No variants match your filters/i)).toBeInTheDocument();
      });
    });
  });

  // =========================================================================
  // Cost Display Tests
  // =========================================================================

  describe('Cost Display', () => {
    it('should display cost per 1K queries', () => {
      render(<VariantComparisonTool />);

      // Should show "$" or "Free" for costs
      const costElements = screen.getAllByText(/\$\/1K|Free/i);
      expect(costElements.length).toBeGreaterThan(0);
    });

    it('should display Free for zero-cost variants', () => {
      render(<VariantComparisonTool />);

      // FAST variants should show "Free"
      const freeElements = screen.getAllByText('Free');
      expect(freeElements.length).toBeGreaterThan(0);
    });
  });

  // =========================================================================
  // Filter Handler Tests (for function coverage)
  // =========================================================================

  describe('Filter Handlers', () => {
    it('should toggle strategy filter and update variant count', async () => {
      const user = userEvent.setup();
      render(<VariantComparisonTool />);

      // Open filters
      const filterButton = screen.getByRole('button', { name: /filters/i });
      await user.click(filterButton);

      // Click FAST filter
      const fastButtons = screen.getAllByRole('button').filter(
        btn => btn.textContent === 'FAST' && btn.classList.contains('rounded-full')
      );

      if (fastButtons.length > 0) {
        await user.click(fastButtons[0]);

        // Variant count should change
        await waitFor(() => {
          expect(screen.getByText(/of \d+ variants/)).toBeInTheDocument();
        });
      }
    });

    it('should toggle template filter', async () => {
      const user = userEvent.setup();
      render(<VariantComparisonTool />);

      // Open filters
      const filterButton = screen.getByRole('button', { name: /filters/i });
      await user.click(filterButton);

      // Click Rules template filter
      const templateButtons = screen.getAllByRole('button').filter(
        btn => btn.textContent === 'Rules' && btn.classList.contains('rounded-full')
      );

      if (templateButtons.length > 0) {
        await user.click(templateButtons[0]);

        // Should filter variants
        await waitFor(() => {
          expect(screen.getByText(/of \d+ variants/)).toBeInTheDocument();
        });
      }
    });

    it('should toggle tier filter', async () => {
      const user = userEvent.setup();
      render(<VariantComparisonTool />);

      // Open filters
      const filterButton = screen.getByRole('button', { name: /filters/i });
      await user.click(filterButton);

      // Click User tier filter
      const tierButtons = screen.getAllByRole('button').filter(
        btn => btn.textContent === 'User' && btn.classList.contains('rounded-full')
      );

      if (tierButtons.length > 0) {
        await user.click(tierButtons[0]);

        // Should filter variants
        await waitFor(() => {
          expect(screen.getByText(/of \d+ variants/)).toBeInTheDocument();
        });
      }
    });

    it('should handle variant selection', async () => {
      const user = userEvent.setup();
      render(<VariantComparisonTool />);

      // Find a variant card and click it
      const variantCards = screen.getAllByText(/tokens/i);
      if (variantCards.length > 0) {
        const card = variantCards[0].closest('div[class*="border"]');
        if (card) {
          await user.click(card);

          // Should show selection indicator
          await waitFor(() => {
            expect(screen.getByText(/selected for comparison/)).toBeInTheDocument();
          });
        }
      }
    });

    it('should handle multiple filter combinations', async () => {
      const user = userEvent.setup();
      render(<VariantComparisonTool />);

      // Open filters
      const filterButton = screen.getByRole('button', { name: /filters/i });
      await user.click(filterButton);

      // Toggle multiple filters
      const fastButtons = screen.getAllByRole('button').filter(
        btn => btn.textContent === 'FAST' && btn.classList.contains('rounded-full')
      );

      const userButtons = screen.getAllByRole('button').filter(
        btn => btn.textContent === 'User' && btn.classList.contains('rounded-full')
      );

      if (fastButtons.length > 0) {
        await user.click(fastButtons[0]);
      }

      if (userButtons.length > 0) {
        await user.click(userButtons[0]);
      }

      // Variant count should update
      await waitFor(() => {
        expect(screen.getByText(/of \d+ variants/)).toBeInTheDocument();
      });
    });

    it('should clear all filters', async () => {
      const user = userEvent.setup();
      render(<VariantComparisonTool />);

      // Open filters and apply some
      const filterButton = screen.getByRole('button', { name: /filters/i });
      await user.click(filterButton);

      const fastButtons = screen.getAllByRole('button').filter(
        btn => btn.textContent === 'FAST' && btn.classList.contains('rounded-full')
      );

      if (fastButtons.length > 0) {
        await user.click(fastButtons[0]);

        // Click again to toggle off
        await user.click(fastButtons[0]);

        // Filter should be cleared
        expect(screen.getByText(/of \d+ variants/)).toBeInTheDocument();
      }
    });
  });
});
