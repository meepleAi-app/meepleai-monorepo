/**
 * FlipCard Component Tests
 * Epic #3820 - MeepleCard System
 *
 * Tests 3D flip animation, variant-adaptive back content,
 * controlled/uncontrolled modes, and keyboard accessibility.
 */

import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { FlipCard, type MeepleCardFlipData } from '../FlipCard';

// Mock framer-motion to avoid animation complexity in tests
vi.mock('framer-motion', () => ({
  motion: {
    div: React.forwardRef(
      (
        { children, animate, style, ...props }: React.PropsWithChildren<Record<string, unknown>>,
        ref: React.Ref<HTMLDivElement>,
      ) => (
        <div ref={ref} data-animate={JSON.stringify(animate)} style={style as React.CSSProperties} {...props}>
          {children}
        </div>
      ),
    ),
  },
}));

const sampleFlipData: MeepleCardFlipData = {
  description: 'A strategy board game about resource management and territory control.',
  categories: [
    { id: 'cat-1', name: 'Strategy' },
    { id: 'cat-2', name: 'Economic' },
    { id: 'cat-3', name: 'Territory Building' },
    { id: 'cat-4', name: 'Civilization' },
  ],
  mechanics: [
    { id: 'mech-1', name: 'Worker Placement' },
    { id: 'mech-2', name: 'Area Control' },
    { id: 'mech-3', name: 'Hand Management' },
    { id: 'mech-4', name: 'Deck Building' },
  ],
  designers: [
    { id: 'des-1', name: 'Uwe Rosenberg' },
  ],
  publishers: [
    { id: 'pub-1', name: 'Lookout Games' },
  ],
  complexityRating: 3.5,
  minAge: 12,
};

