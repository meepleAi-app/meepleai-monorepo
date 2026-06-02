/**
 * @vitest-environment jsdom
 *
 * AiTopBand — Issue #1722 PR 1/4.
 *
 * Header band rendered by /admin/ai layout. One h1 + AiCrumbs reactive
 * to ?tab= search param + AiTopActions placeholder.
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { useSearchParams } from 'next/navigation';

import { AiTopBand } from '../AiTopBand';

vi.mock('next/navigation', () => ({
  useSearchParams: vi.fn(),
}));

const mockUseSearchParams = useSearchParams as ReturnType<typeof vi.fn>;

function makeParams(tab?: string): URLSearchParams {
  const p = new URLSearchParams();
  if (tab) p.set('tab', tab);
  return p;
}

describe('AiTopBand', () => {
  it('renders a single h1 "AI & Agents"', () => {
    mockUseSearchParams.mockReturnValue(makeParams('agents'));
    render(<AiTopBand />);
    const h1s = screen.getAllByRole('heading', { level: 1 });
    expect(h1s).toHaveLength(1);
    expect(h1s[0]).toHaveTextContent('AI & Agents');
  });

  it('includes the crumb trail for the active tab', () => {
    mockUseSearchParams.mockReturnValue(makeParams('rag'));
    render(<AiTopBand />);
    expect(screen.getByText(/RAG/)).toBeInTheDocument();
  });
});
