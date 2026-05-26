/* eslint-disable local/no-hardcoded-color-utility -- text-white / button color on style-prop colored bg or decorative inline gradient; mockup .e-bg pattern. Will be re-evaluated in DS-15 finalization audit. */
'use client';

import { useEffect } from 'react';

import { ChevronLeft } from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { ADMIN_NAV_GROUPS } from '@/components/layout/admin-nav/admin-nav-config';
import { AdminNavList } from '@/components/layout/admin-nav/AdminNavList';
import { filterNavByRole } from '@/components/layout/admin-nav/filter-nav-by-role';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface AdminSideDrawerProps {
  open: boolean;
  onClose: () => void;
}

// ---------------------------------------------------------------------------
// AdminSideDrawer
// ---------------------------------------------------------------------------

export function AdminSideDrawer({ open, onClose }: AdminSideDrawerProps) {
  const { data: user } = useCurrentUser();
  const pathname = usePathname();

  const visibleGroups = filterNavByRole(ADMIN_NAV_GROUPS, user ?? null);

  // Prevent body scroll when drawer is open
  useEffect(() => {
    if (open) {
      document.body.style.overflow = 'hidden';
    } else {
      document.body.style.overflow = '';
    }
    return () => {
      document.body.style.overflow = '';
    };
  }, [open]);

  // Close on Escape key
  useEffect(() => {
    if (!open) return;

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        onClose();
      }
    };

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [open, onClose]);

  if (!open) return null;

  const userInitial =
    user?.displayName?.charAt(0).toUpperCase() || user?.email?.charAt(0).toUpperCase() || 'A';

  return (
    <>
      {/* Overlay */}
      <div
        data-testid="drawer-overlay"
        className="fixed inset-0 bg-foreground/40 backdrop-blur-sm z-40"
        onClick={onClose}
        aria-hidden="true"
      />

      {/* Drawer panel */}
      <div
        role="dialog"
        aria-modal="true"
        aria-label="Admin navigation menu"
        className="fixed top-0 left-0 bottom-0 w-[280px] bg-background border-r shadow-xl z-50 flex flex-col animate-in slide-in-from-left duration-200"
      >
        {/* User info header */}
        <div className="flex items-center gap-3 px-4 py-5 border-b shrink-0">
          <div className="w-10 h-10 rounded-full bg-[hsl(25,95%,45%)] flex items-center justify-center shrink-0">
            <span className="text-white text-sm font-bold">{userInitial}</span>
          </div>
          <div className="flex flex-col min-w-0">
            <span className="text-sm font-semibold truncate">{user?.displayName || 'Admin'}</span>
            <span className="text-xs text-muted-foreground truncate">{user?.email}</span>
            <span className="text-xs font-medium text-[hsl(var(--c-game-text))] capitalize mt-0.5">
              {user?.role || 'admin'}
            </span>
          </div>
        </div>

        {/* Navigation — scrollable */}
        <div className="flex-1 overflow-y-auto px-3 py-3">
          {/* Back to app */}
          <Link
            href="/dashboard"
            onClick={onClose}
            className="flex items-center gap-2 rounded-lg px-3 py-2 text-sm font-medium transition-colors mb-2"
            style={{ color: 'hsl(var(--c-kb-text))' }}
          >
            <ChevronLeft className="h-4 w-4 shrink-0" />
            <span>Torna all&apos;app</span>
          </Link>

          <div className="border-t mb-1" />

          {/* Role-filtered 4-group navigation */}
          <AdminNavList groups={visibleGroups} pathname={pathname} onNavigate={onClose} />
        </div>
      </div>
    </>
  );
}
