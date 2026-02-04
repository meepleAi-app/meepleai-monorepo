import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { AccuracyScore } from '../AccuracyScore';
import type { AccuracyMetrics } from '../types';

const mockAccuracyData: AccuracyMetrics = {
  overallScore: 92,
  byStrategy: {
    Hybrid: 94,
    Semantic: 91,
    Keyword: 78,
    Contextual: 89,
    MultiQuery: 93,
    Agentic: 96,
  },
  citationAccuracy: 88,
  userFeedbackScore: 4.5,
};

describe('AccuracyScore', () => {
  it('renders the component title', () => {
    render(<AccuracyScore data={mockAccuracyData} />);
    expect(screen.getByText('Accuracy')).toBeInTheDocument();
  });

  it('displays overall score', () => {
    render(<AccuracyScore data={mockAccuracyData} />);
    expect(screen.getByText('Overall Score')).toBeInTheDocument();
    expect(screen.getByText('92%')).toBeInTheDocument();
  });

  it('shows user feedback score', () => {
    render(<AccuracyScore data={mockAccuracyData} />);
    expect(screen.getByText('User Feedback')).toBeInTheDocument();
    expect(screen.getByText('4.5/5')).toBeInTheDocument();
  });

  it('displays strategy breakdown section', () => {
    render(<AccuracyScore data={mockAccuracyData} />);
    expect(screen.getByText('By Strategy')).toBeInTheDocument();
  });

  it('shows first 4 strategies', () => {
    render(<AccuracyScore data={mockAccuracyData} />);
    // The component shows first 4 strategies from RETRIEVAL_STRATEGY_ORDER
    // Hybrid, Semantic, Keyword, Contextual
    expect(screen.getByText('Hybrid')).toBeInTheDocument();
    expect(screen.getByText('94%')).toBeInTheDocument();
  });

  it('displays citation accuracy', () => {
    render(<AccuracyScore data={mockAccuracyData} />);
    expect(screen.getByText('Citation Accuracy')).toBeInTheDocument();
    expect(screen.getByText('88%')).toBeInTheDocument();
  });

  it('applies custom className', () => {
    const { container } = render(
      <AccuracyScore data={mockAccuracyData} className="custom-class" />
    );
    expect(container.firstChild).toHaveClass('custom-class');
  });

  it('handles low overall score', () => {
    const lowScoreData: AccuracyMetrics = {
      ...mockAccuracyData,
      overallScore: 55,
    };
    render(<AccuracyScore data={lowScoreData} />);
    expect(screen.getByText('55%')).toBeInTheDocument();
  });

  it('handles high overall score', () => {
    const highScoreData: AccuracyMetrics = {
      ...mockAccuracyData,
      overallScore: 98,
    };
    render(<AccuracyScore data={highScoreData} />);
    expect(screen.getByText('98%')).toBeInTheDocument();
  });

  it('renders strategy progress bars', () => {
    const { container } = render(<AccuracyScore data={mockAccuracyData} />);
    // Check for progress bar containers
    const progressBars = container.querySelectorAll('.bg-muted.rounded-full');
    expect(progressBars.length).toBeGreaterThan(0);
  });
});
