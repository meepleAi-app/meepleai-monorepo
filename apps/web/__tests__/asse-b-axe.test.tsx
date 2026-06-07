/**
 * Asse B (#1897) WP7 T7 — axe AA smoke test.
 *
 * Verifies that the new primitives shipped under asse B (WizardModal in T4,
 * MainSidebar+MainNavList in T2) have no axe AA violations in their default
 * rendered state. Per DEC-6 (no Storybook, vitest+playwright minimal) we
 * stick to component-level scans with the existing `jest-axe` setup that
 * is already wired in `apps/web/src/components/ui/drawer/drawer.test.tsx`.
 *
 * The MainNavList scan is the canonical asse-B AA gate for the sidebar
 * because `MainSidebar` itself depends on `useCurrentUser` (TanStack Query
 * provider) and `usePathname` (Next router context); rendering it in
 * isolation would require those providers. `MainNavList` is the pure
 * presentational renderer it wraps and is the surface that owns the
 * `<nav>`/`<a>` semantics.
 */

import { describe, it, expect } from 'vitest';
import { render } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';

import { MainNavList } from '@/components/layout/main-nav/MainNavList';
import { MAIN_NAV_ITEMS } from '@/components/layout/main-nav/main-nav-config';
import { WizardModal } from '@/components/ui/wizard-modal';

expect.extend(toHaveNoViolations);

describe('Asse B (#1897) axe AA gate (T7)', () => {
  it('MainNavList — no axe violations (asse B sidebar surface)', async () => {
    const { container } = render(
      <MainNavList items={MAIN_NAV_ITEMS} pathname="/dashboard" ariaLabel="Main navigation" />
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('MainNavList with notification badge — no axe violations', async () => {
    const { container } = render(
      <MainNavList
        items={MAIN_NAV_ITEMS}
        pathname="/notifications"
        ariaLabel="Main navigation"
        notificationCount={3}
      />
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('WizardModal open — no axe violations on step 1', async () => {
    const { container } = render(
      <WizardModal
        steps={[
          { title: 'Step 1', content: <p>Content 1</p> },
          { title: 'Step 2', content: <p>Content 2</p> },
        ]}
        onComplete={async () => undefined}
        onCancel={() => undefined}
        open={true}
        onOpenChange={() => undefined}
      />
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
