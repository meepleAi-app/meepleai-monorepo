import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

vi.mock('next/navigation', () => ({
  usePathname: () => '/agents',
}));

import { MobileBottomBar } from '../MobileBottomBar';

describe('MobileBottomBar', () => {
  it('renders 5 tab items', () => {
    render(<MobileBottomBar />);
    expect(screen.getByText('Home')).toBeInTheDocument();
    expect(screen.getByText('Games')).toBeInTheDocument();
    expect(screen.getByText('AI')).toBeInTheDocument();
    expect(screen.getByText('Library')).toBeInTheDocument();
    expect(screen.getByText('Settings')).toBeInTheDocument();
  });

  it('renders navigation links', () => {
    render(<MobileBottomBar />);
    const links = screen.getAllByRole('link');
    expect(links).toHaveLength(5);
  });

  it('has accessible touch targets', () => {
    render(<MobileBottomBar />);
    const links = screen.getAllByRole('link');
    links.forEach(link => {
      expect(link.className).toContain('min-h-[44px]');
    });
  });
});
