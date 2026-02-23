/**
 * LayoutShell — 3-Tier Navigation System, Concept 4: Floating
 * Issue #5035 — LayoutShell Component
 *
 * Full-page shell implementing the Concept 4 "Floating" layout:
 *
 *   ┌────────────────────────────────────┐
 *   │ TopNavbar (sticky h-14)            │
 *   ├────────────────────────────────────┤
 *   │ MiniNav (h-10, context-aware)      │ ← auto-hides when no tabs
 *   ├────────────────────────────────────┤
 *   │                                    │
 *   │   Page Content (scrollable)        │
 *   │                                    │
 *   │   ┌───────────────────┐            │
 *   │   │ FloatingActionBar │            │ ← fixed bottom-center pill
 *   │   └───────────────────┘            │
 *   └────────────────────────────────────┘
 *
 * Responsibilities:
 * - Provides NavigationProvider so pages can call useSetNavConfig()
 * - Renders the 3 nav layers in the correct order
 * - Preserves ImpersonationBanner and CardStackPanel
 * - Padding-bottom on content area to clear FloatingActionBar
 *
 * Usage (replace AuthenticatedLayout in route layouts):
 * ```tsx
 * import { LayoutShell } from '@/components/layout/LayoutShell';
 *
 * export default function AuthenticatedRouteLayout({ children }) {
 *   return <LayoutShell>{children}</LayoutShell>;
 * }
 * ```
 */

'use client';

import { type ReactNode, Suspense } from 'react';

import { ImpersonationBanner } from '@/components/ui/feedback/impersonation-banner';
import { CardStackPanel } from '@/components/ui/navigation/card-stack-panel';
import { NavigationProvider } from '@/context/NavigationContext';
import { cn } from '@/lib/utils';
import { useImpersonationStore } from '@/store/impersonation';

import { FloatingActionBar } from '../FloatingActionBar';
import { MiniNav } from '../MiniNav';
import { TopNavbar } from '../TopNavbar';

// ─── LayoutShell ──────────────────────────────────────────────────────────────

export interface LayoutShellProps {
  children: ReactNode;
  /** Remove horizontal padding from the content area (for full-bleed pages) */
  fullWidth?: boolean;
  /** Additional className for the main element */
  className?: string;
}

/**
 * LayoutShell (Concept 4 — Floating)
 *
 * Composes the 3-tier navigation system into a single shell.
 * NavigationProvider is internal so pages can register their nav config
 * via useSetNavConfig() anywhere in the tree.
 */
export function LayoutShell({ children, fullWidth = false, className }: LayoutShellProps) {
  return (
    <NavigationProvider>
      <LayoutShellInner fullWidth={fullWidth} className={className}>
        {children}
      </LayoutShellInner>
    </NavigationProvider>
  );
}

// ─── Inner shell (needs NavigationProvider in scope) ─────────────────────────

function LayoutShellInner({ children, fullWidth, className }: LayoutShellProps) {
  const { isImpersonating, impersonatedUser, isLoading, endImpersonation } =
    useImpersonationStore();

  return (
    <div className="min-h-screen flex flex-col bg-background">
      {/* Impersonation Banner (fixed at top when active) */}
      <ImpersonationBanner
        isImpersonating={isImpersonating}
        impersonatedUser={impersonatedUser}
        onEndImpersonation={endImpersonation}
        isLoading={isLoading}
      />

      {/* ── Level 1: TopNavbar ─────────────────────────────────────────────── */}
      <TopNavbar />

      {/* ── Level 2: MiniNav (auto-hides when no tabs) ─────────────────────── */}
      <Suspense>
        <MiniNav />
      </Suspense>

      {/* ── Main content ───────────────────────────────────────────────────── */}
      <main
        id="main-content"
        tabIndex={-1}
        className={cn(
          'flex-1',
          // Horizontal padding unless fullWidth
          !fullWidth && 'px-4 sm:px-6 lg:px-8',
          // Bottom padding to clear FloatingActionBar pill (h≈52px + gap-6 = 80px)
          'pb-24',
          // Top spacing
          'pt-4',
          className
        )}
      >
        {children}
      </main>

      {/* ── Level 3: FloatingActionBar (auto-hides when no actions) ─────────── */}
      <FloatingActionBar />

      {/* Card Stack Panel — "Carte in Mano" */}
      <CardStackPanel />
    </div>
  );
}
