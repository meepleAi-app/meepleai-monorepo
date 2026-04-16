'use client';

import { useEffect, useTransition } from 'react';

import { LogOut } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { logoutAction } from '@/actions/auth';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';

import { SideDrawerItems } from './SideDrawerItems';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface SideDrawerProps {
  open: boolean;
  onClose: () => void;
}

// ---------------------------------------------------------------------------
// SideDrawer
// ---------------------------------------------------------------------------

export function SideDrawer({ open, onClose }: SideDrawerProps) {
  const router = useRouter();
  const { data: user } = useCurrentUser();
  const [isLoggingOut, startTransition] = useTransition();

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

  const handleLogout = () => {
    startTransition(async () => {
      const result = await logoutAction();
      if (result.success) {
        onClose();
        router.push('/login');
      }
    });
  };

  if (!open) return null;

  const userInitial =
    user?.displayName?.charAt(0).toUpperCase() || user?.email?.charAt(0).toUpperCase() || 'U';

  return (
    <>
      {/* Overlay */}
      <div
        data-testid="drawer-overlay"
        className="fixed inset-0 bg-black/40 backdrop-blur-sm z-40"
        onClick={onClose}
        aria-hidden="true"
      />

      {/* Drawer panel */}
      <div
        role="dialog"
        aria-modal="true"
        aria-label="Navigation menu"
        className="fixed top-0 left-0 bottom-0 w-[280px] bg-background border-r shadow-xl z-50 flex flex-col animate-in slide-in-from-left duration-200"
      >
        {/* User info header */}
        <div className="flex items-center gap-3 px-4 py-5 border-b shrink-0">
          <div className="w-10 h-10 rounded-full bg-[hsl(25,95%,45%)] flex items-center justify-center shrink-0">
            <span className="text-white text-sm font-bold">{userInitial}</span>
          </div>
          <div className="flex flex-col min-w-0">
            <span className="text-sm font-semibold truncate">{user?.displayName || 'Utente'}</span>
            <span className="text-xs text-muted-foreground truncate">{user?.email}</span>
          </div>
        </div>

        {/* Navigation items — scrollable */}
        <div className="flex-1 overflow-y-auto px-3 py-3">
          <SideDrawerItems userRole={user?.role} onNavigate={onClose} />
        </div>

        {/* Logout button */}
        <div className="px-3 py-4 border-t shrink-0">
          <button
            type="button"
            onClick={handleLogout}
            disabled={isLoggingOut}
            className="flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium text-destructive hover:bg-destructive/10 transition-colors disabled:opacity-50"
          >
            <LogOut className="h-4 w-4 shrink-0" />
            <span>{isLoggingOut ? 'Disconnessione...' : 'Esci'}</span>
          </button>
        </div>
      </div>
    </>
  );
}
