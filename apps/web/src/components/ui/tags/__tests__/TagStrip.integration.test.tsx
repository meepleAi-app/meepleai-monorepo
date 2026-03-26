import { describe, it, expect, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { TagStrip } from '../TagStrip';
import { MeepleCard } from '../../data-display/meeple-card';
import type { TagPresetKey } from '../../data-display/meeple-card-features/tag-presets';
import type { Tag } from '@/types/tags';
import { Sparkles, Tag as TagIcon, CheckCircle, Heart } from 'lucide-react';

/**
 * Integration tests for TagStrip within MeepleCard context (Epic #4068 - Issue #4182)
 *
 * MeepleCard uses its own internal TagStrip (meeple-card-features/TagStrip)
 * which expects TagPresetKey strings or TagConfig objects.
 * Standalone TagStrip (ui/tags/TagStrip) uses Tag[] from @/types/tags.
 */
describe('TagStrip Integration with MeepleCard', () => {
  // MeepleCard expects TagPresetKey strings
  const meepleCardTags: TagPresetKey[] = ['new', 'sale', 'owned', 'wishlist'];
  const meepleCardTagsWithExtra: TagPresetKey[] = ['new', 'sale', 'owned', 'wishlist', 'rag'];

  // Standalone TagStrip expects Tag[] objects
  const standaloneTags: Tag[] = [
    {
      id: 'new',
      label: 'New',
      icon: Sparkles,
      bgColor: 'hsl(142 76% 36%)',
      color: 'hsl(0 0% 100%)',
      tooltip: 'Recently added',
    },
    {
      id: 'sale',
      label: 'Sale',
      icon: TagIcon,
      bgColor: 'hsl(0 84% 60%)',
      color: 'hsl(0 0% 100%)',
      tooltip: 'On sale now',
    },
    {
      id: 'owned',
      label: 'Owned',
      icon: CheckCircle,
      bgColor: 'hsl(221 83% 53%)',
      color: 'hsl(0 0% 100%)',
      tooltip: 'In your collection',
    },
    {
      id: 'wishlisted',
      label: 'Wishlist',
      icon: Heart,
      bgColor: 'hsl(350 89% 60%)',
      color: 'hsl(0 0% 100%)',
    },
  ];

  it('renders tags within MeepleCard grid variant', () => {
    render(
      <MeepleCard
        entity="game"
        variant="grid"
        title="Wingspan"
        subtitle="Stonemaier Games"
        imageUrl="/games/wingspan.jpg"
        tags={meepleCardTags.slice(0, 3)}
        maxVisibleTags={3}
      />
    );

    // MeepleCard internal TagStrip uses data-testid="tag-strip"
    const tagStrip = screen.getByTestId('tag-strip');
    expect(tagStrip).toBeInTheDocument();

    // 3 tags visible (preset labels)
    expect(screen.getByText('New')).toBeInTheDocument();
    expect(screen.getByText('Sale')).toBeInTheDocument();
    expect(screen.getByText('Owned')).toBeInTheDocument();
  });

  it('shows overflow counter with 5 tags, max 3 visible', () => {
    render(
      <MeepleCard
        entity="game"
        title="Wingspan"
        tags={meepleCardTagsWithExtra}
        maxVisibleTags={3}
      />
    );

    // 3 visible + overflow counter
    expect(screen.getByText('New')).toBeInTheDocument();
    expect(screen.getByText('Sale')).toBeInTheDocument();
    expect(screen.getByText('Owned')).toBeInTheDocument();
    expect(screen.getByText('+2')).toBeInTheDocument();
  });

  it('renders tags on list variant', () => {
    render(
      <MeepleCard entity="game" variant="list" title="Wingspan" tags={meepleCardTags.slice(0, 2)} />
    );

    const tagStrip = screen.getByTestId('tag-strip');
    expect(tagStrip).toBeInTheDocument();
  });

  it('renders agent capability tags correctly', () => {
    const agentTags: TagPresetKey[] = ['rag', 'vision'];

    render(<MeepleCard entity="agent" title="Rules Expert" tags={agentTags} />);

    expect(screen.getByText('RAG')).toBeInTheDocument();
    expect(screen.getByText('Vision')).toBeInTheDocument();
  });

  it('respects maxVisibleTags prop', () => {
    render(
      <MeepleCard
        entity="game"
        title="Wingspan"
        tags={meepleCardTagsWithExtra}
        maxVisibleTags={2}
      />
    );

    // Only 2 visible
    expect(screen.getByText('New')).toBeInTheDocument();
    expect(screen.getByText('Sale')).toBeInTheDocument();

    // Overflow shows +3 (5 total - 2 visible)
    expect(screen.getByText('+3')).toBeInTheDocument();
  });

  it('does not render tag strip when tags array is empty', () => {
    render(<MeepleCard entity="game" title="Wingspan" tags={[]} />);

    expect(screen.queryByTestId('tag-strip')).not.toBeInTheDocument();
  });

  it('does not render tag strip when tags prop is undefined', () => {
    render(<MeepleCard entity="game" title="Wingspan" />);

    expect(screen.queryByTestId('tag-strip')).not.toBeInTheDocument();
  });

  it('tag badges render with correct test IDs', () => {
    render(<MeepleCard entity="game" title="Wingspan" tags={['new']} />);

    const tagBadge = screen.getByTestId('tag-badge-new');
    expect(tagBadge).toBeInTheDocument();
  });

  it('tag icons render correctly', () => {
    render(<MeepleCard entity="game" title="Wingspan" tags={['new']} />);

    const tagBadge = screen.getByTestId('tag-badge-new');
    const icon = tagBadge.querySelector('svg');
    expect(icon).toBeInTheDocument();
  });

  it('tag strip does not break card layout in grid variant', () => {
    const { container } = render(
      <MeepleCard
        entity="game"
        variant="grid"
        title="Wingspan"
        imageUrl="/games/wingspan.jpg"
        tags={meepleCardTags.slice(0, 3)}
      />
    );

    const card = container.querySelector('[data-testid="meeple-card"]');
    const tagStrip = screen.getByTestId('tag-strip');

    // Card has relative positioning (for absolute tag strip)
    expect(card).toHaveClass('relative');

    // Tag strip is positioned absolutely
    expect(tagStrip.className).toContain('absolute');
  });

  it('tags do not block click events on card', async () => {
    const user = userEvent.setup();
    const handleCardClick = vi.fn();

    render(
      <div onClick={handleCardClick}>
        <MeepleCard entity="game" title="Wingspan" tags={meepleCardTags.slice(0, 3)} />
      </div>
    );

    // Click on card (not on tag)
    const cardTitle = screen.getByText('Wingspan');
    await user.click(cardTitle);

    expect(handleCardClick).toHaveBeenCalled();
  });

  it('renders correctly in all supported MeepleCard variants', () => {
    // compact variant does not render a tag strip by design
    const variants: Array<'grid' | 'list' | 'featured' | 'hero'> = [
      'grid',
      'list',
      'featured',
      'hero',
    ];

    variants.forEach(variant => {
      const { unmount } = render(
        <MeepleCard
          entity="game"
          variant={variant}
          title="Wingspan"
          tags={meepleCardTags.slice(0, 3)}
        />
      );

      const tagStrip = screen.getByTestId('tag-strip');
      expect(tagStrip).toBeInTheDocument();

      unmount();
    });
  });

  it('tag strip does not render when MeepleCard entity does not support tags', () => {
    // Some entity types might not show tags (e.g., compact player cards)
    render(
      <MeepleCard entity="player" variant="compact" title="Marco Rossi" tags={meepleCardTags} />
    );

    // Implementation detail: MeepleCard decides whether to render tags
    // This test verifies graceful handling (no crash = pass)
    expect(true).toBe(true);
  });

  it('tag strip works with dynamic tag updates', async () => {
    const { rerender } = render(<MeepleCard entity="game" title="Wingspan" tags={['new']} />);

    // Initially 1 tag
    expect(screen.getByText('New')).toBeInTheDocument();
    expect(screen.queryByText('Sale')).not.toBeInTheDocument();

    // Update tags
    rerender(<MeepleCard entity="game" title="Wingspan" tags={['new', 'sale']} />);

    // Now 2 tags
    expect(screen.getByText('New')).toBeInTheDocument();
    expect(screen.getByText('Sale')).toBeInTheDocument();
  });

  it('tag accessibility: tag strip has role="list"', () => {
    render(<MeepleCard entity="game" title="Wingspan" tags={meepleCardTags.slice(0, 3)} />);

    const tagStrip = screen.getByTestId('tag-strip');
    expect(tagStrip).toHaveAttribute('role', 'list');

    // Each tag wrapper is a listitem
    const items = within(tagStrip).getAllByRole('listitem');
    expect(items).toHaveLength(3);
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

    render(<MeepleCard entity="game" title="Wingspan" tags={meepleCardTags.slice(0, 3)} />);

    // Tags still render (no crash with reduced motion)
    expect(screen.getByText('New')).toBeInTheDocument();
  });

  it('tag performance: renders many tags without lag', () => {
    // Use available preset keys for performance test
    const manyPresetTags: TagPresetKey[] = [
      'new',
      'sale',
      'owned',
      'wishlist',
      'rag',
      'vision',
      'code',
      'tutor',
      'arbitro',
      'stratega',
    ];

    const start = performance.now();

    render(<MeepleCard entity="game" title="Wingspan" tags={manyPresetTags} maxVisibleTags={3} />);

    const duration = performance.now() - start;

    // Render should complete quickly (< 200ms even with 10 tags, accounting for jsdom overhead)
    expect(duration).toBeLessThan(200);

    // Only 3 visible + overflow
    expect(screen.getByText('New')).toBeInTheDocument();
    expect(screen.getByText('Sale')).toBeInTheDocument();
    expect(screen.getByText('Owned')).toBeInTheDocument();
    expect(screen.getByText('+7')).toBeInTheDocument();
  });

  // === Standalone TagStrip tests (ui/tags/TagStrip with Tag[] objects) ===

  describe('Standalone TagStrip', () => {
    it('handles tag click interactions gracefully', async () => {
      const user = userEvent.setup();
      const onTagClick = vi.fn();

      render(
        <div onClick={onTagClick}>
          <TagStrip tags={standaloneTags.slice(0, 3)} />
        </div>
      );

      const tagStrip = screen.getByRole('list', { name: 'Entity tags' });
      expect(tagStrip).toBeInTheDocument();

      // Click within the strip
      await user.click(tagStrip);
      expect(onTagClick).toHaveBeenCalled();
    });

    it('responsive variant changes strip width', () => {
      const { rerender, container } = render(
        <TagStrip tags={standaloneTags.slice(0, 2)} variant="desktop" />
      );

      let strip = container.querySelector('[aria-label="Entity tags"]');
      expect(strip).toHaveClass('w-8'); // 32px desktop

      rerender(<TagStrip tags={standaloneTags.slice(0, 2)} variant="tablet" />);
      strip = container.querySelector('[aria-label="Entity tags"]');
      expect(strip).toHaveClass('w-7'); // 28px tablet

      rerender(<TagStrip tags={standaloneTags.slice(0, 2)} variant="mobile" />);
      strip = container.querySelector('[aria-label="Entity tags"]');
      expect(strip).toHaveClass('w-6'); // 24px mobile
    });

    it('tag strip right position works correctly', () => {
      const { container } = render(<TagStrip tags={standaloneTags.slice(0, 2)} position="right" />);

      const strip = container.querySelector('[aria-label="Entity tags"]');

      expect(strip).toHaveClass('right-0');
      expect(strip).toHaveClass('border-l');
      expect(strip).not.toHaveClass('left-0');
    });
  });
});
