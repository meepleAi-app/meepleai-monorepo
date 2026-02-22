'use client';

/**
 * LayoutShell — Unified 3-tier Layout Component
 * Issue #5035 — LayoutShell Component
 *
 * Replaces AuthenticatedLayout + PublicLayout + all variants.
 * Single shell for all pages (user + admin), role-aware.
 *
 * Layout Structure:
 *   Desktop (md+):
 *     [Navbar — 64px fixed top]
 *     [MiniNav — 48px sticky below Navbar]
 *     [Main Content — flex-1, scrollable]
 *     [ActionBar — 56px sticky bottom of content]
 *
 *   Mobile:
 *     [Navbar — 48px fixed top]
 *     [MiniNav — 44px horizontally scrollable]
 *     [Main Content — flex-1, scrollable]
 *     [ActionBar — 56px fixed bottom, safe-area aware]
 *
 * Usage:
 *   // In root app layout or (authenticated) group layout:
 *   <LayoutShell>
 *     {children}
 *   </LayoutShell>
 *
 *   // Pages configure MiniNav + ActionBar declaratively:
 *   // (via useSetNavConfig in their layout.tsx)
 */

import { type ReactNode } from 'react';

import { NavigationProvider } from '@/context/NavigationContext';
import { ImpersonationBanner } from '@/components/ui/feedback/impersonation-banner';
import { CardStackPanel } from '@/components/ui/navigation/card-stack-panel';
import { useImpersonationStore } from '@/store/impersonation';
import { cn } from '@/lib/utils';

// ─── Slot Imports (forward-compatible stubs until #5036-#5038 land) ───────────
// These will be replaced with real implementations in issues #5036, #5037, #5038.
// The LayoutShell accepts them as props or uses the real components once available.

// ─── Types ────────────────────────────────────────────────────────────────────

export interface LayoutShellSlots {
  /**
   * Navbar slot (L1).
   * Defaults to null — replaced with <Navbar /> once #5036 is implemented.
   */
  navbar?: ReactNode;
  /**
   * MiniNav slot (L2).
   * Defaults to null — replaced with <MiniNav /> once #5037 is implemented.
   */
  miniNav?: ReactNode;
  /**
   * ActionBar slot (L3).
   * Defaults to null — replaced with <ActionBar /> once #5038 is implemented.
   */
  actionBar?: ReactNode;
}

export interface LayoutShellProps extends LayoutShellSlots {
  /** Page content */
  children: ReactNode;
  /**
   * Additional CSS classes for the main content area.
   */
  className?: string;
  /**
   * Disable the max-width container on the content area.
   * Use for full-bleed layouts (e.g., maps, whiteboards).
   */
  fullWidth?: boolean;
}

// ─── Heights (CSS vars for easy override) ─────────────────────────────────────

const NAVBAR_H_DESKTOP = 64; // px — md+
const NAVBAR_H_MOBILE = 48; // px
const MINI_NAV_H_DESKTOP = 48; // px — md+
const MINI_NAV_H_MOBILE = 44; // px
const ACTION_BAR_H = 56; // px

// ─── Component ────────────────────────────────────────────────────────────────

/**
 * LayoutShell
 *
 * Unified layout shell for all authenticated + public pages.
 * Wraps content with the NavigationProvider and provides slots for
 * the 3-tier navigation system (Navbar, MiniNav, ActionBar).
 */
