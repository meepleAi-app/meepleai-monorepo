/**
 * AiInsightsWidget Tests (Issue #3316)
 *
 * Test coverage for RAG-powered AI suggestions widget.
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { AiInsightsWidget, type AiInsight } from '../AiInsightsWidget';

// Mock Next.js Link
vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

// Mock framer-motion to avoid animation issues in tests
vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, ...props }: React.HTMLAttributes<HTMLDivElement>) => (
      <div {...props}>{children}</div>
    ),
  },
  AnimatePresence: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

// ============================================================================
// Test Data
// ============================================================================

const mockInsights: AiInsight[] = [
  {
    id: 'insight-1',
    type: 'backlog',
    icon: '🎯',
    title: '5 giochi non giocati da 30+ giorni',
    description: 'Wingspan, Azul e altri aspettano di essere giocati',
    actionUrl: '/library?filter=unplayed',
    actionLabel: 'Scopri',
    priority: 1,
  },
  {
    id: 'insight-2',
    type: 'rules_reminder',
    icon: '📖',
    title: 'Regole di "Wingspan" salvate',
    description: 'Hai salvato le regole 3 giorni fa',
    actionUrl: '/chat/wingspan-rules',
    actionLabel: 'Rivedi',
    priority: 2,
  },
  {
    id: 'insight-3',
    type: 'recommendation',
    icon: '🆕',
    title: '3 giochi simili a "Catan"',
    description: 'Scopri nuovi giochi basati sui tuoi gusti',
    actionUrl: '/games/recommendations?based-on=catan',
    actionLabel: 'Esplora',
    priority: 3,
  },
  {
    id: 'insight-4',
    type: 'streak',
    icon: '🔥',
    title: 'Streak: 7 giorni - Mantienilo!',
    description: 'Gioca oggi per mantenere la tua serie',
    actionUrl: '/stats/streak',
    actionLabel: 'Stats',
    priority: 4,
  },
  {
    id: 'insight-5',
    type: 'achievement',
    icon: '📊',
    title: 'Achievement "Collezionista" al 85%',
    description: 'Ti mancano solo 15 giochi!',
    actionUrl: '/achievements/collector',
    actionLabel: 'Vedi',
    priority: 5,
  },
];

// ============================================================================
// Test Helpers
// ============================================================================

let queryClient: QueryClient;

function renderComponent(props: Partial<React.ComponentProps<typeof AiInsightsWidget>> = {}) {
  queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <AiInsightsWidget {...props} />
    </QueryClientProvider>
  );
}

// ============================================================================
// Tests
// ============================================================================

describe('AiInsightsWidget', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Loading State', () => {
    it('renders skeleton when loading', () => {
      renderComponent({ isLoading: true });

      expect(screen.getByTestId('ai-insights-widget-skeleton')).toBeInTheDocument();
    });

    it('shows skeleton with correct structure', () => {
      renderComponent({ isLoading: true });

      const skeleton = screen.getByTestId('ai-insights-widget-skeleton');
      expect(skeleton).toHaveClass('rounded-2xl');
      expect(skeleton).toHaveClass('border-amber-500/30');
    });
  });

  describe('Success State', () => {
    it('renders widget with insights', () => {
      renderComponent({ insights: mockInsights });

      expect(screen.getByTestId('ai-insights-widget')).toBeInTheDocument();
      expect(screen.getByTestId('ai-insights-title')).toHaveTextContent('AI Insights & Suggerimenti');
    });

    it('renders all insight items', () => {
      renderComponent({ insights: mockInsights });

      mockInsights.forEach((insight) => {
        expect(screen.getByTestId(`insight-item-${insight.id}`)).toBeInTheDocument();
        expect(screen.getByTestId(`insight-title-${insight.id}`)).toHaveTextContent(insight.title);
        expect(screen.getByTestId(`insight-description-${insight.id}`)).toHaveTextContent(insight.description);
      });
    });

    it('limits to 5 insights maximum', () => {
      const manyInsights: AiInsight[] = [
        ...mockInsights,
        {
          id: 'insight-6',
          type: 'backlog',
          icon: '🎯',
          title: 'Extra insight 6',
          description: 'Should not be displayed',
          actionUrl: '/test',
          actionLabel: 'Test',
          priority: 6,
        },
        {
          id: 'insight-7',
          type: 'backlog',
          icon: '🎯',
          title: 'Extra insight 7',
          description: 'Should not be displayed',
          actionUrl: '/test',
          actionLabel: 'Test',
          priority: 7,
        },
      ];

      renderComponent({ insights: manyInsights });

      const insightsList = screen.getByTestId('ai-insights-list');
      const items = insightsList.querySelectorAll('[data-testid^="insight-item-"]');
      expect(items.length).toBe(5);
    });

    it('sorts insights by priority', () => {
      const unsortedInsights: AiInsight[] = [
        { ...mockInsights[4], priority: 5 },
        { ...mockInsights[0], priority: 1 },
        { ...mockInsights[2], priority: 3 },
      ];

      renderComponent({ insights: unsortedInsights });

      const insightsList = screen.getByTestId('ai-insights-list');
      const titles = insightsList.querySelectorAll('[data-testid^="insight-title-"]');

      // First should be priority 1 (backlog)
      expect(titles[0]).toHaveTextContent(mockInsights[0].title);
    });

    it('renders correct icons for each insight type', () => {
      renderComponent({ insights: mockInsights });

      mockInsights.forEach((insight) => {
        const iconContainer = screen.getByTestId(`insight-icon-${insight.id}`);
        expect(iconContainer).toBeInTheDocument();
      });
    });

    it('renders action buttons with correct labels', () => {
      renderComponent({ insights: mockInsights });

      mockInsights.forEach((insight) => {
        const actionButton = screen.getByTestId(`insight-action-${insight.id}`);
        expect(actionButton).toHaveTextContent(insight.actionLabel);
      });
    });

    it('links to correct action URLs', () => {
      renderComponent({ insights: mockInsights });

      mockInsights.forEach((insight) => {
        const link = screen.getByTestId(`insight-action-${insight.id}`).closest('a');
        expect(link).toHaveAttribute('href', insight.actionUrl);
      });
    });
  });

  describe('Empty State', () => {
    it('renders empty state when no insights', () => {
      renderComponent({ insights: [] });

      expect(screen.getByTestId('ai-insights-empty')).toBeInTheDocument();
      expect(screen.getByText('Nessun suggerimento disponibile')).toBeInTheDocument();
    });

    it('shows helpful message in empty state', () => {
      renderComponent({ insights: [] });

      expect(screen.getByText('Continua a giocare per ricevere insight personalizzati')).toBeInTheDocument();
    });
  });

  describe('Error State', () => {
    it('renders error state when error occurs', () => {
      renderComponent({ error: new Error('API Error') });

      expect(screen.getByTestId('ai-insights-error')).toBeInTheDocument();
      expect(screen.getByText('Impossibile caricare i suggerimenti')).toBeInTheDocument();
    });

    it('shows retry button on error', () => {
      const onRefresh = vi.fn();
      renderComponent({ error: new Error('API Error'), onRefresh });

      expect(screen.getByTestId('ai-insights-error-refresh')).toBeInTheDocument();
    });

    it('calls onRefresh when retry button clicked', async () => {
      const user = userEvent.setup();
      const onRefresh = vi.fn();
      renderComponent({ error: new Error('API Error'), onRefresh });

      await user.click(screen.getByTestId('ai-insights-error-refresh'));

      expect(onRefresh).toHaveBeenCalledTimes(1);
    });
  });

  describe('AI Unavailable State', () => {
    it('renders AI unavailable state', () => {
      renderComponent({ isAiUnavailable: true, insights: [] });

      expect(screen.getByTestId('ai-insights-unavailable')).toBeInTheDocument();
      expect(screen.getByText('Servizio AI temporaneamente non disponibile')).toBeInTheDocument();
    });

    it('shows retry button when AI unavailable', () => {
      const onRefresh = vi.fn();
      renderComponent({ isAiUnavailable: true, insights: [], onRefresh });

      expect(screen.getByTestId('ai-insights-refresh')).toBeInTheDocument();
    });

    it('calls onRefresh when retry clicked in AI unavailable state', async () => {
      const user = userEvent.setup();
      const onRefresh = vi.fn();
      renderComponent({ isAiUnavailable: true, insights: [], onRefresh });

      await user.click(screen.getByTestId('ai-insights-refresh'));

      expect(onRefresh).toHaveBeenCalledTimes(1);
    });
  });

  describe('Refresh Functionality', () => {
    it('shows refresh button in header when onRefresh provided', () => {
      const onRefresh = vi.fn();
      renderComponent({ insights: mockInsights, onRefresh });

      expect(screen.getByTestId('ai-insights-header-refresh')).toBeInTheDocument();
    });

    it('does not show refresh button when no onRefresh', () => {
      renderComponent({ insights: mockInsights });

      expect(screen.queryByTestId('ai-insights-header-refresh')).not.toBeInTheDocument();
    });

    it('calls onRefresh when header refresh clicked', async () => {
      const user = userEvent.setup();
      const onRefresh = vi.fn();
      renderComponent({ insights: mockInsights, onRefresh });

      await user.click(screen.getByTestId('ai-insights-header-refresh'));

      expect(onRefresh).toHaveBeenCalledTimes(1);
    });
  });

  describe('Styling', () => {
    it('applies custom className', () => {
      renderComponent({ insights: mockInsights, className: 'custom-class' });

      expect(screen.getByTestId('ai-insights-widget')).toHaveClass('custom-class');
    });

    it('has amber/yellow gradient background', () => {
      renderComponent({ insights: mockInsights });

      const widget = screen.getByTestId('ai-insights-widget');
      expect(widget).toHaveClass('bg-gradient-to-br');
      expect(widget).toHaveClass('from-amber-50/50');
    });

    it('has amber border', () => {
      renderComponent({ insights: mockInsights });

      const widget = screen.getByTestId('ai-insights-widget');
      expect(widget).toHaveClass('border-amber-500/30');
    });
  });

  describe('Insight Type Colors', () => {
    it('applies correct color for backlog type', () => {
      renderComponent({ insights: [mockInsights[0]] });

      const iconContainer = screen.getByTestId(`insight-icon-${mockInsights[0].id}`);
      expect(iconContainer).toHaveClass('bg-amber-500/10');
    });

    it('applies correct color for rules_reminder type', () => {
      renderComponent({ insights: [mockInsights[1]] });

      const iconContainer = screen.getByTestId(`insight-icon-${mockInsights[1].id}`);
      expect(iconContainer).toHaveClass('bg-blue-500/10');
    });

    it('applies correct color for recommendation type', () => {
      renderComponent({ insights: [mockInsights[2]] });

      const iconContainer = screen.getByTestId(`insight-icon-${mockInsights[2].id}`);
      expect(iconContainer).toHaveClass('bg-purple-500/10');
    });

    it('applies correct color for streak type', () => {
      renderComponent({ insights: [mockInsights[3]] });

      const iconContainer = screen.getByTestId(`insight-icon-${mockInsights[3].id}`);
      expect(iconContainer).toHaveClass('bg-orange-500/10');
    });

    it('applies correct color for achievement type', () => {
      renderComponent({ insights: [mockInsights[4]] });

      const iconContainer = screen.getByTestId(`insight-icon-${mockInsights[4].id}`);
      expect(iconContainer).toHaveClass('bg-emerald-500/10');
    });
  });

  describe('Accessibility', () => {
    it('has semantic section element', () => {
      renderComponent({ insights: mockInsights });

      expect(screen.getByTestId('ai-insights-widget').tagName).toBe('SECTION');
    });

    it('has heading element', () => {
      renderComponent({ insights: mockInsights });

      expect(screen.getByRole('heading', { level: 3 })).toBeInTheDocument();
    });

    it('buttons are accessible', () => {
      renderComponent({ insights: mockInsights });

      const buttons = screen.getAllByRole('button');
      buttons.forEach((button) => {
        expect(button).toBeEnabled();
      });
    });

    it('links have href attributes', () => {
      renderComponent({ insights: mockInsights });

      const links = screen.getAllByRole('link');
      links.forEach((link) => {
        expect(link).toHaveAttribute('href');
      });
    });
  });
});
