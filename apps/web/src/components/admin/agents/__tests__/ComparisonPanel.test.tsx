/**
 * ComparisonPanel Component Tests
 * Issue #3380
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { ComparisonPanel } from '../ComparisonPanel';

const mockStrategies = [
  { value: 'FAST', label: 'Fast', description: 'Quick lookups', color: 'green' },
  { value: 'PRECISE', label: 'Precise', description: 'Complex rules', color: 'purple' },
] as const;

const mockModels = [
  { value: 'gpt-4o-mini', label: 'GPT-4o Mini', tier: 'free' },
  { value: 'claude-3-sonnet', label: 'Claude 3 Sonnet', tier: 'premium' },
] as const;

const mockResult = {
  configId: 'test-1',
  strategy: 'FAST',
  model: 'gpt-4o-mini',
  response: 'This is a test response about game rules.',
  latency: 1.25,
  tokensUsed: 350,
  costEstimate: 0.0015,
  confidenceScore: 0.85,
  citations: [
    { page: 12, text: 'Section 4.2: Game Setup' },
    { page: 15, text: 'Section 5.1: Turn Structure' },
  ],
  timestamp: new Date('2026-02-05T10:00:00Z'),
};

describe('ComparisonPanel', () => {
  it('renders strategy label', () => {
    render(
      <ComparisonPanel
        result={mockResult}
        strategies={mockStrategies}
        models={mockModels}
      />
    );

    expect(screen.getByText('Fast')).toBeInTheDocument();
  });

  it('renders model label and tier', () => {
    render(
      <ComparisonPanel
        result={mockResult}
        strategies={mockStrategies}
        models={mockModels}
      />
    );

    expect(screen.getByText('GPT-4o Mini')).toBeInTheDocument();
    expect(screen.getByText('free')).toBeInTheDocument();
  });

  it('renders response text', () => {
    render(
      <ComparisonPanel
        result={mockResult}
        strategies={mockStrategies}
        models={mockModels}
      />
    );

    expect(screen.getByText('This is a test response about game rules.')).toBeInTheDocument();
  });

  it('renders latency metric', () => {
    render(
      <ComparisonPanel
        result={mockResult}
        strategies={mockStrategies}
        models={mockModels}
      />
    );

    expect(screen.getByText('1.25s')).toBeInTheDocument();
    expect(screen.getByText('Latency')).toBeInTheDocument();
  });

  it('renders cost metric', () => {
    render(
      <ComparisonPanel
        result={mockResult}
        strategies={mockStrategies}
        models={mockModels}
      />
    );

    expect(screen.getByText('$0.0015')).toBeInTheDocument();
    expect(screen.getByText('Cost')).toBeInTheDocument();
  });

  it('renders tokens metric', () => {
    render(
      <ComparisonPanel
        result={mockResult}
        strategies={mockStrategies}
        models={mockModels}
      />
    );

    expect(screen.getByText('350')).toBeInTheDocument();
    expect(screen.getByText('Tokens')).toBeInTheDocument();
  });

  it('renders confidence score', () => {
    render(
      <ComparisonPanel
        result={mockResult}
        strategies={mockStrategies}
        models={mockModels}
      />
    );

    expect(screen.getByText('85%')).toBeInTheDocument();
    expect(screen.getByText('Confidence')).toBeInTheDocument();
  });

  it('renders citations count', () => {
    render(
      <ComparisonPanel
        result={mockResult}
        strategies={mockStrategies}
        models={mockModels}
      />
    );

    expect(screen.getByText('2')).toBeInTheDocument();
    expect(screen.getByText('Citations')).toBeInTheDocument();
  });

  it('renders citation badges', () => {
    render(
      <ComparisonPanel
        result={mockResult}
        strategies={mockStrategies}
        models={mockModels}
      />
    );

    expect(screen.getByText('p.12')).toBeInTheDocument();
    expect(screen.getByText('p.15')).toBeInTheDocument();
  });

  it('shows winner badge when isWinner is true', () => {
    render(
      <ComparisonPanel
        result={mockResult}
        strategies={mockStrategies}
        models={mockModels}
        isWinner={true}
      />
    );

    expect(screen.getByText('Winner')).toBeInTheDocument();
  });

  it('does not show winner badge when isWinner is false', () => {
    render(
      <ComparisonPanel
        result={mockResult}
        strategies={mockStrategies}
        models={mockModels}
        isWinner={false}
      />
    );

    expect(screen.queryByText('Winner')).not.toBeInTheDocument();
  });

  it('applies winner styling when isWinner is true', () => {
    const { container } = render(
      <ComparisonPanel
        result={mockResult}
        strategies={mockStrategies}
        models={mockModels}
        isWinner={true}
      />
    );

    const card = container.querySelector('.ring-2.ring-yellow-400');
    expect(card).toBeInTheDocument();
  });

  it('handles result with many citations showing +more badge', () => {
    const resultWithManyCitations = {
      ...mockResult,
      citations: [
        { page: 1, text: 'Page 1' },
        { page: 2, text: 'Page 2' },
        { page: 3, text: 'Page 3' },
        { page: 4, text: 'Page 4' },
        { page: 5, text: 'Page 5' },
      ],
    };

    render(
      <ComparisonPanel
        result={resultWithManyCitations}
        strategies={mockStrategies}
        models={mockModels}
      />
    );

    expect(screen.getByText('+2 more')).toBeInTheDocument();
  });

  it('handles high confidence score with green color', () => {
    const highConfidenceResult = { ...mockResult, confidenceScore: 0.92 };

    const { container } = render(
      <ComparisonPanel
        result={highConfidenceResult}
        strategies={mockStrategies}
        models={mockModels}
      />
    );

    const confidenceText = container.querySelector('.text-green-500');
    expect(confidenceText).toBeInTheDocument();
  });

  it('handles medium confidence score with yellow color', () => {
    const medConfidenceResult = { ...mockResult, confidenceScore: 0.75 };

    const { container } = render(
      <ComparisonPanel
        result={medConfidenceResult}
        strategies={mockStrategies}
        models={mockModels}
      />
    );

    const confidenceText = container.querySelector('.text-yellow-500');
    expect(confidenceText).toBeInTheDocument();
  });

  it('handles low confidence score with red color', () => {
    const lowConfidenceResult = { ...mockResult, confidenceScore: 0.55 };

    const { container } = render(
      <ComparisonPanel
        result={lowConfidenceResult}
        strategies={mockStrategies}
        models={mockModels}
      />
    );

    const confidenceText = container.querySelector('.text-red-500');
    expect(confidenceText).toBeInTheDocument();
  });
});
