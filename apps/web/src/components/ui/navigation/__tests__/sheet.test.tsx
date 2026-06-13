/**
 * Sheet primitive — a11y regression guard.
 *
 * Closes #2273. Every Sheet consumer historically needed to add
 * `aria-modal="true"` ad-hoc because Radix Dialog primitive does not emit
 * it (memory `radix-dialog-no-aria-modal`). Pre-#2273 audit found 31
 * consumers without the attribute — only AddGameDrawer (added in #2269)
 * had it. This test pins the primitive-level default so the fragile
 * per-consumer pattern cannot return.
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { Sheet, SheetContent, SheetHeader, SheetTitle } from '../sheet';

describe('SheetContent — a11y primitive defaults', () => {
  it('emits role="dialog" + aria-modal="true" + aria-labelledby tied to SheetTitle', () => {
    render(
      <Sheet open onOpenChange={() => undefined}>
        <SheetContent data-testid="sheet">
          <SheetHeader>
            <SheetTitle data-testid="sheet-title">Title</SheetTitle>
          </SheetHeader>
        </SheetContent>
      </Sheet>
    );

    const dialog = screen.getByRole('dialog');
    expect(dialog).toHaveAttribute('aria-modal', 'true');

    const labelledBy = dialog.getAttribute('aria-labelledby');
    expect(labelledBy).toBeTruthy();

    const title = screen.getByTestId('sheet-title');
    expect(title.id).toBe(labelledBy);
  });
});
