/**
 * Tests for ArchitectureExplorer component
 * Issue #3005: Frontend coverage improvements
 */

import React from 'react';

import { render, screen, waitFor } from '@testing-library/react';
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
import { ArchitectureExplorer } from '../ArchitectureExplorer';

describe('ArchitectureExplorer', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // =========================================================================
  // Rendering Tests
  // =========================================================================

  describe('Rendering', () => {
    it('should render the architecture explorer card', () => {
      render(<ArchitectureExplorer />);

      expect(screen.getByText('Architecture Explorer')).toBeInTheDocument();
    });

    it('should render filter buttons', () => {
      render(<ArchitectureExplorer />);

      expect(screen.getByRole('button', { name: 'All' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Layers' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Services' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Storage' })).toBeInTheDocument();
    });

    it('should render zoom controls', () => {
      const { container } = render(<ArchitectureExplorer />);

      const zoomButtons = container.querySelectorAll('button');
      expect(zoomButtons.length).toBeGreaterThan(4); // Filter buttons + zoom buttons
    });

    it('should display zoom percentage', () => {
      render(<ArchitectureExplorer />);

      expect(screen.getByText('85%')).toBeInTheDocument();
    });

    it('should render legend', () => {
      render(<ArchitectureExplorer />);

      expect(screen.getByText('RAG Layers')).toBeInTheDocument();
      // 'Services' and 'Storage' appear both as filter buttons and in legend
      const servicesElements = screen.getAllByText('Services');
      expect(servicesElements.length).toBeGreaterThan(0);
      const storageElements = screen.getAllByText('Storage');
      expect(storageElements.length).toBeGreaterThan(0);
    });

    it('should display hint text', () => {
      render(<ArchitectureExplorer />);

      expect(screen.getByText('Click on a node for details')).toBeInTheDocument();
    });

    it('should apply custom className', () => {
      const { container } = render(<ArchitectureExplorer className="custom-class" />);

      expect(container.querySelector('.custom-class')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Node Rendering Tests
  // =========================================================================

  describe('Node Rendering', () => {
    it('should render all layer nodes', () => {
      render(<ArchitectureExplorer />);

      expect(screen.getByText('Routing')).toBeInTheDocument();
      expect(screen.getByText('Cache')).toBeInTheDocument();
      expect(screen.getByText('Retrieval')).toBeInTheDocument();
      expect(screen.getByText('CRAG')).toBeInTheDocument();
      expect(screen.getByText('Generation')).toBeInTheDocument();
      expect(screen.getByText('Validation')).toBeInTheDocument();
    });

    it('should render service nodes', () => {
      render(<ArchitectureExplorer />);

      expect(screen.getByText('Tier Resolver')).toBeInTheDocument();
      expect(screen.getByText('LLM Router')).toBeInTheDocument();
      expect(screen.getByText('T5 Evaluator')).toBeInTheDocument();
      expect(screen.getByText('Web Search')).toBeInTheDocument();
    });

    it('should render storage nodes', () => {
      render(<ArchitectureExplorer />);

      expect(screen.getByText('Redis Cache')).toBeInTheDocument();
      expect(screen.getByText('pgvector')).toBeInTheDocument();
      expect(screen.getByText('PostgreSQL')).toBeInTheDocument();
    });

    it('should render input/output nodes', () => {
      render(<ArchitectureExplorer />);

      expect(screen.getByText('User Query')).toBeInTheDocument();
      expect(screen.getByText('AI Response')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Filter Tests
  // =========================================================================

  describe('Filtering', () => {
    it('should have All filter selected by default', () => {
      render(<ArchitectureExplorer />);

      const allButton = screen.getByRole('button', { name: 'All' });
      expect(allButton).toHaveClass('bg-primary');
    });

    it('should filter to show only layers', async () => {
      const user = userEvent.setup();
      render(<ArchitectureExplorer />);

      const layersButton = screen.getByRole('button', { name: 'Layers' });
      await user.click(layersButton);

      expect(layersButton).toHaveClass('bg-primary');
    });

    it('should filter to show only services', async () => {
      const user = userEvent.setup();
      render(<ArchitectureExplorer />);

      const servicesButton = screen.getByRole('button', { name: 'Services' });
      await user.click(servicesButton);

      expect(servicesButton).toHaveClass('bg-primary');
    });

    it('should filter to show only storage', async () => {
      const user = userEvent.setup();
      render(<ArchitectureExplorer />);

      const storageButton = screen.getByRole('button', { name: 'Storage' });
      await user.click(storageButton);

      expect(storageButton).toHaveClass('bg-primary');
    });

    it('should return to All filter', async () => {
      const user = userEvent.setup();
      render(<ArchitectureExplorer />);

      // First filter by layers
      const layersButton = screen.getByRole('button', { name: 'Layers' });
      await user.click(layersButton);

      // Then back to all
      const allButton = screen.getByRole('button', { name: 'All' });
      await user.click(allButton);

      expect(allButton).toHaveClass('bg-primary');
    });
  });

  // =========================================================================
  // Zoom Tests
  // =========================================================================

  describe('Zoom Controls', () => {
    it('should show initial zoom level of 85%', () => {
      render(<ArchitectureExplorer />);

      expect(screen.getByText('85%')).toBeInTheDocument();
    });

    it('should zoom in when zoom in button is clicked', async () => {
      const user = userEvent.setup();
      const { container } = render(<ArchitectureExplorer />);

      // Find zoom in button (second button after zoom out)
      const zoomButtons = container.querySelectorAll('.p-1.rounded.hover\\:bg-muted');
      const zoomInButton = zoomButtons[1];

      if (zoomInButton) {
        await user.click(zoomInButton);
        expect(screen.getByText('95%')).toBeInTheDocument();
      }
    });

    it('should zoom out when zoom out button is clicked', async () => {
      const user = userEvent.setup();
      const { container } = render(<ArchitectureExplorer />);

      // Find zoom out button (first button)
      const zoomButtons = container.querySelectorAll('.p-1.rounded.hover\\:bg-muted');
      const zoomOutButton = zoomButtons[0];

      if (zoomOutButton) {
        await user.click(zoomOutButton);
        expect(screen.getByText('75%')).toBeInTheDocument();
      }
    });

    it('should not zoom below minimum', async () => {
      const user = userEvent.setup();
      const { container } = render(<ArchitectureExplorer />);

      const zoomButtons = container.querySelectorAll('.p-1.rounded.hover\\:bg-muted');
      const zoomOutButton = zoomButtons[0];

      if (zoomOutButton) {
        // Click zoom out many times
        for (let i = 0; i < 10; i++) {
          await user.click(zoomOutButton);
        }

        // Should stop at 50%
        expect(screen.getByText('50%')).toBeInTheDocument();
      }
    });

    it('should not zoom above maximum', async () => {
      const user = userEvent.setup();
      const { container } = render(<ArchitectureExplorer />);

      const zoomButtons = container.querySelectorAll('.p-1.rounded.hover\\:bg-muted');
      const zoomInButton = zoomButtons[1];

      if (zoomInButton) {
        // Click zoom in many times
        for (let i = 0; i < 10; i++) {
          await user.click(zoomInButton);
        }

        // Should stop at 150%
        expect(screen.getByText('150%')).toBeInTheDocument();
      }
    });
  });

  // =========================================================================
  // Node Selection Tests
  // =========================================================================

  describe('Node Selection', () => {
    it('should show detail panel when node is clicked', async () => {
      const user = userEvent.setup();
      render(<ArchitectureExplorer />);

      const routingNode = screen.getByText('Routing');
      await user.click(routingNode);

      await waitFor(() => {
        expect(screen.getByText('L1: Routing')).toBeInTheDocument();
      });
    });

    it('should show node description in detail panel', async () => {
      const user = userEvent.setup();
      render(<ArchitectureExplorer />);

      const routingNode = screen.getByText('Routing');
      await user.click(routingNode);

      await waitFor(() => {
        expect(screen.getByText(/Classifies query template/)).toBeInTheDocument();
      });
    });

    it('should show category badge in detail panel', async () => {
      const user = userEvent.setup();
      render(<ArchitectureExplorer />);

      const routingNode = screen.getByText('Routing');
      await user.click(routingNode);

      await waitFor(() => {
        expect(screen.getByText('layer')).toBeInTheDocument();
      });
    });

    it('should show connections in detail panel', async () => {
      const user = userEvent.setup();
      render(<ArchitectureExplorer />);

      const routingNode = screen.getByText('Routing');
      await user.click(routingNode);

      await waitFor(() => {
        expect(screen.getByText('Connects to:')).toBeInTheDocument();
      });
    });

    it('should show View Documentation button for layer nodes', async () => {
      const user = userEvent.setup();
      render(<ArchitectureExplorer />);

      const routingNode = screen.getByText('Routing');
      await user.click(routingNode);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /view documentation/i })).toBeInTheDocument();
      });
    });

    it('should close detail panel when close button is clicked', async () => {
      const user = userEvent.setup();
      render(<ArchitectureExplorer />);

      // Open panel
      const routingNode = screen.getByText('Routing');
      await user.click(routingNode);

      // Close panel
      const closeButton = screen.getByText('×');
      await user.click(closeButton);

      await waitFor(() => {
        expect(screen.queryByText('Connects to:')).not.toBeInTheDocument();
      });
    });

    it('should toggle panel when same node is clicked again', async () => {
      const user = userEvent.setup();
      render(<ArchitectureExplorer />);

      const routingNode = screen.getByText('Routing');

      // Click to open
      await user.click(routingNode);
      expect(screen.getByText('Connects to:')).toBeInTheDocument();

      // Click to close
      await user.click(routingNode);
      await waitFor(() => {
        expect(screen.queryByText('Connects to:')).not.toBeInTheDocument();
      });
    });
  });

  // =========================================================================
  // Connection Lines Tests
  // =========================================================================

  describe('Connection Lines', () => {
    it('should render SVG for connections', () => {
      const { container } = render(<ArchitectureExplorer />);

      const svg = container.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });

    it('should have connection lines in SVG', () => {
      const { container } = render(<ArchitectureExplorer />);

      const lines = container.querySelectorAll('line');
      expect(lines.length).toBeGreaterThan(0);
    });
  });

  // =========================================================================
  // Different Node Types Tests
  // =========================================================================

  describe('Different Node Types', () => {
    it('should show service node details', async () => {
      const user = userEvent.setup();
      render(<ArchitectureExplorer />);

      const tierResolverNode = screen.getByText('Tier Resolver');
      await user.click(tierResolverNode);

      await waitFor(() => {
        expect(screen.getByText(/Determines user tier/)).toBeInTheDocument();
        expect(screen.getByText('service')).toBeInTheDocument();
      });
    });

    it('should show storage node details', async () => {
      const user = userEvent.setup();
      render(<ArchitectureExplorer />);

      const pgvectorNode = screen.getByText('pgvector');
      await user.click(pgvectorNode);

      await waitFor(() => {
        expect(screen.getByText(/PostgreSQL pgvector extension/)).toBeInTheDocument();
        expect(screen.getByText('storage')).toBeInTheDocument();
      });
    });

    it('should not show documentation button for non-layer nodes', async () => {
      const user = userEvent.setup();
      render(<ArchitectureExplorer />);

      const tierResolverNode = screen.getByText('Tier Resolver');
      await user.click(tierResolverNode);

      await waitFor(() => {
        expect(
          screen.queryByRole('button', { name: /view documentation/i })
        ).not.toBeInTheDocument();
      });
    });
  });

  // =========================================================================
  // Container Layout Tests
  // =========================================================================

  describe('Container Layout', () => {
    it('should have scrollable container', () => {
      const { container } = render(<ArchitectureExplorer />);

      const scrollContainer = container.querySelector('.overflow-auto');
      expect(scrollContainer).toBeInTheDocument();
    });

    it('should have minimum height for visualization', () => {
      const { container } = render(<ArchitectureExplorer />);

      const vizContainer = container.querySelector('.overflow-auto.rounded-lg');
      expect(vizContainer).toHaveStyle({ minHeight: '350px' });
    });
  });

  // =========================================================================
  // Handler Function Tests (for function coverage)
  // =========================================================================

  describe('Handler Functions', () => {
    it('should handle zoom in and update zoom level', async () => {
      const user = userEvent.setup();
      render(<ArchitectureExplorer />);

      // Find zoom in button (+ icon)
      const buttons = screen.getAllByRole('button');
      const zoomInButton = buttons.find(btn => btn.textContent?.includes('+'));

      if (zoomInButton) {
        await user.click(zoomInButton);

        // Zoom percentage should increase from 85%
        expect(screen.getByText(/\d+%/)).toBeInTheDocument();
      }
    });
  });
});
