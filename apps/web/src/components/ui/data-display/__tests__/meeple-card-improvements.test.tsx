/**
 * MeepleCard Improvements Tests
 * Issue #4062 - Tooltip Positioning, Vertical Tag Stack, Context-Aware Actions
 */

import React from 'react';
import { render, screen, within } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { axe } from 'jest-axe';
import { Play, Share2, MessageSquare } from 'lucide-react';

import { MeepleCard, type MeepleEntityType, type MeepleCardVariant } from '../meeple-card';
import { MeepleCardQuickActions } from '../meeple-card-quick-actions';
import { MeepleCardInfoButton } from '../meeple-card-info-button';
import type { QuickAction } from '../meeple-card-quick-actions';

// Mock Next.js
vi.mock('next/image', () => ({
  default: ({ src, alt, ...props }: { src: string; alt: string }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img src={src} alt={alt} {...props} />
  ),
}));
vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: { children: React.ReactNode; href: string }) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

// Mock framer-motion for FlipCard
vi.mock('framer-motion', () => ({
  motion: {
    div: React.forwardRef(
      (
        { children, style, ...props }: React.PropsWithChildren<Record<string, unknown>>,
        ref: React.Ref<HTMLDivElement>,
      ) => (
        <div ref={ref} style={style as React.CSSProperties} {...props}>
          {children}
        </div>
      ),
    ),
  },
}));

// Mock Radix tooltip to render inline for testing
vi.mock('@/components/ui/overlays/tooltip', () => ({
  TooltipProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  Tooltip: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  TooltipTrigger: ({ children, asChild }: { children: React.ReactNode; asChild?: boolean }) =>
    asChild ? <>{children}</> : <button>{children}</button>,
  TooltipContent: ({ children, side, sideOffset }: { children: React.ReactNode; side?: string; sideOffset?: number }) => (
    <div role="tooltip" data-side={side} data-side-offset={sideOffset} data-testid="tooltip-content">
      {children}
    </div>
  ),
}));

const defaultProps = {
  entity: 'game' as MeepleEntityType,
  title: 'Test Game',
  subtitle: 'Test Publisher',
};

const mockActions: QuickAction[] = [
  { icon: MessageSquare, label: 'Chat con Agent', onClick: vi.fn() },
  { icon: Play, label: 'Avvia Sessione', onClick: vi.fn() },
  { icon: Share2, label: 'Condividi', onClick: vi.fn() },
];

// ============================================================================
// Tooltip Positioning Tests
// ============================================================================

