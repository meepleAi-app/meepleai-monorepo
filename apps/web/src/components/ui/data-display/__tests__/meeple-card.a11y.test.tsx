/**
 * MeepleCard Accessibility Tests (Issue #3328)
 *
 * Tests WCAG 2.1 AA compliance using jest-axe
 *
 * Coverage:
 * - All entity types (game, player, session, agent, document, chatSession, event, custom)
 * - All layout variants (grid, list, compact, featured, hero)
 * - Interactive states (clickable vs static)
 * - Keyboard navigation
 * - ARIA attributes
 * - Image accessibility
 */

import React from 'react';
import { render, screen } from '@testing-library/react';
import { axe } from 'jest-axe';
import { Users, Clock, MapPin, Trophy } from 'lucide-react';
import { MeepleCard, MeepleCardSkeleton, type MeepleEntityType, type MeepleCardVariant } from '../meeple-card';
import { vi } from 'vitest';

// Mock Next.js Image component
vi.mock('next/image', () => ({
  default: ({ src, alt, ...props }: { src: string; alt: string }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img src={src} alt={alt} {...props} />
  ),
}));

// Default props for testing
const defaultProps = {
  entity: 'game' as MeepleEntityType,
  title: 'Test Game Title',
  subtitle: 'Test Publisher · 2024',
  imageUrl: 'https://example.com/game.jpg',
};

