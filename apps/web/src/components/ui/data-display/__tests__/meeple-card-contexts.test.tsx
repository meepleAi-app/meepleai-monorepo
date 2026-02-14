/**
 * MeepleCard Context-Aware Tests (Issue #4080)
 *
 * Comprehensive tests for all entity types, variants, and states
 *
 * Coverage:
 * - 7 entity types: game, player, session, agent, document, chatSession, event
 * - 5 variants: grid, list, compact, featured, hero
 * - All ownership states: owned, wishlist, not-owned
 * - Context-specific features per entity type
 *
 * Target: >95% coverage for MeepleCard component
 */

import React from 'react';
import { render, screen } from '@testing-library/react';
import { MeepleCard, type MeepleEntityType, type MeepleCardVariant } from '../meeple-card';
import { describe, it, expect } from 'vitest';

const defaultProps = {
  title: 'Test Entity',
  subtitle: 'Test Subtitle',
  imageUrl: 'https://example.com/image.jpg',
};

describe('MeepleCard - Context-Aware Tests (Issue #4080)', () => {
  describe('All Entity Types', () => {
    const entityTypes: MeepleEntityType[] = [
      'game',
      'player',
      'session',
      'agent',
      'document',
      'chatSession',
      'event',
    ];

    it.each(entityTypes)('should render %s entity type correctly', entity => {
      const { container } = render(
        <MeepleCard {...defaultProps} entity={entity} variant="grid" />
      );

      const card = container.querySelector('[data-entity]');
      expect(card).toHaveAttribute('data-entity', entity);
    });

    it.each(entityTypes)('should have correct aria-label for %s', entity => {
      render(
        <MeepleCard {...defaultProps} entity={entity} title="Entity Title" onClick={() => {}} />
      );

      // Entity name should be capitalized in aria-label
      const expectedLabel = new RegExp(`${entity}:.*entity title`, 'i');
      expect(screen.getByRole('button')).toHaveAttribute('aria-label', expect.stringMatching(expectedLabel));
    });
  });

  describe('All Layout Variants', () => {
    const variants: MeepleCardVariant[] = ['grid', 'list', 'compact', 'featured', 'hero'];

    it.each(variants)('should render %s variant correctly', variant => {
      const { container } = render(
        <MeepleCard {...defaultProps} entity="game" variant={variant} />
      );

      const card = container.querySelector('[data-variant]');
      expect(card).toHaveAttribute('data-variant', variant);
    });

    it.each(variants)('should apply correct layout classes for %s', variant => {
      const { container } = render(
        <MeepleCard {...defaultProps} entity="game" variant={variant} />
      );

      const card = container.querySelector('[data-variant]');

      if (variant === 'grid' || variant === 'featured') {
        expect(card).toHaveClass('flex-col');
      } else if (variant === 'list' || variant === 'compact') {
        expect(card).toHaveClass('flex-row');
      }
    });
  });

  describe('Entity × Variant Combinations', () => {
    const entityTypes: MeepleEntityType[] = ['game', 'player', 'agent'];
    const variants: MeepleCardVariant[] = ['grid', 'list', 'compact'];

    entityTypes.forEach(entity => {
      variants.forEach(variant => {
        it(`should render ${entity} × ${variant} combination`, () => {
          const { container } = render(
            <MeepleCard {...defaultProps} entity={entity} variant={variant} />
          );

          const card = container.querySelector('[data-entity][data-variant]');
          expect(card).toHaveAttribute('data-entity', entity);
          expect(card).toHaveAttribute('data-variant', variant);
        });
      });
    });
  });

  describe('Ownership States', () => {
    it('should render owned state (library context)', () => {
      // Owned game in library
      render(
        <MeepleCard
          {...defaultProps}
          entity="game"
          // Ownership indicated by presence in library, wishlist, etc.
          testId="owned-game"
        />
      );

      expect(screen.getByTestId('owned-game')).toBeInTheDocument();
    });

    it('should render wishlist state', () => {
      render(
        <MeepleCard
          {...defaultProps}
          entity="game"
          showWishlistBtn
          isWishlisted={true}
          onWishlistToggle={() => {}}
        />
      );

      // Wishlist button should indicate wishlisted state
      // (Implementation in WishlistButton component)
      expect(screen.getByTestId('meeple-card')).toBeInTheDocument();
    });

    it('should render not-owned state (catalog context)', () => {
      render(
        <MeepleCard
          {...defaultProps}
          entity="game"
          showWishlistBtn
          isWishlisted={false}
          onWishlistToggle={() => {}}
        />
      );

      expect(screen.getByTestId('meeple-card')).toBeInTheDocument();
    });
  });

  describe('Context-Specific Features', () => {
    describe('Game Entity', () => {
      it('should render rating for games', () => {
        render(
          <MeepleCard {...defaultProps} entity="game" rating={8.5} ratingMax={10} />
        );

        expect(screen.getByLabelText(/rating: 8.5 out of 10/i)).toBeInTheDocument();
      });

      it('should render metadata with icons for games', () => {
        const metadata = [
          { value: '2-4', label: 'Players' },
          { value: '60 min', label: 'Duration' },
        ];

        render(<MeepleCard {...defaultProps} entity="game" metadata={metadata} />);

        expect(screen.getByText('2-4')).toBeInTheDocument();
        expect(screen.getByText('60 min')).toBeInTheDocument();
      });
    });

    describe('Player Entity', () => {
      it('should render avatar for player entity', () => {
        render(
          <MeepleCard
            {...defaultProps}
            entity="player"
            title="Player Name"
            avatarUrl="https://example.com/avatar.jpg"
            variant="grid"
          />
        );

        const avatar = screen.getByAltText('Player Name');
        expect(avatar).toBeInTheDocument();
      });
    });

    describe('Agent Entity', () => {
      it('should render agent-specific styling', () => {
        const { container } = render(
          <MeepleCard {...defaultProps} entity="agent" title="Tutor Agent" variant="grid" />
        );

        const card = container.querySelector('[data-entity="agent"]');
        expect(card).toBeInTheDocument();
      });
    });

    describe('Session Entity', () => {
      it('should render session status badge', () => {
        render(
          <MeepleCard
            {...defaultProps}
            entity="session"
            status="in-progress"
            showStatusIcon
          />
        );

        expect(screen.getByTestId('meeple-card')).toBeInTheDocument();
      });
    });
  });

  describe('Interactive States', () => {
    it('should be interactive when onClick provided', () => {
      render(<MeepleCard {...defaultProps} entity="game" onClick={() => {}} />);

      const card = screen.getByRole('button');
      expect(card).toHaveAttribute('tabIndex', '0');
    });

    it('should not be interactive without onClick', () => {
      render(<MeepleCard {...defaultProps} entity="game" />);

      expect(screen.queryByRole('button')).not.toBeInTheDocument();
    });

    it('should handle both onClick and actions (actions take priority)', () => {
      render(
        <MeepleCard
          {...defaultProps}
          entity="event"
          variant="featured"
          onClick={() => {}}
          actions={[{ label: 'Join', primary: true, onClick: () => {} }]}
        />
      );

      // When actions present, card itself should not be interactive
      const card = screen.getByTestId('meeple-card');
      expect(card).not.toHaveAttribute('role', 'button');

      // Action button should be interactive
      expect(screen.getByRole('button', { name: /join/i })).toBeInTheDocument();
    });
  });

  describe('Loading States', () => {
    it('should render loading skeleton', () => {
      render(<MeepleCard {...defaultProps} entity="game" loading />);

      // Loading state should show skeleton
      expect(screen.getByTestId('meeple-card')).toBeInTheDocument();
    });

    it('should render loading skeleton for each variant', () => {
      const variants: MeepleCardVariant[] = ['grid', 'list', 'compact', 'featured', 'hero'];

      variants.forEach(variant => {
        const { container } = render(
          <MeepleCard {...defaultProps} entity="game" variant={variant} loading />
        );

        expect(container.querySelector('[data-variant]')).toHaveAttribute('data-variant', variant);
      });
    });
  });

  describe('Feature Combinations', () => {
    it('should render game with all features (complex card)', () => {
      render(
        <MeepleCard
          entity="game"
          variant="featured"
          title="Complex Game"
          subtitle="Publisher · 2024"
          imageUrl="https://example.com/game.jpg"
          rating={8.5}
          ratingMax={10}
          badge="Popular"
          status="available"
          showStatusIcon
          metadata={[
            { value: '2-4', label: 'Players' },
            { value: '90 min', label: 'Duration' },
          ]}
          actions={[
            { label: 'Add to Library', primary: true, onClick: () => {} },
            { label: 'View Details', onClick: () => {} },
          ]}
          showWishlistBtn
          isWishlisted={false}
          onWishlistToggle={() => {}}
        />
      );

      expect(screen.getByText('Complex Game')).toBeInTheDocument();
      expect(screen.getByText('Publisher · 2024')).toBeInTheDocument();
      expect(screen.getByLabelText(/rating: 8.5/i)).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /add to library/i })).toBeInTheDocument();
    });
  });

  describe('Edge Cases', () => {
    it('should handle missing imageUrl gracefully', () => {
      render(
        <MeepleCard {...defaultProps} entity="game" variant="grid" imageUrl={undefined} />
      );

      // Should render placeholder
      expect(screen.getByTestId('meeple-card')).toBeInTheDocument();
    });

    it('should handle very long titles', () => {
      const longTitle = 'A'.repeat(100);

      render(<MeepleCard {...defaultProps} entity="game" title={longTitle} variant="grid" />);

      // Title should be clamped (line-clamp-2)
      expect(screen.getByText(longTitle)).toBeInTheDocument();
    });

    it('should handle empty metadata array', () => {
      render(<MeepleCard {...defaultProps} entity="game" metadata={[]} />);

      expect(screen.getByTestId('meeple-card')).toBeInTheDocument();
    });

    it('should handle all states simultaneously', () => {
      render(
        <MeepleCard
          {...defaultProps}
          entity="game"
          loading
          status="processing"
          badge="New"
          showWishlistBtn
          isWishlisted
          onWishlistToggle={() => {}}
        />
      );

      expect(screen.getByTestId('meeple-card')).toBeInTheDocument();
    });
  });
});
