/**
 * Tests for RagDashboard main component
 * Issue #3005: Frontend coverage improvements
 *
 * Updated for tabbed dashboard design (Issue #3547)
 * Updated for URL-based navigation state: component uses useSearchParams + useRouter
 */

import React from 'react';

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { Mock } from 'vitest';

import { useRouter, useSearchParams, usePathname } from 'next/navigation';

// =========================================================================
// Next.js Navigation Mock (required: RagDashboard uses useSearchParams + useRouter)
// =========================================================================

vi.mock('next/navigation', () => ({
  useRouter: vi.fn(),
  useSearchParams: vi.fn(),
  usePathname: vi.fn(),
}));

const mockUseRouter = useRouter as Mock;
const mockUseSearchParams = useSearchParams as Mock;
const mockUsePathname = usePathname as Mock;

function setupSearchParams(params: Record<string, string> = {}) {
  const urlSearchParams = new URLSearchParams(params);
  mockUseSearchParams.mockReturnValue({
    get: (key: string) => params[key] ?? null,
    toString: () => urlSearchParams.toString(),
  });
}

// =========================================================================
// Framer Motion Mock
// =========================================================================

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

// =========================================================================
// Technical Child Component Mocks
// =========================================================================

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
  PerformanceMetricsTable: () => (
    <div data-testid="performance-metrics">PerformanceMetricsTable</div>
  ),
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

// =========================================================================
// Business Child Component Mocks
// =========================================================================

vi.mock('../BusinessKpiCards', () => ({
  BusinessKpiCards: () => <div data-testid="business-kpi-cards">BusinessKpiCards</div>,
}));

vi.mock('../BusinessStrategyComparison', () => ({
  BusinessStrategyComparison: () => (
    <div data-testid="business-strategy-comparison">BusinessStrategyComparison</div>
  ),
}));

vi.mock('../BusinessRoiCalculator', () => ({
  BusinessRoiCalculator: () => (
    <div data-testid="business-roi-calculator">BusinessRoiCalculator</div>
  ),
}));

vi.mock('../BusinessUseCases', () => ({
  BusinessUseCases: () => <div data-testid="business-use-cases">BusinessUseCases</div>,
}));

