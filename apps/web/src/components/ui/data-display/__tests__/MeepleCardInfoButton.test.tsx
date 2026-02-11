/**
 * MeepleCardInfoButton Tests
 * Issue #4033 - Comprehensive Testing
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { MeepleCardInfoButton } from '../meeple-card-info-button';

describe('MeepleCardInfoButton', () => {
  const defaultProps = {
    href: '/games/123',
    entityType: 'game' as const,
  };

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
    const { container } = render(<MeepleCardInfoButton {...defaultProps} />);

    const button = screen.getByTestId('meeple-card-info-button');
    expect(button).toHaveClass('w-[30px]', 'h-[30px]');
  });

  it('renders with medium size when specified', () => {
    render(<MeepleCardInfoButton {...defaultProps} size="md" />);

    const button = screen.getByTestId('meeple-card-info-button');
    expect(button).toHaveClass('w-[36px]', 'h-[36px]');
  });

  it('is always visible (opacity 100)', () => {
    render(<MeepleCardInfoButton {...defaultProps} />);

    const button = screen.getByTestId('meeple-card-info-button');
    expect(button).toHaveClass('opacity-100');
  });
});
