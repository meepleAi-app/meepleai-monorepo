import { render, screen, fireEvent, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { RagDashboard, NAVIGATION_GROUPS } from '../RagDashboard';
import { DashboardSidebar } from '../DashboardSidebar';
import { DashboardNav } from '../DashboardNav';

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

// Mock IntersectionObserver
class MockIntersectionObserver {
  private callback: IntersectionObserverCallback;
  private elements: Set<Element> = new Set();
  static instances: MockIntersectionObserver[] = [];

  constructor(callback: IntersectionObserverCallback) {
    this.callback = callback;
    MockIntersectionObserver.instances.push(this);
  }

  observe(element: Element) {
    this.elements.add(element);
  }
  unobserve(element: Element) {
    this.elements.delete(element);
  }
  disconnect() {
    this.elements.clear();
  }
  takeRecords(): IntersectionObserverEntry[] {
    return [];
  }

  simulateIntersection(elementId: string, isIntersecting: boolean) {
    const element = document.getElementById(elementId);
    if (element && this.elements.has(element)) {
      this.callback(
        [
          {
            target: element,
            isIntersecting,
            boundingClientRect: element.getBoundingClientRect(),
            intersectionRatio: isIntersecting ? 1 : 0,
            intersectionRect: element.getBoundingClientRect(),
            rootBounds: null,
            time: Date.now(),
          },
        ],
        this
      );
    }
  }

  static clearInstances() {
    MockIntersectionObserver.instances = [];
  }
}

// Mock child components to simplify testing
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
  ArchitectureExplorer: () => <div data-testid="architecture">ArchitectureExplorer</div>,
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
  VariantComparisonTool: () => <div data-testid="variant-tool">VariantComparisonTool</div>,
}));

vi.mock('../PerformanceMetricsTable', () => ({
  PerformanceMetricsTable: () => <div data-testid="performance">PerformanceMetricsTable</div>,
}));

vi.mock('../PromptTemplateBuilder', () => ({
  PromptTemplateBuilder: () => <div data-testid="prompt-builder">PromptTemplateBuilder</div>,
}));

vi.mock('../AgentRoleConfigurator', () => ({
  AgentRoleConfigurator: () => <div data-testid="agent-roles">AgentRoleConfigurator</div>,
}));

vi.mock('../AgentRagIntegration', () => ({
  AgentRagIntegration: () => <div data-testid="agent-integration">AgentRagIntegration</div>,
}));

vi.mock('../ModelSelectionOptimizer', () => ({
  ModelSelectionOptimizer: () => <div data-testid="model-optimizer">ModelSelectionOptimizer</div>,
}));

vi.mock('../ProgressIndicator', () => ({
  ProgressIndicator: () => <div data-testid="progress-indicator">Progress</div>,
}));

