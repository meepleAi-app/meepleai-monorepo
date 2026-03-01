/**
 * Navigation Integration Tests for Tabbed RAG Dashboard
 *
 * Tests the tabbed navigation system introduced in the dashboard redesign (Issue #3547).
 * Tests cover: tab switching, view modes, and content visibility.
 *
 * Note: RagDashboard uses URL-based state (useSearchParams + router.replace).
 * Tab switching is tested by simulating URL param changes via mock + rerender.
 */

import React from 'react';

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach, type Mock } from 'vitest';
import { useRouter, useSearchParams, usePathname } from 'next/navigation';

import { RagDashboard, NAVIGATION_GROUPS } from '../RagDashboard';

// ─── next/navigation mock ─────────────────────────────────────────────────────

vi.mock('next/navigation', () => ({
  useRouter: vi.fn(),
  useSearchParams: vi.fn(),
  usePathname: vi.fn(),
}));

const mockUseRouter = useRouter as Mock;
const mockUseSearchParams = useSearchParams as Mock;
const mockUsePathname = usePathname as Mock;

// ─── framer-motion mock ───────────────────────────────────────────────────────

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

// ─── IntersectionObserver mock ────────────────────────────────────────────────

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

// ─── Technical component mocks ────────────────────────────────────────────────

vi.mock('../StatsGrid', () => ({
  StatsGrid: () => <div data-testid="stats-grid">StatsGrid</div>,
}));

vi.mock('../QuerySimulator', () => ({
  QuerySimulator: () => <div data-testid="query-simulator">QuerySimulator</div>,
}));

vi.mock('../TokenFlowVisualizer', () => ({
  TokenFlowVisualizer: () => <div data-testid="token-flow">TokenFlowVisualizer</div>,
}));

vi.mock('../ArchitectureExplorer', () => ({
  ArchitectureExplorer: () => <div data-testid="architecture-explorer">ArchitectureExplorer</div>,
}));

vi.mock('../LayerDeepDocs', () => ({
  LayerDeepDocs: () => <div data-testid="layer-docs">LayerDeepDocs</div>,
}));

vi.mock('../CostCalculator', () => ({
  CostCalculator: () => <div data-testid="cost-calculator">CostCalculator</div>,
}));

vi.mock('../DecisionWalkthrough', () => ({
  DecisionWalkthrough: () => <div data-testid="decision-walkthrough">DecisionWalkthrough</div>,
}));

vi.mock('../VariantComparisonTool', () => ({
  VariantComparisonTool: () => <div data-testid="variant-comparison">VariantComparisonTool</div>,
}));

vi.mock('../PerformanceMetricsTable', () => ({
  PerformanceMetricsTable: () => <div data-testid="performance-metrics">PerformanceMetricsTable</div>,
}));

vi.mock('../PromptTemplateBuilder', () => ({
  PromptTemplateBuilder: () => <div data-testid="prompt-builder">PromptTemplateBuilder</div>,
}));

vi.mock('../AgentRoleConfigurator', () => ({
  AgentRoleConfigurator: () => <div data-testid="agent-role-config">AgentRoleConfigurator</div>,
}));

vi.mock('../AgentRagIntegration', () => ({
  AgentRagIntegration: () => <div data-testid="agent-rag-integration">AgentRagIntegration</div>,
}));

