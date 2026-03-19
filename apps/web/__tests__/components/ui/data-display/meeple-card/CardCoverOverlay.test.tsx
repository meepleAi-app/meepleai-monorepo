/**
 * CardCoverOverlay Tests
 *
 * Tests for the MtG-inspired cover overlay slots on MeepleCard:
 * - mechanicIcon: bottom-left overlay on cover image
 * - stateLabel: bottom-right overlay on cover image
 * - Both together
 * - Absence when props not provided
 */

import React from 'react';

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { CardCover } from '@/components/ui/data-display/meeple-card/parts/CardCover';

describe('MeepleCard cover overlay slots', () => {
  it('renders mechanicIcon in bottom-left of cover', () => {
    render(
      <CardCover
        src="/test-image.jpg"
        alt="Test Card"
        variant="grid"
        entity="game"
        mechanicIcon={<span data-testid="mechanic-icon-content">dice</span>}
      />
    );

    const slot = screen.getByTestId('mechanic-icon-slot');
    expect(slot).toBeInTheDocument();
    expect(slot).toHaveAttribute('data-slot', 'mechanic-icon');

    const iconContent = screen.getByTestId('mechanic-icon-content');
    expect(iconContent).toBeInTheDocument();
    expect(slot).toContainElement(iconContent);
  });

  it('renders stateLabel in bottom-right of cover', () => {
    render(
      <CardCover
        src="/test-image.jpg"
        alt="Test Card"
        variant="grid"
        entity="game"
        stateLabel={{ text: 'Indexed', variant: 'success' }}
      />
    );

    const slot = screen.getByTestId('state-label-slot');
    expect(slot).toBeInTheDocument();
    expect(slot).toHaveAttribute('data-slot', 'state-label');
    expect(slot).toHaveTextContent('Indexed');
  });

  it('renders both overlay slots together', () => {
    render(
      <CardCover
        src="/test-image.jpg"
        alt="Test Card"
        variant="featured"
        entity="game"
        mechanicIcon={<span data-testid="mechanic-icon-content">area-control</span>}
        stateLabel={{ text: 'Processing', variant: 'warning' }}
      />
    );

    expect(screen.getByTestId('mechanic-icon-slot')).toBeInTheDocument();
    expect(screen.getByTestId('state-label-slot')).toBeInTheDocument();
    expect(screen.getByTestId('state-label-slot')).toHaveTextContent('Processing');
  });

  it('does not render overlays when props are not provided', () => {
    render(<CardCover src="/test-image.jpg" alt="Test Card" variant="grid" entity="game" />);

    expect(screen.queryByTestId('mechanic-icon-slot')).not.toBeInTheDocument();
    expect(screen.queryByTestId('state-label-slot')).not.toBeInTheDocument();
  });

  it('does not render overlays in compact variant (CardCover returns null)', () => {
    const { container } = render(
      <CardCover
        src="/test-image.jpg"
        alt="Test Card"
        variant="compact"
        entity="game"
        mechanicIcon={<span data-testid="mechanic-icon-content">dice</span>}
        stateLabel={{ text: 'Active', variant: 'info' }}
      />
    );

    expect(container.firstChild).toBeNull();
    expect(screen.queryByTestId('mechanic-icon-slot')).not.toBeInTheDocument();
    expect(screen.queryByTestId('state-label-slot')).not.toBeInTheDocument();
  });

  it('applies correct variant styling to stateLabel success variant', () => {
    render(
      <CardCover
        src="/test-image.jpg"
        alt="Test Card"
        variant="grid"
        entity="game"
        stateLabel={{ text: 'Done', variant: 'success' }}
      />
    );

    const label = screen.getByTestId('state-label-slot');
    expect(label).toHaveClass('bg-emerald-500/80');
    expect(label).toHaveClass('text-white');
  });

  it('applies correct variant styling to stateLabel warning variant', () => {
    render(
      <CardCover
        src="/test-image.jpg"
        alt="Test Card"
        variant="grid"
        entity="game"
        stateLabel={{ text: 'Pending', variant: 'warning' }}
      />
    );

    const label = screen.getByTestId('state-label-slot');
    expect(label).toHaveClass('bg-amber-500/80');
    expect(label).toHaveClass('text-black');
  });

  it('applies correct variant styling to stateLabel error variant', () => {
    render(
      <CardCover
        src="/test-image.jpg"
        alt="Test Card"
        variant="grid"
        entity="game"
        stateLabel={{ text: 'Failed', variant: 'error' }}
      />
    );

    const label = screen.getByTestId('state-label-slot');
    expect(label).toHaveClass('bg-red-500/80');
    expect(label).toHaveClass('text-white');
  });

  it('renders empty spacer div when only stateLabel is provided (no mechanicIcon)', () => {
    render(
      <CardCover
        src="/test-image.jpg"
        alt="Test Card"
        variant="grid"
        entity="game"
        stateLabel={{ text: 'Info', variant: 'info' }}
      />
    );

    // mechanicIcon slot should not be rendered, stateLabel should be
    expect(screen.queryByTestId('mechanic-icon-slot')).not.toBeInTheDocument();
    expect(screen.getByTestId('state-label-slot')).toBeInTheDocument();
  });
});