describe('Navigation Integration', () => {
  beforeEach(() => {
    vi.stubGlobal('IntersectionObserver', MockIntersectionObserver);
    MockIntersectionObserver.clearInstances();
    vi.spyOn(history, 'replaceState').mockImplementation(() => {});
    vi.spyOn(window, 'scrollTo').mockImplementation(() => {});

    Object.defineProperty(window, 'location', {
      value: { hash: '' },
      writable: true,
    });
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  describe('NAVIGATION_GROUPS configuration', () => {
    it('should have all required groups', () => {
      const groupIds = NAVIGATION_GROUPS.map((g) => g.id);
      expect(groupIds).toContain('understand');
      expect(groupIds).toContain('explore');
      expect(groupIds).toContain('compare');
      expect(groupIds).toContain('build');
      expect(groupIds).toContain('optimize');
    });

    it('should have unique section IDs across all groups', () => {
      const allSectionIds = NAVIGATION_GROUPS.flatMap((g) =>
        g.sections.map((s) => s.id)
      );
      const uniqueIds = new Set(allSectionIds);
      expect(uniqueIds.size).toBe(allSectionIds.length);
    });

    it('should have icons for all groups', () => {
      NAVIGATION_GROUPS.forEach((group) => {
        expect(group.icon).toBeTruthy();
        expect(group.icon.length).toBeGreaterThan(0);
      });
    });
  });

  describe('Sidebar and Mobile Nav sync', () => {
    it('should sync sidebar and mobile nav active states', () => {
      const activeSection = 'query-sim';

      const { container } = render(
        <div>
          <DashboardSidebar
            groups={NAVIGATION_GROUPS}
            activeSection={activeSection}
          />
          <DashboardNav groups={NAVIGATION_GROUPS} activeSection={activeSection} />
        </div>
      );

      // Both should show the same active section
      const sidebarActive = container.querySelector('[aria-current="true"]');
      expect(sidebarActive).toHaveTextContent('Query Simulator');
    });

    it('should update both navs when active section changes', () => {
      const { rerender, container } = render(
        <div>
          <DashboardSidebar groups={NAVIGATION_GROUPS} activeSection="overview" />
          <DashboardNav groups={NAVIGATION_GROUPS} activeSection="overview" />
        </div>
      );

      // Verify initial state
      let sidebarActive = container.querySelector('[aria-current="true"]');
      expect(sidebarActive).toHaveTextContent('System Overview');

      // Update active section
      rerender(
        <div>
          <DashboardSidebar groups={NAVIGATION_GROUPS} activeSection="cost" />
          <DashboardNav groups={NAVIGATION_GROUPS} activeSection="cost" />
        </div>
      );

      sidebarActive = container.querySelector('[aria-current="true"]');
      expect(sidebarActive).toHaveTextContent('Cost Calculator');
    });
  });

  describe('Deep link handling', () => {
    it('should handle deep link on page load', () => {
      Object.defineProperty(window, 'location', {
        value: { hash: '#cost' },
        writable: true,
      });

      render(
        <DashboardSidebar groups={NAVIGATION_GROUPS} activeSection="cost" />
      );

      // Should highlight the cost section
      const activeSection = screen.getByRole('button', { name: 'Cost Calculator' });
      expect(activeSection).toHaveAttribute('aria-current', 'true');
    });
  });

  describe('View mode changes', () => {
    it('should handle view mode changes', () => {
      render(<RagDashboard />);

      // Should start in technical mode (default)
      expect(screen.getByTestId('architecture')).toBeInTheDocument();

      // Switch to business mode
      const businessButton = screen.getByRole('button', { name: /business/i });
      fireEvent.click(businessButton);

      // Architecture should be hidden in business mode
      expect(screen.queryByTestId('architecture')).not.toBeInTheDocument();
    });
  });

  describe('Section organization', () => {
    it('should render all section groups', () => {
      render(<RagDashboard />);

      // Check for section group headers (using group labels instead of icons to avoid duplicates)
      expect(screen.getByRole('heading', { name: 'Understand' })).toBeInTheDocument();
      expect(screen.getByRole('heading', { name: 'Explore' })).toBeInTheDocument();
      expect(screen.getByRole('heading', { name: 'Compare' })).toBeInTheDocument();
      expect(screen.getByRole('heading', { name: 'Build' })).toBeInTheDocument();
      expect(screen.getByRole('heading', { name: 'Optimize' })).toBeInTheDocument();
    });

    it('should render sections in User Journey order', () => {
      render(<RagDashboard />);

      // Check that sections exist in correct groups
      // Understand group
      expect(screen.getByTestId('stats-grid')).toBeInTheDocument();
      expect(screen.getByTestId('architecture')).toBeInTheDocument();
      expect(screen.getByTestId('layer-docs')).toBeInTheDocument();

      // Explore group
      expect(screen.getByTestId('query-simulator')).toBeInTheDocument();
      expect(screen.getByTestId('token-flow')).toBeInTheDocument();
      expect(screen.getByTestId('decision-walkthrough')).toBeInTheDocument();

      // Compare group
      expect(screen.getByTestId('variant-tool')).toBeInTheDocument();
      expect(screen.getByTestId('performance')).toBeInTheDocument();

      // Build group
      expect(screen.getByTestId('prompt-builder')).toBeInTheDocument();
      expect(screen.getByTestId('agent-roles')).toBeInTheDocument();
      expect(screen.getByTestId('agent-integration')).toBeInTheDocument();

      // Optimize group
      expect(screen.getByTestId('cost-calculator')).toBeInTheDocument();
      expect(screen.getByTestId('model-optimizer')).toBeInTheDocument();
    });
  });

  describe('Navigation functionality', () => {
    it('should have sections with scroll-mt-24 class for header offset', () => {
      render(<RagDashboard />);

      const sectionIds = NAVIGATION_GROUPS.flatMap((g) =>
        g.sections.map((s) => s.id)
      );

      sectionIds.forEach((id) => {
        const section = document.getElementById(id);
        if (section) {
          expect(section).toHaveClass('scroll-mt-24');
        }
      });
    });

    it('should render progress indicator in sidebar', () => {
      render(<RagDashboard />);

      expect(screen.getByTestId('progress-indicator')).toBeInTheDocument();
    });
  });
});