vi.mock('../ModelSelectionOptimizer', () => ({
  ModelSelectionOptimizer: () => <div data-testid="model-optimizer">ModelSelectionOptimizer</div>,
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

// ─── Business component mocks ─────────────────────────────────────────────────

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

// ─── Helper ───────────────────────────────────────────────────────────────────

/**
 * Set up URL search params mock.
 * RagDashboard reads `view` (default 'technical') and `tab` (default 'overview') from params.
 */
function setupSearchParams(params: Record<string, string> = {}) {
  mockUseSearchParams.mockReturnValue({
    get: (key: string) => params[key] ?? null,
    toString: () => {
      const sp = new URLSearchParams();
      Object.entries(params).forEach(([k, v]) => sp.set(k, v));
      return sp.toString();
    },
  });
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('Navigation Integration (Tabbed Dashboard)', () => {
  beforeEach(() => {
    vi.stubGlobal('IntersectionObserver', MockIntersectionObserver);

    // Default: technical view, overview tab
    setupSearchParams({});
    mockUseRouter.mockReturnValue({
      push: vi.fn(),
      replace: vi.fn(),
      prefetch: vi.fn(),
    });
    mockUsePathname.mockReturnValue('/');
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  describe('NAVIGATION_GROUPS configuration', () => {
    it('should have all required tab groups', () => {
      const groupIds = NAVIGATION_GROUPS.map((g) => g.id);
      expect(groupIds).toContain('overview');
      expect(groupIds).toContain('architecture');
      expect(groupIds).toContain('agents');
      expect(groupIds).toContain('performance');
      expect(groupIds).toContain('walkthrough');
    });

    it('should have 5 tab groups total', () => {
      expect(NAVIGATION_GROUPS).toHaveLength(5);
    });

    it('should have labels for all groups', () => {
      NAVIGATION_GROUPS.forEach((group) => {
        expect(group.label).toBeTruthy();
        expect(group.label.length).toBeGreaterThan(0);
      });
    });

    it('should have icons for all groups', () => {
      NAVIGATION_GROUPS.forEach((group) => {
        expect(group.icon).toBeTruthy();
        expect(group.icon.length).toBeGreaterThan(0);
      });
    });

    it('should have descriptions for all groups', () => {
      NAVIGATION_GROUPS.forEach((group) => {
        expect(group.description).toBeTruthy();
        expect(group.description.length).toBeGreaterThan(0);
      });
    });
  });

  describe('Tab navigation', () => {
    it('should show Overview tab content by default', () => {
      render(<RagDashboard />);

      // Overview tab content should be visible
      expect(screen.getByTestId('poc-status')).toBeInTheDocument();
      expect(screen.getByTestId('stats-grid')).toBeInTheDocument();
      expect(screen.getByTestId('query-simulator')).toBeInTheDocument();
      expect(screen.getByTestId('token-flow')).toBeInTheDocument();
    });

    it('should switch to Architecture tab when clicked', async () => {
      const { rerender } = render(<RagDashboard />);

      // Click Architecture tab
      const architectureButtons = screen.getAllByText('Architecture');
      fireEvent.click(architectureButtons[0]);

      // Simulate URL update that router.replace would trigger
      setupSearchParams({ tab: 'architecture' });
      rerender(<RagDashboard />);

      // Architecture content should be visible
      await waitFor(() => {
        expect(screen.getByTestId('architecture-explorer')).toBeInTheDocument();
        expect(screen.getByTestId('layer-docs')).toBeInTheDocument();
        expect(screen.getByTestId('technical-reference')).toBeInTheDocument();
      });

      // Overview content should not be visible
      expect(screen.queryByTestId('poc-status')).not.toBeInTheDocument();
    });

    it('should switch to Agents & Prompts tab when clicked', async () => {
      const { rerender } = render(<RagDashboard />);

      // Click Agents tab
      const agentsButtons = screen.getAllByText('Agents & Prompts');
      fireEvent.click(agentsButtons[0]);

      // Simulate URL update
      setupSearchParams({ tab: 'agents' });
      rerender(<RagDashboard />);

      // Agents content should be visible
      await waitFor(() => {
        expect(screen.getByTestId('agent-rag-integration')).toBeInTheDocument();
        expect(screen.getByTestId('agent-role-config')).toBeInTheDocument();
        expect(screen.getByTestId('prompt-builder')).toBeInTheDocument();
      });
    });

    it('should switch to Cost & Metrics tab when clicked', async () => {
      const { rerender } = render(<RagDashboard />);

      // Click Cost & Metrics tab
      const metricsButtons = screen.getAllByText('Cost & Metrics');
      fireEvent.click(metricsButtons[0]);

      // Simulate URL update
      setupSearchParams({ tab: 'performance' });
      rerender(<RagDashboard />);

      // Cost & Metrics content should be visible
      await waitFor(() => {
        expect(screen.getByTestId('parameter-guide')).toBeInTheDocument();
        expect(screen.getByTestId('cost-calculator')).toBeInTheDocument();
        expect(screen.getByTestId('model-optimizer')).toBeInTheDocument();
        expect(screen.getByTestId('performance-metrics')).toBeInTheDocument();
        expect(screen.getByTestId('variant-comparison')).toBeInTheDocument();
      });
    });

    it('should switch to Walkthrough tab when clicked', async () => {
      const { rerender } = render(<RagDashboard />);

      // Click Walkthrough tab
      const walkthroughButtons = screen.getAllByText('Walkthrough');
      fireEvent.click(walkthroughButtons[0]);

      // Simulate URL update
      setupSearchParams({ tab: 'walkthrough' });
      rerender(<RagDashboard />);

      // Walkthrough content should be visible
      await waitFor(() => {
        expect(screen.getByTestId('decision-walkthrough')).toBeInTheDocument();
      });
    });
  });

  describe('View mode changes', () => {
    it('should start in technical mode by default', () => {
      render(<RagDashboard />);

      // Technical button should be active
      const technicalButton = screen.getByText('Technical').closest('button');
      expect(technicalButton).toHaveAttribute('data-active', 'true');
    });

    it('should switch to business mode when Business ROI button is clicked', () => {
      const { rerender } = render(<RagDashboard />);

      const businessButton = screen.getByText('Business ROI').closest('button');
      fireEvent.click(businessButton!);

      // Simulate URL update
      setupSearchParams({ view: 'business' });
      rerender(<RagDashboard />);

      const updatedBusinessButton = screen.getByText('Business ROI').closest('button');
      expect(updatedBusinessButton).toHaveAttribute('data-active', 'true');
    });

    it('should hide tab navigation in business mode', async () => {
      const { rerender } = render(<RagDashboard />);

      const businessButton = screen.getByText('Business ROI').closest('button');
      fireEvent.click(businessButton!);

      // Simulate URL update
      setupSearchParams({ view: 'business' });
      rerender(<RagDashboard />);

      // Tab-specific navigation (like "Agents & Prompts") should not be visible
      await waitFor(() => {
        expect(screen.queryByText('Agents & Prompts')).not.toBeInTheDocument();
      });
    });

    it('should show Business Metrics section in business mode', async () => {
      const { rerender } = render(<RagDashboard />);

      const businessButton = screen.getByText('Business ROI').closest('button');
      fireEvent.click(businessButton!);

      setupSearchParams({ view: 'business' });
      rerender(<RagDashboard />);

      await waitFor(() => {
        expect(screen.getByText('Business Metrics')).toBeInTheDocument();
      });
    });

    it('should show business sections in business mode', async () => {
      const { rerender } = render(<RagDashboard />);

      const businessButton = screen.getByText('Business ROI').closest('button');
      fireEvent.click(businessButton!);

      setupSearchParams({ view: 'business' });
      rerender(<RagDashboard />);

      await waitFor(() => {
        expect(screen.getByText('Strategy Comparison')).toBeInTheDocument();
        expect(screen.getByText('ROI Calculator')).toBeInTheDocument();
        expect(screen.getByText('Use Cases')).toBeInTheDocument();
      });
    });

    it('should preserve tab state when switching back to technical mode', async () => {
      const { rerender } = render(<RagDashboard />);

      // Switch to Architecture tab
      const architectureButtons = screen.getAllByText('Architecture');
      fireEvent.click(architectureButtons[0]);

      setupSearchParams({ tab: 'architecture' });
      rerender(<RagDashboard />);

      await waitFor(() => {
        expect(screen.getByTestId('architecture-explorer')).toBeInTheDocument();
      });

      // Switch to business mode
      const businessButton = screen.getByText('Business ROI').closest('button');
      fireEvent.click(businessButton!);

      setupSearchParams({ view: 'business', tab: 'architecture' });
      rerender(<RagDashboard />);

      // Switch back to technical mode — tab state is preserved in the URL
      const technicalButton = screen.getByText('Technical').closest('button');
      fireEvent.click(technicalButton!);

      setupSearchParams({ view: 'technical', tab: 'architecture' });
      rerender(<RagDashboard />);

      // Should still be on Architecture tab
      await waitFor(() => {
        expect(screen.getByTestId('architecture-explorer')).toBeInTheDocument();
      });
    });
  });

  describe('Tab content organization', () => {
    it('should render Overview tab with all expected sections', () => {
      render(<RagDashboard />);

      expect(screen.getByText('POC Status')).toBeInTheDocument();
      expect(screen.getByText('System Overview')).toBeInTheDocument();
      expect(screen.getByText('Query Simulator')).toBeInTheDocument();
      expect(screen.getByText('Token Flow')).toBeInTheDocument();
    });

    it('should render Architecture tab with all expected sections', async () => {
      const { rerender } = render(<RagDashboard />);

      setupSearchParams({ tab: 'architecture' });
      rerender(<RagDashboard />);

      await waitFor(() => {
        expect(screen.getByText('Architecture Explorer')).toBeInTheDocument();
        expect(screen.getByText('Technical Reference')).toBeInTheDocument();
        expect(screen.getByText('Layer Documentation')).toBeInTheDocument();
      });
    });

    it('should render Agents tab with all expected sections', async () => {
      const { rerender } = render(<RagDashboard />);

      setupSearchParams({ tab: 'agents' });
      rerender(<RagDashboard />);

      await waitFor(() => {
        expect(screen.getByText('Agent-RAG Integration')).toBeInTheDocument();
        expect(screen.getByText('Agent Roles')).toBeInTheDocument();
        expect(screen.getByText('Prompt Builder')).toBeInTheDocument();
      });
    });

    it('should render Cost & Metrics tab with all expected sections', async () => {
      const { rerender } = render(<RagDashboard />);

      setupSearchParams({ tab: 'performance' });
      rerender(<RagDashboard />);

      await waitFor(() => {
        expect(screen.getByText('Parametri & Strategie')).toBeInTheDocument();
        expect(screen.getByText('Cost Projection')).toBeInTheDocument();
        expect(screen.getByText('Model Selection')).toBeInTheDocument();
        expect(screen.getByText('Performance Metrics')).toBeInTheDocument();
      });
    });

    it('should render Walkthrough tab with expected section', async () => {
      const { rerender } = render(<RagDashboard />);

      setupSearchParams({ tab: 'walkthrough' });
      rerender(<RagDashboard />);

      await waitFor(() => {
        expect(screen.getByText('Decision Walkthrough')).toBeInTheDocument();
      });
    });
  });

  describe('Business view content', () => {
    it('should render all business sections', async () => {
      const { rerender } = render(<RagDashboard />);

      setupSearchParams({ view: 'business' });
      rerender(<RagDashboard />);

      await waitFor(() => {
        expect(screen.getByTestId('business-kpi-cards')).toBeInTheDocument();
        expect(screen.getByTestId('business-roi-calculator')).toBeInTheDocument();
        expect(screen.getByTestId('business-strategy-comparison')).toBeInTheDocument();
        expect(screen.getByTestId('business-use-cases')).toBeInTheDocument();
      });
    });

    it('should show Use Cases section in business view', async () => {
      const { rerender } = render(<RagDashboard />);

      setupSearchParams({ view: 'business' });
      rerender(<RagDashboard />);

      await waitFor(() => {
        expect(screen.getByText('Use Cases')).toBeInTheDocument();
      });
    });
  });
});
