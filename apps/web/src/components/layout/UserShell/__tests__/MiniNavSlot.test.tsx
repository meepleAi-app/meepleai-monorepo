import { render, screen } from '@testing-library/react';
import { describe, it, expect, beforeEach, vi } from 'vitest';

import { useMiniNavConfigStore } from '@/lib/stores/mini-nav-config-store';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => '/',
}));

import { MiniNavSlot } from '../MiniNavSlot';

describe('MiniNavSlot', () => {
  beforeEach(() => {
    useMiniNavConfigStore.getState().clear();
  });

  it('renders nothing when config is null', () => {
    const { container } = render(<MiniNavSlot />);
    expect(container).toBeEmptyDOMElement();
  });

  it('renders breadcrumb and tabs when config is set', () => {
    useMiniNavConfigStore.getState().setConfig({
      breadcrumb: 'Libreria · Hub',
      tabs: [
        { id: 'hub', label: 'Hub', href: '/library' },
        { id: 'personal', label: 'Personal', href: '/library?tab=personal', count: 47 },
      ],
      activeTabId: 'hub',
    });
    render(<MiniNavSlot />);
    expect(screen.getByText(/Libreria/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Hub/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Personal/i })).toBeInTheDocument();
    expect(screen.getByText('47')).toBeInTheDocument();
  });

  it('marks active tab with aria-current', () => {
    useMiniNavConfigStore.getState().setConfig({
      breadcrumb: 'Libreria',
      tabs: [
        { id: 'hub', label: 'Hub', href: '/library' },
        { id: 'personal', label: 'Personal', href: '/library?tab=personal' },
      ],
      activeTabId: 'personal',
    });
    render(<MiniNavSlot />);
    expect(screen.getByRole('link', { name: /Personal/i })).toHaveAttribute('aria-current', 'page');
  });

  it('renders primary action button when provided', () => {
    useMiniNavConfigStore.getState().setConfig({
      breadcrumb: 'Home',
      tabs: [{ id: 'a', label: 'A', href: '/' }],
      activeTabId: 'a',
      primaryAction: { label: '＋ Nuova partita', onClick: () => {} },
    });
    render(<MiniNavSlot />);
    expect(screen.getByRole('button', { name: /Nuova partita/i })).toBeInTheDocument();
  });

  it('renders the primary action icon when provided', () => {
    useMiniNavConfigStore.getState().setConfig({
      breadcrumb: 'Home',
      tabs: [{ id: 'a', label: 'A', href: '/' }],
      activeTabId: 'a',
      primaryAction: {
        label: 'Nuova partita',
        icon: '＋',
        onClick: () => {},
      },
    });
    render(<MiniNavSlot />);
    expect(screen.getByText('＋')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Nuova partita/i })).toBeInTheDocument();
  });

  // Phone width. The strip is a single 48px row with a 104px left gutter and tabs that cannot shrink: on a 390px
  // screen four tabs measured 510px, so the whole page scrolled sideways and the last tabs fell off the screen.
  // jsdom has no layout; the measurement was taken in a browser, and what these pin down is the class contract
  // that produced it (scroll the strip, drop the gutter, keep the tabs whole, keep desktop as it was).
  describe('at phone width', () => {
    function renderStrip() {
      useMiniNavConfigStore.getState().setConfig({
        breadcrumb: 'Games',
        tabs: [
          { id: 'discover', label: 'Discover', href: '/games' },
          { id: 'catalog', label: 'Catalogo', href: '/games?tab=catalog' },
        ],
        activeTabId: 'discover',
      });
      render(<MiniNavSlot />);
      return screen.getByTestId('mini-nav-slot');
    }

    it('scrolls inside the strip instead of widening the page, and only below md', () => {
      const strip = renderStrip();
      expect(strip).toHaveClass('overflow-x-auto', 'md:overflow-visible');
    });

    it('keeps the 104px gutter for desktop only', () => {
      const strip = renderStrip();
      expect(strip).toHaveClass('pl-4', 'md:pl-[104px]');
      expect(strip.className.split(/\s+/)).not.toContain('pl-[104px]');
    });

    it('does not let the tabs shrink or wrap', () => {
      renderStrip();
      for (const link of screen.getAllByRole('link')) {
        expect(link).toHaveClass('shrink-0', 'whitespace-nowrap');
      }
    });

    it('hides the breadcrumb below md (the app bar already names the page)', () => {
      renderStrip();
      const breadcrumb = screen.getByText(/Games/).closest('div');
      expect(breadcrumb).toHaveClass('hidden', 'md:block');
    });

    it('keeps the active underline inside the strip below md, where overflow would clip it', () => {
      const strip = renderStrip();
      const underline = strip.querySelector('[aria-current="page"] span[aria-hidden]');
      expect(underline).toHaveClass('bottom-0', 'md:bottom-[-12px]');
    });
  });
});
