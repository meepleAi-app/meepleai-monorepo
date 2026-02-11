/**
 * MeepleCard Snapshot Tests (Issue #3328)
 *
 * Visual regression tests for all variants and states
 */

import React from 'react';
import { render } from '@testing-library/react';
import { Users, Clock, MapPin, Trophy } from 'lucide-react';
import { MeepleCard, MeepleCardSkeleton, type MeepleEntityType, type MeepleCardVariant } from '../meeple-card';
import { vi } from 'vitest';

// Mock Next.js Image component for consistent snapshots
vi.mock('next/image', () => ({
  default: ({ src, alt, fill, sizes, className, loading, placeholder, blurDataURL, ...props }: Record<string, unknown>) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img
      src={src as string}
      alt={alt as string}
      className={className as string}
      data-fill={fill ? 'true' : undefined}
      data-sizes={sizes as string}
      data-loading={loading as string}
      data-placeholder={placeholder as string}
      {...props}
    />
  ),
}));

describe('MeepleCard - Snapshots', () => {
  describe('Layout Variants', () => {
    const baseProps = {
      entity: 'game' as MeepleEntityType,
      title: 'Twilight Imperium',
      subtitle: 'Fantasy Flight Games · 2017',
      imageUrl: 'https://example.com/game.jpg',
      rating: 8.7,
      ratingMax: 10,
      metadata: [
        { icon: Users, value: '3-6' },
        { icon: Clock, value: '4-8h' },
      ],
    };

    it('should match snapshot for grid variant', () => {
      const { container } = render(
        <MeepleCard {...baseProps} variant="grid" />
      );
      expect(container.firstChild).toMatchSnapshot();
    });

    it('should match snapshot for list variant', () => {
      const { container } = render(
        <MeepleCard {...baseProps} variant="list" />
      );
      expect(container.firstChild).toMatchSnapshot();
    });

    it('should match snapshot for compact variant', () => {
      const { container } = render(
        <MeepleCard {...baseProps} variant="compact" />
      );
      expect(container.firstChild).toMatchSnapshot();
    });

    it('should match snapshot for featured variant', () => {
      const { container } = render(
        <MeepleCard
          {...baseProps}
          variant="featured"
          actions={[
            { label: 'Browse', primary: true },
            { label: 'Clone' },
          ]}
        />
      );
      expect(container.firstChild).toMatchSnapshot();
    });

    it('should match snapshot for hero variant', () => {
      const { container } = render(
        <MeepleCard
          {...baseProps}
          variant="hero"
          actions={[
            { label: 'Register Now', primary: true },
            { label: 'Learn More' },
          ]}
        />
      );
      expect(container.firstChild).toMatchSnapshot();
    });
  });

  describe('Entity Types', () => {
    it('should match snapshot for game entity', () => {
      const { container } = render(
        <MeepleCard
          entity="game"
          variant="grid"
          title="Azul"
          subtitle="Plan B Games"
          imageUrl="https://example.com/azul.jpg"
          rating={4.2}
          metadata={[{ icon: Users, value: '2-4' }]}
        />
      );
      expect(container.firstChild).toMatchSnapshot();
    });

    it('should match snapshot for player entity', () => {
      const { container } = render(
        <MeepleCard
          entity="player"
          variant="grid"
          title="Marco Rossi"
          subtitle="@marco_games · Since 2019"
          avatarUrl="https://example.com/avatar.jpg"
          metadata={[
            { label: '142 plays' },
            { icon: Trophy, label: 'Top 5%' },
          ]}
        />
      );
      expect(container.firstChild).toMatchSnapshot();
    });

    it('should match snapshot for collection entity', () => {
      const { container } = render(
        <MeepleCard
          entity="custom"
          variant="grid"
          title="Euro Games Essentials"
          subtitle="Curated by @boardgame_guru"
          imageUrl="https://example.com/collection.jpg"
          metadata={[
            { label: '24 games' },
            { label: '1.2K views' },
          ]}
        />
      );
      expect(container.firstChild).toMatchSnapshot();
    });

    it('should match snapshot for event entity', () => {
      const { container } = render(
        <MeepleCard
          entity="event"
          variant="grid"
          title="Saturday Game Night"
          subtitle="Feb 15, 2026 · 7:00 PM"
          imageUrl="https://example.com/event.jpg"
          metadata={[
            { icon: MapPin, label: 'Milano, IT' },
            { icon: Users, label: '4/6 spots' },
          ]}
        />
      );
      expect(container.firstChild).toMatchSnapshot();
    });

    it('should match snapshot for custom entity', () => {
      const { container } = render(
        <MeepleCard
          entity="custom"
          variant="grid"
          title="Custom Entity"
          subtitle="With custom color"
          customColor="142 76% 36%"
          imageUrl="https://example.com/custom.jpg"
          metadata={[{ label: 'Custom metadata' }]}
        />
      );
      expect(container.firstChild).toMatchSnapshot();
    });
  });

  describe('Loading State', () => {
    const variants: MeepleCardVariant[] = ['grid', 'list', 'compact', 'featured', 'hero'];

    it.each(variants)('should match snapshot for %s skeleton', variant => {
      const { container } = render(<MeepleCardSkeleton variant={variant} />);
      expect(container.firstChild).toMatchSnapshot();
    });

    it('should match snapshot for loading prop', () => {
      const { container } = render(
        <MeepleCard
          entity="game"
          title="Loading Card"
          loading
        />
      );
      expect(container.firstChild).toMatchSnapshot();
    });
  });

  describe('Special States', () => {
    it('should match snapshot with badge', () => {
      const { container } = render(
        <MeepleCard
          entity="game"
          variant="grid"
          title="New Release"
          subtitle="Publisher"
          imageUrl="https://example.com/game.jpg"
          badge="New"
        />
      );
      expect(container.firstChild).toMatchSnapshot();
    });

    it('should match snapshot with onClick (interactive)', () => {
      const { container } = render(
        <MeepleCard
          entity="game"
          variant="grid"
          title="Clickable Card"
          subtitle="Publisher"
          onClick={() => {}}
        />
      );
      expect(container.firstChild).toMatchSnapshot();
    });

    it('should match snapshot with disabled action', () => {
      const { container } = render(
        <MeepleCard
          entity="game"
          variant="featured"
          title="Featured Game"
          subtitle="Publisher"
          actions={[
            { label: 'Available', primary: true },
            { label: 'Sold Out', disabled: true },
          ]}
        />
      );
      expect(container.firstChild).toMatchSnapshot();
    });

    it('should match snapshot without image', () => {
      const { container } = render(
        <MeepleCard
          entity="game"
          variant="grid"
          title="No Image Game"
          subtitle="Publisher"
        />
      );
      expect(container.firstChild).toMatchSnapshot();
    });

    it('should match snapshot with minimal props', () => {
      const { container } = render(
        <MeepleCard
          entity="game"
          title="Minimal Card"
        />
      );
      expect(container.firstChild).toMatchSnapshot();
    });
  });

  describe('Complete Examples', () => {
    it('should match snapshot for fully loaded game card', () => {
      const { container } = render(
        <MeepleCard
          entity="game"
          variant="grid"
          title="Twilight Imperium"
          subtitle="Fantasy Flight Games · 2017"
          imageUrl="https://example.com/ti4.jpg"
          rating={8.7}
          ratingMax={10}
          badge="Popular"
          metadata={[
            { icon: Users, value: '3-6' },
            { icon: Clock, value: '4-8h' },
          ]}
          onClick={() => {}}
          data-testid="complete-game-card"
        />
      );
      expect(container.firstChild).toMatchSnapshot();
    });

    it('should match snapshot for tournament hero', () => {
      const { container } = render(
        <MeepleCard
          entity="event"
          variant="hero"
          title="MeepleAI Tournament"
          subtitle="Grand Finals · Live Streamed"
          imageUrl="https://example.com/tournament.jpg"
          metadata={[
            { icon: Trophy, label: '$500 Prize' },
            { icon: MapPin, label: 'Online' },
            { icon: Users, label: '128 players' },
          ]}
          actions={[
            { label: 'Register Now', primary: true },
            { label: 'Learn More' },
          ]}
          onClick={() => {}}
        />
      );
      expect(container.firstChild).toMatchSnapshot();
    });
  });
});