// =========================================================================
// IntersectionObserver Mock (for useScrollSpy)
// =========================================================================

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
    // Default state: technical view, overview tab
    setupSearchParams({});
    mockUseRouter.mockReturnValue({
      push: vi.fn(),
      replace: vi.fn(),
      prefetch: vi.fn(),
    });
    mockUsePathname.mockReturnValue('/rag');
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

      expect(screen.getAllByText(/MeepleAI/).length).toBeGreaterThanOrEqual(1);
      expect(screen.getAllByText('RAG').length).toBeGreaterThanOrEqual(1);
    });

    it('should render TOMAC-RAG subtitle', () => {
      render(<RagDashboard />);

      expect(screen.getAllByText(/TOMAC-RAG/).length).toBeGreaterThanOrEqual(1);
    });

    it('should render view toggle buttons', () => {
      render(<RagDashboard />);

      expect(screen.getByText('Technical')).toBeInTheDocument();
      expect(screen.getByText('Business ROI')).toBeInTheDocument();
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

    it('should NOT show Executive Summary in technical view', () => {
      render(<RagDashboard />);

      expect(screen.queryByText('Executive Summary')).not.toBeInTheDocument();
    });
  });

  // =========================================================================
  // Tab Navigation Tests
  // Component uses URL state: click triggers router.replace, then we simulate
  // the URL update with setupSearchParams + rerender.
  // =========================================================================

  describe('Tab Navigation', () => {
    it('should switch to Architecture tab when clicked', () => {
      const { rerender } = render(<RagDashboard />);

      const architectureButtons = screen.getAllByText('Architecture');
      fireEvent.click(architectureButtons[0]);
      setupSearchParams({ tab: 'architecture' });
      rerender(<RagDashboard />);

      expect(screen.getByTestId('architecture-explorer')).toBeInTheDocument();
      expect(screen.getByTestId('layer-docs')).toBeInTheDocument();
      expect(screen.getByTestId('technical-reference')).toBeInTheDocument();
    });

    it('should switch to Agents tab when clicked', () => {
      const { rerender } = render(<RagDashboard />);

      const agentsButtons = screen.getAllByText('Agents & Prompts');
      fireEvent.click(agentsButtons[0]);
      setupSearchParams({ tab: 'agents' });
      rerender(<RagDashboard />);

      expect(screen.getByTestId('agent-rag-integration')).toBeInTheDocument();
      expect(screen.getByTestId('agent-role-config')).toBeInTheDocument();
      expect(screen.getByTestId('prompt-builder')).toBeInTheDocument();
    });

    it('should switch to Cost & Metrics tab when clicked', () => {
      const { rerender } = render(<RagDashboard />);

      const metricsButtons = screen.getAllByText('Cost & Metrics');
      fireEvent.click(metricsButtons[0]);
      setupSearchParams({ tab: 'performance' });
      rerender(<RagDashboard />);

      expect(screen.getByTestId('parameter-guide')).toBeInTheDocument();
      expect(screen.getByTestId('cost-calculator')).toBeInTheDocument();
      expect(screen.getByTestId('model-optimizer')).toBeInTheDocument();
      expect(screen.getByTestId('performance-metrics')).toBeInTheDocument();
      expect(screen.getByTestId('variant-comparison')).toBeInTheDocument();
    });

    it('should switch to Walkthrough tab when clicked', () => {
      const { rerender } = render(<RagDashboard />);

      const walkthroughButtons = screen.getAllByText('Walkthrough');
      fireEvent.click(walkthroughButtons[0]);
      setupSearchParams({ tab: 'walkthrough' });
      rerender(<RagDashboard />);

      expect(screen.getByTestId('decision-walkthrough')).toBeInTheDocument();
    });

    it('should switch back to Overview tab when clicked', () => {
      const { rerender } = render(<RagDashboard />);

      // Switch to Architecture first
      const architectureButtons = screen.getAllByText('Architecture');
      fireEvent.click(architectureButtons[0]);
      setupSearchParams({ tab: 'architecture' });
      rerender(<RagDashboard />);

      // Switch back to Overview
      const overviewButtons = screen.getAllByText('Overview');
      fireEvent.click(overviewButtons[0]);
      setupSearchParams({});
      rerender(<RagDashboard />);

      expect(screen.getByTestId('poc-status')).toBeInTheDocument();
      expect(screen.getByTestId('stats-grid')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // View Mode Switching Tests
  // =========================================================================

  describe('View Mode Switching', () => {
    it('should switch to Business view when clicked', () => {
      const { rerender } = render(<RagDashboard />);

      const businessButton = screen.getByText('Business ROI').closest('button');
      fireEvent.click(businessButton!);
      setupSearchParams({ view: 'business' });
      rerender(<RagDashboard />);

      const businessButtonUpdated = screen.getByText('Business ROI').closest('button');
      expect(businessButtonUpdated).toHaveAttribute('data-active', 'true');
    });

    it('should switch back to Technical view when clicked', () => {
      setupSearchParams({ view: 'business' });
      const { rerender } = render(<RagDashboard />);

      const technicalButton = screen.getByText('Technical').closest('button');
      fireEvent.click(technicalButton!);
      setupSearchParams({});
      rerender(<RagDashboard />);

      const technicalButtonUpdated = screen.getByText('Technical').closest('button');
      expect(technicalButtonUpdated).toHaveAttribute('data-active', 'true');
    });
  });

  // =========================================================================
  // Business View Tests
  // =========================================================================

  describe('Business View', () => {
    it('should show Business Metrics section in business view', () => {
      setupSearchParams({ view: 'business' });
      render(<RagDashboard />);

      expect(screen.getByText('Business Metrics')).toBeInTheDocument();
    });

    it('should show Strategy Comparison section in business view', () => {
      setupSearchParams({ view: 'business' });
      render(<RagDashboard />);

      expect(screen.getByText('Strategy Comparison')).toBeInTheDocument();
    });

    it('should show ROI Calculator section in business view', () => {
      setupSearchParams({ view: 'business' });
      render(<RagDashboard />);

      expect(screen.getByText('ROI Calculator')).toBeInTheDocument();
    });

    it('should show Use Cases section in business view', () => {
      setupSearchParams({ view: 'business' });
      render(<RagDashboard />);

      expect(screen.getByText('Use Cases')).toBeInTheDocument();
    });

    it('should hide tab navigation in business view', () => {
      setupSearchParams({ view: 'business' });
      render(<RagDashboard />);

      // Tab-specific navigation should not be visible
      expect(screen.queryByText('Agents & Prompts')).not.toBeInTheDocument();
    });

    it('should show BusinessKpiCards in business view', () => {
      setupSearchParams({ view: 'business' });
      render(<RagDashboard />);

      expect(screen.getByTestId('business-kpi-cards')).toBeInTheDocument();
    });

    it('should show BusinessStrategyComparison in business view', () => {
      setupSearchParams({ view: 'business' });
      render(<RagDashboard />);

      expect(screen.getByTestId('business-strategy-comparison')).toBeInTheDocument();
    });

    it('should show BusinessRoiCalculator in business view', () => {
      setupSearchParams({ view: 'business' });
      render(<RagDashboard />);

      expect(screen.getByTestId('business-roi-calculator')).toBeInTheDocument();
    });

    it('should show BusinessUseCases in business view', () => {
      setupSearchParams({ view: 'business' });
      render(<RagDashboard />);

      expect(screen.getByTestId('business-use-cases')).toBeInTheDocument();
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

    it('should display Architecture Explorer section in Architecture tab', () => {
      setupSearchParams({ tab: 'architecture' });
      render(<RagDashboard />);

      expect(screen.getByText('Architecture Explorer')).toBeInTheDocument();
    });

    it('should display Layer Documentation section in Architecture tab', () => {
      setupSearchParams({ tab: 'architecture' });
      render(<RagDashboard />);

      expect(screen.getByText('Layer Documentation')).toBeInTheDocument();
    });

    it('should display Agent-RAG Integration section in Agents tab', () => {
      setupSearchParams({ tab: 'agents' });
      render(<RagDashboard />);

      expect(screen.getByText('Agent-RAG Integration')).toBeInTheDocument();
    });

    it('should display Cost Projection section in Cost & Metrics tab', () => {
      setupSearchParams({ tab: 'performance' });
      render(<RagDashboard />);

      expect(screen.getByText('Cost Projection')).toBeInTheDocument();
    });

    it('should display Decision Walkthrough section in Walkthrough tab', () => {
      setupSearchParams({ tab: 'walkthrough' });
      render(<RagDashboard />);

      expect(screen.getByText('Decision Walkthrough')).toBeInTheDocument();
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

    it('should show business description for Business Metrics section in business view', () => {
      setupSearchParams({ view: 'business' });
      render(<RagDashboard />);

      expect(screen.getByText('Key cost efficiency and accuracy indicators')).toBeInTheDocument();
    });
  });

  // =========================================================================
  // Navigation Links Tests
  // =========================================================================

  describe('Navigation Links', () => {
    it('should have GitHub footer link', () => {
      render(<RagDashboard />);

      // GitHub link is in the footer
      const githubLink = screen.getByText('GitHub');
      expect(githubLink).toBeInTheDocument();
    });

    it('should have footer link with correct href', () => {
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
});
