/**
 * Tests for RagDashboard main component
 * Issue #3005: Frontend coverage improvements
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

// Mock navigation components to avoid duplicate text issues
vi.mock('../DashboardSidebar', () => ({
  DashboardSidebar: () => <aside data-testid="dashboard-sidebar">Sidebar</aside>,
}));

vi.mock('../DashboardNav', () => ({
  DashboardNav: () => <nav data-testid="dashboard-nav">MobileNav</nav>,
}));

vi.mock('../ProgressIndicator', () => ({
  ProgressIndicator: () => <div data-testid="progress-indicator">Progress</div>,
}));

// Mock IntersectionObserver for useScrollSpy
class MockIntersectionObserver {
  constructor(callback: IntersectionObserverCallback) {
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
      expect(screen.getByText('RAG Strategy Dashboard')).toBeInTheDocument();
    });

    it('should render TOMAC-RAG subtitle', () => {
      render(<RagDashboard />);

      expect(screen.getByText(/Token-Optimized Modular Adaptive Corrective RAG/)).toBeInTheDocument();
    });

    it('should render view toggle buttons', () => {
      render(<RagDashboard />);

      expect(screen.getByText('Technical')).toBeInTheDocument();
      expect(screen.getByText('Business')).toBeInTheDocument();
    });

    it('should render Docs and Source buttons', () => {
      render(<RagDashboard />);

      expect(screen.getByText('Docs')).toBeInTheDocument();
      expect(screen.getByText('Source')).toBeInTheDocument();
    });

    it('should render footer', () => {
      render(<RagDashboard />);

      expect(screen.getByText(/MeepleAI RAG Dashboard/)).toBeInTheDocument();
      expect(screen.getByText('View on GitHub')).toBeInTheDocument();
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

    it('should render StatsGrid', () => {
      render(<RagDashboard />);

      expect(screen.getByTestId('stats-grid')).toBeInTheDocument();
    });

    it('should render QuerySimulator', () => {
      render(<RagDashboard />);

      expect(screen.getByTestId('query-simulator')).toBeInTheDocument();
    });

    it('should render TokenFlowVisualizer', () => {
      render(<RagDashboard />);

      expect(screen.getByTestId('token-flow')).toBeInTheDocument();
    });

    it('should render CostCalculator', () => {
      render(<RagDashboard />);

      expect(screen.getByTestId('cost-calculator')).toBeInTheDocument();
    });

    it('should render ArchitectureExplorer in technical view', () => {
      render(<RagDashboard />);

      expect(screen.getByTestId('architecture-explorer')).toBeInTheDocument();
    });

    it('should render LayerDeepDocs in technical view', () => {
      render(<RagDashboard />);

      expect(screen.getByTestId('layer-docs')).toBeInTheDocument();
    });

    it('should render DecisionWalkthrough', () => {
      render(<RagDashboard />);

      expect(screen.getByTestId('decision-walkthrough')).toBeInTheDocument();
    });

    it('should render AgentRagIntegration in technical view', () => {
      render(<RagDashboard />);

      expect(screen.getByTestId('agent-rag-integration')).toBeInTheDocument();
    });

    it('should render VariantComparisonTool in technical view', () => {
      render(<RagDashboard />);

      expect(screen.getByTestId('variant-comparison')).toBeInTheDocument();
    });

    it('should render PromptTemplateBuilder in technical view', () => {
      render(<RagDashboard />);

      expect(screen.getByTestId('prompt-builder')).toBeInTheDocument();
    });

    it('should render AgentRoleConfigurator in technical view', () => {
      render(<RagDashboard />);

      expect(screen.getByTestId('agent-role-config')).toBeInTheDocument();
    });

    it('should render ModelSelectionOptimizer in technical view', () => {
      render(<RagDashboard />);

      expect(screen.getByTestId('model-optimizer')).toBeInTheDocument();
    });

    it('should render PerformanceMetricsTable in technical view', () => {
      render(<RagDashboard />);

      expect(screen.getByTestId('performance-metrics')).toBeInTheDocument();
    });

    it('should NOT show quick links in technical view', () => {
      render(<RagDashboard />);

      expect(screen.queryByText('Executive Summary')).not.toBeInTheDocument();
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
    it('should show quick links in business view', async () => {
      const user = userEvent.setup();
      render(<RagDashboard />);

      const businessButton = screen.getByText('Business').closest('button');
      await user.click(businessButton!);

      await waitFor(() => {
        // Quick links that appear in business view
        const links = screen.getAllByText(/Executive Summary|Cost Projections|ROI Analysis|Implementation Timeline/);
        expect(links.length).toBeGreaterThan(0);
      });
    });

    it('should hide ArchitectureExplorer in business view', async () => {
      const user = userEvent.setup();
      render(<RagDashboard />);

      const businessButton = screen.getByText('Business').closest('button');
      await user.click(businessButton!);

      await waitFor(() => {
        expect(screen.queryByTestId('architecture-explorer')).not.toBeInTheDocument();
      });
    });

    it('should hide LayerDeepDocs in business view', async () => {
      const user = userEvent.setup();
      render(<RagDashboard />);

      const businessButton = screen.getByText('Business').closest('button');
      await user.click(businessButton!);

      await waitFor(() => {
        expect(screen.queryByTestId('layer-docs')).not.toBeInTheDocument();
      });
    });

    it('should hide AgentRagIntegration in business view', async () => {
      const user = userEvent.setup();
      render(<RagDashboard />);

      const businessButton = screen.getByText('Business').closest('button');
      await user.click(businessButton!);

      await waitFor(() => {
        expect(screen.queryByTestId('agent-rag-integration')).not.toBeInTheDocument();
      });
    });

    it('should hide VariantComparisonTool in business view', async () => {
      const user = userEvent.setup();
      render(<RagDashboard />);

      const businessButton = screen.getByText('Business').closest('button');
      await user.click(businessButton!);

      await waitFor(() => {
        expect(screen.queryByTestId('variant-comparison')).not.toBeInTheDocument();
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

    it('should show Executive Summary section in business view', async () => {
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
    it('should display System Overview section', () => {
      render(<RagDashboard />);

      expect(screen.getByText('System Overview')).toBeInTheDocument();
    });

    it('should display Query Simulator section', () => {
      render(<RagDashboard />);

      expect(screen.getByText('Query Simulator')).toBeInTheDocument();
    });

    it('should display Token Flow section', () => {
      render(<RagDashboard />);

      expect(screen.getByText('Token Flow')).toBeInTheDocument();
    });

    it('should display Cost Projection section', () => {
      render(<RagDashboard />);

      expect(screen.getByText('Cost Projection')).toBeInTheDocument();
    });

    it('should display Decision Walkthrough section', () => {
      render(<RagDashboard />);

      expect(screen.getByText('Decision Walkthrough')).toBeInTheDocument();
    });

    it('should display technical section titles in technical view', () => {
      render(<RagDashboard />);

      expect(screen.getByText('Architecture Explorer')).toBeInTheDocument();
      expect(screen.getByText('Layer Documentation')).toBeInTheDocument();
      expect(screen.getByText('Agent-RAG Integration')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Section Descriptions Tests
  // =========================================================================

  describe('Section Descriptions', () => {
    it('should show technical description for System Overview in technical view', () => {
      render(<RagDashboard />);

      expect(screen.getByText(/Key performance metrics for the TOMAC-RAG system/)).toBeInTheDocument();
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

      const docsLink = screen.getByText('Docs').closest('a');
      expect(docsLink).toHaveAttribute('href', '/docs/03-api/rag/README.md');
    });

    it('should have Source link with correct href', () => {
      render(<RagDashboard />);

      const sourceLink = screen.getByText('Source').closest('a');
      expect(sourceLink).toHaveAttribute('href', 'https://github.com/meepleai');
    });

    it('should have GitHub footer link with correct href', () => {
      render(<RagDashboard />);

      const githubLink = screen.getByText('View on GitHub');
      expect(githubLink).toHaveAttribute('href', 'https://github.com/meepleai');
    });
  });

  // =========================================================================
  // Responsive Layout Tests
  // =========================================================================

  describe('Layout', () => {
    it('should have main content container with max-width', () => {
      const { container } = render(<RagDashboard />);

      const main = container.querySelector('main');
      expect(main).toHaveClass('max-w-5xl');
    });

    it('should render sidebar navigation', () => {
      render(<RagDashboard />);

      expect(screen.getByTestId('dashboard-sidebar')).toBeInTheDocument();
    });

    it('should render mobile navigation', () => {
      render(<RagDashboard />);

      expect(screen.getByTestId('dashboard-nav')).toBeInTheDocument();
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
        expect(screen.getByText(/CRAG evaluation prevents hallucinations/)).toBeInTheDocument();
      });
    });

    it('should display Scalability highlights', async () => {
      const user = userEvent.setup();
      render(<RagDashboard />);

      const businessButton = screen.getByText('Business').closest('button');
      await user.click(businessButton!);

      await waitFor(() => {
        expect(screen.getByText(/31 configurable RAG variants/)).toBeInTheDocument();
        expect(screen.getByText(/User tier-based resource allocation/)).toBeInTheDocument();
      });
    });
  });
});
