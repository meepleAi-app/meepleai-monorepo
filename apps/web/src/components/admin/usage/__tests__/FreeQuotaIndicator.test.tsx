/**
 * FreeQuotaIndicator unit tests.
 * Issue #5082: Admin usage page — free tier quota indicator.
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { FreeQuotaIndicator } from '../FreeQuotaIndicator';

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const mockData = {
  models: [
    {
      modelId:        'meta-llama/llama-3-8b-instruct:free',
      requestsToday:  30,
      dailyLimit:     50,
      percentUsed:    0.6,
      isExhausted:    false,
      nextResetUtc:   null,
    },
    {
      modelId:        'google/gemma-2-9b-it:free',
      requestsToday:  50,
      dailyLimit:     50,
      percentUsed:    1.0,
      isExhausted:    true,
      nextResetUtc:   '2026-02-23T00:00:00Z',
    },
  ],
  totalFreeRequestsToday: 80,
  generatedAt:            '2026-02-22T10:00:00Z',
};

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('FreeQuotaIndicator', () => {
  it('renders card title', () => {
    render(<FreeQuotaIndicator data={mockData} />);
    expect(screen.getByText('Free Tier Quota')).toBeInTheDocument();
  });

  it('shows total free requests today', () => {
    render(<FreeQuotaIndicator data={mockData} />);
    // Total is rendered as a standalone number in its own span
    expect(screen.getByText('80')).toBeInTheDocument();
    expect(screen.getByText('Total free requests today')).toBeInTheDocument();
  });

  it('renders short model names', () => {
    render(<FreeQuotaIndicator data={mockData} />);
    expect(screen.getByText('llama-3-8b-instruct:free')).toBeInTheDocument();
    expect(screen.getByText('gemma-2-9b-it:free')).toBeInTheDocument();
  });

  it('shows requests / limit for each model', () => {
    render(<FreeQuotaIndicator data={mockData} />);
    expect(screen.getByText('30 / 50')).toBeInTheDocument();
    expect(screen.getByText('50 / 50')).toBeInTheDocument();
  });

  it('renders progress bars for each model', () => {
    const { container } = render(<FreeQuotaIndicator data={mockData} />);
    // Each model row has a track div with h-2 rounded-full bg-muted overflow-hidden
    const tracks = container.querySelectorAll('.h-2.rounded-full.bg-muted');
    expect(tracks.length).toBe(2);
  });

  it('shows Exhausted label for exhausted model', () => {
    render(<FreeQuotaIndicator data={mockData} />);
    expect(screen.getByText(/Exhausted/)).toBeInTheDocument();
  });

  it('does not show Exhausted label when no model is exhausted', () => {
    const noExhausted = {
      ...mockData,
      models: [mockData.models[0]],
    };
    render(<FreeQuotaIndicator data={noExhausted} />);
    expect(screen.queryByText(/Exhausted/)).not.toBeInTheDocument();
  });

  it('shows exhausted reset time as UTC when nextResetUtc is provided', () => {
    render(<FreeQuotaIndicator data={mockData} />);
    expect(screen.getByText(/UTC/)).toBeInTheDocument();
  });

  it('shows loading skeleton when isLoading', () => {
    render(<FreeQuotaIndicator data={null} isLoading />);
    const skeletons = document.querySelectorAll('.animate-pulse');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('shows empty state when no models', () => {
    render(<FreeQuotaIndicator data={{ ...mockData, models: [] }} />);
    expect(screen.getByText('No free-model requests today')).toBeInTheDocument();
  });

  it('shows empty state when data is null and not loading', () => {
    render(<FreeQuotaIndicator data={null} />);
    expect(screen.getByText('No free-model requests today')).toBeInTheDocument();
  });
});
