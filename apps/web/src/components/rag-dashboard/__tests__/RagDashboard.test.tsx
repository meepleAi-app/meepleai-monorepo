/**
 * Tests for RagDashboard main component
 * Issue #3005: Frontend coverage improvements
 *
 * Updated for tabbed dashboard design (Issue #3547)
 */

import React from 'react';

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

// Mock framer-motion
vi.mock('framer-motion', () => ({
  motion: {
    header: ({ children, ...props }: React.PropsWithChildren<Record<string, unknown>>) => (
      <header {...props}>{children}</header>
    ),
    section: ({ children, ...props }: React.PropsWithChildren<Record<string, unknown>>) => (
      <section {...props}>{children}</section>
    ),
    div: ({ children, ...props }: React.PropsWithChildren<Record<string, unknown>>) => (
      <div {...props}>{children}</div>
    ),
  },
  AnimatePresence: ({ children }: React.PropsWithChildren) => <>{children}</>,
}));

// Mock child components to isolate RagDashboard testing
vi.mock('../StatsGrid', () => ({
  StatsGrid: () => <div data-testid="stats-grid">StatsGrid</div>,
}));

vi.mock('../QuerySimulator', () => ({
  QuerySimulator: () => <div data-testid="query-simulator">QuerySimulator</div>,
}));

vi.mock('../TokenFlowVisualizer', () => ({
  TokenFlowVisualizer: () => <div data-testid="token-flow">TokenFlowVisualizer</div>,
}));

vi.mock('../CostCalculator', () => ({
  CostCalculator: () => <div data-testid="cost-calculator">CostCalculator</div>,
}));

vi.mock('../ArchitectureExplorer', () => ({
  ArchitectureExplorer: () => <div data-testid="architecture-explorer">ArchitectureExplorer</div>,
}));

vi.mock('../LayerDeepDocs', () => ({
  LayerDeepDocs: () => <div data-testid="layer-docs">LayerDeepDocs</div>,
}));

vi.mock('../DecisionWalkthrough', () => ({
  DecisionWalkthrough: () => <div data-testid="decision-walkthrough">DecisionWalkthrough</div>,
}));

vi.mock('../AgentRagIntegration', () => ({
  AgentRagIntegration: () => <div data-testid="agent-rag-integration">AgentRagIntegration</div>,
}));

vi.mock('../VariantComparisonTool', () => ({
  VariantComparisonTool: () => <div data-testid="variant-comparison">VariantComparisonTool</div>,
}));

vi.mock('../PromptTemplateBuilder', () => ({
  PromptTemplateBuilder: () => <div data-testid="prompt-builder">PromptTemplateBuilder</div>,
}));

vi.mock('../AgentRoleConfigurator', () => ({
  AgentRoleConfigurator: () => <div data-testid="agent-role-config">AgentRoleConfigurator</div>,
}));

vi.mock('../ModelSelectionOptimizer', () => ({
  ModelSelectionOptimizer: () => <div data-testid="model-optimizer">ModelSelectionOptimizer</div>,
}));

vi.mock('../PerformanceMetricsTable', () => ({
  PerformanceMetricsTable: () => <div data-testid="performance-metrics">PerformanceMetricsTable</div>,
}));

vi.mock('../PocStatus', () => ({
  PocStatus: () => <div data-testid="poc-status">PocStatus</div>,
}));

vi.mock('../TechnicalReference', () => ({
  TechnicalReference: () => <div data-testid="technical-reference">TechnicalReference</div>,
}));

vi.mock('../ParameterGuide', () => ({
  ParameterGuide: () => <div data-testid="parameter-guide">ParameterGuide</div>,
}));

// Mock IntersectionObserver for useScrollSpy
class MockIntersectionObserver {
  constructor(_callback: IntersectionObserverCallback) {
    // no-op
  }
  observe() {}
  unobserve() {}
  disconnect() {}
  takeRecords() {
    return [];
  }
}
vi.stubGlobal('IntersectionObserver', MockIntersectionObserver);

