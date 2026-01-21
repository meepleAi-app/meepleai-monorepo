/**
 * KPICardsGrid Component Tests (Issue #2785)
 *
 * Tests for KPI cards grid layout and helper functions:
 * - Grid rendering with multiple cards
 * - Responsive column layout
 * - buildKPICards helper
 * - calculateTrendPercent helper
 * - estimateAiCost helper
 *
 * Part of Epic #2783 - Admin Dashboard Redesign
 */

import { render, screen } from '@testing-library/react';
import { Users, Activity, Gamepad2, Zap } from 'lucide-react';
import { describe, it, expect } from 'vitest';

import {
  KPICardsGrid,
  buildKPICards,
  calculateTrendPercent,
  estimateAiCost,
  type KPICardData,
} from '@/components/admin/KPICardsGrid';
import type { DashboardMetrics } from '@/lib/api';

describe('KPICardsGrid', () => {
  const mockCards: KPICardData[] = [
    {
      title: 'Card 1',
      value: '100',
      icon: <Users className="h-5 w-5" />,
    },
    {
      title: 'Card 2',
      value: '200',
      icon: <Activity className="h-5 w-5" />,
      trend: 10,
    },
    {
      title: 'Card 3',
      value: '300',
      icon: <Gamepad2 className="h-5 w-5" />,
      badge: 'New',
      badgeVariant: 'success',
    },
    {
      title: 'Card 4',
      value: '400',
      icon: <Zap className="h-5 w-5" />,
      subtitle: 'Subtitle text',
    },
  ];

  describe('Grid Rendering', () => {
    it('should render all cards', () => {
      render(<KPICardsGrid cards={mockCards} data-testid="kpi-grid" />);

      expect(screen.getByText('Card 1')).toBeInTheDocument();
      expect(screen.getByText('Card 2')).toBeInTheDocument();
      expect(screen.getByText('Card 3')).toBeInTheDocument();
      expect(screen.getByText('Card 4')).toBeInTheDocument();
    });

    it('should render correct number of cards', () => {
      render(<KPICardsGrid cards={mockCards} data-testid="kpi-grid" />);

      const grid = screen.getByTestId('kpi-grid');
      // Each card has data-testid="kpi-grid-card-N"
      expect(screen.getByTestId('kpi-grid-card-0')).toBeInTheDocument();
      expect(screen.getByTestId('kpi-grid-card-1')).toBeInTheDocument();
      expect(screen.getByTestId('kpi-grid-card-2')).toBeInTheDocument();
      expect(screen.getByTestId('kpi-grid-card-3')).toBeInTheDocument();
    });

    it('should have responsive grid classes', () => {
      render(<KPICardsGrid cards={mockCards} data-testid="kpi-grid" />);

      const grid = screen.getByTestId('kpi-grid');
      expect(grid).toHaveClass('grid', 'grid-cols-1', 'sm:grid-cols-2', 'lg:grid-cols-4');
    });

    it('should apply custom className', () => {
      render(<KPICardsGrid cards={mockCards} className="custom-grid" data-testid="kpi-grid" />);

      expect(screen.getByTestId('kpi-grid')).toHaveClass('custom-grid');
    });

    it('should render empty grid when no cards', () => {
      render(<KPICardsGrid cards={[]} data-testid="kpi-grid" />);

      const grid = screen.getByTestId('kpi-grid');
      expect(grid.children).toHaveLength(0);
    });
  });
});

describe('buildKPICards', () => {
  const mockMetrics: DashboardMetrics = {
    totalUsers: 2847,
    activeSessions: 156,
    totalGames: 1234,
    apiRequestsToday: 8492,
    apiRequests7d: 45000,
    apiRequests30d: 150000,
    averageLatency24h: 120,
    averageLatency7d: 115,
    errorRate24h: 0.02,
    totalPdfDocuments: 500,
    totalChatMessages: 10000,
    averageConfidenceScore: 0.85,
    totalRagRequests: 5000,
    totalTokensUsed: 2500000,
    activeAlerts: 2,
    resolvedAlerts: 50,
  };

  it('should build 4 KPI cards from metrics', () => {
    const cards = buildKPICards(mockMetrics);

    expect(cards).toHaveLength(4);
    expect(cards[0].title).toBe('Utenti Totali');
    expect(cards[1].title).toBe('Sessioni Attive');
    expect(cards[2].title).toBe('Giochi in Catalogo');
    expect(cards[3].title).toBe('Richieste AI Oggi');
  });

  it('should format values with Italian locale', () => {
    const cards = buildKPICards(mockMetrics);

    expect(cards[0].value).toBe('2.847'); // Italian uses . as thousands separator
    expect(cards[1].value).toBe('156');
    expect(cards[2].value).toBe('1.234');
    expect(cards[3].value).toBe('8.492');
  });

  it('should include pending games badge when provided', () => {
    const cards = buildKPICards(mockMetrics, { pendingGamesCount: 12 });

    expect(cards[2].badge).toBe('12 in attesa');
    expect(cards[2].badgeVariant).toBe('warning');
  });

  it('should not include badge when pendingGamesCount is 0', () => {
    const cards = buildKPICards(mockMetrics, { pendingGamesCount: 0 });

    expect(cards[2].badge).toBeUndefined();
  });

  it('should include user trend when provided', () => {
    const cards = buildKPICards(mockMetrics, { userTrendPercent: 15 });

    expect(cards[0].trend).toBe(15);
    expect(cards[0].trendLabel).toBe('vs mese scorso');
  });

  it('should calculate AI cost estimate', () => {
    const cards = buildKPICards(mockMetrics);

    // Should have a cost estimate in subtitle
    expect(cards[3].subtitle).toMatch(/~€[\d.]+\s+stimati/);
  });

  it('should use provided AI cost when given', () => {
    const cards = buildKPICards(mockMetrics, { estimatedAiCostEur: 50.00 });

    expect(cards[3].subtitle).toBe('~€50.00 stimati');
  });

  it('should return placeholder cards when metrics is null', () => {
    const cards = buildKPICards(null);

    expect(cards).toHaveLength(4);
    expect(cards[0].value).toBe('-');
    expect(cards[1].value).toBe('-');
    expect(cards[2].value).toBe('-');
    expect(cards[3].value).toBe('-');
  });
});

