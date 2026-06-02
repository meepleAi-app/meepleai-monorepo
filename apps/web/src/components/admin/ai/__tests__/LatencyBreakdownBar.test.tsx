/**
 * @vitest-environment jsdom
 *
 * LatencyBreakdownBar — Issue #1722 PR 4/4.
 *
 * Segmented horizontal bar showing how a single AI query's latency
 * splits across pipeline stages (retrieval / rerank / llm / post).
 * When the drill endpoint doesn't include breakdown info, the
 * component renders an "unavailable" fallback with the total only.
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { LatencyBreakdownBar, type LatencyBreakdown } from '../LatencyBreakdownBar';

const sample: LatencyBreakdown = {
  retrievalMs: 200,
  rerankMs: 100,
  llmMs: 600,
  postMs: 100,
};

describe('LatencyBreakdownBar', () => {
  it('renders one segment per stage when breakdown is provided', () => {
    render(<LatencyBreakdownBar breakdown={sample} totalMs={1000} />);
    const segments = screen.getAllByRole('listitem');
    expect(segments).toHaveLength(4);
  });

  it('labels each segment with its stage name and duration', () => {
    render(<LatencyBreakdownBar breakdown={sample} totalMs={1000} />);
    expect(screen.getByText(/retrieval/i)).toBeInTheDocument();
    expect(screen.getByText(/rerank/i)).toBeInTheDocument();
    expect(screen.getByText(/llm/i)).toBeInTheDocument();
    expect(screen.getByText(/post/i)).toBeInTheDocument();
    expect(screen.getByText(/200\s*ms/)).toBeInTheDocument();
    expect(screen.getByText(/600\s*ms/)).toBeInTheDocument();
  });

  it('renders the total in the header', () => {
    render(<LatencyBreakdownBar breakdown={sample} totalMs={1000} />);
    expect(screen.getByText(/1000\s*ms/)).toBeInTheDocument();
  });

  it('sets segment widths proportional to their share of the total', () => {
    const { container } = render(<LatencyBreakdownBar breakdown={sample} totalMs={1000} />);
    const llmSegment = container.querySelector('[data-stage="llm"]') as HTMLElement | null;
    expect(llmSegment?.style.width).toBe('60%'); // 600 / 1000
  });

  it('falls back to the unavailable state when breakdown is null', () => {
    render(<LatencyBreakdownBar breakdown={null} totalMs={842} />);
    expect(screen.getByText(/breakdown unavailable/i)).toBeInTheDocument();
    expect(screen.getByText(/842\s*ms/)).toBeInTheDocument();
  });

  it('exposes a list semantics with descriptive label', () => {
    render(<LatencyBreakdownBar breakdown={sample} totalMs={1000} />);
    expect(screen.getByRole('list', { name: /latency breakdown/i })).toBeInTheDocument();
  });
});
