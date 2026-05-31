/**
 * @vitest-environment jsdom
 *
 * AiCrumbs — Issue #1722 PR 1/4.
 *
 * Reads ?tab= from search params, renders "Admin · AI · <label>".
 * Falls back to "Agents" when no tab is set (default).
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { useSearchParams } from 'next/navigation';

import { AiCrumbs } from '../AiCrumbs';

vi.mock('next/navigation', () => ({
  useSearchParams: vi.fn(),
}));

const mockUseSearchParams = useSearchParams as ReturnType<typeof vi.fn>;

function makeParams(tab?: string): URLSearchParams {
  const p = new URLSearchParams();
  if (tab) p.set('tab', tab);
  return p;
}

describe('AiCrumbs', () => {
  it('falls back to "Agents" when no tab is set', () => {
    mockUseSearchParams.mockReturnValue(makeParams());
    render(<AiCrumbs />);
    expect(screen.getByText(/Agents/)).toBeInTheDocument();
  });

  it('renders the RAG label for ?tab=rag', () => {
    mockUseSearchParams.mockReturnValue(makeParams('rag'));
    render(<AiCrumbs />);
    expect(screen.getByText(/RAG/)).toBeInTheDocument();
  });

  it('renders the Requests label for ?tab=requests', () => {
    mockUseSearchParams.mockReturnValue(makeParams('requests'));
    render(<AiCrumbs />);
    expect(screen.getByText(/Requests/)).toBeInTheDocument();
  });

  it('renders the Config label for ?tab=config', () => {
    mockUseSearchParams.mockReturnValue(makeParams('config'));
    render(<AiCrumbs />);
    expect(screen.getByText(/Config/)).toBeInTheDocument();
  });

  it('falls back to "Agents" for an unknown tab', () => {
    mockUseSearchParams.mockReturnValue(makeParams('does-not-exist'));
    render(<AiCrumbs />);
    expect(screen.getByText(/Agents/)).toBeInTheDocument();
  });
});
