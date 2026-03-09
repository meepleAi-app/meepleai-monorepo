/**
 * SplitViewLayout Tests
 * Issue #16 from mobile-first-ux-epic.md
 */

import React from 'react';

import { render, screen, fireEvent } from '@testing-library/react';
import { vi, describe, it, expect } from 'vitest';

import { SplitViewLayout } from '../SplitViewLayout';

describe('SplitViewLayout', () => {
  const listContent = <div data-testid="list-content">List items</div>;
  const detailContent = <div data-testid="detail-content">Detail view</div>;
  const emptyContent = <p>Select an item</p>;

  describe('Rendering', () => {
    it('renders the layout container', () => {
      render(<SplitViewLayout list={listContent} />);
      expect(screen.getByTestId('split-view-layout')).toBeInTheDocument();
    });

    it('renders list and detail panels', () => {
      render(<SplitViewLayout list={listContent} detail={detailContent} />);
      expect(screen.getByTestId('split-view-list')).toBeInTheDocument();
      expect(screen.getByTestId('split-view-detail')).toBeInTheDocument();
    });

    it('renders list content', () => {
      render(<SplitViewLayout list={listContent} />);
      expect(screen.getByTestId('list-content')).toBeInTheDocument();
    });

    it('renders detail content when provided', () => {
      render(<SplitViewLayout list={listContent} detail={detailContent} />);
      expect(screen.getByTestId('detail-content')).toBeInTheDocument();
    });
  });

  describe('Mobile behavior (stacked)', () => {
    it('hides list panel when detail is selected', () => {
      render(<SplitViewLayout list={listContent} detail={detailContent} />);
      const listPanel = screen.getByTestId('split-view-list');
      // On mobile: hidden when detail selected (hidden lg:flex)
      expect(listPanel.className).toContain('hidden');
      expect(listPanel.className).toContain('lg:flex');
    });

    it('shows list panel when no detail selected', () => {
      render(<SplitViewLayout list={listContent} />);
      const listPanel = screen.getByTestId('split-view-list');
      expect(listPanel.className).toContain('flex');
      expect(listPanel.className).not.toMatch(/^hidden/);
    });

    it('shows back button on mobile when detail is selected', () => {
      render(<SplitViewLayout list={listContent} detail={detailContent} onBack={() => {}} />);
      expect(screen.getByTestId('split-view-back')).toBeInTheDocument();
    });

    it('back button is mobile-only (lg:hidden)', () => {
      render(<SplitViewLayout list={listContent} detail={detailContent} onBack={() => {}} />);
      const backButton = screen.getByTestId('split-view-back');
      expect(backButton.className).toContain('lg:hidden');
    });

    it('calls onBack when back button is clicked', () => {
      const onBack = vi.fn();
      render(<SplitViewLayout list={listContent} detail={detailContent} onBack={onBack} />);
      fireEvent.click(screen.getByTestId('split-view-back'));
      expect(onBack).toHaveBeenCalledTimes(1);
    });

    it('hides back button when showBackButton is false', () => {
      render(<SplitViewLayout list={listContent} detail={detailContent} showBackButton={false} />);
      expect(screen.queryByTestId('split-view-back')).not.toBeInTheDocument();
    });
  });

  describe('Desktop behavior (side-by-side)', () => {
    it('applies lg:flex-row for side-by-side layout', () => {
      render(<SplitViewLayout list={listContent} detail={detailContent} />);
      const container = screen.getByTestId('split-view-layout');
      expect(container.className).toContain('lg:flex-row');
    });

    it('list panel has border-r on desktop', () => {
      render(<SplitViewLayout list={listContent} detail={detailContent} />);
      const listPanel = screen.getByTestId('split-view-list');
      expect(listPanel.className).toContain('lg:border-r');
    });
  });

  describe('Empty state', () => {
    it('shows empty detail placeholder when no detail and emptyDetail provided', () => {
      render(<SplitViewLayout list={listContent} emptyDetail={emptyContent} />);
      expect(screen.getByTestId('split-view-empty')).toBeInTheDocument();
      expect(screen.getByText('Select an item')).toBeInTheDocument();
    });

    it('does not show empty placeholder when detail is provided', () => {
      render(
        <SplitViewLayout list={listContent} detail={detailContent} emptyDetail={emptyContent} />
      );
      expect(screen.queryByTestId('split-view-empty')).not.toBeInTheDocument();
    });
  });

  describe('List ratio', () => {
    it('uses balanced ratio by default (lg:w-2/5)', () => {
      render(<SplitViewLayout list={listContent} />);
      const listPanel = screen.getByTestId('split-view-list');
      expect(listPanel.className).toContain('lg:w-2/5');
    });

    it('applies narrow ratio (lg:w-1/3)', () => {
      render(<SplitViewLayout list={listContent} listRatio="narrow" />);
      const listPanel = screen.getByTestId('split-view-list');
      expect(listPanel.className).toContain('lg:w-1/3');
    });

    it('applies wide ratio (lg:w-1/2)', () => {
      render(<SplitViewLayout list={listContent} listRatio="wide" />);
      const listPanel = screen.getByTestId('split-view-list');
      expect(listPanel.className).toContain('lg:w-1/2');
    });
  });

  describe('Accessibility', () => {
    it('list panel has region role with label', () => {
      render(<SplitViewLayout list={listContent} />);
      expect(screen.getByRole('region', { name: 'List' })).toBeInTheDocument();
    });

    it('detail panel has region role with label', () => {
      render(<SplitViewLayout list={listContent} detail={detailContent} />);
      expect(screen.getByRole('region', { name: 'Detail' })).toBeInTheDocument();
    });

    it('supports custom labels', () => {
      render(
        <SplitViewLayout
          list={listContent}
          detail={detailContent}
          listLabel="Games list"
          detailLabel="Game details"
        />
      );
      expect(screen.getByRole('region', { name: 'Games list' })).toBeInTheDocument();
      expect(screen.getByRole('region', { name: 'Game details' })).toBeInTheDocument();
    });

    it('back button has accessible label', () => {
      render(<SplitViewLayout list={listContent} detail={detailContent} onBack={() => {}} />);
      expect(screen.getByLabelText('Back to list')).toBeInTheDocument();
    });

    it('back button has focus-visible ring', () => {
      render(<SplitViewLayout list={listContent} detail={detailContent} onBack={() => {}} />);
      const backButton = screen.getByTestId('split-view-back');
      expect(backButton.className).toContain('focus-visible:ring-2');
    });
  });

  describe('Custom className', () => {
    it('applies custom className to container', () => {
      render(<SplitViewLayout list={listContent} className="custom-class" />);
      const container = screen.getByTestId('split-view-layout');
      expect(container.className).toContain('custom-class');
    });
  });
});
