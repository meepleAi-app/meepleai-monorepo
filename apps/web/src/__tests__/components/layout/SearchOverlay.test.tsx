import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';

import { SearchOverlay } from '@/components/layout/SearchOverlay';

describe('SearchOverlay', () => {
  let rafSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    // Mock requestAnimationFrame to call callback synchronously in tests
    rafSpy = vi.spyOn(window, 'requestAnimationFrame').mockImplementation(cb => {
      cb(0);
      return 0;
    });
  });

  afterEach(() => {
    rafSpy.mockRestore();
  });

  it('renders search input when open', () => {
    render(<SearchOverlay open={true} onClose={vi.fn()} />);
    const input = screen.getByRole('searchbox');
    expect(input).toBeDefined();
  });

  it('does not render when closed', () => {
    render(<SearchOverlay open={false} onClose={vi.fn()} />);
    expect(screen.queryByRole('searchbox')).toBeNull();
  });

  it('renders placeholder text', () => {
    render(<SearchOverlay open={true} onClose={vi.fn()} />);
    const input = screen.getByRole('searchbox');
    expect(input.getAttribute('placeholder')).toBe('Cerca giochi, sessioni, giocatori...');
  });

  it('focuses input on open', async () => {
    render(<SearchOverlay open={true} onClose={vi.fn()} />);
    const input = screen.getByRole('searchbox') as HTMLInputElement;
    // requestAnimationFrame was mocked to fire synchronously
    expect(document.activeElement).toBe(input);
  });

  it('calls onClose when Escape key is pressed', () => {
    const onClose = vi.fn();
    render(<SearchOverlay open={true} onClose={onClose} />);
    const input = screen.getByRole('searchbox');
    fireEvent.keyDown(input, { key: 'Escape' });
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('calls onClose when backdrop is clicked', () => {
    const onClose = vi.fn();
    render(<SearchOverlay open={true} onClose={onClose} />);
    const backdrop = screen.getByTestId('search-overlay-backdrop');
    fireEvent.click(backdrop);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('calls onClose when close button is clicked', () => {
    const onClose = vi.fn();
    render(<SearchOverlay open={true} onClose={onClose} />);
    const closeButton = screen.getByRole('button', { name: /chiudi/i });
    fireEvent.click(closeButton);
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('does not call onClose when search bar is clicked', () => {
    const onClose = vi.fn();
    render(<SearchOverlay open={true} onClose={onClose} />);
    const bar = screen.getByTestId('search-overlay-bar');
    fireEvent.click(bar);
    expect(onClose).not.toHaveBeenCalled();
  });

  it('input has type search', () => {
    render(<SearchOverlay open={true} onClose={vi.fn()} />);
    const input = screen.getByRole('searchbox') as HTMLInputElement;
    expect(input.type).toBe('search');
  });
});
