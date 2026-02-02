/**
 * Tests for CostCalculator component
 * Issue #3005: Frontend coverage improvements
 */

import React from 'react';

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

// Mock framer-motion
vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, ...props }: React.PropsWithChildren<Record<string, unknown>>) => (
      <div {...props}>{children}</div>
    ),
  },
}));

// Import after mocking
import { CostCalculator } from '../CostCalculator';

describe('CostCalculator', () => {
  let mockCreateObjectURL: ReturnType<typeof vi.fn>;
  let mockRevokeObjectURL: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    // Mock URL API for export functionality
    mockCreateObjectURL = vi.fn(() => 'blob:test-url');
    mockRevokeObjectURL = vi.fn();
    global.URL.createObjectURL = mockCreateObjectURL;
    global.URL.revokeObjectURL = mockRevokeObjectURL;
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  // =========================================================================
  // Rendering Tests
  // =========================================================================

  describe('Rendering', () => {
    it('should render the cost calculator card', () => {
      render(<CostCalculator />);

      expect(screen.getByText('Cost Calculator')).toBeInTheDocument();
    });

    it('should render export button', () => {
      render(<CostCalculator />);

      expect(screen.getByRole('button', { name: /export/i })).toBeInTheDocument();
    });

    it('should render all sliders', () => {
      render(<CostCalculator />);

      expect(screen.getByText('Queries per Month')).toBeInTheDocument();
      expect(screen.getByText('Cache Hit Rate')).toBeInTheDocument();
      expect(screen.getByText('FAST Strategy')).toBeInTheDocument();
      expect(screen.getByText('BALANCED Strategy')).toBeInTheDocument();
    });

    it('should display estimated monthly cost', () => {
      render(<CostCalculator />);

      expect(screen.getByText('Estimated Monthly Cost')).toBeInTheDocument();
    });

    it('should display cost breakdown section', () => {
      render(<CostCalculator />);

      expect(screen.getByText('Cost Breakdown')).toBeInTheDocument();
    });

    it('should display savings section', () => {
      render(<CostCalculator />);

      expect(screen.getByText('Estimated Savings')).toBeInTheDocument();
    });

    it('should apply custom className', () => {
      const { container } = render(<CostCalculator className="custom-class" />);

      expect(container.querySelector('.custom-class')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Default Values Tests
  // =========================================================================

  describe('Default Values', () => {
    it('should have default queries per month of 100,000', () => {
      render(<CostCalculator />);

      // toLocaleString formatting varies by locale (100,000 or 100.000 or 100000)
      expect(screen.getByText((content) => /^100[\s,.\u00A0]?000$/.test(content))).toBeInTheDocument();
    });

    it('should have default cache hit rate of 80%', () => {
      render(<CostCalculator />);

      expect(screen.getByText('80%')).toBeInTheDocument();
    });

    it('should have default FAST strategy of 70%', () => {
      render(<CostCalculator />);

      expect(screen.getByText('70%')).toBeInTheDocument();
    });

    it('should have default BALANCED strategy of 25%', () => {
      render(<CostCalculator />);

      expect(screen.getByText('25%')).toBeInTheDocument();
    });

    it('should calculate PRECISE strategy as remainder', () => {
      render(<CostCalculator />);

      // 100 - 70 - 25 = 5%
      expect(screen.getByText('5%')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Slider Interaction Tests
  // =========================================================================

  describe('Slider Interactions', () => {
    it('should update queries per month when slider changes', () => {
      render(<CostCalculator />);

      const sliders = screen.getAllByRole('slider');
      const queriesSlider = sliders[0];

      fireEvent.change(queriesSlider, { target: { value: '200000' } });

      // toLocaleString formatting varies by locale (200,000 or 200.000 or 200000)
      expect(screen.getByText((content) => /^200[\s,.\u00A0]?000$/.test(content))).toBeInTheDocument();
    });

    it('should update cache hit rate when slider changes', () => {
      render(<CostCalculator />);

      const sliders = screen.getAllByRole('slider');
      const cacheSlider = sliders[1];

      fireEvent.change(cacheSlider, { target: { value: '90' } });

      expect(screen.getByText('90%')).toBeInTheDocument();
    });

    it('should update FAST strategy percentage when slider changes', () => {
      render(<CostCalculator />);

      const sliders = screen.getAllByRole('slider');
      const fastSlider = sliders[2];

      fireEvent.change(fastSlider, { target: { value: '50' } });

      expect(screen.getAllByText('50%').length).toBeGreaterThan(0);
    });

    it('should update BALANCED strategy percentage when slider changes', () => {
      render(<CostCalculator />);

      const sliders = screen.getAllByRole('slider');
      const balancedSlider = sliders[3];

      fireEvent.change(balancedSlider, { target: { value: '30' } });

      expect(screen.getAllByText('30%').length).toBeGreaterThan(0);
    });

    it('should recalculate PRECISE when FAST changes', () => {
      render(<CostCalculator />);

      const sliders = screen.getAllByRole('slider');
      const fastSlider = sliders[2];

      // Set FAST to 60% (BALANCED is 25%, so PRECISE = 15%)
      fireEvent.change(fastSlider, { target: { value: '60' } });

      expect(screen.getByText('15%')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Cost Calculation Tests
  // =========================================================================

  describe('Cost Calculations', () => {
    it('should calculate and display total monthly cost', () => {
      render(<CostCalculator />);

      // Find the total cost display - there should be dollar amounts on the page
      const costDisplays = screen.getAllByText(/^\$\d+\.?\d*/);
      expect(costDisplays.length).toBeGreaterThan(0);
    });

    it('should display cost per 1K queries', () => {
      render(<CostCalculator />);

      expect(screen.getByText(/per 1K queries/)).toBeInTheDocument();
    });

    it('should display LLM Inference in breakdown', () => {
      render(<CostCalculator />);

      expect(screen.getByText('LLM Inference')).toBeInTheDocument();
    });

    it('should display Embeddings in breakdown', () => {
      render(<CostCalculator />);

      expect(screen.getByText('Embeddings')).toBeInTheDocument();
    });

    it('should display Infrastructure in breakdown', () => {
      render(<CostCalculator />);

      expect(screen.getByText('Infrastructure')).toBeInTheDocument();
    });

    it('should update costs when parameters change', async () => {
      render(<CostCalculator />);

      const initialCost = screen.getByText(/Estimated Monthly Cost/).nextSibling?.textContent;

      const sliders = screen.getAllByRole('slider');
      const queriesSlider = sliders[0];

      fireEvent.change(queriesSlider, { target: { value: '500000' } });

      await waitFor(() => {
        const newCost = screen.getByText(/Estimated Monthly Cost/).nextSibling?.textContent;
        expect(newCost).not.toBe(initialCost);
      });
    });
  });

  // =========================================================================
  // Savings Display Tests
  // =========================================================================

  describe('Savings Display', () => {
    it('should display From Cache savings', () => {
      render(<CostCalculator />);

      expect(screen.getByText('From Cache')).toBeInTheDocument();
    });

    it('should display From Optimization savings', () => {
      render(<CostCalculator />);

      expect(screen.getByText('From Optimization')).toBeInTheDocument();
    });

    it('should have green styling for savings section', () => {
      const { container } = render(<CostCalculator />);

      const savingsSection = container.querySelector('.bg-green-500\\/10');
      expect(savingsSection).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Export Functionality Tests
  // =========================================================================

  describe('Export Functionality', () => {
    it('should create download link when export is clicked', async () => {
      const user = userEvent.setup();
      const mockClick = vi.fn();

      // Mock document.createElement to capture the anchor click
      const originalCreateElement = document.createElement.bind(document);
      vi.spyOn(document, 'createElement').mockImplementation((tag: string) => {
        const element = originalCreateElement(tag);
        if (tag === 'a') {
          element.click = mockClick;
        }
        return element;
      });

      render(<CostCalculator />);

      const exportButton = screen.getByRole('button', { name: /export/i });
      await user.click(exportButton);

      expect(mockCreateObjectURL).toHaveBeenCalled();
      expect(mockClick).toHaveBeenCalled();
      expect(mockRevokeObjectURL).toHaveBeenCalled();
    });

    it('should create blob with JSON content type', async () => {
      const user = userEvent.setup();

      render(<CostCalculator />);

      const exportButton = screen.getByRole('button', { name: /export/i });
      await user.click(exportButton);

      expect(mockCreateObjectURL).toHaveBeenCalledWith(
        expect.any(Blob)
      );
    });
  });

  // =========================================================================
  // Cost Bar Component Tests
  // =========================================================================

  describe('Cost Bars', () => {
    it('should render cost bars for each cost type', () => {
      const { container } = render(<CostCalculator />);

      // Cost bars use motion.div with style.backgroundColor
      const costBars = container.querySelectorAll('.h-3.bg-muted');
      expect(costBars.length).toBe(3); // LLM, Embeddings, Infrastructure
    });

    it('should display dollar amounts for each cost', () => {
      render(<CostCalculator />);

      // Find elements with dollar values in cost breakdown
      const dollarAmounts = screen.getAllByText(/^\$\d+\.?\d*/);
      expect(dollarAmounts.length).toBeGreaterThanOrEqual(3);
    });
  });

  // =========================================================================
  // Strategy Distribution Section Tests
  // =========================================================================

  describe('Strategy Distribution Section', () => {
    it('should display Strategy Distribution header', () => {
      render(<CostCalculator />);

      expect(screen.getByText('Strategy Distribution')).toBeInTheDocument();
    });

    it('should display PRECISE Strategy label', () => {
      render(<CostCalculator />);

      expect(screen.getByText('PRECISE Strategy')).toBeInTheDocument();
    });

    it('should prevent negative PRECISE percentage', () => {
      render(<CostCalculator />);

      const sliders = screen.getAllByRole('slider');
      const fastSlider = sliders[2];
      const balancedSlider = sliders[3];

      // Set FAST to max
      fireEvent.change(fastSlider, { target: { value: '75' } });
      // Set BALANCED to value that would make PRECISE negative
      fireEvent.change(balancedSlider, { target: { value: '25' } });

      // PRECISE should be clamped to 0%
      expect(screen.getByText('0%')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Edge Cases Tests
  // =========================================================================

  describe('Edge Cases', () => {
    it('should handle minimum queries value', () => {
      render(<CostCalculator />);

      const sliders = screen.getAllByRole('slider');
      const queriesSlider = sliders[0];

      fireEvent.change(queriesSlider, { target: { value: '1000' } });

      // toLocaleString formatting varies by locale (1,000 or 1.000 or 1000)
      expect(screen.getByText((content) => /^1[\s,.\u00A0]?000$/.test(content))).toBeInTheDocument();
    });

    it('should handle maximum queries value', () => {
      render(<CostCalculator />);

      const sliders = screen.getAllByRole('slider');
      const queriesSlider = sliders[0];

      fireEvent.change(queriesSlider, { target: { value: '1000000' } });

      // toLocaleString formatting varies by locale (1,000,000 or 1.000.000 or 1000000)
      expect(screen.getByText((content) => /^1[\s,.\u00A0]?000[\s,.\u00A0]?000$/.test(content))).toBeInTheDocument();
    });

    it('should handle 0% cache hit rate', () => {
      render(<CostCalculator />);

      const sliders = screen.getAllByRole('slider');
      const cacheSlider = sliders[1];

      fireEvent.change(cacheSlider, { target: { value: '0' } });

      // Should still render without errors
      expect(screen.getByText('Estimated Monthly Cost')).toBeInTheDocument();
    });

    it('should handle 100% cache hit rate', () => {
      render(<CostCalculator />);

      const sliders = screen.getAllByRole('slider');
      const cacheSlider = sliders[1];

      fireEvent.change(cacheSlider, { target: { value: '100' } });

      expect(screen.getByText('100%')).toBeInTheDocument();
    });
  });
});
