/**
 * AdvancedFiltersDrawer — Android/browser Back guard (gap A-03, issue #3197).
 *
 * The filters drawer is a transient overlay opened from LibraryHub. Without a
 * history guard, the hardware/gesture Back (Android) or the browser Back button
 * would navigate away from the /library route instead of just closing the drawer.
 * The drawer wires `useHistoryBackGuard(open, () => onOpenChange(false))`, so a
 * Back press (popstate) closes the drawer and stays on the route.
 *
 * `installMatchMedia(true)` forces desktop (Radix Dialog) mode; the guard is
 * window-level so it is orthogonal to the Vaul/Radix breakpoint swap.
 */

import { afterEach, describe, expect, it, vi } from 'vitest';
import { render } from '@testing-library/react';
import { IntlProvider } from 'react-intl';

import { AdvancedFiltersDrawer } from '../AdvancedFiltersDrawer';
import type { LibraryFilters } from '../types';

function installMatchMedia(matches: boolean) {
  Object.defineProperty(window, 'matchMedia', {
    configurable: true,
    writable: true,
    value: vi.fn().mockReturnValue({
      matches,
      media: '(min-width: 768px)',
      addEventListener: () => {},
      removeEventListener: () => {},
      onchange: null,
    }),
  });
}

const noop = () => {};
const emptyFilters: LibraryFilters = {};

function renderDrawer(open: boolean, onOpenChange: (o: boolean) => void) {
  return render(
    <IntlProvider locale="it" messages={{}} onError={() => {}}>
      <AdvancedFiltersDrawer
        open={open}
        onOpenChange={onOpenChange}
        activeFilters={emptyFilters}
        onApply={noop}
        onClear={noop}
      />
    </IntlProvider>
  );
}

afterEach(() => vi.restoreAllMocks());

describe('AdvancedFiltersDrawer — Android Back guard (A-03, #3197)', () => {
  it('closes via onOpenChange(false) when the user presses Back while open', () => {
    installMatchMedia(true);
    const onOpenChange = vi.fn();
    renderDrawer(true, onOpenChange);

    window.dispatchEvent(new PopStateEvent('popstate'));

    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it('does not arm the guard when the drawer is closed (no pushState)', () => {
    installMatchMedia(true);
    const pushSpy = vi.spyOn(window.history, 'pushState');
    renderDrawer(false, vi.fn());

    expect(pushSpy).not.toHaveBeenCalled();
  });
});