describe('FlipCard', () => {
  const frontContent = <div data-testid="front-content">Front Side</div>;

  describe('Rendering', () => {
    it('should render front content', () => {
      render(
        <FlipCard flipData={sampleFlipData}>
          {frontContent}
        </FlipCard>,
      );

      expect(screen.getByTestId('front-content')).toBeInTheDocument();
      expect(screen.getByText('Front Side')).toBeInTheDocument();
    });

    it('should render back content with description', () => {
      render(
        <FlipCard flipData={sampleFlipData}>
          {frontContent}
        </FlipCard>,
      );

      expect(screen.getByText(/strategy board game/i)).toBeInTheDocument();
    });

    it('should render with correct test IDs', () => {
      render(
        <FlipCard flipData={sampleFlipData}>
          {frontContent}
        </FlipCard>,
      );

      expect(screen.getByTestId('meeple-card-flip-container')).toBeInTheDocument();
      expect(screen.getByTestId('meeple-card-front')).toBeInTheDocument();
      expect(screen.getByTestId('meeple-card-back')).toBeInTheDocument();
    });

    it('should render "Informazioni" heading on back', () => {
      render(
        <FlipCard flipData={sampleFlipData}>
          {frontContent}
        </FlipCard>,
      );

      expect(screen.getByText('Informazioni')).toBeInTheDocument();
    });

    it('should render "Gira" button on back', () => {
      render(
        <FlipCard flipData={sampleFlipData}>
          {frontContent}
        </FlipCard>,
      );

      expect(screen.getByText('Gira')).toBeInTheDocument();
    });

    it('should render flip hint for non-row variants', () => {
      render(
        <FlipCard flipData={sampleFlipData} variant="grid">
          {frontContent}
        </FlipCard>,
      );

      expect(screen.getByText('Clicca per girare la carta')).toBeInTheDocument();
    });

    it('should not render flip hint for list variant', () => {
      render(
        <FlipCard flipData={sampleFlipData} variant="list">
          {frontContent}
        </FlipCard>,
      );

      expect(screen.queryByText('Clicca per girare la carta')).not.toBeInTheDocument();
    });

    it('should not render flip hint for compact variant', () => {
      render(
        <FlipCard flipData={sampleFlipData} variant="compact">
          {frontContent}
        </FlipCard>,
      );

      expect(screen.queryByText('Clicca per girare la carta')).not.toBeInTheDocument();
    });
  });

  describe('Flip Interaction (Uncontrolled)', () => {
    it('should flip on click', () => {
      const onFlip = vi.fn();
      render(
        <FlipCard flipData={sampleFlipData} onFlip={onFlip}>
          {frontContent}
        </FlipCard>,
      );

      fireEvent.click(screen.getByTestId('meeple-card-flip-container'));
      expect(onFlip).toHaveBeenCalledWith(true);
    });

    it('should toggle flip state on multiple clicks', () => {
      const onFlip = vi.fn();
      render(
        <FlipCard flipData={sampleFlipData} onFlip={onFlip}>
          {frontContent}
        </FlipCard>,
      );

      const container = screen.getByTestId('meeple-card-flip-container');

      fireEvent.click(container);
      expect(onFlip).toHaveBeenCalledWith(true);

      fireEvent.click(container);
      expect(onFlip).toHaveBeenCalledWith(false);
    });

    it('should flip on Enter key', () => {
      const onFlip = vi.fn();
      render(
        <FlipCard flipData={sampleFlipData} onFlip={onFlip}>
          {frontContent}
        </FlipCard>,
      );

      fireEvent.keyDown(screen.getByTestId('meeple-card-flip-container'), { key: 'Enter' });
      expect(onFlip).toHaveBeenCalledWith(true);
    });

    it('should flip on Space key', () => {
      const onFlip = vi.fn();
      render(
        <FlipCard flipData={sampleFlipData} onFlip={onFlip}>
          {frontContent}
        </FlipCard>,
      );

      fireEvent.keyDown(screen.getByTestId('meeple-card-flip-container'), { key: ' ' });
      expect(onFlip).toHaveBeenCalledWith(true);
    });

    it('should not flip on other keys', () => {
      const onFlip = vi.fn();
      render(
        <FlipCard flipData={sampleFlipData} onFlip={onFlip}>
          {frontContent}
        </FlipCard>,
      );

      fireEvent.keyDown(screen.getByTestId('meeple-card-flip-container'), { key: 'Tab' });
      expect(onFlip).not.toHaveBeenCalled();
    });
  });

  describe('Flip Interaction (Controlled)', () => {
    it('should respect controlled isFlipped prop', () => {
      const onFlip = vi.fn();
      render(
        <FlipCard flipData={sampleFlipData} isFlipped={false} onFlip={onFlip}>
          {frontContent}
        </FlipCard>,
      );

      fireEvent.click(screen.getByTestId('meeple-card-flip-container'));
      expect(onFlip).toHaveBeenCalledWith(true);
    });

    it('should call onFlip with false when flipped back', () => {
      const onFlip = vi.fn();
      render(
        <FlipCard flipData={sampleFlipData} isFlipped={true} onFlip={onFlip}>
          {frontContent}
        </FlipCard>,
      );

      fireEvent.click(screen.getByTestId('meeple-card-flip-container'));
      expect(onFlip).toHaveBeenCalledWith(false);
    });
  });

  describe('Gira Button', () => {
    it('should flip back when "Gira" button is clicked', () => {
      const onFlip = vi.fn();
      render(
        <FlipCard flipData={sampleFlipData} onFlip={onFlip}>
          {frontContent}
        </FlipCard>,
      );

      // First flip to back
      fireEvent.click(screen.getByTestId('meeple-card-flip-container'));
      expect(onFlip).toHaveBeenCalledWith(true);

      // Click "Gira" button to flip back to front
      fireEvent.click(screen.getByText('Gira'));
      expect(onFlip).toHaveBeenCalledWith(false);
    });
  });

  describe('Variant-Specific Back Content', () => {
    it('should show categories and mechanics for grid variant', () => {
      render(
        <FlipCard flipData={sampleFlipData} variant="grid">
          {frontContent}
        </FlipCard>,
      );

      expect(screen.getByText('Categorie')).toBeInTheDocument();
      expect(screen.getByText('Meccaniche')).toBeInTheDocument();
      expect(screen.getByText('Strategy')).toBeInTheDocument();
      expect(screen.getByText('Worker Placement')).toBeInTheDocument();
    });

    it('should limit categories to 3 for grid variant', () => {
      render(
        <FlipCard flipData={sampleFlipData} variant="grid">
          {frontContent}
        </FlipCard>,
      );

      // Grid shows max 3 categories
      expect(screen.getByText('Strategy')).toBeInTheDocument();
      expect(screen.getByText('Economic')).toBeInTheDocument();
      expect(screen.getByText('Territory Building')).toBeInTheDocument();
      expect(screen.queryByText('Civilization')).not.toBeInTheDocument();
    });

    it('should show designers for grid variant', () => {
      render(
        <FlipCard flipData={sampleFlipData} variant="grid">
          {frontContent}
        </FlipCard>,
      );

      expect(screen.getByText('Designer')).toBeInTheDocument();
      expect(screen.getByText('Uwe Rosenberg')).toBeInTheDocument();
    });

    it('should not show publishers for grid variant', () => {
      render(
        <FlipCard flipData={sampleFlipData} variant="grid">
          {frontContent}
        </FlipCard>,
      );

      expect(screen.queryByText('Editori')).not.toBeInTheDocument();
    });

    it('should not show categories or mechanics for compact variant', () => {
      render(
        <FlipCard flipData={sampleFlipData} variant="compact">
          {frontContent}
        </FlipCard>,
      );

      expect(screen.queryByText('Categorie')).not.toBeInTheDocument();
      expect(screen.queryByText('Meccaniche')).not.toBeInTheDocument();
    });

    it('should show designers for list variant', () => {
      render(
        <FlipCard flipData={sampleFlipData} variant="list">
          {frontContent}
        </FlipCard>,
      );

      expect(screen.getByText('Designer')).toBeInTheDocument();
    });

    it('should show publishers for featured variant', () => {
      render(
        <FlipCard flipData={sampleFlipData} variant="featured">
          {frontContent}
        </FlipCard>,
      );

      expect(screen.getByText('Editori')).toBeInTheDocument();
      expect(screen.getByText('Lookout Games')).toBeInTheDocument();
    });

    it('should show all content for hero variant', () => {
      render(
        <FlipCard flipData={sampleFlipData} variant="hero">
          {frontContent}
        </FlipCard>,
      );

      expect(screen.getByText('Descrizione')).toBeInTheDocument();
      expect(screen.getByText('Categorie')).toBeInTheDocument();
      expect(screen.getByText('Meccaniche')).toBeInTheDocument();
      expect(screen.getByText('Designer')).toBeInTheDocument();
      expect(screen.getByText('Editori')).toBeInTheDocument();
      // Hero shows all 4 categories (no truncation)
      expect(screen.getByText('Civilization')).toBeInTheDocument();
    });

    it('should show min age for hero variant', () => {
      render(
        <FlipCard flipData={sampleFlipData} variant="hero">
          {frontContent}
        </FlipCard>,
      );

      expect(screen.getByText(/Età minima: 12\+/)).toBeInTheDocument();
    });

    it('should not show min age for grid variant', () => {
      render(
        <FlipCard flipData={sampleFlipData} variant="grid">
          {frontContent}
        </FlipCard>,
      );

      expect(screen.queryByText(/Età minima/)).not.toBeInTheDocument();
    });
  });

  describe('Empty Data Handling', () => {
    it('should handle empty flipData gracefully', () => {
      render(
        <FlipCard flipData={{}}>
          {frontContent}
        </FlipCard>,
      );

      expect(screen.getByText('Informazioni')).toBeInTheDocument();
      expect(screen.queryByText('Descrizione')).not.toBeInTheDocument();
      expect(screen.queryByText('Categorie')).not.toBeInTheDocument();
    });

    it('should handle flipData with only description', () => {
      render(
        <FlipCard flipData={{ description: 'Just a description' }}>
          {frontContent}
        </FlipCard>,
      );

      expect(screen.getByText('Just a description')).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('should have role="button" on container', () => {
      render(
        <FlipCard flipData={sampleFlipData}>
          {frontContent}
        </FlipCard>,
      );

      const container = screen.getByTestId('meeple-card-flip-container');
      expect(container).toHaveAttribute('role', 'button');
    });

    it('should have tabIndex={0}', () => {
      render(
        <FlipCard flipData={sampleFlipData}>
          {frontContent}
        </FlipCard>,
      );

      expect(screen.getByTestId('meeple-card-flip-container')).toHaveAttribute('tabIndex', '0');
    });

    it('should have descriptive aria-label', () => {
      render(
        <FlipCard flipData={sampleFlipData}>
          {frontContent}
        </FlipCard>,
      );

      expect(screen.getByTestId('meeple-card-flip-container')).toHaveAttribute(
        'aria-label',
        expect.stringContaining('dettagli sul retro'),
      );
    });
  });

  describe('Button Mode (flipTrigger="button")', () => {
    it('should render flip button with correct test ID', () => {
      render(
        <FlipCard flipData={sampleFlipData} flipTrigger="button">
          {frontContent}
        </FlipCard>,
      );

      expect(screen.getByTestId('meeple-card-flip-button')).toBeInTheDocument();
    });

    it('should not render flip button in card mode', () => {
      render(
        <FlipCard flipData={sampleFlipData} flipTrigger="card">
          {frontContent}
        </FlipCard>,
      );

      expect(screen.queryByTestId('meeple-card-flip-button')).not.toBeInTheDocument();
    });

    it('should not have role="button" on container in button mode', () => {
      render(
        <FlipCard flipData={sampleFlipData} flipTrigger="button">
          {frontContent}
        </FlipCard>,
      );

      const container = screen.getByTestId('meeple-card-flip-container');
      expect(container).not.toHaveAttribute('role');
    });

    it('should not have tabIndex on container in button mode', () => {
      render(
        <FlipCard flipData={sampleFlipData} flipTrigger="button">
          {frontContent}
        </FlipCard>,
      );

      const container = screen.getByTestId('meeple-card-flip-container');
      expect(container).not.toHaveAttribute('tabIndex');
    });

    it('should not have cursor-pointer on container in button mode', () => {
      render(
        <FlipCard flipData={sampleFlipData} flipTrigger="button">
          {frontContent}
        </FlipCard>,
      );

      const container = screen.getByTestId('meeple-card-flip-container');
      expect(container.className).not.toContain('cursor-pointer');
    });

    it('should flip when flip button is clicked', () => {
      const onFlip = vi.fn();
      render(
        <FlipCard flipData={sampleFlipData} flipTrigger="button" onFlip={onFlip}>
          {frontContent}
        </FlipCard>,
      );

      fireEvent.click(screen.getByTestId('meeple-card-flip-button'));
      expect(onFlip).toHaveBeenCalledWith(true);
    });

    it('should not flip when container is clicked in button mode', () => {
      const onFlip = vi.fn();
      render(
        <FlipCard flipData={sampleFlipData} flipTrigger="button" onFlip={onFlip}>
          {frontContent}
        </FlipCard>,
      );

      fireEvent.click(screen.getByTestId('meeple-card-flip-container'));
      expect(onFlip).not.toHaveBeenCalled();
    });

    it('should hide flip hint in button mode', () => {
      render(
        <FlipCard flipData={sampleFlipData} flipTrigger="button" variant="grid">
          {frontContent}
        </FlipCard>,
      );

      expect(screen.queryByText('Clicca per girare la carta')).not.toBeInTheDocument();
    });

    it('should have accessible aria-label on flip button', () => {
      render(
        <FlipCard flipData={sampleFlipData} flipTrigger="button">
          {frontContent}
        </FlipCard>,
      );

      const button = screen.getByTestId('meeple-card-flip-button');
      expect(button).toHaveAttribute('aria-label', 'Mostra dettagli carta');
    });
  });
});