describe('MeepleCard - Accessibility', () => {
  describe('Entity Types - No Violations', () => {
    const entityTypes: MeepleEntityType[] = ['game', 'player', 'session', 'agent', 'document', 'chatSession', 'event', 'custom'];

    it.each(entityTypes)('should have no accessibility violations for %s entity', async entity => {
      const { container } = render(
        <MeepleCard {...defaultProps} entity={entity} />
      );

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });
  });

  describe('Layout Variants - No Violations', () => {
    const variants: MeepleCardVariant[] = ['grid', 'list', 'compact', 'featured', 'hero'];

    it.each(variants)('should have no accessibility violations for %s variant', async variant => {
      const { container } = render(
        <MeepleCard {...defaultProps} variant={variant} />
      );

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });
  });

  describe('Interactive States - No Violations', () => {
    it('should have no accessibility violations when clickable', async () => {
      const { container } = render(
        <MeepleCard {...defaultProps} onClick={() => {}} />
      );

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('should have no accessibility violations when non-interactive', async () => {
      const { container } = render(
        <MeepleCard {...defaultProps} />
      );

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('should have no accessibility violations with action buttons', async () => {
      const { container } = render(
        <MeepleCard
          {...defaultProps}
          variant="featured"
          actions={[
            { label: 'Primary Action', primary: true, onClick: () => {} },
            { label: 'Secondary Action', onClick: () => {} },
          ]}
        />
      );

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('should have no accessibility violations with disabled action', async () => {
      const { container } = render(
        <MeepleCard
          {...defaultProps}
          variant="featured"
          actions={[
            { label: 'Disabled Action', disabled: true },
          ]}
        />
      );

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });
  });

  describe('ARIA Attributes', () => {
    it('should have proper aria-label when interactive', () => {
      render(<MeepleCard {...defaultProps} onClick={() => {}} />);

      const card = screen.getByRole('button', { name: /game: test game title/i });
      expect(card).toHaveAttribute('aria-label', 'Game: Test Game Title');
    });

    it('should have aria-label even when not interactive', () => {
      render(<MeepleCard {...defaultProps} />);

      const card = screen.getByLabelText(/game: test game title/i);
      expect(card).toBeInTheDocument();
    });

    it('should have entity-specific aria-label for player', () => {
      render(<MeepleCard {...defaultProps} entity="player" title="John Doe" onClick={() => {}} />);

      const card = screen.getByRole('button', { name: /player: john doe/i });
      expect(card).toBeInTheDocument();
    });

    it('should have entity-specific aria-label for custom', () => {
      render(<MeepleCard {...defaultProps} entity="custom" title="My Custom Entity" onClick={() => {}} />);

      const card = screen.getByRole('button', { name: /custom: my custom entity/i });
      expect(card).toBeInTheDocument();
    });

    it('should have entity-specific aria-label for event', () => {
      render(<MeepleCard {...defaultProps} entity="event" title="Game Night" onClick={() => {}} />);

      const card = screen.getByRole('button', { name: /event: game night/i });
      expect(card).toBeInTheDocument();
    });

    it('should have entity-specific aria-label for custom', () => {
      render(<MeepleCard {...defaultProps} entity="custom" title="Custom Item" onClick={() => {}} />);

      const card = screen.getByRole('button', { name: /custom: custom item/i });
      expect(card).toBeInTheDocument();
    });
  });

  describe('Keyboard Navigation', () => {
    it('should be keyboard accessible when interactive', () => {
      render(<MeepleCard {...defaultProps} onClick={() => {}} />);

      const card = screen.getByRole('button');
      expect(card).toHaveAttribute('tabindex', '0');
    });

    it('should not be tabbable when not interactive', () => {
      render(<MeepleCard {...defaultProps} />);

      const card = screen.getByLabelText(/game: test game title/i);
      expect(card).not.toHaveAttribute('tabindex');
    });

    it('should have role="button" when clickable', () => {
      render(<MeepleCard {...defaultProps} onClick={() => {}} />);

      expect(screen.getByRole('button')).toBeInTheDocument();
    });

    it('should not have role="button" when not clickable', () => {
      render(<MeepleCard {...defaultProps} />);

      expect(screen.queryByRole('button')).not.toBeInTheDocument();
    });
  });

  describe('Image Accessibility', () => {
    it('should have alt text on card image', () => {
      render(<MeepleCard {...defaultProps} variant="grid" />);

      const image = screen.getByAltText('Test Game Title');
      expect(image).toBeInTheDocument();
    });

    it('should have alt text with placeholder image', () => {
      render(<MeepleCard {...defaultProps} variant="grid" imageUrl={undefined} />);

      const image = screen.getByAltText('Test Game Title');
      expect(image).toBeInTheDocument();
    });

    it('should have alt text on avatar for player entity', () => {
      render(
        <MeepleCard
          {...defaultProps}
          entity="player"
          title="Player Name"
          avatarUrl="https://example.com/avatar.jpg"
          variant="grid"
        />
      );

      const image = screen.getByAltText('Player Name');
      expect(image).toBeInTheDocument();
    });
  });

  describe('Rating Accessibility', () => {
    it('should have accessible rating display', () => {
      render(<MeepleCard {...defaultProps} rating={4.5} />);

      const rating = screen.getByLabelText(/rating: 4.5 out of 5/i);
      expect(rating).toBeInTheDocument();
    });

    it('should have accessible rating display for 10-scale', () => {
      render(<MeepleCard {...defaultProps} rating={8.5} ratingMax={10} />);

      const rating = screen.getByLabelText(/rating: 8.5 out of 10/i);
      expect(rating).toBeInTheDocument();
    });
  });

  describe('Metadata Accessibility', () => {
    it('should render metadata with icons accessibly', async () => {
      const { container } = render(
        <MeepleCard
          {...defaultProps}
          metadata={[
            { icon: Users, value: '2-4' },
            { icon: Clock, value: '30-60 min' },
          ]}
        />
      );

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('should render metadata without icons accessibly', async () => {
      const { container } = render(
        <MeepleCard
          {...defaultProps}
          metadata={[
            { label: '2-4 players' },
            { label: '30-60 min' },
          ]}
        />
      );

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });
  });

  describe('Loading State Accessibility', () => {
    it('should have no accessibility violations in loading state', async () => {
      const { container } = render(<MeepleCard {...defaultProps} loading />);

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('should have no accessibility violations for skeleton component', async () => {
      const { container } = render(<MeepleCardSkeleton />);

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it.each(['grid', 'list', 'compact', 'featured', 'hero'] as MeepleCardVariant[])(
      'should have no accessibility violations for %s skeleton variant',
      async variant => {
        const { container } = render(<MeepleCardSkeleton variant={variant} />);

        const results = await axe(container);
        expect(results).toHaveNoViolations();
      }
    );
  });

  describe('Badge Accessibility', () => {
    it('should have no accessibility violations with badge', async () => {
      const { container } = render(
        <MeepleCard {...defaultProps} badge="New" />
      );

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });
  });

  describe('Custom Color Accessibility', () => {
    it('should have no accessibility violations with custom color', async () => {
      const { container } = render(
        <MeepleCard
          {...defaultProps}
          entity="custom"
          customColor="142 76% 36%"
        />
      );

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });
  });

  describe('Complex Cards - No Violations', () => {
    it('should have no violations for fully loaded game card', async () => {
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
        />
      );

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('should have no violations for featured card with actions (no onClick on card)', async () => {
      // Note: When card has actions, onClick is not applied to card to avoid nested-interactive
      const { container } = render(
        <MeepleCard
          entity="event"
          variant="featured"
          title="Game Night"
          subtitle="Saturday, Feb 15 · 7:00 PM"
          imageUrl="https://example.com/event.jpg"
          metadata={[
            { icon: MapPin, label: 'Milano, IT' },
            { icon: Users, label: '4/6 spots' },
          ]}
          actions={[
            { label: 'Join Event', primary: true, onClick: () => {} },
            { label: 'Details', onClick: () => {} },
          ]}
        />
      );

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('should have no violations for hero card with actions', async () => {
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
          ]}
          actions={[
            { label: 'Register Now', primary: true, onClick: () => {} },
            { label: 'Learn More', onClick: () => {} },
          ]}
        />
      );

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });
  });
});
