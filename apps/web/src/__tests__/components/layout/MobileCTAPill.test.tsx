import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, act } from '@testing-library/react';

// next/navigation mock — overridden per-test via vi.mocked
vi.mock('next/navigation', () => ({
  usePathname: vi.fn(() => '/'),
  useRouter: vi.fn(() => ({ push: vi.fn() })),
}));

import { usePathname } from 'next/navigation';
import { MobileCTAPill } from '@/components/layout/MobileCTAPill';

const mockUsePathname = vi.mocked(usePathname);

describe('MobileCTAPill', () => {
  beforeEach(() => {
    // Default: page without CTA mapping
    mockUsePathname.mockReturnValue('/');

    // Reset scroll position
    Object.defineProperty(window, 'scrollY', {
      writable: true,
      value: 0,
    });
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('returns null for pages without CTA mapping', () => {
    mockUsePathname.mockReturnValue('/about');
    const { container } = render(<MobileCTAPill />);
    expect(container.firstChild).toBeNull();
  });

  it('returns null for the home page', () => {
    mockUsePathname.mockReturnValue('/');
    const { container } = render(<MobileCTAPill />);
    expect(container.firstChild).toBeNull();
  });

  it('renders CTA pill for /library page', () => {
    mockUsePathname.mockReturnValue('/library');
    render(<MobileCTAPill />);
    expect(screen.getByText('+ Aggiungi gioco')).toBeDefined();
  });

  it('renders CTA pill for /sessions page', () => {
    mockUsePathname.mockReturnValue('/sessions');
    render(<MobileCTAPill />);
    expect(screen.getByText('+ Nuova sessione')).toBeDefined();
  });

  it('renders CTA pill for /play-records page', () => {
    mockUsePathname.mockReturnValue('/play-records');
    render(<MobileCTAPill />);
    expect(screen.getByText('+ Nuova partita')).toBeDefined();
  });

  it('renders CTA pill for /chat page', () => {
    mockUsePathname.mockReturnValue('/chat');
    render(<MobileCTAPill />);
    expect(screen.getByText('+ Nuova chat')).toBeDefined();
  });

  it('has md:hidden class for mobile-only visibility', () => {
    mockUsePathname.mockReturnValue('/library');
    render(<MobileCTAPill />);
    const pill = screen.getByTestId('mobile-cta-pill');
    expect(pill.className).toContain('md:hidden');
  });

  it('renders as a link with correct href for /library', () => {
    mockUsePathname.mockReturnValue('/library');
    render(<MobileCTAPill />);
    const link = screen.getByRole('link');
    expect(link.getAttribute('href')).toBe('/library?action=add');
  });

  it('renders as a link with correct href for /sessions', () => {
    mockUsePathname.mockReturnValue('/sessions');
    render(<MobileCTAPill />);
    const link = screen.getByRole('link');
    expect(link.getAttribute('href')).toBe('/sessions/new');
  });

  it('renders as a link with correct href for /play-records', () => {
    mockUsePathname.mockReturnValue('/play-records');
    render(<MobileCTAPill />);
    const link = screen.getByRole('link');
    expect(link.getAttribute('href')).toBe('/play-records/new');
  });

  it('renders as a link with correct href for /chat', () => {
    mockUsePathname.mockReturnValue('/chat');
    render(<MobileCTAPill />);
    const link = screen.getByRole('link');
    expect(link.getAttribute('href')).toBe('/chat/new');
  });

  it('is visible when at the top of the page (scrollY < 50)', () => {
    mockUsePathname.mockReturnValue('/library');
    Object.defineProperty(window, 'scrollY', { writable: true, value: 0 });
    render(<MobileCTAPill />);
    const pill = screen.getByTestId('mobile-cta-pill');
    // Should be visible (not translated away)
    expect(pill.className).not.toContain('translate-y-24');
  });

  it('hides on scroll down (shows translate-y-24)', () => {
    mockUsePathname.mockReturnValue('/library');
    Object.defineProperty(window, 'scrollY', { writable: true, value: 0 });
    render(<MobileCTAPill />);

    // Simulate scrolling down
    act(() => {
      Object.defineProperty(window, 'scrollY', { writable: true, value: 200 });
      window.dispatchEvent(new Event('scroll'));
    });

    const pill = screen.getByTestId('mobile-cta-pill');
    expect(pill.className).toContain('translate-y-24');
  });

  it('reappears on scroll up after being hidden', () => {
    mockUsePathname.mockReturnValue('/library');
    Object.defineProperty(window, 'scrollY', { writable: true, value: 0 });
    render(<MobileCTAPill />);

    // Scroll down to hide
    act(() => {
      Object.defineProperty(window, 'scrollY', { writable: true, value: 200 });
      window.dispatchEvent(new Event('scroll'));
    });

    // Scroll up to show
    act(() => {
      Object.defineProperty(window, 'scrollY', { writable: true, value: 100 });
      window.dispatchEvent(new Event('scroll'));
    });

    const pill = screen.getByTestId('mobile-cta-pill');
    expect(pill.className).not.toContain('translate-y-24');
  });

  it('applies orange background styling', () => {
    mockUsePathname.mockReturnValue('/library');
    render(<MobileCTAPill />);
    const link = screen.getByRole('link');
    expect(link.className).toMatch(/bg-orange/);
  });

  it('is fixed positioned at bottom-center', () => {
    mockUsePathname.mockReturnValue('/library');
    render(<MobileCTAPill />);
    const pill = screen.getByTestId('mobile-cta-pill');
    expect(pill.className).toContain('fixed');
    expect(pill.className).toContain('bottom-4');
  });
});
