import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { BottomSheet } from './BottomSheet';

describe('BottomSheet', () => {
  it('renders children when open', () => {
    render(
      <BottomSheet open onOpenChange={() => {}}>
        <p>Sheet content</p>
      </BottomSheet>
    );
    expect(screen.getByText('Sheet content')).toBeInTheDocument();
  });

  it('does not render children when closed', () => {
    render(
      <BottomSheet open={false} onOpenChange={() => {}}>
        <p>Sheet content</p>
      </BottomSheet>
    );
    expect(screen.queryByText('Sheet content')).not.toBeInTheDocument();
  });

  it('renders title when provided', () => {
    render(
      <BottomSheet open onOpenChange={() => {}} title="My Sheet">
        <p>Content</p>
      </BottomSheet>
    );
    expect(screen.getByText('My Sheet')).toBeInTheDocument();
  });

  it('calls onOpenChange when overlay is clicked', () => {
    const onOpenChange = vi.fn();
    render(
      <BottomSheet open onOpenChange={onOpenChange}>
        <p>Content</p>
      </BottomSheet>
    );
    const overlay = screen.getByTestId('bottom-sheet-overlay');
    fireEvent.click(overlay);
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it('renders drag handle', () => {
    render(
      <BottomSheet open onOpenChange={() => {}}>
        <p>Content</p>
      </BottomSheet>
    );
    expect(screen.getByTestId('drag-handle')).toBeInTheDocument();
  });

  it('applies sheet-surface class to content', () => {
    render(
      <BottomSheet open onOpenChange={() => {}}>
        <p>Content</p>
      </BottomSheet>
    );
    const content = screen.getByTestId('bottom-sheet-content');
    expect(content).toHaveClass('sheet-surface');
  });
});
