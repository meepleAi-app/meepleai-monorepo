/**
 * DebugStepCard Tests
 * Issue #4916: Tab Debug sulla chat — single pipeline step card
 *
 * Tests:
 * 1. Renders step name and index
 * 2. Collapsed by default (no payload visible)
 * 3. Expands on click to show JSON payload
 * 4. Shows latency when available
 * 5. Redacts systemPrompt for non-admin users (DebugPromptContext type=16)
 * 6. Shows systemPrompt for admin users
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import React from 'react';

import { DebugStepCard } from '../DebugStepCard';
import type { DebugStep } from '@/hooks/useAgentChatStream';

// ─── Helpers ──────────────────────────────────────────────────────────────────

function makeStep(overrides: Partial<DebugStep> = {}): DebugStep {
  return {
    type: 13, // DebugRetrievalResults
    name: 'Retrieval Results',
    payload: { filteredCount: 5, totalResults: 10, latencyMs: 42 },
    timestamp: '2026-01-01T12:00:00.000Z',
    latencyMs: 42,
    ...overrides,
  };
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('DebugStepCard', () => {
  it('renders step index and name', () => {
    render(<DebugStepCard step={makeStep()} index={0} />);
    expect(screen.getByText(/1\. Retrieval Results/i)).toBeInTheDocument();
  });

  it('is collapsed by default (no JSON visible)', () => {
    render(<DebugStepCard step={makeStep()} index={0} />);
    expect(screen.queryByText(/"filteredCount"/)).not.toBeInTheDocument();
  });

  it('expands when clicked to show JSON payload', () => {
    render(<DebugStepCard step={makeStep()} index={0} />);
    fireEvent.click(screen.getByRole('button'));
    expect(screen.getByText(/"filteredCount"/)).toBeInTheDocument();
  });

  it('collapses again on second click', () => {
    render(<DebugStepCard step={makeStep()} index={0} />);
    const btn = screen.getByRole('button');
    fireEvent.click(btn);
    expect(screen.getByText(/"filteredCount"/)).toBeInTheDocument();
    fireEvent.click(btn);
    expect(screen.queryByText(/"filteredCount"/)).not.toBeInTheDocument();
  });

  it('shows latencyMs when available', () => {
    render(<DebugStepCard step={makeStep({ latencyMs: 123 })} index={0} />);
    expect(screen.getByText('123ms')).toBeInTheDocument();
  });

  it('does not show latency when not available', () => {
    render(<DebugStepCard step={makeStep({ latencyMs: undefined })} index={0} />);
    expect(screen.queryByText(/ms/)).not.toBeInTheDocument();
  });

  it('redacts systemPrompt for non-admin (type=16, showSystemPrompt=false)', () => {
    const step: DebugStep = {
      type: 16,
      name: 'Prompt Context',
      payload: {
        systemPrompt: 'secret system instructions',
        userPromptPreview: 'user question',
        estimatedPromptTokens: 200,
      },
      timestamp: '2026-01-01T12:00:00.000Z',
    };

    render(<DebugStepCard step={step} index={0} showSystemPrompt={false} />);
    fireEvent.click(screen.getByRole('button'));
    expect(screen.getByText(/\[redacted\]/)).toBeInTheDocument();
    expect(screen.queryByText(/secret system instructions/)).not.toBeInTheDocument();
  });

  it('shows systemPrompt for admin (type=16, showSystemPrompt=true)', () => {
    const step: DebugStep = {
      type: 16,
      name: 'Prompt Context',
      payload: {
        systemPrompt: 'secret system instructions',
        userPromptPreview: 'user question',
        estimatedPromptTokens: 200,
      },
      timestamp: '2026-01-01T12:00:00.000Z',
    };

    render(<DebugStepCard step={step} index={0} showSystemPrompt={true} />);
    fireEvent.click(screen.getByRole('button'));
    expect(screen.getByText(/secret system instructions/)).toBeInTheDocument();
    expect(screen.queryByText(/\[redacted\]/)).not.toBeInTheDocument();
  });

  it('renders TypologyProfileView for type 22 instead of JSON', () => {
    const step: DebugStep = {
      type: 22,
      name: 'Typology Profile',
      payload: {
        typology: 'Arbitro',
        topK: 5,
        minScore: 0.7,
        searchStrategy: 'Precise',
        temperature: 0.3,
        maxTokens: 1024,
      },
      timestamp: '2026-01-01T12:00:00.000Z',
    };

    render(<DebugStepCard step={step} index={0} />);
    fireEvent.click(screen.getByRole('button'));
    expect(screen.getByTestId('typology-profile-view')).toBeInTheDocument();
    expect(screen.getByText('Arbitro Profile')).toBeInTheDocument();
    expect(screen.getByText(/TopK: 5/)).toBeInTheDocument();
    expect(screen.getByText(/MinScore: 0.7/)).toBeInTheDocument();
    expect(screen.getByText(/Temp: 0.3/)).toBeInTheDocument();
  });

  it('renders correct step index for non-zero index', () => {
    render(<DebugStepCard step={makeStep({ name: 'Cost Update', type: 17 })} index={3} />);
    expect(screen.getByText(/4\. Cost Update/i)).toBeInTheDocument();
  });
});