describe('calculateTrendPercent', () => {
  it('should calculate positive trend', () => {
    const trendData = [
      // Previous period (days 1-7)
      { date: '2026-01-01', value: 100 },
      { date: '2026-01-02', value: 100 },
      { date: '2026-01-03', value: 100 },
      { date: '2026-01-04', value: 100 },
      { date: '2026-01-05', value: 100 },
      { date: '2026-01-06', value: 100 },
      { date: '2026-01-07', value: 100 },
      // Current period (days 8-14)
      { date: '2026-01-08', value: 150 },
      { date: '2026-01-09', value: 150 },
      { date: '2026-01-10', value: 150 },
      { date: '2026-01-11', value: 150 },
      { date: '2026-01-12', value: 150 },
      { date: '2026-01-13', value: 150 },
      { date: '2026-01-14', value: 150 },
    ];

    const trend = calculateTrendPercent(trendData, 7);
    expect(trend).toBe(50); // 50% increase
  });

  it('should calculate negative trend', () => {
    const trendData = [
      // Previous period
      { date: '2026-01-01', value: 200 },
      { date: '2026-01-02', value: 200 },
      { date: '2026-01-03', value: 200 },
      { date: '2026-01-04', value: 200 },
      { date: '2026-01-05', value: 200 },
      { date: '2026-01-06', value: 200 },
      { date: '2026-01-07', value: 200 },
      // Current period
      { date: '2026-01-08', value: 100 },
      { date: '2026-01-09', value: 100 },
      { date: '2026-01-10', value: 100 },
      { date: '2026-01-11', value: 100 },
      { date: '2026-01-12', value: 100 },
      { date: '2026-01-13', value: 100 },
      { date: '2026-01-14', value: 100 },
    ];

    const trend = calculateTrendPercent(trendData, 7);
    expect(trend).toBe(-50); // 50% decrease
  });

  it('should return undefined for insufficient data', () => {
    const trendData = [
      { date: '2026-01-01', value: 100 },
      { date: '2026-01-02', value: 100 },
    ];

    const trend = calculateTrendPercent(trendData, 7);
    expect(trend).toBeUndefined();
  });

  it('should return undefined for empty data', () => {
    const trend = calculateTrendPercent([], 7);
    expect(trend).toBeUndefined();
  });

  it('should return undefined for undefined data', () => {
    const trend = calculateTrendPercent(undefined, 7);
    expect(trend).toBeUndefined();
  });

  it('should handle zero previous sum', () => {
    const trendData = [
      // Previous period (all zeros)
      { date: '2026-01-01', value: 0 },
      { date: '2026-01-02', value: 0 },
      { date: '2026-01-03', value: 0 },
      { date: '2026-01-04', value: 0 },
      { date: '2026-01-05', value: 0 },
      { date: '2026-01-06', value: 0 },
      { date: '2026-01-07', value: 0 },
      // Current period
      { date: '2026-01-08', value: 100 },
      { date: '2026-01-09', value: 100 },
      { date: '2026-01-10', value: 100 },
      { date: '2026-01-11', value: 100 },
      { date: '2026-01-12', value: 100 },
      { date: '2026-01-13', value: 100 },
      { date: '2026-01-14', value: 100 },
    ];

    const trend = calculateTrendPercent(trendData, 7);
    expect(trend).toBe(100); // 100% when starting from 0
  });
});

describe('estimateAiCost', () => {
  it('should estimate cost for token usage', () => {
    const cost = estimateAiCost(1000000); // 1M tokens

    // Using ~$0.012 per 1K tokens * 0.92 EUR rate
    // 1000 * 0.012 * 0.92 ≈ €11.04
    expect(cost).toBeGreaterThan(10);
    expect(cost).toBeLessThan(15);
  });

  it('should return 0 for 0 tokens', () => {
    const cost = estimateAiCost(0);
    expect(cost).toBe(0);
  });

  it('should round to 2 decimal places', () => {
    const cost = estimateAiCost(123456);

    // Should be a number with at most 2 decimal places
    const decimals = (cost.toString().split('.')[1] || '').length;
    expect(decimals).toBeLessThanOrEqual(2);
  });
});
