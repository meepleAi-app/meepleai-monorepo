import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { axe, toHaveNoViolations } from 'jest-axe';

import {
  Drawer,
  DrawerContent,
  DrawerHeader,
  DrawerTitle,
  DrawerDescription,
  DrawerFooter,
  DrawerClose,
} from './drawer';

expect.extend(toHaveNoViolations);

type Listener = (e: MediaQueryListEvent) => void;

interface MockMQL {
  matches: boolean;
  media: string;
  addEventListener: (event: 'change', cb: Listener) => void;
  removeEventListener: (event: 'change', cb: Listener) => void;
  onchange: Listener | null;
}

function installMatchMedia(matches: boolean): MockMQL {
  const listeners = new Set<Listener>();
  const mql: MockMQL = {
    matches,
    media: '(min-width: 768px)',
    addEventListener: (_e, cb) => listeners.add(cb),
    removeEventListener: (_e, cb) => listeners.delete(cb),
    onchange: null,
  };
  Object.defineProperty(window, 'matchMedia', {
    configurable: true,
    writable: true,
    value: vi.fn().mockReturnValue(mql),
  });
  return mql;
}

// jsdom does not implement PointerEvent capture / scroll APIs that vaul uses.
beforeEach(() => {
  if (typeof Element.prototype.hasPointerCapture !== 'function') {
    Element.prototype.hasPointerCapture = () => false;
  }
  if (typeof Element.prototype.setPointerCapture !== 'function') {
    Element.prototype.setPointerCapture = () => {};
  }
  if (typeof Element.prototype.releasePointerCapture !== 'function') {
    Element.prototype.releasePointerCapture = () => {};
  }
  if (typeof Element.prototype.scrollIntoView !== 'function') {
    Element.prototype.scrollIntoView = () => {};
  }
  // Vaul reads `style.transform` from getComputedStyle on release; jsdom returns
  // empty string which crashes its matrix parser. Provide a safe identity value.
  const realGCS = window.getComputedStyle.bind(window);
  vi.spyOn(window, 'getComputedStyle').mockImplementation((el: Element, pseudo?: string | null) => {
    const cs = realGCS(el, pseudo ?? undefined);
    if (!cs.transform) {
      try {
        Object.defineProperty(cs, 'transform', { configurable: true, value: 'none' });
      } catch {
        // ignore — already defined
      }
    }
    return cs;
  });
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe('Drawer (desktop / Radix)', () => {
  beforeEach(() => {
    installMatchMedia(true);
  });

  it('renders DrawerTitle and body when open', () => {
    render(
      <Drawer open onOpenChange={() => {}} side="desktop-right">
        <DrawerContent>
          <DrawerHeader>
            <DrawerTitle>My title</DrawerTitle>
            <DrawerDescription>desc text</DrawerDescription>
          </DrawerHeader>
          <p>body content</p>
          <DrawerFooter>
            <DrawerClose>Close</DrawerClose>
          </DrawerFooter>
        </DrawerContent>
      </Drawer>
    );
    expect(screen.getByText('My title')).toBeInTheDocument();
    expect(screen.getByText('desc text')).toBeInTheDocument();
    expect(screen.getByText('body content')).toBeInTheDocument();
  });

  it('does not render content when closed', () => {
    render(
      <Drawer open={false} onOpenChange={() => {}} side="desktop-right">
        <DrawerContent>
          <DrawerHeader>
            <DrawerTitle>Hidden title</DrawerTitle>
          </DrawerHeader>
          <p>hidden body</p>
        </DrawerContent>
      </Drawer>
    );
    expect(screen.queryByText('Hidden title')).not.toBeInTheDocument();
    expect(screen.queryByText('hidden body')).not.toBeInTheDocument();
  });

  it('DrawerClose triggers onOpenChange(false)', async () => {
    const onOpenChange = vi.fn();
    const user = userEvent.setup();
    render(
      <Drawer open onOpenChange={onOpenChange} side="desktop-right">
        <DrawerContent>
          <DrawerHeader>
            <DrawerTitle>t</DrawerTitle>
          </DrawerHeader>
          <DrawerFooter>
            <DrawerClose>Dismiss</DrawerClose>
          </DrawerFooter>
        </DrawerContent>
      </Drawer>
    );
    await user.click(screen.getByRole('button', { name: 'Dismiss' }));
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it('ESC closes the drawer on desktop', async () => {
    const onOpenChange = vi.fn();
    const user = userEvent.setup();
    render(
      <Drawer open onOpenChange={onOpenChange} side="desktop-right">
        <DrawerContent>
          <DrawerHeader>
            <DrawerTitle>t</DrawerTitle>
          </DrawerHeader>
          <p>body</p>
        </DrawerContent>
      </Drawer>
    );
    await user.keyboard('{Escape}');
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it('uses Radix Dialog (role="dialog") on desktop', () => {
    render(
      <Drawer open onOpenChange={() => {}} side="desktop-right">
        <DrawerContent>
          <DrawerHeader>
            <DrawerTitle>t</DrawerTitle>
          </DrawerHeader>
        </DrawerContent>
      </Drawer>
    );
    const dialog = screen.getByRole('dialog');
    expect(dialog).toBeInTheDocument();
    // desktop content is fixed to right side
    expect(dialog.className).toMatch(/right-0/);
  });

  it('renders entity accent on desktop when entity prop set', () => {
    render(
      <Drawer open onOpenChange={() => {}} side="desktop-right" entity="game">
        <DrawerContent>
          <DrawerHeader>
            <DrawerTitle>t</DrawerTitle>
          </DrawerHeader>
        </DrawerContent>
      </Drawer>
    );
    const dialog = screen.getByRole('dialog');
    const accent = dialog.querySelector('[data-drawer-accent="game"]');
    expect(accent).toBeInTheDocument();
    expect(accent?.getAttribute('style')).toContain('hsl(var(--e-game))');
  });

  it('maps kb entity to --e-document on desktop accent', () => {
    render(
      <Drawer open onOpenChange={() => {}} side="desktop-right" entity="kb">
        <DrawerContent>
          <DrawerHeader>
            <DrawerTitle>t</DrawerTitle>
          </DrawerHeader>
        </DrawerContent>
      </Drawer>
    );
    const accent = screen.getByRole('dialog').querySelector('[data-drawer-accent="kb"]');
    expect(accent?.getAttribute('style')).toContain('hsl(var(--e-document))');
  });

  it('has no a11y violations on desktop', async () => {
    const { container } = render(
      <Drawer open onOpenChange={() => {}} side="desktop-right">
        <DrawerContent>
          <DrawerHeader>
            <DrawerTitle>Accessible title</DrawerTitle>
            <DrawerDescription>Accessible desc</DrawerDescription>
          </DrawerHeader>
          <p>body</p>
        </DrawerContent>
      </Drawer>
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});

describe('Drawer (mobile / vaul)', () => {
  beforeEach(() => {
    installMatchMedia(false);
  });

  it('renders DrawerTitle when open as bottom sheet', async () => {
    render(
      <Drawer open onOpenChange={() => {}} side="mobile-bottom">
        <DrawerContent>
          <DrawerHeader>
            <DrawerTitle>Mobile title</DrawerTitle>
          </DrawerHeader>
          <p>mobile body</p>
        </DrawerContent>
      </Drawer>
    );
    // vaul mounts asynchronously via portal; queryByText handles both sync & async cases
    expect(await screen.findByText('Mobile title')).toBeInTheDocument();
    expect(screen.getByText('mobile body')).toBeInTheDocument();
  });

  it('uses vaul (data-vaul-drawer attribute on content)', async () => {
    render(
      <Drawer open onOpenChange={() => {}} side="mobile-bottom">
        <DrawerContent>
          <DrawerHeader>
            <DrawerTitle>vaul check</DrawerTitle>
          </DrawerHeader>
        </DrawerContent>
      </Drawer>
    );
    await screen.findByText('vaul check');
    const vaulEl = document.querySelector('[data-vaul-drawer]');
    expect(vaulEl).not.toBeNull();
    expect(vaulEl?.getAttribute('data-vaul-drawer-direction')).toBe('bottom');
  });

  it('renders entity accent handle on mobile when entity prop set', async () => {
    render(
      <Drawer open onOpenChange={() => {}} side="mobile-bottom" entity="player">
        <DrawerContent>
          <DrawerHeader>
            <DrawerTitle>player drawer</DrawerTitle>
          </DrawerHeader>
        </DrawerContent>
      </Drawer>
    );
    await screen.findByText('player drawer');
    const accent = document.querySelector('[data-drawer-accent="player"]');
    expect(accent).not.toBeNull();
    expect(accent?.getAttribute('style')).toContain('hsl(var(--e-player))');
  });

  it('DrawerClose triggers onOpenChange(false) on mobile', async () => {
    const onOpenChange = vi.fn();
    const user = userEvent.setup();
    render(
      <Drawer open onOpenChange={onOpenChange} side="mobile-bottom">
        <DrawerContent>
          <DrawerHeader>
            <DrawerTitle>t</DrawerTitle>
          </DrawerHeader>
          <DrawerFooter>
            <DrawerClose>Bye</DrawerClose>
          </DrawerFooter>
        </DrawerContent>
      </Drawer>
    );
    const btn = await screen.findByRole('button', { name: 'Bye' });
    await user.click(btn);
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });
});

describe('Drawer (auto mode)', () => {
  it('side="auto" picks bottom sheet on mobile', async () => {
    installMatchMedia(false);
    render(
      <Drawer open onOpenChange={() => {}} side="auto">
        <DrawerContent>
          <DrawerHeader>
            <DrawerTitle>auto-mobile</DrawerTitle>
          </DrawerHeader>
        </DrawerContent>
      </Drawer>
    );
    await screen.findByText('auto-mobile');
    expect(document.querySelector('[data-vaul-drawer]')).not.toBeNull();
  });

  it('side="auto" picks Radix dialog on desktop', () => {
    installMatchMedia(true);
    render(
      <Drawer open onOpenChange={() => {}} side="auto">
        <DrawerContent>
          <DrawerHeader>
            <DrawerTitle>auto-desktop</DrawerTitle>
          </DrawerHeader>
        </DrawerContent>
      </Drawer>
    );
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(document.querySelector('[data-vaul-drawer]')).toBeNull();
  });

  it('defaults side to "auto" when omitted', () => {
    installMatchMedia(true);
    render(
      <Drawer open onOpenChange={() => {}}>
        <DrawerContent>
          <DrawerHeader>
            <DrawerTitle>auto-default</DrawerTitle>
          </DrawerHeader>
        </DrawerContent>
      </Drawer>
    );
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it('honours direction="bottom" alias for mobile-bottom', async () => {
    installMatchMedia(true); // desktop matchMedia, but direction forces bottom
    render(
      <Drawer open onOpenChange={() => {}} direction="bottom">
        <DrawerContent>
          <DrawerHeader>
            <DrawerTitle>direction-bottom</DrawerTitle>
          </DrawerHeader>
        </DrawerContent>
      </Drawer>
    );
    await screen.findByText('direction-bottom');
    expect(document.querySelector('[data-vaul-drawer]')).not.toBeNull();
  });

  it('honours direction="right" alias for desktop-right', () => {
    installMatchMedia(false); // mobile matchMedia, but direction forces right
    render(
      <Drawer open onOpenChange={() => {}} direction="right">
        <DrawerContent>
          <DrawerHeader>
            <DrawerTitle>direction-right</DrawerTitle>
          </DrawerHeader>
        </DrawerContent>
      </Drawer>
    );
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });
});

// Touch act() to satisfy lint rules — used implicitly via user-event.
void act;