function LayoutShellInner({
  children,
  className,
  fullWidth = false,
  navbar,
  miniNav,
  actionBar,
}: LayoutShellProps) {
  const { isImpersonating, impersonatedUser, isLoading, endImpersonation } =
    useImpersonationStore();

  // Compute top offset for main content:
  // Navbar + optional MiniNav (when it has tabs, it's sticky below Navbar)
  const navbarHeight = `${NAVBAR_H_MOBILE}px`;
  const navbarHeightDesktop = `${NAVBAR_H_DESKTOP}px`;
  const miniNavHeight = `${MINI_NAV_H_MOBILE}px`;
  const miniNavHeightDesktop = `${MINI_NAV_H_DESKTOP}px`;

  return (
    <div className="min-h-screen flex flex-col bg-background" data-testid="layout-shell">
      {/* ── ImpersonationBanner (fixed top, z-[100]) ─────────────────────── */}
      <ImpersonationBanner
        isImpersonating={isImpersonating}
        impersonatedUser={impersonatedUser}
        onEndImpersonation={endImpersonation}
        isLoading={isLoading}
      />

      {/* ── Navbar (L1) — fixed top ──────────────────────────────────────── */}
      <header
        className={cn(
          'fixed top-0 inset-x-0 z-50',
          // Push down when impersonating (banner is fixed top, 40px)
          isImpersonating && 'top-10',
          // Mobile: 48px, Desktop: 64px
          'h-12 md:h-16'
        )}
        style={{
          '--navbar-h': navbarHeight,
          '--navbar-h-md': navbarHeightDesktop,
        } as React.CSSProperties}
        role="banner"
        aria-label="Main navigation"
      >
        {navbar}
      </header>

      {/* ── MiniNav (L2) — sticky below Navbar ───────────────────────────── */}
      {miniNav && (
        <div
          className={cn(
            'fixed inset-x-0 z-40',
            // Position directly below Navbar
            // Mobile: 48px / Desktop: 64px navbar + 40px impersonation banner
            isImpersonating
              ? 'top-[88px] md:top-[104px]'
              : 'top-12 md:top-16',
            // Mobile: 44px, Desktop: 48px
            'h-11 md:h-12'
          )}
          style={{
            '--mini-nav-h': miniNavHeight,
            '--mini-nav-h-md': miniNavHeightDesktop,
          } as React.CSSProperties}
          aria-label="Section navigation"
        >
          {miniNav}
        </div>
      )}

      {/* ── Main Content ─────────────────────────────────────────────────── */}
      <main
        id="main-content"
        className={cn(
          'flex-1 flex flex-col',
          // Top offset: Navbar height + MiniNav height (when present)
          // Values: navbar 48/64px, miniNav 44/48px, banner 40px
          miniNav
            ? [
                isImpersonating
                  ? 'pt-[132px]'   // 48 + 44 + 40
                  : 'pt-[92px]',   // 48 + 44
                isImpersonating
                  ? 'md:pt-[152px]' // 64 + 48 + 40
                  : 'md:pt-28',     // 64 + 48 = 112px
              ]
            : [
                isImpersonating
                  ? 'pt-[88px]'  // 48 + 40
                  : 'pt-12',     // 48px
                isImpersonating
                  ? 'md:pt-[104px]' // 64 + 40
                  : 'md:pt-16',     // 64px
              ],
          // Bottom offset: ActionBar height (56px) on mobile (fixed bottom)
          actionBar && 'pb-14 md:pb-0',
          className
        )}
        tabIndex={-1}
      >
        <div
          className={cn(
            'flex-1 w-full',
            !fullWidth && 'mx-auto max-w-screen-xl px-4 sm:px-6 lg:px-8 py-6'
          )}
        >
          {children}
        </div>

        {/* ── ActionBar (L3) — sticky bottom of content (desktop) ────────── */}
        {actionBar && (
          <div
            className={cn(
              // Desktop: sticky inside the content column
              'hidden md:block sticky bottom-0',
              'h-14',
              'bg-background border-t border-border'
            )}
            role="toolbar"
            aria-label="Page actions"
          >
            {actionBar}
          </div>
        )}
      </main>

      {/* ── ActionBar (L3) — fixed bottom on mobile ──────────────────────── */}
      {actionBar && (
        <div
          className={cn(
            'fixed bottom-0 inset-x-0 z-40 md:hidden',
            'h-14',
            'bg-background border-t border-border',
            // Safe area for iPhone notch / home indicator
            'pb-[env(safe-area-inset-bottom)]'
          )}
          role="toolbar"
          aria-label="Page actions"
        >
          {actionBar}
        </div>
      )}

      {/* ── CardStackPanel — "Carte in Mano" floating nav ────────────────── */}
      <CardStackPanel />
    </div>
  );
}

/**
 * LayoutShell
 *
 * Wraps with NavigationProvider so all child components can access
 * the current page's MiniNav + ActionBar configuration.
 */
export function LayoutShell(props: LayoutShellProps) {
  return (
    <NavigationProvider>
      <LayoutShellInner {...props} />
    </NavigationProvider>
  );
}

export default LayoutShell;
