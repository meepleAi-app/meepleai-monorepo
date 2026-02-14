/**
 * TagStrip Test Suite
 * Issue #4181 - Vertical Tag Component
 *
 * Tests for TagStrip, TagBadge, and TagOverflow components
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { TagBadge } from '../TagBadge';
import { TagOverflow } from '../TagOverflow';
import { TagStrip } from '../TagStrip';
import { gameTagPresets, agentTagPresets, documentTagPresets } from '../tag-presets';

describe('TagBadge', () => {
  describe('Rendering', () => {
    it('renders with preset key', () => {
      render(<TagBadge tag="new" />);
      expect(screen.getByTestId('tag-badge-new')).toBeInTheDocument();
    });

    it('renders with custom config', () => {
      render(<TagBadge tag={gameTagPresets.sale} />);
      expect(screen.getByTestId('tag-badge-sale')).toBeInTheDocument();
    });

    it('returns null for unknown preset', () => {
      const { container } = render(<TagBadge tag={'unknown' as any} />);
      expect(container.firstChild).toBeNull();
    });
  });

  describe('Variants', () => {
    it('renders desktop variant with full label', () => {
      const { container } = render(<TagBadge tag="new" variant="desktop" />);
      const badge = container.querySelector('[data-testid="tag-badge-new"]');
      expect(badge).toHaveClass('w-8');
      expect(badge).toHaveTextContent('New');
    });

    it('renders tablet variant with abbreviated label', () => {
      const { container } = render(<TagBadge tag="wishlist" variant="tablet" />);
      const badge = container.querySelector('[data-testid="tag-badge-wishlist"]');
      expect(badge).toHaveClass('w-7');
      expect(badge).toHaveTextContent('Wish');
    });

    it('renders mobile variant with icon only', () => {
      const { container } = render(<TagBadge tag="owned" variant="mobile" />);
      const badge = container.querySelector('[data-testid="tag-badge-owned"]');
      expect(badge).toHaveClass('w-6');
      // Icon present but no text label
      const svg = badge?.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });
  });

  describe('Icon Display', () => {
    it('shows icon by default', () => {
      const { container } = render(<TagBadge tag="rag" showIcon={true} />);
      const svg = container.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });

    it('hides icon when showIcon=false', () => {
      const { container } = render(<TagBadge tag="rag" showIcon={false} />);
      const svg = container.querySelector('svg');
      expect(svg).not.toBeInTheDocument();
    });

    it('icon-only mode hides label', () => {
      const { container } = render(<TagBadge tag="new" iconOnly={true} />);
      const badge = container.querySelector('[data-testid="tag-badge-new"]');
      expect(badge).not.toHaveTextContent('New');
    });
  });

  describe('Accessibility', () => {
    it('has aria-label attribute', () => {
      const { container } = render(<TagBadge tag="new" aria-label="New game tag" />);
      const badge = container.querySelector('[data-testid="tag-badge-new"]');
      expect(badge).toHaveAttribute('aria-label', 'New game tag');
    });

    it('defaults to description for aria-label', () => {
      const { container } = render(<TagBadge tag="new" />);
      const badge = container.querySelector('[data-testid="tag-badge-new"]');
      expect(badge).toHaveAttribute('aria-label', gameTagPresets.new.description);
    });
  });

  describe('Entity Tag Presets', () => {
    it('renders game tags correctly', () => {
      render(<TagBadge tag="new" />);
      expect(screen.getByTestId('tag-badge-new')).toBeInTheDocument();
    });

    it('renders agent tags correctly', () => {
      render(<TagBadge tag="rag" />);
      expect(screen.getByTestId('tag-badge-rag')).toBeInTheDocument();
    });

    it('renders document tags correctly', () => {
      render(<TagBadge tag="pdf" />);
      expect(screen.getByTestId('tag-badge-pdf')).toBeInTheDocument();
    });
  });
});

describe('TagOverflow', () => {
  describe('Rendering', () => {
    it('renders with count > 0', () => {
      render(<TagOverflow count={2} />);
      expect(screen.getByTestId('tag-overflow')).toBeInTheDocument();
      expect(screen.getByText('+2')).toBeInTheDocument();
    });

    it('returns null when count <= 0', () => {
      const { container } = render(<TagOverflow count={0} />);
      expect(container.firstChild).toBeNull();
    });

    it('displays "99+" for count > 99', () => {
      render(<TagOverflow count={150} />);
      expect(screen.getByText('99+')).toBeInTheDocument();
    });
  });

  describe('Variants', () => {
    it('renders desktop variant correctly', () => {
      const { container } = render(<TagOverflow count={3} variant="desktop" />);
      const overflow = screen.getByTestId('tag-overflow');
      expect(overflow).toHaveClass('w-8', 'h-8');
    });

    it('renders tablet variant correctly', () => {
      const { container } = render(<TagOverflow count={3} variant="tablet" />);
      const overflow = screen.getByTestId('tag-overflow');
      expect(overflow).toHaveClass('w-7', 'h-7');
    });

    it('renders mobile variant correctly', () => {
      const { container } = render(<TagOverflow count={3} variant="mobile" />);
      const overflow = screen.getByTestId('tag-overflow');
      expect(overflow).toHaveClass('w-6', 'h-6');
    });
  });

  describe('Accessibility', () => {
    it('has descriptive aria-label', () => {
      render(<TagOverflow count={5} />);
      const overflow = screen.getByTestId('tag-overflow');
      expect(overflow).toHaveAttribute('aria-label', '5 altri tag nascosti');
    });

    it('uses singular label for count=1', () => {
      render(<TagOverflow count={1} />);
      const overflow = screen.getByTestId('tag-overflow');
      expect(overflow).toHaveAttribute('aria-label', '1 altro tag nascosto');
    });
  });
});

describe('TagStrip', () => {
  const mockTags = ['new', 'sale', 'owned', 'wishlist', 'rag'];

  describe('Rendering', () => {
    it('renders with tags', () => {
      render(<TagStrip tags={mockTags.slice(0, 3)} />);
      expect(screen.getByTestId('tag-strip')).toBeInTheDocument();
    });

    it('returns null for empty tags array', () => {
      const { container } = render(<TagStrip tags={[]} />);
      expect(container.firstChild).toBeNull();
    });

    it('returns null for undefined tags', () => {
      const { container } = render(<TagStrip tags={undefined as any} />);
      expect(container.firstChild).toBeNull();
    });
  });

  describe('Max Visible Tags', () => {
    it('shows max 3 tags by default', () => {
      render(<TagStrip tags={mockTags} />);
      // 3 visible tags + 1 overflow
      const listItems = screen.getAllByRole('listitem');
      expect(listItems).toHaveLength(4); // 3 tags + overflow
    });

    it('respects custom maxVisible prop', () => {
      render(<TagStrip tags={mockTags} maxVisible={2} />);
      const listItems = screen.getAllByRole('listitem');
      expect(listItems).toHaveLength(3); // 2 tags + overflow
    });

    it('no overflow when tags.length <= maxVisible', () => {
      render(<TagStrip tags={mockTags.slice(0, 2)} maxVisible={3} />);
      expect(screen.queryByTestId('tag-overflow')).not.toBeInTheDocument();
    });
  });

  describe('Overflow Counter', () => {
    it('displays correct overflow count', () => {
      render(<TagStrip tags={mockTags} maxVisible={3} />);
      expect(screen.getByTestId('tag-overflow')).toBeInTheDocument();
      expect(screen.getByText('+2')).toBeInTheDocument(); // 5 tags - 3 visible = 2 overflow
    });

    it('no overflow badge when all tags visible', () => {
      render(<TagStrip tags={mockTags.slice(0, 3)} maxVisible={3} />);
      expect(screen.queryByTestId('tag-overflow')).not.toBeInTheDocument();
    });
  });

  describe('Responsive Variants', () => {
    it('desktop variant uses full width', () => {
      render(<TagStrip tags={mockTags.slice(0, 2)} variant="desktop" />);
      const strip = screen.getByTestId('tag-strip');
      expect(strip).toHaveClass('w-8');
    });

    it('tablet variant uses medium width', () => {
      render(<TagStrip tags={mockTags.slice(0, 2)} variant="tablet" />);
      const strip = screen.getByTestId('tag-strip');
      expect(strip).toHaveClass('w-7');
    });

    it('mobile variant uses small width and icon-only mode', () => {
      render(<TagStrip tags={mockTags.slice(0, 2)} variant="mobile" />);
      const strip = screen.getByTestId('tag-strip');
      expect(strip).toHaveClass('w-6');
      // Verify tags are in icon-only mode (no text labels)
      const badges = screen.getAllByRole('listitem');
      badges.forEach((badge) => {
        // In mobile mode, badges should not contain text labels
        const badgeElement = badge.querySelector('[data-testid^="tag-badge"]');
        expect(badgeElement?.textContent).not.toMatch(/New|Sale|Owned/);
      });
    });
  });

  describe('Accessibility', () => {
    it('has role="list" attribute', () => {
      render(<TagStrip tags={mockTags.slice(0, 2)} />);
      const strip = screen.getByTestId('tag-strip');
      expect(strip).toHaveAttribute('role', 'list');
    });

    it('has descriptive aria-label', () => {
      render(<TagStrip tags={mockTags.slice(0, 3)} />);
      const strip = screen.getByTestId('tag-strip');
      expect(strip).toHaveAttribute('aria-label', '3 tags');
    });

    it('uses singular label for single tag', () => {
      render(<TagStrip tags={['new']} />);
      const strip = screen.getByTestId('tag-strip');
      expect(strip).toHaveAttribute('aria-label', '1 tag');
    });

    it('each tag has role="listitem"', () => {
      render(<TagStrip tags={mockTags.slice(0, 3)} />);
      const listItems = screen.getAllByRole('listitem');
      expect(listItems.length).toBeGreaterThan(0);
    });
  });

  describe('Custom Config Tags', () => {
    it('renders with custom TagConfig objects', () => {
      const customTags = [gameTagPresets.new, agentTagPresets.rag, documentTagPresets.pdf];
      render(<TagStrip tags={customTags} />);
      expect(screen.getByTestId('tag-badge-new')).toBeInTheDocument();
      expect(screen.getByTestId('tag-badge-rag')).toBeInTheDocument();
      expect(screen.getByTestId('tag-badge-pdf')).toBeInTheDocument();
    });
  });

  describe('Edge Cases', () => {
    it('handles maxVisible=0', () => {
      render(<TagStrip tags={mockTags} maxVisible={0} />);
      // All tags should be in overflow
      expect(screen.getByTestId('tag-overflow')).toBeInTheDocument();
      expect(screen.getByText('+5')).toBeInTheDocument();
    });

    it('handles large number of tags', () => {
      const largeTags = Array(150).fill('new');
      render(<TagStrip tags={largeTags} maxVisible={3} />);
      expect(screen.getByText('99+')).toBeInTheDocument(); // Max overflow display
    });
  });
});
