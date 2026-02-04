import { render, screen, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { ProgressIndicator } from '../ProgressIndicator';

// Mock framer-motion
vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, style, animate, ...props }: React.PropsWithChildren<Record<string, unknown>>) => (
      <div
        {...props}
        style={{
          ...((style || {}) as React.CSSProperties),
          width: (animate as { width?: string })?.width || '0%',
        }}
      >
        {children}
      </div>
    ),
  },
}));

describe('ProgressIndicator', () => {
  let scrollListeners: Array<(e: Event) => void> = [];
  let resizeListeners: Array<(e: Event) => void> = [];

  beforeEach(() => {
    scrollListeners = [];
    resizeListeners = [];

    // Mock addEventListener
    vi.spyOn(window, 'addEventListener').mockImplementation(
      (event: string, handler: EventListenerOrEventListenerObject) => {
        if (event === 'scroll') {
          scrollListeners.push(handler as (e: Event) => void);
        } else if (event === 'resize') {
          resizeListeners.push(handler as (e: Event) => void);
        }
      }
    );

    vi.spyOn(window, 'removeEventListener').mockImplementation(
      (event: string, handler: EventListenerOrEventListenerObject) => {
        if (event === 'scroll') {
          scrollListeners = scrollListeners.filter((h) => h !== handler);
        } else if (event === 'resize') {
          resizeListeners = resizeListeners.filter((h) => h !== handler);
        }
      }
    );

    // Mock window dimensions
    Object.defineProperty(window, 'innerHeight', { value: 800, writable: true });
    Object.defineProperty(document.documentElement, 'scrollHeight', {
      value: 2000,
      writable: true,
    });
    Object.defineProperty(window, 'scrollY', { value: 0, writable: true });
  });

  afterEach(() => {
    vi.restoreAllMocks();
    scrollListeners = [];
    resizeListeners = [];
  });

  it('should show 0% at top of page', () => {
    Object.defineProperty(window, 'scrollY', { value: 0 });

    render(<ProgressIndicator />);

    expect(screen.getByText('0%')).toBeInTheDocument();
  });

  it('should show 100% at bottom of page', () => {
    // scrollY = scrollHeight - innerHeight = 2000 - 800 = 1200
    Object.defineProperty(window, 'scrollY', { value: 1200 });

    render(<ProgressIndicator />);

    expect(screen.getByText('100%')).toBeInTheDocument();
  });

  it('should show intermediate values while scrolling', () => {
    // 50% scroll: scrollY = (2000 - 800) * 0.5 = 600
    Object.defineProperty(window, 'scrollY', { value: 600 });

    render(<ProgressIndicator />);

    expect(screen.getByText('50%')).toBeInTheDocument();
  });

  it('should update on scroll', () => {
    render(<ProgressIndicator />);

    expect(screen.getByText('0%')).toBeInTheDocument();

    // Simulate scroll to 50%
    act(() => {
      Object.defineProperty(window, 'scrollY', { value: 600 });
      scrollListeners.forEach((listener) => listener(new Event('scroll')));
    });

    expect(screen.getByText('50%')).toBeInTheDocument();
  });

  it('should update on window resize', () => {
    render(<ProgressIndicator />);

    // Start at top
    expect(screen.getByText('0%')).toBeInTheDocument();

    // Change document height and trigger resize
    act(() => {
      Object.defineProperty(document.documentElement, 'scrollHeight', { value: 4000 });
      Object.defineProperty(window, 'scrollY', { value: 1600 }); // 50% of new height
      resizeListeners.forEach((listener) => listener(new Event('resize')));
    });

    expect(screen.getByText('50%')).toBeInTheDocument();
  });

  it('should handle edge case of short pages (document shorter than viewport)', () => {
    Object.defineProperty(document.documentElement, 'scrollHeight', { value: 400 });
    Object.defineProperty(window, 'innerHeight', { value: 800 });

    render(<ProgressIndicator />);

    // Should show 100% when document is shorter than viewport
    expect(screen.getByText('100%')).toBeInTheDocument();
  });

  it('should cleanup event listeners on unmount', () => {
    const { unmount } = render(<ProgressIndicator />);

    expect(scrollListeners.length).toBeGreaterThan(0);
    expect(resizeListeners.length).toBeGreaterThan(0);

    unmount();

    // After unmount, listeners should be removed
    expect(window.removeEventListener).toHaveBeenCalledWith(
      'scroll',
      expect.any(Function)
    );
    expect(window.removeEventListener).toHaveBeenCalledWith(
      'resize',
      expect.any(Function)
    );
  });

  it('should use passive event listeners for performance', () => {
    render(<ProgressIndicator />);

    expect(window.addEventListener).toHaveBeenCalledWith(
      'scroll',
      expect.any(Function),
      { passive: true }
    );
    expect(window.addEventListener).toHaveBeenCalledWith(
      'resize',
      expect.any(Function),
      { passive: true }
    );
  });

  it('should apply custom className', () => {
    const { container } = render(<ProgressIndicator className="custom-class" />);

    expect(container.querySelector('.custom-class')).toBeInTheDocument();
  });

  it('should have correct accessibility attributes', () => {
    render(<ProgressIndicator />);

    const progressbar = screen.getByRole('progressbar');
    expect(progressbar).toHaveAttribute('aria-valuenow', '0');
    expect(progressbar).toHaveAttribute('aria-valuemin', '0');
    expect(progressbar).toHaveAttribute('aria-valuemax', '100');
    expect(progressbar).toHaveAttribute('aria-label', 'Page scroll progress');
  });

  it('should clamp values between 0 and 100', () => {
    // Test negative scroll (shouldn't happen but handle it)
    Object.defineProperty(window, 'scrollY', { value: -100 });

    render(<ProgressIndicator />);

    expect(screen.getByText('0%')).toBeInTheDocument();

    // Test scroll beyond document
    act(() => {
      Object.defineProperty(window, 'scrollY', { value: 5000 });
      scrollListeners.forEach((listener) => listener(new Event('scroll')));
    });

    expect(screen.getByText('100%')).toBeInTheDocument();
  });

  it('should display Progress label', () => {
    render(<ProgressIndicator />);

    expect(screen.getByText('Progress')).toBeInTheDocument();
  });
});
