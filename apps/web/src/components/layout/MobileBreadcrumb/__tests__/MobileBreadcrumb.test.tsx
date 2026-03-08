/**
 * MobileBreadcrumb Tests
 * Issue #5 from mobile-first-ux-epic.md
 */

import React from 'react';
import { render, screen } from '@testing-library/react';
import { vi, describe, it, expect, beforeEach } from 'vitest';

import { MobileBreadcrumb } from '../MobileBreadcrumb';

// ─── Mocks ───────────────────────────────────────────────────────────────────

const mockPathname = vi.fn(() => '/');

vi.mock('next/navigation', () => ({
  usePathname: () => mockPathname(),
}));

vi.mock('next/link', () => ({
  default: ({
    children,
    href,
    ...props
  }: {
    children: React.ReactNode;
    href: string;
    [key: string]: unknown;
  }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('MobileBreadcrumb', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Visibility', () => {
    it('does not render on root path', () => {
      mockPathname.mockReturnValue('/');
      const { container } = render(<MobileBreadcrumb />);
      expect(container.innerHTML).toBe('');
    });

    it('does not render on single-segment path', () => {
      mockPathname.mockReturnValue('/library');
      const { container } = render(<MobileBreadcrumb />);
      expect(container.innerHTML).toBe('');
    });

    it('renders on two-segment path', () => {
      mockPathname.mockReturnValue('/library/private');
      render(<MobileBreadcrumb />);
      expect(screen.getByTestId('mobile-breadcrumb')).toBeInTheDocument();
    });

    it('renders on deep paths', () => {
      mockPathname.mockReturnValue('/admin/agents/builder');
      render(<MobileBreadcrumb />);
      expect(screen.getByTestId('mobile-breadcrumb')).toBeInTheDocument();
    });
  });

  describe('Labels', () => {
    it('shows configured labels from breadcrumb-labels', () => {
      mockPathname.mockReturnValue('/library/private');
      render(<MobileBreadcrumb />);
      expect(screen.getByText('Libreria')).toBeInTheDocument();
      expect(screen.getByText('Giochi Privati')).toBeInTheDocument();
    });

    it('title-cases unknown segments', () => {
      mockPathname.mockReturnValue('/library/custom-section');
      render(<MobileBreadcrumb />);
      expect(screen.getByText('Custom Section')).toBeInTheDocument();
    });

    it('shows last two segments for deep paths', () => {
      mockPathname.mockReturnValue('/admin/agents/builder');
      render(<MobileBreadcrumb />);
      // Parent = Agenti, Current = Builder
      expect(screen.getByText('Agenti')).toBeInTheDocument();
      expect(screen.getByText('Builder')).toBeInTheDocument();
    });
  });

  describe('Navigation', () => {
    it('back arrow links to parent path', () => {
      mockPathname.mockReturnValue('/library/private');
      render(<MobileBreadcrumb />);
      const backLink = screen.getByLabelText('Torna a Libreria');
      expect(backLink).toHaveAttribute('href', '/library');
    });

    it('parent label links to parent path', () => {
      mockPathname.mockReturnValue('/admin/agents/builder');
      render(<MobileBreadcrumb />);
      const parentLink = screen.getByText('Agenti');
      expect(parentLink.closest('a')).toHaveAttribute('href', '/admin/agents');
    });

    it('current segment is not a link', () => {
      mockPathname.mockReturnValue('/library/private');
      render(<MobileBreadcrumb />);
      const current = screen.getByText('Giochi Privati');
      expect(current.closest('a')).toBeNull();
    });
  });

  describe('UUID filtering', () => {
    it('skips UUID segments in breadcrumb', () => {
      mockPathname.mockReturnValue('/games/a1b2c3d4-e5f6-7890-abcd-ef1234567890');
      const { container } = render(<MobileBreadcrumb />);
      // Only "games" remains, which is a single segment → not rendered
      expect(container.innerHTML).toBe('');
    });

    it('handles path with UUID in middle', () => {
      mockPathname.mockReturnValue('/library/a1b2c3d4-e5f6-7890-abcd-ef1234567890/edit');
      render(<MobileBreadcrumb />);
      // Segments: library, edit (UUID skipped)
      expect(screen.getByText('Libreria')).toBeInTheDocument();
      expect(screen.getByText('Modifica')).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('has role="navigation" with aria-label', () => {
      mockPathname.mockReturnValue('/library/private');
      render(<MobileBreadcrumb />);
      const nav = screen.getByRole('navigation', { name: 'Breadcrumb' });
      expect(nav).toBeInTheDocument();
    });

    it('marks current page with aria-current', () => {
      mockPathname.mockReturnValue('/library/private');
      render(<MobileBreadcrumb />);
      const current = screen.getByText('Giochi Privati');
      expect(current).toHaveAttribute('aria-current', 'page');
    });

    it('back button has descriptive aria-label', () => {
      mockPathname.mockReturnValue('/admin/agents/builder');
      render(<MobileBreadcrumb />);
      expect(screen.getByLabelText('Torna a Agenti')).toBeInTheDocument();
    });
  });

  describe('Responsive', () => {
    it('has md:hidden class for mobile-only', () => {
      mockPathname.mockReturnValue('/library/private');
      render(<MobileBreadcrumb />);
      const nav = screen.getByTestId('mobile-breadcrumb');
      expect(nav.className).toContain('md:hidden');
    });
  });
});
