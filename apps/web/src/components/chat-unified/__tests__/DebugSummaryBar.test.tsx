/**
 * DebugSummaryBar Tests
 * Issue #4916: Tab Debug sulla chat — summary metrics bar
 *
 * Tests:
 * 1. Returns null when no steps
 * 2. Shows total latency when retrieval step present
 * 3. Shows token counts from DebugCostUpdate
 * 4. Shows confidence percentage from DebugCostUpdate
 * 5. Shows KB chunks from DebugRetrievalResults
 * 6. Shows model name from DebugCostUpdate
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import React from 'react';

import { DebugSummaryBar } from '../DebugSummaryBar';
import type { DebugStep } from '@/hooks/useAgentChatStream';

// ─── Helpers ──────────────────────────────────────────────────────────────────

function makeRetrievalStartStep(latencyMs = 0): DebugStep {
  return {
    type: 12,
    name: 'Retrieval Start',
    payload: {},
    timestamp: '2026-01-01T12:00:00.000Z',
    latencyMs,
  };
}

function makeRetrievalResultsStep(filteredCount = 5, latencyMs = 150): DebugStep {
  return {
    type: 13,
    name: 'Retrieval Results',
    payload: { filteredCount, totalResults: 10, latencyMs },
    timestamp: '2026-01-01T12:00:01.000Z',
    latencyMs,
  };
}

function makeCostUpdateStep(overrides: Record<string, unknown> = {}): DebugStep {
  return {
    type: 17,
    name: 'Cost Update',
    payload: {
      model: 'claude-3-haiku',
      promptTokens: 500,
      completionTokens: 200,
      totalTokens: 700,
      confidence: 0.85,
      ...overrides,
    },
    timestamp: '2026-01-01T12:00:02.000Z',
  };
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('DebugSummaryBar', () => {
  it('renders nothing when steps array is empty', () => {
    const { container } = render(<DebugSummaryBar steps={[]} />);
    expect(container.firstChild).toBeNull();
  });

  it('shows summed latency from steps with latencyMs', () => {
    render(
      <DebugSummaryBar steps={[makeRetrievalResultsStep(5, 150), makeRetrievalStartStep(50)]} />
    );
    // 150 + 50 = 200ms total
    expect(screen.getByText('200ms')).toBeInTheDocument();
  });

  it('shows token in/out from DebugCostUpdate step', () => {
    render(
      <DebugSummaryBar steps={[makeCostUpdateStep({ promptTokens: 400, completionTokens: 300 })]} />
    );
    expect(screen.getByText('400 / 300')).toBeInTheDocument();
  });

  it('shows confidence as percentage', () => {
    render(<DebugSummaryBar steps={[makeCostUpdateStep({ confidence: 0.92 })]} />);
    expect(screen.getByText('92%')).toBeInTheDocument();
  });

  it('shows KB chunk count from DebugRetrievalResults', () => {
    render(<DebugSummaryBar steps={[makeRetrievalResultsStep(7)]} />);
    expect(screen.getByText('7')).toBeInTheDocument();
  });

  it('shows model name from DebugCostUpdate', () => {
    render(<DebugSummaryBar steps={[makeCostUpdateStep({ model: 'claude-3-sonnet' })]} />);
    expect(screen.getByText('claude-3-sonnet')).toBeInTheDocument();
  });

  it('does not show confidence section when confidence is null', () => {
    render(<DebugSummaryBar steps={[makeCostUpdateStep({ confidence: null })]} />);
    expect(screen.queryByText(/%/)).not.toBeInTheDocument();
  });

  it('shows typology name from DebugTypologyProfile step', () => {
    const typologyStep: DebugStep = {
      type: 22,
      name: 'Typology Profile',
      payload: { typology: 'Tutor', topK: 8, minScore: 0.5, temperature: 0.6 },
      timestamp: '2026-01-01T12:00:00.000Z',
    };
    render(<DebugSummaryBar steps={[typologyStep]} />);
    expect(screen.getByText('Tutor')).toBeInTheDocument();
  });

  it('does not show typology when no DebugTypologyProfile step', () => {
    render(<DebugSummaryBar steps={[makeRetrievalStartStep(100)]} />);
    expect(screen.queryByText('Typology')).not.toBeInTheDocument();
  });

  it('uses last DebugCostUpdate when multiple present', () => {
    render(
      <DebugSummaryBar
        steps={[
          makeCostUpdateStep({ promptTokens: 100, completionTokens: 50 }),
          makeCostUpdateStep({ promptTokens: 600, completionTokens: 250 }),
        ]}
      />
    );
    // Should show the last one
    expect(screen.getByText('600 / 250')).toBeInTheDocument();
  });
});
