import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { usePathname } from 'next/navigation';

import { KbTopBand } from '../KbTopBand';

vi.mock('next/navigation', () => ({
  usePathname: vi.fn(),
}));

const mockUsePathname = usePathname as ReturnType<typeof vi.fn>;

describe('KbTopBand', () => {
  it('renders a single h1 "Knowledge Base"', () => {
    mockUsePathname.mockReturnValue('/admin/knowledge-base/vectors');
    render(<KbTopBand />);
    const h1s = screen.getAllByRole('heading', { level: 1 });
    expect(h1s).toHaveLength(1);
    expect(h1s[0]).toHaveTextContent('Knowledge Base');
  });
  it('renders the Upload PDF action', () => {
    mockUsePathname.mockReturnValue('/admin/knowledge-base/vectors');
    render(<KbTopBand />);
    expect(screen.getByRole('link', { name: /Upload PDF/i })).toBeInTheDocument();
  });
});
