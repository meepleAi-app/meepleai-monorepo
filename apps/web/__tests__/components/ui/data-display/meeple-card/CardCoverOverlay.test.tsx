/**
 * CardCoverOverlay Tests
 *
 * Tests for the 4-corner cover overlay system on MeepleCard:
 * - subtypeIcons: bottom-left overlay on cover image
 * - stateLabel: bottom-right overlay on cover image
 * - Both together
 * - Absence when props not provided
 */

import React from 'react';

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { CardCover } from '@/components/ui/data-display/meeple-card/parts/CardCover';

describe('MeepleCard cover overlay slots', () => {
  it('renders subtypeIcons in bottom-left of cover', () => {
    render(
      <CardCover
        src="/test-image.jpg"
        alt="Test Card"
        variant="grid"
        entity="game"
        subtypeIcons={[
          { icon: <span data-testid="subtype-icon-content">dice</span>, tooltip: 'Dice' },
        ]}
      />
    );

    const slot = screen.getByTestId('cover-subtypes');
    expect(slot).toBeInTheDocument();

    const iconContent = screen.getByTestId('subtype-icon-content');
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

    const slot = screen.getByTestId('cover-state-label');
    expect(slot).toBeInTheDocument();
    expect(slot).toHaveTextContent('Indexed');
  });

  it('renders both overlay slots together', () => {
    render(
      <CardCover
        src="/test-image.jpg"
        alt="Test Card"
        variant="featured"
        entity="game"
        subtypeIcons={[
          {
            icon: <span data-testid="subtype-icon-content">area-control</span>,
            tooltip: 'Area Control',
          },
        ]}
        stateLabel={{ text: 'Processing', variant: 'warning' }}
      />
    );

    expect(screen.getByTestId('cover-subtypes')).toBeInTheDocument();
    expect(screen.getByTestId('cover-state-label')).toBeInTheDocument();
    expect(screen.getByTestId('cover-state-label')).toHaveTextContent('Processing');
  });

  it('does not render overlays when props are not provided', () => {
    render(<CardCover src="/test-image.jpg" alt="Test Card" variant="grid" entity="game" />);

    expect(screen.queryByTestId('cover-subtypes')).not.toBeInTheDocument();
    expect(screen.queryByTestId('cover-state-label')).not.toBeInTheDocument();
  });

  it('does not render overlays in compact variant (CardCover returns null)', () => {
    const { container } = render(
      <CardCover
        src="/test-image.jpg"
        alt="Test Card"
        variant="compact"
        entity="game"
        subtypeIcons={[
          { icon: <span data-testid="subtype-icon-content">dice</span>, tooltip: 'Dice' },
        ]}
        stateLabel={{ text: 'Active', variant: 'info' }}
      />
    );

    expect(container.firstChild).toBeNull();
    expect(screen.queryByTestId('cover-subtypes')).not.toBeInTheDocument();
    expect(screen.queryByTestId('cover-state-label')).not.toBeInTheDocument();
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

    const label = screen.getByTestId('cover-state-label');
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

    const label = screen.getByTestId('cover-state-label');
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

    const label = screen.getByTestId('cover-state-label');
    expect(label).toHaveClass('bg-red-500/80');
    expect(label).toHaveClass('text-white');
  });

  it('renders empty spacer div when only stateLabel is provided (no subtypeIcons)', () => {
    render(
      <CardCover
        src="/test-image.jpg"
        alt="Test Card"
        variant="grid"
        entity="game"
        stateLabel={{ text: 'Info', variant: 'info' }}
      />
    );

    // subtypeIcons slot should not be rendered, stateLabel should be
    expect(screen.queryByTestId('cover-subtypes')).not.toBeInTheDocument();
    expect(screen.getByTestId('cover-state-label')).toBeInTheDocument();
  });
});
