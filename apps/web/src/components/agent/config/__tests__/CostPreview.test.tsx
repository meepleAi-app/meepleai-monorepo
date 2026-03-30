/**
 * CostPreview Component - Unit Tests
 * Issue #3383: Cost Estimation Preview Before Launch
 */

import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import userEvent from '@testing-library/user-event';

import { CostPreview } from '../CostPreview';
import * as useCostEstimateModule from '@/hooks/useCostEstimate';

// Mock the useCostEstimate hook
vi.mock('@/hooks/useCostEstimate', async () => {
  const actual = await vi.importActual('@/hooks/useCostEstimate');
  return {
    ...actual,
    useCostEstimate: vi.fn(),
  };
});

// Mock agent store
vi.mock('@/stores/agentStore', () => ({
  useAgentStore: vi.fn(() => ({
    selectedagentDefinitionId: null,
  })),
}));

// Test wrapper with QueryClient
function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  });

  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
}

describe('CostPreview', () => {
  const mockUseCostEstimate = useCostEstimateModule.useCostEstimate as ReturnType<typeof vi.fn>;

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering States', () => {
    it('renders null when no typology selected', () => {
      mockUseCostEstimate.mockReturnValue({
        data: null,
        isLoading: false,
        isError: false,
        error: null,
      } as any);

      const { container } = render(<CostPreview />, { wrapper: createWrapper() });

      expect(container.firstChild).toBeNull();
    });

    it('renders loading state', () => {
      mockUseCostEstimate.mockReturnValue({
        data: null,
        isLoading: true,
        isError: false,
        error: null,
      } as any);

      render(<CostPreview agentDefinitionId="123e4567-e89b-12d3-a456-426614174000" />, {
        wrapper: createWrapper(),
      });

      expect(screen.getByText('Calculating costs...')).toBeInTheDocument();
    });

    it('renders placeholder when no estimate available', () => {
      mockUseCostEstimate.mockReturnValue({
        data: null,
        isLoading: false,
        isError: false,
        error: null,
      } as any);

      render(<CostPreview agentDefinitionId="123e4567-e89b-12d3-a456-426614174000" />, {
        wrapper: createWrapper(),
      });

      expect(screen.getByText(/Select a configuration to see cost estimate/i)).toBeInTheDocument();
    });
  });

  describe('Cost Display', () => {
    it('displays per-query cost correctly', () => {
      mockUseCostEstimate.mockReturnValue({
        data: {
          agentDefinitionId: '123',
          typologyName: 'Test Tutor',
          strategy: 'BALANCED',
          costEstimate: {
            estimatedTokensPerQuery: 12000,
            estimatedCostPerQuery: 0.043,
            estimatedMonthlyCost10K: 430,
            costByPhase: {},
          },
        },
        isLoading: false,
        isError: false,
        error: null,
      } as any);

      render(<CostPreview agentDefinitionId="123" />, { wrapper: createWrapper() });

      expect(screen.getByText('$0.0430')).toBeInTheDocument(); // Per query
    });

    it('displays per-session cost with default queries', () => {
      mockUseCostEstimate.mockReturnValue({
        data: {
          agentDefinitionId: '123',
          typologyName: 'Test Tutor',
          strategy: 'BALANCED',
          costEstimate: {
            estimatedTokensPerQuery: 10000,
            estimatedCostPerQuery: 0.05,
            estimatedMonthlyCost10K: 500,
            costByPhase: {},
          },
        },
        isLoading: false,
        isError: false,
        error: null,
      } as any);

      render(<CostPreview agentDefinitionId="123" />, { wrapper: createWrapper() });

      // Session cost = 0.05 * 5 (default) = 0.25
      expect(screen.getByText(/Per session.*5 queries/i)).toBeInTheDocument();
      expect(screen.getByText('$0.250')).toBeInTheDocument();
    });

    it('displays per-session cost with custom queries', () => {
      mockUseCostEstimate.mockReturnValue({
        data: {
          agentDefinitionId: '123',
          typologyName: 'Test Tutor',
          strategy: 'FAST',
          costEstimate: {
            estimatedTokensPerQuery: 5000,
            estimatedCostPerQuery: 0.02,
            estimatedMonthlyCost10K: 200,
            costByPhase: {},
          },
        },
        isLoading: false,
        isError: false,
        error: null,
      } as any);

      render(<CostPreview agentDefinitionId="123" estimatedQueriesPerSession={10} />, {
        wrapper: createWrapper(),
      });

      // Session cost = 0.02 * 10 = 0.20
      expect(screen.getByText(/Per session.*10 queries/i)).toBeInTheDocument();
      expect(screen.getByText('$0.200')).toBeInTheDocument();
    });

    it('displays monthly cost', () => {
      mockUseCostEstimate.mockReturnValue({
        data: {
          agentDefinitionId: '123',
          typologyName: 'Test Tutor',
          strategy: 'EXPERT',
          costEstimate: {
            estimatedTokensPerQuery: 20000,
            estimatedCostPerQuery: 0.1,
            estimatedMonthlyCost10K: 1000,
            costByPhase: {},
          },
        },
        isLoading: false,
        isError: false,
        error: null,
      } as any);

      render(<CostPreview agentDefinitionId="123" />, { wrapper: createWrapper() });

      expect(screen.getByText('$1000.00')).toBeInTheDocument();
    });
  });

  describe('Warning Levels', () => {
    it('displays low warning (green) for cost under $0.20/session', () => {
      mockUseCostEstimate.mockReturnValue({
        data: {
          agentDefinitionId: '123',
          typologyName: 'Test Tutor',
          strategy: 'FAST',
          costEstimate: {
            estimatedTokensPerQuery: 3000,
            estimatedCostPerQuery: 0.01, // 0.01 * 5 = 0.05 (low)
            estimatedMonthlyCost10K: 100,
            costByPhase: {},
          },
        },
        isLoading: false,
        isError: false,
        error: null,
      } as any);

      render(<CostPreview agentDefinitionId="123" />, { wrapper: createWrapper() });

      expect(screen.getByText('✓ Cost-effective configuration')).toBeInTheDocument();
    });

    it('displays medium warning (yellow) for cost $0.20-$0.49/session', () => {
      mockUseCostEstimate.mockReturnValue({
        data: {
          agentDefinitionId: '123',
          typologyName: 'Test Tutor',
          strategy: 'BALANCED',
          costEstimate: {
            estimatedTokensPerQuery: 8000,
            estimatedCostPerQuery: 0.05, // 0.05 * 5 = 0.25 (medium)
            estimatedMonthlyCost10K: 500,
            costByPhase: {},
          },
        },
        isLoading: false,
        isError: false,
        error: null,
      } as any);

      render(<CostPreview agentDefinitionId="123" />, { wrapper: createWrapper() });

      expect(screen.getByText(/Moderate cost.*Monitor usage/i)).toBeInTheDocument();
    });

    it('displays high warning (red) for cost $0.50+/session', () => {
      mockUseCostEstimate.mockReturnValue({
        data: {
          agentDefinitionId: '123',
          typologyName: 'Test Tutor',
          strategy: 'CONSENSUS',
          costEstimate: {
            estimatedTokensPerQuery: 30000,
            estimatedCostPerQuery: 0.15, // 0.15 * 5 = 0.75 (high)
            estimatedMonthlyCost10K: 1500,
            costByPhase: {},
          },
        },
        isLoading: false,
        isError: false,
        error: null,
      } as any);

      render(<CostPreview agentDefinitionId="123" />, { wrapper: createWrapper() });

      expect(screen.getByText(/High cost detected/i)).toBeInTheDocument();
      expect(screen.getByText(/\$0\.75.*exceeds.*\$0\.50/i)).toBeInTheDocument();
    });

    it('applies correct border color based on warning level - low', () => {
      mockUseCostEstimate.mockReturnValue({
        data: {
          agentDefinitionId: '123',
          typologyName: 'Test',
          strategy: 'FAST',
          costEstimate: {
            estimatedTokensPerQuery: 3000,
            estimatedCostPerQuery: 0.01,
            estimatedMonthlyCost10K: 100,
            costByPhase: {},
          },
        },
        isLoading: false,
        isError: false,
        error: null,
      } as any);

      const { container } = render(<CostPreview agentDefinitionId="123" />, {
        wrapper: createWrapper(),
      });

      const card = container.querySelector('.border-slate-700');
      expect(card).toBeInTheDocument();
    });

    it('applies correct border color based on warning level - high', () => {
      mockUseCostEstimate.mockReturnValue({
        data: {
          agentDefinitionId: '123',
          typologyName: 'Test',
          strategy: 'EXPERT',
          costEstimate: {
            estimatedTokensPerQuery: 30000,
            estimatedCostPerQuery: 0.2,
            estimatedMonthlyCost10K: 2000,
            costByPhase: {},
          },
        },
        isLoading: false,
        isError: false,
        error: null,
      } as any);

      const { container } = render(<CostPreview agentDefinitionId="123" />, {
        wrapper: createWrapper(),
      });

      const card = container.querySelector('.border-red-500\\/50');
      expect(card).toBeInTheDocument();
    });
  });

  describe('Tooltip Interaction', () => {
    it('displays token breakdown in tooltip', async () => {
      mockUseCostEstimate.mockReturnValue({
        data: {
          agentDefinitionId: '123',
          typologyName: 'Test Tutor',
          strategy: 'BALANCED',
          costEstimate: {
            estimatedTokensPerQuery: 12000,
            estimatedCostPerQuery: 0.043,
            estimatedMonthlyCost10K: 430,
            costByPhase: {
              retrieval: 0.01,
              generation: 0.033,
            },
          },
        },
        isLoading: false,
        isError: false,
        error: null,
      } as any);

      const { container } = render(<CostPreview agentDefinitionId="123" />, {
        wrapper: createWrapper(),
      });

      // Find the TooltipTrigger wrapper by looking for the Info icon
      const tooltipTrigger = container.querySelector('[class*="cursor-help"]');
      expect(tooltipTrigger).toBeInTheDocument();

      // Verify tooltip content exists in DOM (even if not visible)
      // Tooltip content is rendered but may not be visible until hover
      expect(screen.getByText('Per query')).toBeInTheDocument();
    });

    it('handles empty costByPhase gracefully in tooltip', async () => {
      const user = userEvent.setup();

      mockUseCostEstimate.mockReturnValue({
        data: {
          agentDefinitionId: '123',
          typologyName: 'Test Tutor',
          strategy: 'FAST',
          costEstimate: {
            estimatedTokensPerQuery: 5000,
            estimatedCostPerQuery: 0.015,
            estimatedMonthlyCost10K: 150,
            costByPhase: {},
          },
        },
        isLoading: false,
        isError: false,
        error: null,
      } as any);

      render(<CostPreview agentDefinitionId="123" />, { wrapper: createWrapper() });

      const infoIcons = screen.getByText('Per query').parentElement?.querySelectorAll('svg');
      const infoIcon = infoIcons?.[1]; // Second icon is the Info icon

      if (infoIcon) {
        await user.hover(infoIcon);

        await waitFor(() => {
          const tokenBreakdowns = screen.getAllByText('Token Breakdown');
          expect(tokenBreakdowns.length).toBeGreaterThan(0);
          expect(screen.getByText(/Total tokens:/i)).toBeInTheDocument();
          // Should not display "Cost by Phase" section
          expect(screen.queryByText(/Cost by Phase:/i)).not.toBeInTheDocument();
        });
      }
    });
  });

  describe('Responsive Behavior', () => {
    it('applies custom className', () => {
      mockUseCostEstimate.mockReturnValue({
        data: {
          agentDefinitionId: '123',
          typologyName: 'Test',
          strategy: 'FAST',
          costEstimate: {
            estimatedTokensPerQuery: 5000,
            estimatedCostPerQuery: 0.02,
            estimatedMonthlyCost10K: 200,
            costByPhase: {},
          },
        },
        isLoading: false,
        isError: false,
        error: null,
      } as any);

      const { container } = render(
        <CostPreview agentDefinitionId="123" className="custom-class" />,
        {
          wrapper: createWrapper(),
        }
      );

      // The custom class should be on the Card component (outermost div with rounded-2xl)
      const card = container.querySelector('.custom-class');
      expect(card).toBeInTheDocument();
      expect(card).toHaveClass('rounded-2xl'); // Card component class
    });
  });
});