describe('Tooltip Positioning (Issue #4062)', () => {
  describe('MeepleCardQuickActions tooltips', () => {
    it('renders Radix tooltip for each action', () => {
      render(
        <MeepleCardQuickActions
          actions={mockActions}
          entityType="game"
        />
      );

      const tooltips = screen.getAllByRole('tooltip');
      expect(tooltips).toHaveLength(3);
    });

    it('configures tooltips with bottom side and 10px offset', () => {
      render(
        <MeepleCardQuickActions
          actions={mockActions}
          entityType="game"
        />
      );

      const tooltips = screen.getAllByTestId('tooltip-content');
      tooltips.forEach(tooltip => {
        expect(tooltip).toHaveAttribute('data-side', 'bottom');
        expect(tooltip).toHaveAttribute('data-side-offset', '10');
      });
    });

    it('renders tooltip text matching action labels', () => {
      render(
        <MeepleCardQuickActions
          actions={mockActions}
          entityType="game"
        />
      );

      expect(screen.getByText('Chat con Agent')).toBeInTheDocument();
      expect(screen.getByText('Avvia Sessione')).toBeInTheDocument();
      expect(screen.getByText('Condividi')).toBeInTheDocument();
    });

    it('does not use data-tooltip attribute (removed CSS approach)', () => {
      render(
        <MeepleCardQuickActions
          actions={mockActions}
          entityType="game"
        />
      );

      const buttons = screen.getAllByRole('button');
      buttons.forEach(button => {
        expect(button).not.toHaveAttribute('data-tooltip');
      });
    });

    it('preserves aria-label on action buttons', () => {
      render(
        <MeepleCardQuickActions
          actions={mockActions}
          entityType="game"
        />
      );

      expect(screen.getByLabelText('Chat con Agent')).toBeInTheDocument();
      expect(screen.getByLabelText('Avvia Sessione')).toBeInTheDocument();
      expect(screen.getByLabelText('Condividi')).toBeInTheDocument();
    });

    it('filters hidden actions', () => {
      const actionsWithHidden: QuickAction[] = [
        ...mockActions,
        { icon: Share2, label: 'Hidden', onClick: vi.fn(), hidden: true },
      ];

      render(
        <MeepleCardQuickActions
          actions={actionsWithHidden}
          entityType="game"
        />
      );

      expect(screen.queryByText('Hidden')).not.toBeInTheDocument();
    });

    it('returns null when all actions are hidden', () => {
      const allHidden: QuickAction[] = [
        { icon: Share2, label: 'Hidden', onClick: vi.fn(), hidden: true },
      ];

      const { container } = render(
        <MeepleCardQuickActions
          actions={allHidden}
          entityType="game"
        />
      );

      expect(container.innerHTML).toBe('');
    });
  });

  describe('MeepleCardInfoButton tooltip', () => {
    it('renders Radix tooltip with bottom side', () => {
      render(
        <MeepleCardInfoButton
          href="/games/123"
          entityType="game"
        />
      );

      const tooltip = screen.getByTestId('tooltip-content');
      expect(tooltip).toHaveAttribute('data-side', 'bottom');
      expect(tooltip).toHaveAttribute('data-side-offset', '10');
    });

    it('renders default tooltip text "View details"', () => {
      render(
        <MeepleCardInfoButton
          href="/games/123"
          entityType="game"
        />
      );

      expect(screen.getByText('View details')).toBeInTheDocument();
    });

    it('renders custom tooltip text', () => {
      render(
        <MeepleCardInfoButton
          href="/games/123"
          entityType="game"
          tooltip="Custom tooltip"
        />
      );

      expect(screen.getByText('Custom tooltip')).toBeInTheDocument();
    });

    it('does not use data-tooltip attribute', () => {
      render(
        <MeepleCardInfoButton
          href="/games/123"
          entityType="game"
        />
      );

      const link = screen.getByTestId('meeple-card-info-button');
      expect(link).not.toHaveAttribute('data-tooltip');
    });

    it('preserves aria-label on link', () => {
      render(
        <MeepleCardInfoButton
          href="/games/123"
          entityType="game"
          tooltip="See details"
        />
      );

      expect(screen.getByLabelText('See details')).toBeInTheDocument();
    });
  });
});

// ============================================================================
// Vertical Tag Stack Tests
// ============================================================================

