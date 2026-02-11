/**
 * MeepleCard Component Tests (Issue #3326)
 *
 * Tests rendering, entity types, layout variants, accessibility, and interactions.
 */

import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { Users, Clock } from 'lucide-react';
import {
  MeepleCard,
  MeepleCardSkeleton,
  type MeepleEntityType,
  type MeepleCardVariant,
  type MeepleCardFlipData,
} from '../meeple-card';

// Mock Next.js Image component
vi.mock('next/image', () => ({
  default: ({ src, alt, ...props }: { src: string; alt: string }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img src={src} alt={alt} {...props} />
  ),
}));

// Mock framer-motion for FlipCard
vi.mock('framer-motion', () => ({
  motion: {
    div: React.forwardRef(
      (
        { children, animate, style, ...props }: React.PropsWithChildren<Record<string, unknown>>,
        ref: React.Ref<HTMLDivElement>,
      ) => (
        <div ref={ref} style={style as React.CSSProperties} {...props}>
          {children}
        </div>
      ),
    ),
  },
}));

describe('MeepleCard', () => {
  const defaultProps = {
    entity: 'game' as MeepleEntityType,
    title: 'Test Game',
    subtitle: 'Test Publisher',
  };

  describe('Rendering', () => {
    it('should render with title', () => {
      render(<MeepleCard {...defaultProps} />);

      expect(screen.getByText('Test Game')).toBeInTheDocument();
    });

    it('should render with subtitle', () => {
      render(<MeepleCard {...defaultProps} />);

      expect(screen.getByText('Test Publisher')).toBeInTheDocument();
    });

    it('should render with default data-testid', () => {
      render(<MeepleCard {...defaultProps} />);

      expect(screen.getByTestId('meeple-card')).toBeInTheDocument();
    });

    it('should render with custom data-testid', () => {
      render(<MeepleCard {...defaultProps} data-testid="custom-card" />);

      expect(screen.getByTestId('custom-card')).toBeInTheDocument();
    });

    it('should render with data-entity attribute', () => {
      render(<MeepleCard {...defaultProps} entity="game" />);

      const card = screen.getByTestId('meeple-card');
      expect(card).toHaveAttribute('data-entity', 'game');
    });

    it('should render with data-variant attribute', () => {
      render(<MeepleCard {...defaultProps} variant="list" />);

      const card = screen.getByTestId('meeple-card');
      expect(card).toHaveAttribute('data-variant', 'list');
    });
  });

  describe('Entity Types', () => {
    const entityTypes: MeepleEntityType[] = ['game', 'player', 'collection', 'event', 'custom'];

    it.each(entityTypes)('should render %s entity type correctly', entity => {
      render(<MeepleCard {...defaultProps} entity={entity} />);

      const card = screen.getByTestId('meeple-card');
      expect(card).toHaveAttribute('data-entity', entity);
    });

    it('should use avatarUrl for player entity', () => {
      render(
        <MeepleCard
          {...defaultProps}
          entity="player"
          avatarUrl="/avatar.jpg"
          imageUrl="/fallback.jpg"
        />
      );

      const img = screen.getByAltText('Test Game');
      expect(img).toHaveAttribute('src', '/avatar.jpg');
    });

    it('should use imageUrl for non-player entity', () => {
      render(
        <MeepleCard
          {...defaultProps}
          entity="game"
          avatarUrl="/avatar.jpg"
          imageUrl="/game.jpg"
        />
      );

      const img = screen.getByAltText('Test Game');
      expect(img).toHaveAttribute('src', '/game.jpg');
    });

    it('should support custom color for custom entity', () => {
      render(
        <MeepleCard
          {...defaultProps}
          entity="custom"
          customColor="142 76% 36%"
        />
      );

      const card = screen.getByTestId('meeple-card');
      expect(card).toHaveAttribute('data-entity', 'custom');
    });
  });

  describe('Layout Variants', () => {
    const variants: MeepleCardVariant[] = ['grid', 'list', 'compact', 'featured', 'hero'];

    it.each(variants)('should render %s variant correctly', variant => {
      render(<MeepleCard {...defaultProps} variant={variant} />);

      const card = screen.getByTestId('meeple-card');
      expect(card).toHaveAttribute('data-variant', variant);
    });

    it('should default to grid variant', () => {
      render(<MeepleCard {...defaultProps} />);

      const card = screen.getByTestId('meeple-card');
      expect(card).toHaveAttribute('data-variant', 'grid');
    });

    it('should not render cover image for compact variant', () => {
      render(<MeepleCard {...defaultProps} variant="compact" imageUrl="/test.jpg" />);

      expect(screen.queryByAltText('Test Game')).not.toBeInTheDocument();
    });

    it('should render cover image for grid variant', () => {
      render(<MeepleCard {...defaultProps} variant="grid" imageUrl="/test.jpg" />);

      expect(screen.getByAltText('Test Game')).toBeInTheDocument();
    });
  });

  describe('Rating Display', () => {
    it('should render rating when provided', () => {
      render(<MeepleCard {...defaultProps} rating={4.5} />);

      expect(screen.getByText('4.5')).toBeInTheDocument();
    });

    it('should convert 10-scale rating to 5-scale', () => {
      render(<MeepleCard {...defaultProps} rating={8.0} ratingMax={10} />);

      expect(screen.getByText('8.0')).toBeInTheDocument();
      expect(screen.getByLabelText('Rating: 8 out of 10')).toBeInTheDocument();
    });

    it('should not render rating for compact variant', () => {
      render(<MeepleCard {...defaultProps} variant="compact" rating={4.5} />);

      expect(screen.queryByText('4.5')).not.toBeInTheDocument();
    });
  });

  describe('Metadata Display', () => {
    it('should render metadata items', () => {
      render(
        <MeepleCard
          {...defaultProps}
          metadata={[{ label: '3-6 players' }, { label: '2-4 hours' }]}
        />
      );

      expect(screen.getByText('3-6 players')).toBeInTheDocument();
      expect(screen.getByText('2-4 hours')).toBeInTheDocument();
    });

    it('should render metadata with icons', () => {
      render(
        <MeepleCard
          {...defaultProps}
          metadata={[{ icon: Users, value: '3-6' }]}
        />
      );

      expect(screen.getByText('3-6')).toBeInTheDocument();
    });

    it('should not render metadata for compact variant', () => {
      render(
        <MeepleCard
          {...defaultProps}
          variant="compact"
          metadata={[{ label: '3-6 players' }]}
        />
      );

      expect(screen.queryByText('3-6 players')).not.toBeInTheDocument();
    });
  });

  describe('Action Buttons', () => {
    const actions = [
      { label: 'Primary', primary: true },
      { label: 'Secondary' },
    ];

    it('should render actions for featured variant', () => {
      render(<MeepleCard {...defaultProps} variant="featured" actions={actions} />);

      expect(screen.getByText('Primary')).toBeInTheDocument();
      expect(screen.getByText('Secondary')).toBeInTheDocument();
    });

    it('should render actions for hero variant', () => {
      render(<MeepleCard {...defaultProps} variant="hero" actions={actions} />);

      expect(screen.getByText('Primary')).toBeInTheDocument();
      expect(screen.getByText('Secondary')).toBeInTheDocument();
    });

    it('should not render actions for grid variant', () => {
      render(<MeepleCard {...defaultProps} variant="grid" actions={actions} />);

      expect(screen.queryByText('Primary')).not.toBeInTheDocument();
    });

    it('should handle action click without propagating to card', () => {
      const cardClick = vi.fn();
      const actionClick = vi.fn();

      render(
        <MeepleCard
          {...defaultProps}
          variant="featured"
          onClick={cardClick}
          actions={[{ label: 'Click Me', onClick: actionClick }]}
        />
      );

      fireEvent.click(screen.getByText('Click Me'));

      expect(actionClick).toHaveBeenCalledTimes(1);
      expect(cardClick).not.toHaveBeenCalled();
    });

    it('should disable action button when disabled prop is true', () => {
      render(
        <MeepleCard
          {...defaultProps}
          variant="featured"
          actions={[{ label: 'Disabled', disabled: true }]}
        />
      );

      expect(screen.getByText('Disabled')).toBeDisabled();
    });
  });

  describe('Badge Display', () => {
    it('should render badge text', () => {
      render(<MeepleCard {...defaultProps} badge="New" />);

      expect(screen.getByText('New')).toBeInTheDocument();
    });

    it('should not render badge for hero variant', () => {
      render(<MeepleCard {...defaultProps} variant="hero" badge="New" />);

      // Badge text should not appear in the badge position for hero
      const card = screen.getByTestId('meeple-card');
      const badges = card.querySelectorAll('[class*="absolute"][class*="right-3"]');
      expect(badges.length).toBe(0);
    });
  });

  describe('Loading State', () => {
    it('should render skeleton when loading', () => {
      render(<MeepleCard {...defaultProps} loading />);

      expect(screen.getByTestId('meeple-card-skeleton')).toBeInTheDocument();
    });

    it('should not render content when loading', () => {
      render(<MeepleCard {...defaultProps} loading />);

      expect(screen.queryByText('Test Game')).not.toBeInTheDocument();
    });
  });

  describe('Click Interaction', () => {
    it('should call onClick when card is clicked', () => {
      const onClick = vi.fn();
      render(<MeepleCard {...defaultProps} onClick={onClick} />);

      fireEvent.click(screen.getByTestId('meeple-card'));

      expect(onClick).toHaveBeenCalledTimes(1);
    });

    it('should call onClick on Enter key press', () => {
      const onClick = vi.fn();
      render(<MeepleCard {...defaultProps} onClick={onClick} />);

      fireEvent.keyDown(screen.getByTestId('meeple-card'), { key: 'Enter' });

      expect(onClick).toHaveBeenCalledTimes(1);
    });

    it('should call onClick on Space key press', () => {
      const onClick = vi.fn();
      render(<MeepleCard {...defaultProps} onClick={onClick} />);

      fireEvent.keyDown(screen.getByTestId('meeple-card'), { key: ' ' });

      expect(onClick).toHaveBeenCalledTimes(1);
    });

    it('should not call onClick on other key press', () => {
      const onClick = vi.fn();
      render(<MeepleCard {...defaultProps} onClick={onClick} />);

      fireEvent.keyDown(screen.getByTestId('meeple-card'), { key: 'Tab' });

      expect(onClick).not.toHaveBeenCalled();
    });

    it('should not make card interactive when it has action buttons (prevents nested-interactive)', () => {
      const cardClick = vi.fn();
      render(
        <MeepleCard
          {...defaultProps}
          variant="featured"
          onClick={cardClick}
          actions={[{ label: 'Action' }]}
        />
      );

      // Card should not be a button when it has actions
      expect(screen.queryByRole('button', { name: /game/i })).not.toBeInTheDocument();

      // Clicking the card should not trigger onClick
      fireEvent.click(screen.getByTestId('meeple-card'));
      expect(cardClick).not.toHaveBeenCalled();
    });
  });

  describe('Accessibility', () => {
    it('should have role="button" when clickable (without actions)', () => {
      render(<MeepleCard {...defaultProps} onClick={() => {}} />);

      expect(screen.getByRole('button')).toBeInTheDocument();
    });

    it('should not have role="button" when not clickable', () => {
      render(<MeepleCard {...defaultProps} />);

      expect(screen.queryByRole('button')).not.toBeInTheDocument();
    });

    it('should not have role="button" when clickable but has actions (prevents nested-interactive)', () => {
      render(
        <MeepleCard
          {...defaultProps}
          variant="featured"
          onClick={() => {}}
          actions={[{ label: 'Action' }]}
        />
      );

      // The card itself should not be a button
      expect(screen.queryByRole('button', { name: /game/i })).not.toBeInTheDocument();
      // But action buttons should exist
      expect(screen.getByRole('button', { name: 'Action' })).toBeInTheDocument();
    });

    it('should have tabIndex={0} when clickable (without actions)', () => {
      render(<MeepleCard {...defaultProps} onClick={() => {}} />);

      expect(screen.getByTestId('meeple-card')).toHaveAttribute('tabIndex', '0');
    });

    it('should not have tabIndex when not clickable', () => {
      render(<MeepleCard {...defaultProps} />);

      expect(screen.getByTestId('meeple-card')).not.toHaveAttribute('tabIndex');
    });

    it('should have aria-label with entity and title', () => {
      render(<MeepleCard {...defaultProps} entity="game" title="Test Game" />);

      expect(screen.getByLabelText('Game: Test Game')).toBeInTheDocument();
    });

    it('should render as article element when not interactive', () => {
      render(<MeepleCard {...defaultProps} />);

      expect(screen.getByRole('article')).toBeInTheDocument();
    });

    it('should render as div element when interactive (article cannot have role=button)', () => {
      render(<MeepleCard {...defaultProps} onClick={() => {}} />);

      // Should be a button (div with role=button), not article
      expect(screen.getByRole('button')).toBeInTheDocument();
      expect(screen.queryByRole('article')).not.toBeInTheDocument();
    });
  });

  describe('Custom Styling', () => {
    it('should apply custom className', () => {
      render(<MeepleCard {...defaultProps} className="custom-class" />);

      expect(screen.getByTestId('meeple-card')).toHaveClass('custom-class');
    });
  });
});

