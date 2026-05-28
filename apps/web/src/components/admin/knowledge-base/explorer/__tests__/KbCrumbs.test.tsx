import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { usePathname } from 'next/navigation';

import { KbCrumbs } from '../KbCrumbs';

vi.mock('next/navigation', () => ({
  usePathname: vi.fn(),
}));

const mockUsePathname = usePathname as ReturnType<typeof vi.fn>;

describe('KbCrumbs', () => {
  it('renders Explorer label at KB root', () => {
    mockUsePathname.mockReturnValue('/admin/knowledge-base');
    render(<KbCrumbs />);
    expect(screen.getByText(/Explorer/)).toBeInTheDocument();
  });
  it('renders Vector Collections label on /vectors', () => {
    mockUsePathname.mockReturnValue('/admin/knowledge-base/vectors');
    render(<KbCrumbs />);
    expect(screen.getByText(/Vector Collections/)).toBeInTheDocument();
  });
  it('renders Processing Queue label on /queue', () => {
    mockUsePathname.mockReturnValue('/admin/knowledge-base/queue');
    render(<KbCrumbs />);
    expect(screen.getByText(/Processing Queue/)).toBeInTheDocument();
  });
});