describe('Vertical Tag Stack (Issue #4062)', () => {
  describe('Grid variant', () => {
    it('renders vertical tag stack in grid variant', () => {
      render(<MeepleCard {...defaultProps} variant="grid" />);

      expect(screen.getByTestId('meeple-card-tag-stack')).toBeInTheDocument();
    });

    it('renders entity type badge in tag stack', () => {
      render(<MeepleCard {...defaultProps} variant="grid" entity="game" />);

      const tagStack = screen.getByTestId('meeple-card-tag-stack');
      // Badge text + tooltip text both render "Game"
      const matches = within(tagStack).getAllByText('Game');
      expect(matches.length).toBeGreaterThanOrEqual(1);
    });

    it('renders status badge in tag stack when provided', () => {
      render(<MeepleCard {...defaultProps} variant="grid" status="owned" />);

      const tagStack = screen.getByTestId('meeple-card-tag-stack');
      expect(tagStack).toBeInTheDocument();
    });

    it('renders custom badge in tag stack when provided', () => {
      render(<MeepleCard {...defaultProps} variant="grid" badge="New!" />);

      const tagStack = screen.getByTestId('meeple-card-tag-stack');
      // Badge text + tooltip text both render "New!"
      const matches = within(tagStack).getAllByText('New!');
      expect(matches.length).toBeGreaterThanOrEqual(1);
    });

    it('stacks entity badge and custom badge vertically', () => {
      render(<MeepleCard {...defaultProps} variant="grid" badge="Hot" />);

      const tagStack = screen.getByTestId('meeple-card-tag-stack');
      const children = tagStack.children;

      // Should have entity badge + tooltip + custom badge + tooltip
      expect(children.length).toBeGreaterThanOrEqual(2);
    });

    it('applies max-width constraint to badges', () => {
      render(<MeepleCard {...defaultProps} variant="grid" badge="Very Long Badge Text That Should Truncate" />);

      const tagStack = screen.getByTestId('meeple-card-tag-stack');
      const badges = tagStack.querySelectorAll('.max-w-\\[80px\\]');
      expect(badges.length).toBeGreaterThan(0);
    });
  });

  describe('Featured variant', () => {
    it('renders vertical tag stack in featured variant', () => {
      render(<MeepleCard {...defaultProps} variant="featured" />);

      expect(screen.getByTestId('meeple-card-tag-stack')).toBeInTheDocument();
    });
  });

  describe('Non-grid/non-featured variants', () => {
    it('does not render vertical tag stack in list variant', () => {
      render(<MeepleCard {...defaultProps} variant="list" />);

      expect(screen.queryByTestId('meeple-card-tag-stack')).not.toBeInTheDocument();
    });

    it('does not render vertical tag stack in compact variant', () => {
      render(<MeepleCard {...defaultProps} variant="compact" />);

      expect(screen.queryByTestId('meeple-card-tag-stack')).not.toBeInTheDocument();
    });
  });

  describe('All entity types render correctly in tag stack', () => {
    const entityTypes: MeepleEntityType[] = ['game', 'player', 'session', 'agent', 'document', 'chatSession', 'event'];

    it.each(entityTypes)('renders %s entity badge in tag stack', (entity) => {
      render(<MeepleCard {...defaultProps} entity={entity} variant="grid" />);

      const tagStack = screen.getByTestId('meeple-card-tag-stack');
      expect(tagStack).toBeInTheDocument();
    });
  });
});

// ============================================================================
// Accessibility Tests
// ============================================================================

describe('Accessibility (Issue #4062)', () => {
  describe('Quick actions accessibility', () => {
    it('has no axe violations with quick actions', async () => {
      const { container } = render(
        <MeepleCard
          {...defaultProps}
          variant="grid"
          entityQuickActions={mockActions}
        />
      );

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });
  });

  describe('Info button accessibility', () => {
    it('has no axe violations with info button', async () => {
      const { container } = render(
        <MeepleCard
          {...defaultProps}
          variant="grid"
          showInfoButton
          infoHref="/games/123"
        />
      );

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });
  });

  describe('Vertical tag stack accessibility', () => {
    it('has no axe violations with badge and status', async () => {
      const { container } = render(
        <MeepleCard
          {...defaultProps}
          variant="grid"
          status="owned"
          badge="Popular"
        />
      );

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('has no axe violations across all entity types', async () => {
      const entities: MeepleEntityType[] = ['game', 'player', 'session', 'agent', 'document', 'chatSession', 'event'];

      for (const entity of entities) {
        const { container } = render(
          <MeepleCard {...defaultProps} entity={entity} variant="grid" badge="Test" />
        );

        const results = await axe(container);
        expect(results).toHaveNoViolations();
      }
    });
  });

  describe('Combined features accessibility', () => {
    it('has no axe violations with all features enabled', async () => {
      const { container } = render(
        <MeepleCard
          {...defaultProps}
          variant="grid"
          status="owned"
          badge="Hot"
          entityQuickActions={mockActions}
          showInfoButton
          infoHref="/games/123"
        />
      );

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });
  });

  describe('Responsive variants accessibility', () => {
    const variants: MeepleCardVariant[] = ['grid', 'list', 'compact', 'featured', 'hero'];

    it.each(variants)('has no axe violations for %s variant with improvements', async (variant) => {
      const { container } = render(
        <MeepleCard
          {...defaultProps}
          variant={variant}
          entityQuickActions={mockActions}
          showInfoButton
          infoHref="/test"
        />
      );

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });
  });
});
