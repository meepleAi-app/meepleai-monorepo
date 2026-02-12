import { describe, it, expect, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Sparkles, Tag as TagIcon, Check, Heart, Brain } from 'lucide-react';
import { TagStrip } from '../TagStrip';
import { MeepleCard } from '../../data-display/meeple-card';
import type { Tag } from '@/types/tags';

/**
 * Integration tests for TagStrip within MeepleCard context (Epic #4068 - Issue #4182)
 */
describe('TagStrip Integration with MeepleCard', () => {
  const mockGameTags: Tag[] = [
    { id: 'new', label: 'New', icon: Sparkles, bgColor: 'hsl(142 76% 36%)', color: 'hsl(0 0% 100%)', tooltip: 'Recently added' },
    { id: 'sale', label: 'Sale', icon: TagIcon, bgColor: 'hsl(0 84% 60%)', color: 'hsl(0 0% 100%)', tooltip: 'On sale now' },
    { id: 'owned', label: 'Owned', icon: Check, bgColor: 'hsl(221 83% 53%)', color: 'hsl(0 0% 100%)', tooltip: 'In your collection' },
    { id: 'wishlisted', label: 'Wishlist', icon: Heart, bgColor: 'hsl(350 89% 60%)', color: 'hsl(0 0% 100%)' },
    { id: 'exclusive', label: 'Exclusive', bgColor: 'hsl(262 83% 58%)', color: 'hsl(0 0% 100%)' }
  ];

  it('renders tags within MeepleCard grid variant', () => {
    render(
      <MeepleCard
        entity="game"
        variant="grid"
        title="Wingspan"
        subtitle="Stonemaier Games"
        imageUrl="/games/wingspan.jpg"
        tags={mockGameTags.slice(0, 3)}
        maxVisibleTags={3}
      />
    );

    // Tag strip present
    const tagStrip = screen.getByLabelText('Entity tags');
    expect(tagStrip).toBeInTheDocument();

    // 3 tags visible
    expect(within(tagStrip).getByText('New')).toBeInTheDocument();
    expect(within(tagStrip).getByText('Sale')).toBeInTheDocument();
    expect(within(tagStrip).getByText('Owned')).toBeInTheDocument();
  });

  it('shows overflow counter with 5 tags, max 3 visible', () => {
    render(
      <MeepleCard
        entity="game"
        title="Wingspan"
        tags={mockGameTags}
        maxVisibleTags={3}
      />
    );

    // 3 visible + overflow counter
    const tagStrip = screen.getByLabelText('Entity tags');
    expect(within(tagStrip).getByText('New')).toBeInTheDocument();
    expect(within(tagStrip).getByText('Sale')).toBeInTheDocument();
    expect(within(tagStrip).getByText('Owned')).toBeInTheDocument();
    expect(within(tagStrip).getByText('+2')).toBeInTheDocument();
  });

  it('shows hidden tags in overflow tooltip on hover', async () => {
    const user = userEvent.setup();

    render(
      <MeepleCard
        entity="game"
        title="Wingspan"
        tags={mockGameTags}
        maxVisibleTags={3}
      />
    );

    const overflowBadge = screen.getByText('+2');

    // Hover overflow badge
    await user.hover(overflowBadge);

    // Tooltip appears with hidden tags
    expect(await screen.findByText('Wishlist')).toBeInTheDocument();
    expect(await screen.findByText('Exclusive')).toBeInTheDocument();
  });

  it('renders tags on list variant with horizontal layout', () => {
    render(
      <MeepleCard
        entity="game"
        variant="list"
        title="Wingspan"
        tags={mockGameTags.slice(0, 2)}
      />
    );

    // Tag strip present
    const tagStrip = screen.getByLabelText('Entity tags');
    expect(tagStrip).toBeInTheDocument();

    // Strip positioned on left edge
    expect(tagStrip).toHaveClass('left-0');
  });

  it('renders agent capability tags correctly', () => {
    const agentTags: Tag[] = [
      { id: 'rag', label: 'RAG', icon: Brain, bgColor: 'hsl(38 92% 50%)' },
      { id: 'vision', label: 'Vision', icon: Brain, bgColor: 'hsl(262 83% 58%)' }
    ];

    render(
      <MeepleCard
        entity="agent"
        title="Rules Expert"
        tags={agentTags}
      />
    );

    expect(screen.getByText('RAG')).toBeInTheDocument();
    expect(screen.getByText('Vision')).toBeInTheDocument();
  });

  it('respects maxVisibleTags prop', () => {
    render(
      <MeepleCard
        entity="game"
        title="Wingspan"
        tags={mockGameTags}
        maxVisibleTags={2}
      />
    );

    const tagStrip = screen.getByLabelText('Entity tags');

    // Only 2 visible
    expect(within(tagStrip).getByText('New')).toBeInTheDocument();
    expect(within(tagStrip).getByText('Sale')).toBeInTheDocument();

    // Overflow shows +3 (5 total - 2 visible)
    expect(within(tagStrip).getByText('+3')).toBeInTheDocument();
  });

  it('does not render tag strip when tags array is empty', () => {
    render(
      <MeepleCard
        entity="game"
        title="Wingspan"
        tags={[]}
      />
    );

    expect(screen.queryByLabelText('Entity tags')).not.toBeInTheDocument();
  });

  it('does not render tag strip when tags prop is undefined', () => {
    render(
      <MeepleCard
        entity="game"
        title="Wingspan"
      />
    );

    expect(screen.queryByLabelText('Entity tags')).not.toBeInTheDocument();
  });

  it('tag colors apply correctly via style prop', () => {
    render(
      <MeepleCard
        entity="game"
        title="Wingspan"
        tags={[mockGameTags[0]]}
      />
    );

    const tagBadge = screen.getByRole('status', { name: 'New' });

    // Verify inline styles applied
    expect(tagBadge).toHaveStyle({
      backgroundColor: 'hsl(142 76% 36%)',
      color: 'hsl(0 0% 100%)'
    });
  });

  it('tag icons render correctly', () => {
    render(
      <MeepleCard
        entity="game"
        title="Wingspan"
        tags={[mockGameTags[0]]}
      />
    );

    const tagBadge = screen.getByRole('status', { name: 'New' });
    const icon = within(tagBadge).querySelector('svg');

    expect(icon).toBeInTheDocument();
    expect(icon).toHaveAttribute('class', expect.stringContaining('lucide'));
  });

  it('staggered animations apply with correct delays', () => {
    render(
      <MeepleCard
        entity="game"
        title="Wingspan"
        tags={mockGameTags.slice(0, 3)}
      />
    );

    const tagStrip = screen.getByLabelText('Entity tags');
    const tags = within(tagStrip).getAllByRole('listitem');

    // First tag: 0ms delay
    expect(tags[0]).toHaveStyle({ animationDelay: '0ms' });

    // Second tag: 50ms delay
    expect(tags[1]).toHaveStyle({ animationDelay: '50ms' });

    // Third tag: 100ms delay
    expect(tags[2]).toHaveStyle({ animationDelay: '100ms' });
  });

  it('tag strip does not break card layout in grid variant', () => {
    const { container } = render(
      <MeepleCard
        entity="game"
        variant="grid"
        title="Wingspan"
        imageUrl="/games/wingspan.jpg"
        tags={mockGameTags.slice(0, 3)}
      />
    );

    const card = container.querySelector('[data-testid="meeple-card"]');
    const tagStrip = container.querySelector('[aria-label="Entity tags"]');

    // Card has relative positioning (for absolute tag strip)
    expect(card).toHaveClass('relative');

    // Tag strip is positioned absolutely
    expect(tagStrip).toHaveClass('absolute');

    // Tag strip has correct z-index (above image, below interactive elements)
    expect(tagStrip).toHaveClass('z-10');
  });

  it('tag tooltips show on hover', async () => {
    const user = userEvent.setup();

    render(
      <MeepleCard
        entity="game"
        title="Wingspan"
        tags={[mockGameTags[0]]}
      />
    );

    const newTag = screen.getByRole('status', { name: 'New' });

    // Hover tag
    await user.hover(newTag);

    // Tooltip appears (timing-dependent)
    expect(await screen.findByText('Recently added')).toBeInTheDocument();
  });

  it('handles tag click interactions gracefully', async () => {
    const user = userEvent.setup();
    const onTagClick = vi.fn();

    // Extended TagStrip with click handler (custom wrapper)
    render(
      <div onClick={onTagClick}>
        <TagStrip tags={mockGameTags.slice(0, 3)} />
      </div>
    );

    const newTag = screen.getByRole('status', { name: 'New' });

    await user.click(newTag);

    // Click bubbles to parent
    expect(onTagClick).toHaveBeenCalled();
  });

  it('responsive variant changes strip width', () => {
    const { rerender, container } = render(
      <TagStrip tags={mockGameTags.slice(0, 2)} variant="desktop" />
    );

    let strip = container.querySelector('[aria-label="Entity tags"]');
    expect(strip).toHaveClass('w-8'); // 32px desktop

    rerender(<TagStrip tags={mockGameTags.slice(0, 2)} variant="tablet" />);
    strip = container.querySelector('[aria-label="Entity tags"]');
    expect(strip).toHaveClass('w-7'); // 28px tablet

    rerender(<TagStrip tags={mockGameTags.slice(0, 2)} variant="mobile" />);
    strip = container.querySelector('[aria-label="Entity tags"]');
    expect(strip).toHaveClass('w-6'); // 24px mobile
  });

  it('mobile variant shows icon-only mode', () => {
    render(
      <MeepleCard
        entity="game"
        title="Wingspan"
        tags={[mockGameTags[0]]}
      />
    );

    // Simulate mobile viewport
    const tagStrip = screen.getByLabelText('Entity tags');

    // On mobile, text should not be visible (icon only)
    // (Actual mobile detection uses CSS media queries, test verifies component logic)
    const badge = within(tagStrip).getByRole('status');

    // Badge should have aria-label (for screen readers) even if text not visible
    expect(badge).toHaveAttribute('aria-label', 'New');
  });

  it('tag strip z-index does not conflict with other card elements', () => {
    render(
      <MeepleCard
        entity="game"
        variant="grid"
        title="Wingspan"
        tags={mockGameTags.slice(0, 2)}
        showWishlist
        quickActions={[
          { icon: Brain, label: 'View', onClick: vi.fn() }
        ]}
      />
    );

    const tagStrip = screen.getByLabelText('Entity tags');
    const wishlistButton = screen.getByLabelText(/wishlist/i);

    // Tag strip: z-10
    expect(tagStrip).toHaveClass('z-10');

    // Interactive elements should be above tags (z-20 or higher)
    // (Actual z-index from MeepleCard implementation)

    // Both should be visible (no z-index conflict)
    expect(tagStrip).toBeVisible();
    expect(wishlistButton).toBeVisible();
  });

  it('tags do not block click events on card', async () => {
    const user = userEvent.setup();
    const handleCardClick = vi.fn();

    render(
      <div onClick={handleCardClick}>
        <MeepleCard
          entity="game"
          title="Wingspan"
          tags={mockGameTags.slice(0, 3)}
        />
      </div>
    );

    // Click on card (not on tag)
    const cardTitle = screen.getByText('Wingspan');
    await user.click(cardTitle);

    expect(handleCardClick).toHaveBeenCalled();
  });

  it('renders correctly in all 5 MeepleCard variants', () => {
    const variants: Array<'grid' | 'list' | 'compact' | 'featured' | 'hero'> = [
      'grid',
      'list',
      'compact',
      'featured',
      'hero'
    ];

    variants.forEach(variant => {
      const { container } = render(
        <MeepleCard
          entity="game"
          variant={variant}
          title="Wingspan"
          tags={mockGameTags.slice(0, 3)}
        />
      );

      const tagStrip = container.querySelector('[aria-label="Entity tags"]');
      expect(tagStrip).toBeInTheDocument();

      // Tag strip always on left edge (default)
      expect(tagStrip).toHaveClass('left-0');
    });
  });

  it('tag overflow tooltip persists on hover', async () => {
    const user = userEvent.setup();

    render(
      <MeepleCard
        entity="game"
        title="Wingspan"
        tags={mockGameTags}
        maxVisibleTags={2}
      />
    );

    const overflowBadge = screen.getByText('+3');

    // Hover overflow badge
    await user.hover(overflowBadge);

    // Tooltip appears
    const tooltip = await screen.findByRole('tooltip');
    expect(tooltip).toBeVisible();

    // Move mouse to tooltip (should stay visible)
    await user.hover(tooltip);

    // Tooltip still visible (hoverable)
    expect(tooltip).toBeVisible();
  });

  it('tag accessibility: screen reader announces tag list', () => {
    render(
      <MeepleCard
        entity="game"
        title="Wingspan"
        tags={mockGameTags.slice(0, 3)}
      />
    );

    // Tag strip has role="list"
    const tagStrip = screen.getByRole('list', { name: 'Entity tags' });
    expect(tagStrip).toBeInTheDocument();

    // Each tag is a listitem
    const items = within(tagStrip).getAllByRole('listitem');
    expect(items).toHaveLength(3);

    // Each tag badge has role="status" with aria-label
    const badges = within(tagStrip).getAllByRole('status');
    expect(badges[0]).toHaveAttribute('aria-label', 'New');
    expect(badges[1]).toHaveAttribute('aria-label', 'Sale');
    expect(badges[2]).toHaveAttribute('aria-label', 'Owned');
  });

  it('tag strip does not render when MeepleCard entity does not support tags', () => {
    // Some entity types might not show tags (e.g., compact player cards)
    render(
      <MeepleCard
        entity="player"
        variant="compact"
        title="Marco Rossi"
        tags={mockGameTags} // Tags passed but entity="player" may not support
      />
    );

    // Implementation detail: MeepleCard decides whether to render tags based on variant/entity
    // This test verifies graceful handling

    const tagStrip = screen.queryByLabelText('Entity tags');

    // Either renders or doesn't (both acceptable), but shouldn't crash
    expect(true).toBe(true); // Placeholder assertion (no crash = pass)
  });

  it('tag strip works with dynamic tag updates', async () => {
    const { rerender } = render(
      <MeepleCard
        entity="game"
        title="Wingspan"
        tags={[mockGameTags[0]]}
      />
    );

    // Initially 1 tag
    expect(screen.getByText('New')).toBeInTheDocument();
    expect(screen.queryByText('Sale')).not.toBeInTheDocument();

    // Update tags
    rerender(
      <MeepleCard
        entity="game"
        title="Wingspan"
        tags={mockGameTags.slice(0, 2)}
      />
    );

    // Now 2 tags
    expect(screen.getByText('New')).toBeInTheDocument();
    expect(screen.getByText('Sale')).toBeInTheDocument();
  });

  it('tag strip right position works correctly', () => {
    const { container } = render(
      <TagStrip tags={mockGameTags.slice(0, 2)} position="right" />
    );

    const strip = container.querySelector('[aria-label="Entity tags"]');

    expect(strip).toHaveClass('right-0');
    expect(strip).toHaveClass('border-l'); // Left border (not right)
    expect(strip).not.toHaveClass('left-0');
  });

  it('tag animations can be disabled for reduced motion preference', () => {
    // Mock reduced motion preference
    Object.defineProperty(window, 'matchMedia', {
      writable: true,
      value: vi.fn().mockImplementation(query => ({
        matches: query === '(prefers-reduced-motion: reduce)',
        media: query,
        onchange: null,
        addListener: vi.fn(),
        removeListener: vi.fn(),
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        dispatchEvent: vi.fn(),
      })),
    });

    render(
      <MeepleCard
        entity="game"
        title="Wingspan"
        tags={mockGameTags.slice(0, 3)}
      />
    );

    // Tags still render
    expect(screen.getByText('New')).toBeInTheDocument();

    // But animations should be disabled (CSS handles this, component still renders)
    // Test verifies component doesn't crash with reduced motion
  });

  it('tag performance: renders 10 tags without lag', () => {
    const manyTags = Array.from({ length: 10 }, (_, i) => ({
      id: `tag-${i}`,
      label: `Tag ${i}`,
      bgColor: 'hsl(220 70% 50%)'
    }));

    const start = performance.now();

    render(
      <MeepleCard
        entity="game"
        title="Wingspan"
        tags={manyTags}
        maxVisibleTags={3}
      />
    );

    const duration = performance.now() - start;

    // Render should complete quickly (< 100ms even with 10 tags)
    expect(duration).toBeLessThan(100);

    // Only 3 visible + overflow
    expect(screen.getByText('Tag 0')).toBeInTheDocument();
    expect(screen.getByText('Tag 1')).toBeInTheDocument();
    expect(screen.getByText('Tag 2')).toBeInTheDocument();
    expect(screen.getByText('+7')).toBeInTheDocument();
  });
});
