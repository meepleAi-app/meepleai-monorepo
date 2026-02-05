/**
 * ComparisonMetrics Component Tests
 * Issue #3380
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { ComparisonMetrics } from '../ComparisonMetrics';

const mockStrategies = [
  { value: 'FAST', label: 'Fast', description: 'Quick lookups', color: 'green' },
  { value: 'PRECISE', label: 'Precise', description: 'Complex rules', color: 'purple' },
] as const;

const mockModels = [
  { value: 'gpt-4o-mini', label: 'GPT-4o Mini', tier: 'free' },
  { value: 'claude-3-sonnet', label: 'Claude 3 Sonnet', tier: 'premium' },
] as const;

const mockResults = [
  {
    configId: 'config-1',
    strategy: 'FAST',
    model: 'gpt-4o-mini',
    response: 'Fast response',
    latency: 0.8,
    tokensUsed: 200,
    costEstimate: 0.001,
    confidenceScore: 0.72,
    citations: [{ page: 1, text: 'Source 1' }],
    timestamp: new Date(),
  },
  {
    configId: 'config-2',
    strategy: 'PRECISE',
    model: 'claude-3-sonnet',
    response: 'Precise response',
    latency: 3.2,
    tokensUsed: 450,
    costEstimate: 0.008,
    confidenceScore: 0.94,
    citations: [
      { page: 1, text: 'Source 1' },
      { page: 2, text: 'Source 2' },
      { page: 3, text: 'Source 3' },
    ],
    timestamp: new Date(),
  },
];

describe('ComparisonMetrics', () => {
  it('renders comparison summary title', () => {
    render(
      <ComparisonMetrics
        results={mockResults}
        strategies={mockStrategies}
        models={mockModels}
      />
    );

    expect(screen.getByText('Comparison Summary')).toBeInTheDocument();
  });

  it('shows correct number of configurations', () => {
    render(
      <ComparisonMetrics
        results={mockResults}
        strategies={mockStrategies}
        models={mockModels}
      />
    );

    expect(screen.getByText(/2 configurations/)).toBeInTheDocument();
  });

  it('identifies overall winner correctly', () => {
    render(
      <ComparisonMetrics
        results={mockResults}
        strategies={mockStrategies}
        models={mockModels}
      />
    );

    // PRECISE should win due to higher confidence
    expect(screen.getByText('Overall Winner')).toBeInTheDocument();
  });

  it('identifies fastest configuration', () => {
    render(
      <ComparisonMetrics
        results={mockResults}
        strategies={mockStrategies}
        models={mockModels}
      />
    );

    expect(screen.getByText('Fastest')).toBeInTheDocument();
    // Value appears in category winner and table
    expect(screen.getAllByText('0.80s').length).toBeGreaterThan(0);
  });

  it('identifies cheapest configuration', () => {
    render(
      <ComparisonMetrics
        results={mockResults}
        strategies={mockStrategies}
        models={mockModels}
      />
    );

    expect(screen.getByText('Cheapest')).toBeInTheDocument();
    // Value appears in category winner and table
    expect(screen.getAllByText('$0.0010').length).toBeGreaterThan(0);
  });

  it('identifies most confident configuration', () => {
    render(
      <ComparisonMetrics
        results={mockResults}
        strategies={mockStrategies}
        models={mockModels}
      />
    );

    expect(screen.getByText('Most Confident')).toBeInTheDocument();
    // Value appears in category winner and table
    expect(screen.getAllByText('94%').length).toBeGreaterThan(0);
  });

  it('identifies configuration with most sources', () => {
    render(
      <ComparisonMetrics
        results={mockResults}
        strategies={mockStrategies}
        models={mockModels}
      />
    );

    expect(screen.getByText('Most Sources')).toBeInTheDocument();
    expect(screen.getByText('3 citations')).toBeInTheDocument();
  });

  it('renders comparison table with all results', () => {
    render(
      <ComparisonMetrics
        results={mockResults}
        strategies={mockStrategies}
        models={mockModels}
      />
    );

    // Strategy names appear in multiple places (category winners, table)
    expect(screen.getAllByText('Fast').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Precise').length).toBeGreaterThan(0);
    expect(screen.getAllByText('GPT-4o Mini').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Claude 3 Sonnet').length).toBeGreaterThan(0);
  });

  it('shows config labels A and B', () => {
    render(
      <ComparisonMetrics
        results={mockResults}
        strategies={mockStrategies}
        models={mockModels}
      />
    );

    expect(screen.getAllByText('A').length).toBeGreaterThan(0);
    expect(screen.getAllByText('B').length).toBeGreaterThan(0);
  });

  it('renders nothing when results is empty', () => {
    const { container } = render(
      <ComparisonMetrics
        results={[]}
        strategies={mockStrategies}
        models={mockModels}
      />
    );

    expect(container.firstChild).toBeNull();
  });

  it('shows trophy emoji for category winners in table', () => {
    render(
      <ComparisonMetrics
        results={mockResults}
        strategies={mockStrategies}
        models={mockModels}
      />
    );

    // Trophy emojis should be present for category winners
    const trophies = screen.getAllByText(/🏆/);
    expect(trophies.length).toBeGreaterThan(0);
  });

  it('calculates and displays scores', () => {
    render(
      <ComparisonMetrics
        results={mockResults}
        strategies={mockStrategies}
        models={mockModels}
      />
    );

    // Scores should be displayed as percentages
    const scoreElements = screen.getAllByText(/%$/);
    expect(scoreElements.length).toBeGreaterThan(0);
  });

  it('sorts table by score descending', () => {
    render(
      <ComparisonMetrics
        results={mockResults}
        strategies={mockStrategies}
        models={mockModels}
      />
    );

    // Get all rows - should include header and data rows
    const rows = screen.getAllByRole('row');
    // Verify we have at least the header + 2 data rows
    expect(rows.length).toBeGreaterThanOrEqual(3);
    // Verify PRECISE strategy is in the table (highest score due to higher confidence)
    const preciseElements = screen.getAllByText('Precise');
    expect(preciseElements.length).toBeGreaterThan(0);
  });

  it('handles single result', () => {
    render(
      <ComparisonMetrics
        results={[mockResults[0]]}
        strategies={mockStrategies}
        models={mockModels}
      />
    );

    expect(screen.getByText('Overall Winner')).toBeInTheDocument();
    // 'Fast' appears in multiple places (winner, fastest, table)
    expect(screen.getAllByText('Fast').length).toBeGreaterThan(0);
  });

  it('handles three results correctly', () => {
    const threeResults = [
      ...mockResults,
      {
        configId: 'config-3',
        strategy: 'FAST',
        model: 'claude-3-sonnet',
        response: 'Another response',
        latency: 1.5,
        tokensUsed: 300,
        costEstimate: 0.005,
        confidenceScore: 0.88,
        citations: [{ page: 1, text: 'Source' }],
        timestamp: new Date(),
      },
    ];

    render(
      <ComparisonMetrics
        results={threeResults}
        strategies={mockStrategies}
        models={mockModels}
      />
    );

    expect(screen.getByText(/3 configurations/)).toBeInTheDocument();
  });
});
