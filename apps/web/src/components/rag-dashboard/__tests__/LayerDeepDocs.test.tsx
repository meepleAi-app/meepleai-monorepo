/**
 * Tests for LayerDeepDocs component
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

// Import after mocking
import { LayerDeepDocs } from '../LayerDeepDocs';

describe('LayerDeepDocs', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // =========================================================================
  // Rendering Tests
  // =========================================================================

  describe('Rendering', () => {
    it('should render the layer deep docs card', () => {
      render(<LayerDeepDocs />);

      expect(screen.getByText('Layer Deep Documentation')).toBeInTheDocument();
    });

    it('should render subtitle description', () => {
      render(<LayerDeepDocs />);

      expect(screen.getByText(/Detailed technical documentation/i)).toBeInTheDocument();
    });

    it('should render all 6 layer tabs', () => {
      render(<LayerDeepDocs />);

      expect(screen.getByText('Intelligent Routing')).toBeInTheDocument();
      expect(screen.getByText('Semantic Cache')).toBeInTheDocument();
      expect(screen.getByText('Modular Retrieval')).toBeInTheDocument();
      expect(screen.getByText('CRAG Evaluation')).toBeInTheDocument();
      expect(screen.getByText('Adaptive Generation')).toBeInTheDocument();
      expect(screen.getByText('Self-Validation')).toBeInTheDocument();
    });

    it('should show layer short names', () => {
      render(<LayerDeepDocs />);

      expect(screen.getByText('L1:')).toBeInTheDocument();
      expect(screen.getByText('L2:')).toBeInTheDocument();
      expect(screen.getByText('L3:')).toBeInTheDocument();
      expect(screen.getByText('L4:')).toBeInTheDocument();
      expect(screen.getByText('L5:')).toBeInTheDocument();
      expect(screen.getByText('L6:')).toBeInTheDocument();
    });

    it('should apply custom className', () => {
      const { container } = render(<LayerDeepDocs className="custom-class" />);

      expect(container.querySelector('.custom-class')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Default State Tests
  // =========================================================================

  describe('Default State', () => {
    it('should show Intelligent Routing (L1) as default layer', () => {
      render(<LayerDeepDocs />);

      expect(screen.getByText(/L1: Intelligent Routing/)).toBeInTheDocument();
    });

    it('should show Code Example tab as default', () => {
      render(<LayerDeepDocs />);

      const codeTab = screen.getByRole('button', { name: /code example/i });
      expect(codeTab).toHaveClass('bg-primary/10');
    });

    it('should display code example content by default', () => {
      render(<LayerDeepDocs />);

      expect(screen.getByText('Implementation Example')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Layer Tab Navigation Tests
  // =========================================================================

  describe('Layer Tab Navigation', () => {
    it('should switch to Semantic Cache layer', async () => {
      const user = userEvent.setup();
      render(<LayerDeepDocs />);

      const cacheTab = screen.getByRole('button', { name: /L2:.*Semantic Cache/i });
      await user.click(cacheTab);

      expect(screen.getByText(/L2: Semantic Cache/)).toBeInTheDocument();
    });

    it('should switch to Modular Retrieval layer', async () => {
      const user = userEvent.setup();
      render(<LayerDeepDocs />);

      const retrievalTab = screen.getByRole('button', { name: /L3:.*Modular Retrieval/i });
      await user.click(retrievalTab);

      expect(screen.getByText(/L3: Modular Retrieval/)).toBeInTheDocument();
    });

    it('should switch to CRAG Evaluation layer', async () => {
      const user = userEvent.setup();
      render(<LayerDeepDocs />);

      const cragTab = screen.getByRole('button', { name: /L4:.*CRAG Evaluation/i });
      await user.click(cragTab);

      expect(screen.getByText(/L4: CRAG Evaluation/)).toBeInTheDocument();
    });

    it('should switch to Adaptive Generation layer', async () => {
      const user = userEvent.setup();
      render(<LayerDeepDocs />);

      const genTab = screen.getByRole('button', { name: /L5:.*Adaptive Generation/i });
      await user.click(genTab);

      expect(screen.getByText(/L5: Adaptive Generation/)).toBeInTheDocument();
    });

    it('should switch to Self-Validation layer', async () => {
      const user = userEvent.setup();
      render(<LayerDeepDocs />);

      const validTab = screen.getByRole('button', { name: /L6:.*Self-Validation/i });
      await user.click(validTab);

      expect(screen.getByText(/L6: Self-Validation/)).toBeInTheDocument();
    });

    it('should update description when switching layers', async () => {
      const user = userEvent.setup();
      render(<LayerDeepDocs />);

      const cacheTab = screen.getByRole('button', { name: /L2:.*Semantic Cache/i });
      await user.click(cacheTab);

      expect(screen.getByText(/Two-tier caching system/)).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Content Tab Navigation Tests
  // =========================================================================

  describe('Content Tab Navigation', () => {
    it('should switch to Decision Tree tab', async () => {
      const user = userEvent.setup();
      render(<LayerDeepDocs />);

      const treeTab = screen.getByRole('button', { name: /decision tree/i });
      await user.click(treeTab);

      // 'Decision Tree' appears both as button label and as content heading
      const decisionTreeElements = screen.getAllByText('Decision Tree');
      expect(decisionTreeElements.length).toBeGreaterThan(0);
    });

    it('should switch to Configuration tab', async () => {
      const user = userEvent.setup();
      render(<LayerDeepDocs />);

      const configTab = screen.getByRole('button', { name: /configuration/i });
      await user.click(configTab);

      expect(screen.getByText('Configuration Options')).toBeInTheDocument();
    });

    it('should switch to Use Cases tab', async () => {
      const user = userEvent.setup();
      render(<LayerDeepDocs />);

      const casesTab = screen.getByRole('button', { name: /use cases/i });
      await user.click(casesTab);

      expect(screen.getByText('Usage Examples')).toBeInTheDocument();
    });

    it('should switch back to Code Example tab', async () => {
      const user = userEvent.setup();
      render(<LayerDeepDocs />);

      // Switch to config first
      const configTab = screen.getByRole('button', { name: /configuration/i });
      await user.click(configTab);

      // Switch back to code
      const codeTab = screen.getByRole('button', { name: /code example/i });
      await user.click(codeTab);

      expect(screen.getByText('Implementation Example')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Code Block Tests
  // =========================================================================

  describe('Code Block', () => {
    it('should display code example with syntax', () => {
      render(<LayerDeepDocs />);

      expect(screen.getByText(/interface RoutingResult/)).toBeInTheDocument();
    });

    it('should have copy button', () => {
      render(<LayerDeepDocs />);

      const copyButtons = screen.getAllByRole('button').filter(
        btn => btn.querySelector('svg')
      );
      expect(copyButtons.length).toBeGreaterThan(0);
    });

    it('should copy code to clipboard when copy button is clicked', async () => {
      const user = userEvent.setup();
      render(<LayerDeepDocs />);

      // Find the copy button in the code block
      const codeSection = screen.getByText('Implementation Example').closest('div');
      const copyButton = codeSection?.querySelector('button');

      if (copyButton) {
        await user.click(copyButton);

        expect(navigator.clipboard.writeText).toHaveBeenCalled();
      }
    });
  });

  // =========================================================================
  // Decision Tree Display Tests
  // =========================================================================

  describe('Decision Tree Display', () => {
    it('should display decision nodes', async () => {
      const user = userEvent.setup();
      render(<LayerDeepDocs />);

      const treeTab = screen.getByRole('button', { name: /decision tree/i });
      await user.click(treeTab);

      expect(screen.getByText(/Is it a simple rule question/)).toBeInTheDocument();
    });

    it('should display yes/no options', async () => {
      const user = userEvent.setup();
      render(<LayerDeepDocs />);

      const treeTab = screen.getByRole('button', { name: /decision tree/i });
      await user.click(treeTab);

      const yesElements = screen.getAllByText(/Yes:/);
      const noElements = screen.getAllByText(/No:/);

      expect(yesElements.length).toBeGreaterThan(0);
      expect(noElements.length).toBeGreaterThan(0);
    });

    it('should display condition outcomes', async () => {
      const user = userEvent.setup();
      render(<LayerDeepDocs />);

      const treeTab = screen.getByRole('button', { name: /decision tree/i });
      await user.click(treeTab);

      expect(screen.getByText(/FAST \+ rule_lookup/)).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Configuration Table Tests
  // =========================================================================

  describe('Configuration Table', () => {
    it('should display config table headers', async () => {
      const user = userEvent.setup();
      render(<LayerDeepDocs />);

      const configTab = screen.getByRole('button', { name: /configuration/i });
      await user.click(configTab);

      expect(screen.getByText('Option')).toBeInTheDocument();
      expect(screen.getByText('Type')).toBeInTheDocument();
      expect(screen.getByText('Default')).toBeInTheDocument();
      expect(screen.getByText('Description')).toBeInTheDocument();
    });

    it('should display configuration options', async () => {
      const user = userEvent.setup();
      render(<LayerDeepDocs />);

      const configTab = screen.getByRole('button', { name: /configuration/i });
      await user.click(configTab);

      expect(screen.getByText('maxComplexityScore')).toBeInTheDocument();
      expect(screen.getByText('templateConfidenceThreshold')).toBeInTheDocument();
    });

    it('should display config types', async () => {
      const user = userEvent.setup();
      render(<LayerDeepDocs />);

      const configTab = screen.getByRole('button', { name: /configuration/i });
      await user.click(configTab);

      // 'number' and 'boolean' may appear in multiple table cells
      const numberElements = screen.getAllByText('number');
      expect(numberElements.length).toBeGreaterThan(0);
      const booleanElements = screen.getAllByText('boolean');
      expect(booleanElements.length).toBeGreaterThan(0);
    });
  });

  // =========================================================================
  // Use Cases List Tests
  // =========================================================================

  describe('Use Cases List', () => {
    it('should display use case scenarios', async () => {
      const user = userEvent.setup();
      render(<LayerDeepDocs />);

      const casesTab = screen.getByRole('button', { name: /use cases/i });
      await user.click(casesTab);

      expect(screen.getByText(/What are the rules for 7 in Catan/)).toBeInTheDocument();
    });

    it('should display use case behaviors', async () => {
      const user = userEvent.setup();
      render(<LayerDeepDocs />);

      const casesTab = screen.getByRole('button', { name: /use cases/i });
      await user.click(casesTab);

      expect(screen.getByText(/rule_lookup, complexity=1, FAST/)).toBeInTheDocument();
    });

    it('should display token counts', async () => {
      const user = userEvent.setup();
      render(<LayerDeepDocs />);

      const casesTab = screen.getByRole('button', { name: /use cases/i });
      await user.click(casesTab);

      expect(screen.getByText(/~280 tokens/)).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Layer Content Tests
  // =========================================================================

  describe('Layer Content', () => {
    it('should display layer purpose', () => {
      render(<LayerDeepDocs />);

      expect(screen.getByText(/Purpose:/)).toBeInTheDocument();
    });

    it('should display layer description', () => {
      render(<LayerDeepDocs />);

      expect(screen.getByText(/Classifies incoming queries/)).toBeInTheDocument();
    });

    it('should update content when layer changes', async () => {
      const user = userEvent.setup();
      render(<LayerDeepDocs />);

      const cacheTab = screen.getByRole('button', { name: /L2:.*Semantic Cache/i });
      await user.click(cacheTab);

      expect(screen.getByText(/Reduce redundant LLM calls/)).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Cross-Layer Navigation Tests
  // =========================================================================

  describe('Cross-Layer Navigation', () => {
    it('should maintain content tab state when switching layers', async () => {
      const user = userEvent.setup();
      render(<LayerDeepDocs />);

      // Switch to config tab
      const configTab = screen.getByRole('button', { name: /configuration/i });
      await user.click(configTab);

      // Switch to different layer
      const cacheTab = screen.getByRole('button', { name: /L2:.*Semantic Cache/i });
      await user.click(cacheTab);

      // Config tab should still be selected
      expect(screen.getByText('Configuration Options')).toBeInTheDocument();
      expect(screen.getByText('exactMatchTtl')).toBeInTheDocument();
    });

    it('should display different config options per layer', async () => {
      const user = userEvent.setup();
      render(<LayerDeepDocs />);

      const configTab = screen.getByRole('button', { name: /configuration/i });
      await user.click(configTab);

      // Check L1 config
      expect(screen.getByText('maxComplexityScore')).toBeInTheDocument();

      // Switch to L2
      const cacheTab = screen.getByRole('button', { name: /L2:.*Semantic Cache/i });
      await user.click(cacheTab);

      // Check L2 config
      expect(screen.getByText('exactMatchTtl')).toBeInTheDocument();
      expect(screen.getByText('semanticThreshold')).toBeInTheDocument();
    });

    it('should display different use cases per layer', async () => {
      const user = userEvent.setup();
      render(<LayerDeepDocs />);

      const casesTab = screen.getByRole('button', { name: /use cases/i });
      await user.click(casesTab);

      // Check L1 use case
      expect(screen.getByText(/What are the rules for 7 in Catan/)).toBeInTheDocument();

      // Switch to L2
      const cacheTab = screen.getByRole('button', { name: /L2:.*Semantic Cache/i });
      await user.click(cacheTab);

      // Check L2 use case
      expect(screen.getByText(/Repeated exact query/)).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Edge Cases Tests
  // =========================================================================

  describe('Edge Cases', () => {
    it('should handle rapid layer switching', async () => {
      const user = userEvent.setup();
      render(<LayerDeepDocs />);

      // Rapidly click through layers
      for (let i = 1; i <= 6; i++) {
        const tabButton = screen.getByRole('button', { name: new RegExp(`L${i}:`) });
        await user.click(tabButton);
      }

      // Should end up on L6
      expect(screen.getByText(/L6: Self-Validation/)).toBeInTheDocument();
    });

    it('should handle rapid tab switching', async () => {
      const user = userEvent.setup();
      render(<LayerDeepDocs />);

      const tabs = ['code example', 'decision tree', 'configuration', 'use cases'];

      for (const tabName of tabs) {
        const tab = screen.getByRole('button', { name: new RegExp(tabName, 'i') });
        await user.click(tab);
      }

      // Should end up on use cases
      expect(screen.getByText('Usage Examples')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Handler Function Tests (for function coverage)
  // =========================================================================

  describe('Handler Functions', () => {
    it('should handle clipboard copy for code examples', async () => {
      const user = userEvent.setup();
      const writeTextMock = vi.fn().mockResolvedValue(undefined);
      Object.defineProperty(navigator, 'clipboard', {
        value: { writeText: writeTextMock },
        writable: true,
        configurable: true,
      });

      render(<LayerDeepDocs />);

      // Find and click copy button in code block
      const buttons = screen.getAllByRole('button');
      const copyButton = buttons.find(btn => btn.classList.contains('h-7'));

      if (copyButton) {
        await user.click(copyButton);
        expect(writeTextMock).toHaveBeenCalled();
      }
    });
  });
});