describe('MeepleCardSkeleton', () => {
  it('should render with data-testid', () => {
    render(<MeepleCardSkeleton />);

    expect(screen.getByTestId('meeple-card-skeleton')).toBeInTheDocument();
  });

  it('should have animate-pulse class', () => {
    render(<MeepleCardSkeleton />);

    expect(screen.getByTestId('meeple-card-skeleton')).toHaveClass('animate-pulse');
  });

  it('should respect variant prop', () => {
    const { rerender } = render(<MeepleCardSkeleton variant="list" />);

    expect(screen.getByTestId('meeple-card-skeleton')).toBeInTheDocument();

    rerender(<MeepleCardSkeleton variant="hero" />);

    expect(screen.getByTestId('meeple-card-skeleton')).toBeInTheDocument();
  });
});

describe('MeepleCard Flip Integration', () => {
  const defaultProps = {
    entity: 'game' as MeepleEntityType,
    title: 'Test Game',
    subtitle: 'Test Publisher',
  };

  const flipData: MeepleCardFlipData = {
    description: 'A great game about strategy.',
    categories: [{ id: 'cat-1', name: 'Strategy' }],
    designers: [{ id: 'des-1', name: 'Test Designer' }],
  };

  it('should NOT render flip container without flippable prop', () => {
    render(<MeepleCard {...defaultProps} />);

    expect(screen.queryByTestId('meeple-card-flip-container')).not.toBeInTheDocument();
    expect(screen.getByTestId('meeple-card')).toBeInTheDocument();
  });

  it('should NOT render flip container without flipData', () => {
    render(<MeepleCard {...defaultProps} flippable />);

    expect(screen.queryByTestId('meeple-card-flip-container')).not.toBeInTheDocument();
    expect(screen.getByTestId('meeple-card')).toBeInTheDocument();
  });

  it('should render flip container when flippable with flipData', () => {
    render(
      <MeepleCard {...defaultProps} flippable flipData={flipData} />,
    );

    expect(screen.getByTestId('meeple-card-flip-container')).toBeInTheDocument();
    expect(screen.getByTestId('meeple-card-front')).toBeInTheDocument();
    expect(screen.getByTestId('meeple-card-back')).toBeInTheDocument();
  });

  it('should still render card content inside flip front', () => {
    render(
      <MeepleCard {...defaultProps} flippable flipData={flipData} />,
    );

    expect(screen.getByText('Test Game')).toBeInTheDocument();
  });

  it('should show flip back content', () => {
    render(
      <MeepleCard {...defaultProps} flippable flipData={flipData} />,
    );

    expect(screen.getByText('A great game about strategy.')).toBeInTheDocument();
  });

  it('should show back categories', () => {
    render(
      <MeepleCard {...defaultProps} flippable flipData={flipData} />,
    );

    expect(screen.getByText('Strategy')).toBeInTheDocument();
  });

  it('should flip on click', () => {
    const onFlip = vi.fn();
    render(
      <MeepleCard
        {...defaultProps}
        flippable
        flipData={flipData}
        onFlip={onFlip}
      />,
    );

    fireEvent.click(screen.getByTestId('meeple-card-flip-container'));
    expect(onFlip).toHaveBeenCalledWith(true);
  });

  it('should not render HoverPreview when flippable (flip takes priority)', () => {
    render(
      <MeepleCard
        {...defaultProps}
        id="game-1"
        flippable
        flipData={flipData}
        showPreview
        onFetchPreview={vi.fn()}
        previewData={{ description: 'Preview text' }}
      />,
    );

    // FlipCard wrapper should be present, not HoverPreview
    expect(screen.getByTestId('meeple-card-flip-container')).toBeInTheDocument();
  });
});