// Import after mocking
import { RagDashboard } from '../RagDashboard';

describe('RagDashboard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // =========================================================================
  // Rendering Tests
  // =========================================================================

  describe('Rendering', () => {
    it('should render the main dashboard container', () => {
      const { container } = render(<RagDashboard />);

      expect(container.querySelector('.rag-dashboard')).toBeInTheDocument();
    });

    it('should render the dashboard header with title', () => {
      render(<RagDashboard />);

      expect(screen.getByText('MeepleAI')).toBeInTheDocument();
      expect(screen.getByText('RAG Dashboard')).toBeInTheDocument();
    });

    it('should render TOMAC-RAG subtitle', () => {
      render(<RagDashboard />);

      expect(screen.getByText('TOMAC-RAG System')).toBeInTheDocument();
    });

    it('should render view toggle buttons', () => {
      render(<RagDashboard />);

      expect(screen.getByText('Technical')).toBeInTheDocument();
      expect(screen.getByText('Business')).toBeInTheDocument();
    });

    it('should render header action buttons', () => {
      render(<RagDashboard />);

      // Docs and Source buttons are icon-only in the new design
      // Find them by their link hrefs
      const docsLink = document.querySelector('a[href="/docs/03-api/rag/README.md"]');
      const sourceLink = document.querySelector('a[href="https://github.com/meepleai"]');
      expect(docsLink).toBeInTheDocument();
      expect(sourceLink).toBeInTheDocument();
    });

    it('should render footer', () => {
      render(<RagDashboard />);

      expect(screen.getByText(/MeepleAI RAG Dashboard/)).toBeInTheDocument();
      expect(screen.getByText('GitHub')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Default State Tests (Technical View)
  // =========================================================================

  describe('Default State (Technical View)', () => {
    it('should have Technical view selected by default', () => {
      render(<RagDashboard />);

      const technicalButton = screen.getByText('Technical').closest('button');
      expect(technicalButton).toHaveAttribute('data-active', 'true');
    });

    it('should show Overview tab content by default', () => {
      render(<RagDashboard />);

      // Overview tab should show POC Status, Stats, Query Simulator, Token Flow
      expect(screen.getByTestId('poc-status')).toBeInTheDocument();
      expect(screen.getByTestId('stats-grid')).toBeInTheDocument();
      expect(screen.getByTestId('query-simulator')).toBeInTheDocument();
      expect(screen.getByTestId('token-flow')).toBeInTheDocument();
    });

    it('should display tab navigation in sidebar', () => {
      render(<RagDashboard />);

      // Tab labels appear in both desktop sidebar and mobile tab bar
      expect(screen.getAllByText('Overview').length).toBeGreaterThanOrEqual(1);
      expect(screen.getAllByText('Architecture').length).toBeGreaterThanOrEqual(1);
      expect(screen.getAllByText('Agents & Prompts').length).toBeGreaterThanOrEqual(1);
      expect(screen.getAllByText('Cost & Metrics').length).toBeGreaterThanOrEqual(1);
      expect(screen.getAllByText('Walkthrough').length).toBeGreaterThanOrEqual(1);
    });

    it('should NOT show architecture-specific components in Overview tab', () => {
      render(<RagDashboard />);

      // Architecture tab content should not be visible
      expect(screen.queryByTestId('architecture-explorer')).not.toBeInTheDocument();
      expect(screen.queryByTestId('layer-docs')).not.toBeInTheDocument();
    });

    it('should NOT show quick links in technical view', () => {
      render(<RagDashboard />);

      expect(screen.queryByText('Executive Summary')).not.toBeInTheDocument();
    });
  });

  // =========================================================================
  // Tab Navigation Tests
  // =========================================================================

  describe('Tab Navigation', () => {
    it('should switch to Architecture tab when clicked', async () => {
      const user = userEvent.setup();
      render(<RagDashboard />);

      // Click on Architecture tab (sidebar has two instances of each tab - desktop and mobile)
      const architectureButtons = screen.getAllByText('Architecture');
      await user.click(architectureButtons[0]);

      // Architecture components should now be visible
      await waitFor(() => {
        expect(screen.getByTestId('architecture-explorer')).toBeInTheDocument();
        expect(screen.getByTestId('layer-docs')).toBeInTheDocument();
        expect(screen.getByTestId('technical-reference')).toBeInTheDocument();
      });
    });

    it('should switch to Agents tab when clicked', async () => {
      const user = userEvent.setup();
      render(<RagDashboard />);

      const agentsButtons = screen.getAllByText('Agents & Prompts');
      await user.click(agentsButtons[0]);

      await waitFor(() => {
        expect(screen.getByTestId('agent-rag-integration')).toBeInTheDocument();
        expect(screen.getByTestId('agent-role-config')).toBeInTheDocument();
        expect(screen.getByTestId('prompt-builder')).toBeInTheDocument();
      });
    });

    it('should switch to Cost & Metrics tab when clicked', async () => {
      const user = userEvent.setup();
      render(<RagDashboard />);

      const metricsButtons = screen.getAllByText('Cost & Metrics');
      await user.click(metricsButtons[0]);

      await waitFor(() => {
        expect(screen.getByTestId('parameter-guide')).toBeInTheDocument();
        expect(screen.getByTestId('cost-calculator')).toBeInTheDocument();
        expect(screen.getByTestId('model-optimizer')).toBeInTheDocument();
        expect(screen.getByTestId('performance-metrics')).toBeInTheDocument();
        expect(screen.getByTestId('variant-comparison')).toBeInTheDocument();
      });
    });

    it('should switch to Walkthrough tab when clicked', async () => {
      const user = userEvent.setup();
      render(<RagDashboard />);

      const walkthroughButtons = screen.getAllByText('Walkthrough');
      await user.click(walkthroughButtons[0]);

      await waitFor(() => {
        expect(screen.getByTestId('decision-walkthrough')).toBeInTheDocument();
      });
    });

    it('should switch back to Overview tab when clicked', async () => {
      const user = userEvent.setup();
      render(<RagDashboard />);

      // Switch to Architecture first
      const architectureButtons = screen.getAllByText('Architecture');
      await user.click(architectureButtons[0]);

      // Switch back to Overview
      const overviewButtons = screen.getAllByText('Overview');
      await user.click(overviewButtons[0]);

      await waitFor(() => {
        expect(screen.getByTestId('poc-status')).toBeInTheDocument();
        expect(screen.getByTestId('stats-grid')).toBeInTheDocument();
      });
    });
  });

  // =========================================================================
  // View Mode Switching Tests
  // =========================================================================

  describe('View Mode Switching', () => {
    it('should switch to Business view when clicked', async () => {
      const user = userEvent.setup();
      render(<RagDashboard />);

      const businessButton = screen.getByText('Business').closest('button');
      await user.click(businessButton!);

      expect(businessButton).toHaveAttribute('data-active', 'true');
    });

    it('should switch back to Technical view when clicked', async () => {
      const user = userEvent.setup();
      render(<RagDashboard />);

      // Switch to business
      const businessButton = screen.getByText('Business').closest('button');
      await user.click(businessButton!);

      // Switch back to technical
      const technicalButton = screen.getByText('Technical').closest('button');
      await user.click(technicalButton!);

      expect(technicalButton).toHaveAttribute('data-active', 'true');
    });
  });

  // =========================================================================
  // Business View Tests
  // =========================================================================

  describe('Business View', () => {
    it('should show Executive Summary in business view', async () => {
      const user = userEvent.setup();
      render(<RagDashboard />);

      const businessButton = screen.getByText('Business').closest('button');
      await user.click(businessButton!);

      await waitFor(() => {
        expect(screen.getByText('Executive Summary')).toBeInTheDocument();
      });
    });

    it('should hide tab navigation in business view', async () => {
      const user = userEvent.setup();
      render(<RagDashboard />);

      const businessButton = screen.getByText('Business').closest('button');
      await user.click(businessButton!);

      await waitFor(() => {
        // Tab-specific navigation should not be visible
        expect(screen.queryByText('Agents & Prompts')).not.toBeInTheDocument();
      });
    });

    it('should show StatsGrid in business view', async () => {
      const user = userEvent.setup();
      render(<RagDashboard />);

      const businessButton = screen.getByText('Business').closest('button');
      await user.click(businessButton!);

      expect(screen.getByTestId('stats-grid')).toBeInTheDocument();
    });

    it('should show CostCalculator in business view', async () => {
      const user = userEvent.setup();
      render(<RagDashboard />);

      const businessButton = screen.getByText('Business').closest('button');
      await user.click(businessButton!);

      expect(screen.getByTestId('cost-calculator')).toBeInTheDocument();
    });

    it('should show DecisionWalkthrough in business view', async () => {
      const user = userEvent.setup();
      render(<RagDashboard />);

      const businessButton = screen.getByText('Business').closest('button');
      await user.click(businessButton!);

      expect(screen.getByTestId('decision-walkthrough')).toBeInTheDocument();
    });

    it('should show summary highlights in business view', async () => {
      const user = userEvent.setup();
      render(<RagDashboard />);

      const businessButton = screen.getByText('Business').closest('button');
      await user.click(businessButton!);

      await waitFor(() => {
        expect(screen.getByText('Cost Efficiency')).toBeInTheDocument();
        expect(screen.getByText('Quality Assurance')).toBeInTheDocument();
        expect(screen.getByText('Scalability')).toBeInTheDocument();
      });
    });
  });

  // =========================================================================
  // Section Titles Tests
  // =========================================================================

  describe('Section Titles', () => {
    it('should display POC Status section in Overview tab', () => {
      render(<RagDashboard />);

      expect(screen.getByText('POC Status')).toBeInTheDocument();
    });

    it('should display System Overview section in Overview tab', () => {
      render(<RagDashboard />);

      expect(screen.getByText('System Overview')).toBeInTheDocument();
    });

    it('should display Query Simulator section in Overview tab', () => {
      render(<RagDashboard />);

      expect(screen.getByText('Query Simulator')).toBeInTheDocument();
    });

    it('should display Token Flow section in Overview tab', () => {
      render(<RagDashboard />);

      expect(screen.getByText('Token Flow')).toBeInTheDocument();
    });

    it('should display Architecture Explorer section in Architecture tab', async () => {
      const user = userEvent.setup();
      render(<RagDashboard />);

      const architectureButtons = screen.getAllByText('Architecture');
      await user.click(architectureButtons[0]);

      await waitFor(() => {
        expect(screen.getByText('Architecture Explorer')).toBeInTheDocument();
      });
    });

    it('should display Layer Documentation section in Architecture tab', async () => {
      const user = userEvent.setup();
      render(<RagDashboard />);

      const architectureButtons = screen.getAllByText('Architecture');
      await user.click(architectureButtons[0]);

      await waitFor(() => {
        expect(screen.getByText('Layer Documentation')).toBeInTheDocument();
      });
    });

    it('should display Agent-RAG Integration section in Agents tab', async () => {
      const user = userEvent.setup();
      render(<RagDashboard />);

      const agentsButtons = screen.getAllByText('Agents & Prompts');
      await user.click(agentsButtons[0]);

      await waitFor(() => {
        expect(screen.getByText('Agent-RAG Integration')).toBeInTheDocument();
      });
    });

    it('should display Cost Projection section in Cost & Metrics tab', async () => {
      const user = userEvent.setup();
      render(<RagDashboard />);

      const metricsButtons = screen.getAllByText('Cost & Metrics');
      await user.click(metricsButtons[0]);

      await waitFor(() => {
        expect(screen.getByText('Cost Projection')).toBeInTheDocument();
      });
    });

    it('should display Decision Walkthrough section in Walkthrough tab', async () => {
      const user = userEvent.setup();
      render(<RagDashboard />);

      const walkthroughButtons = screen.getAllByText('Walkthrough');
      await user.click(walkthroughButtons[0]);

      await waitFor(() => {
        expect(screen.getByText('Decision Walkthrough')).toBeInTheDocument();
      });
    });
  });

  // =========================================================================
  // Section Descriptions Tests
  // =========================================================================

  describe('Section Descriptions', () => {
    it('should show description for System Overview in technical view', () => {
      render(<RagDashboard />);

      expect(screen.getByText('Key performance metrics')).toBeInTheDocument();
    });

    it('should show business description for System Overview in business view', async () => {
      const user = userEvent.setup();
      render(<RagDashboard />);

      const businessButton = screen.getByText('Business').closest('button');
      await user.click(businessButton!);

      await waitFor(() => {
        expect(screen.getByText(/Key business metrics and cost efficiency indicators/)).toBeInTheDocument();
      });
    });
  });

  // =========================================================================
  // Navigation Links Tests
  // =========================================================================

  describe('Navigation Links', () => {
    it('should have Docs link with correct href', () => {
      render(<RagDashboard />);

      const docsLink = document.querySelector('a[href="/docs/03-api/rag/README.md"]');
      expect(docsLink).toBeInTheDocument();
    });

    it('should have Source link with correct href', () => {
      render(<RagDashboard />);

      // First GitHub link is in the header
      const headerSourceLink = document.querySelector('header a[href="https://github.com/meepleai"]');
      expect(headerSourceLink).toBeInTheDocument();
    });

    it('should have GitHub footer link with correct href', () => {
      render(<RagDashboard />);

      const githubLink = screen.getByText('GitHub');
      expect(githubLink).toHaveAttribute('href', 'https://github.com/meepleai');
    });
  });

  // =========================================================================
  // Layout Tests
  // =========================================================================

  describe('Layout', () => {
    it('should have main content container with max-width', () => {
      const { container } = render(<RagDashboard />);

      const main = container.querySelector('main');
      expect(main).toHaveClass('max-w-7xl');
    });

    it('should render tab navigation sidebar', () => {
      const { container } = render(<RagDashboard />);

      // Sidebar navigation is an internal nav element
      const sidebar = container.querySelector('nav');
      expect(sidebar).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Business Summary Cards Tests
  // =========================================================================

  describe('Business Summary Cards', () => {
    it('should display Cost Efficiency highlights', async () => {
      const user = userEvent.setup();
      render(<RagDashboard />);

      const businessButton = screen.getByText('Business').closest('button');
      await user.click(businessButton!);

      await waitFor(() => {
        expect(screen.getByText(/35% token reduction/)).toBeInTheDocument();
        expect(screen.getByText(/80% cache hit rate/)).toBeInTheDocument();
      });
    });

    it('should display Quality Assurance highlights', async () => {
      const user = userEvent.setup();
      render(<RagDashboard />);

      const businessButton = screen.getByText('Business').closest('button');
      await user.click(businessButton!);

      await waitFor(() => {
        expect(screen.getByText(/95% accuracy target/)).toBeInTheDocument();
        expect(screen.getByText(/CRAG prevents hallucinations/)).toBeInTheDocument();
      });
    });

    it('should display Scalability highlights', async () => {
      const user = userEvent.setup();
      render(<RagDashboard />);

      const businessButton = screen.getByText('Business').closest('button');
      await user.click(businessButton!);

      await waitFor(() => {
        expect(screen.getByText(/31 configurable variants/)).toBeInTheDocument();
        expect(screen.getByText(/Tier-based allocation/)).toBeInTheDocument();
      });
    });
  });
});
