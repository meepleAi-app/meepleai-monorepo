/**
 * MeepleCardInfoButton Tests
 * Issue #4033 - Comprehensive Testing
 * Issue #5025 - Button mode (drawer trigger) support
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { MeepleCardInfoButton } from '../meeple-card-info-button';

describe('MeepleCardInfoButton', () => {
  const defaultProps = {
    href: '/games/123',
    entityType: 'game' as const,
  };

  // --------------------------------------------------------------------------
  // Link mode (legacy — href only)
  // --------------------------------------------------------------------------

  describe('link mode (href)', () => {
    it('renders info button with link', () => {
      render(<MeepleCardInfoButton {...defaultProps} />);

      const link = screen.getByRole('link');
      expect(link).toBeInTheDocument();
      expect(link).toHaveAttribute('href', '/games/123');
    });

    it('displays default tooltip via aria-label', () => {
      render(<MeepleCardInfoButton {...defaultProps} />);

      const button = screen.getByTestId('meeple-card-info-button');
      expect(button).toHaveAttribute('aria-label', 'View details');
    });

    it('displays custom tooltip when provided', () => {
      render(
        <MeepleCardInfoButton
          {...defaultProps}
          tooltip="Vai al dettaglio"
        />
      );

      const button = screen.getByTestId('meeple-card-info-button');
      expect(button).toHaveAttribute('aria-label', 'Vai al dettaglio');
    });

    it('applies entity-colored styling for game', () => {
      render(<MeepleCardInfoButton {...defaultProps} entityType="game" />);

      const button = screen.getByTestId('meeple-card-info-button');
      expect(button).toHaveStyle({ '--tw-ring-color': 'hsl(25 95% 45%)' });
    });

    it('applies entity-colored styling for session', () => {
      render(
        <MeepleCardInfoButton {...defaultProps} entityType="session" />
      );

      const button = screen.getByTestId('meeple-card-info-button');
      expect(button).toHaveStyle({ '--tw-ring-color': 'hsl(240 60% 55%)' });
    });

    it('applies entity-colored styling for agent', () => {
      render(<MeepleCardInfoButton {...defaultProps} entityType="agent" />);

      const button = screen.getByTestId('meeple-card-info-button');
      expect(button).toHaveStyle({ '--tw-ring-color': 'hsl(38 92% 50%)' });
    });

    it('uses custom color when provided', () => {
      render(
        <MeepleCardInfoButton
          {...defaultProps}
          customColor="200 50% 50%"
        />
      );

      const button = screen.getByTestId('meeple-card-info-button');
      expect(button).toHaveStyle({ '--tw-ring-color': 'hsl(200 50% 50%)' });
    });

    it('has correct accessibility attributes', () => {
      render(<MeepleCardInfoButton {...defaultProps} />);

      const link = screen.getByRole('link', { name: 'View details' });
      expect(link).toBeInTheDocument();
    });

    it('renders with small size by default', () => {
      render(<MeepleCardInfoButton {...defaultProps} />);

      const button = screen.getByTestId('meeple-card-info-button');
      // Mobile: 44px touch target (w-11), Desktop: 30px (md:w-[30px])
      expect(button).toHaveClass('w-11', 'h-11', 'md:w-[30px]', 'md:h-[30px]');
    });

    it('renders with medium size when specified', () => {
      render(<MeepleCardInfoButton {...defaultProps} size="md" />);

      const button = screen.getByTestId('meeple-card-info-button');
      // Mobile: 44px touch target (w-11), Desktop: 36px (md:w-[36px])
      expect(button).toHaveClass('w-11', 'h-11', 'md:w-[36px]', 'md:h-[36px]');
    });

    it('is always visible (opacity 100)', () => {
      render(<MeepleCardInfoButton {...defaultProps} />);

      const button = screen.getByTestId('meeple-card-info-button');
      expect(button).toHaveClass('opacity-100');
    });
  });

  // --------------------------------------------------------------------------
  // Button mode (Issue #5025 — onClick opens drawer)
  // --------------------------------------------------------------------------

  describe('button mode (onClick)', () => {
    it('renders as <button> not <link> when onClick is provided', () => {
      const onClick = vi.fn();
      render(
        <MeepleCardInfoButton
          onClick={onClick}
          entityType="game"
        />
      );

      expect(screen.getByRole('button', { name: 'View details' })).toBeInTheDocument();
      expect(screen.queryByRole('link')).not.toBeInTheDocument();
    });

    it('calls onClick when clicked', async () => {
      const onClick = vi.fn();
      render(
        <MeepleCardInfoButton
          onClick={onClick}
          entityType="game"
        />
      );

      await userEvent.click(screen.getByTestId('meeple-card-info-button'));
      expect(onClick).toHaveBeenCalledTimes(1);
    });

    it('applies entity-colored styling in button mode', () => {
      render(
        <MeepleCardInfoButton
          onClick={vi.fn()}
          entityType="agent"
        />
      );

      const button = screen.getByTestId('meeple-card-info-button');
      expect(button).toHaveStyle({ '--tw-ring-color': 'hsl(38 92% 50%)' });
    });

    it('applies custom tooltip in button mode', () => {
      render(
        <MeepleCardInfoButton
          onClick={vi.fn()}
          entityType="game"
          tooltip="Apri dettaglio"
        />
      );

      expect(screen.getByRole('button', { name: 'Apri dettaglio' })).toBeInTheDocument();
    });

    it('onClick takes precedence over href', () => {
      const onClick = vi.fn();
      render(
        <MeepleCardInfoButton
          onClick={onClick}
          href="/games/123"
          entityType="game"
        />
      );

      // Should render as button, not link
      expect(screen.getByRole('button')).toBeInTheDocument();
      expect(screen.queryByRole('link')).not.toBeInTheDocument();
    });
  });
});
