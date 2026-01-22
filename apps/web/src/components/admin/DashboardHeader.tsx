/**
 * Dashboard Header Component - Issue #2784
 *
 * Enhanced header for admin dashboard redesign with:
 * - Welcome message with admin name
 * - Real-time date/time display
 * - Global search bar
 * - Notification bell with unread count badge
 *
 * Part of Epic #2783 - Admin Dashboard Redesign
 */

'use client';

import { useCallback, useEffect, useState } from 'react';

import { Bell, Search } from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { useAuthUser } from '@/hooks/useAuthUser';
import {
  useNotificationStore,
  selectUnreadCount,
} from '@/store/notification/store';

// ============================================================================
// Types
// ============================================================================

export interface DashboardHeaderProps {
  /** Override admin name (useful for testing) */
  adminName?: string;
  /** Custom class name for the header container */
  className?: string;
  /** Callback when search is submitted */
  onSearch?: (query: string) => void;
}

// ============================================================================
// Utility Components
// ============================================================================

/**
 * Meeple SVG decoration for the welcome box
 */
function MeepleDecoration({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 32" className={className} fill="currentColor" aria-hidden="true">
      <path d="M12 0C7.5 0 4 3 4 7c0 2.5 1.5 4.5 3.5 5.5L6 32h12l-1.5-19.5C18.5 11.5 20 9.5 20 7c0-4-3.5-7-8-7z" />
    </svg>
  );
}

/**
 * Subtle dice pattern for background decoration
 */
function DicePattern({ className }: { className?: string }) {
  return (
    <div className={`absolute opacity-[0.03] pointer-events-none ${className ?? ''}`} aria-hidden="true">
      <svg width="60" height="60" viewBox="0 0 60 60" fill="currentColor">
        <circle cx="10" cy="10" r="3" />
        <circle cx="30" cy="10" r="3" />
        <circle cx="50" cy="10" r="3" />
        <circle cx="10" cy="30" r="3" />
        <circle cx="30" cy="30" r="3" />
        <circle cx="50" cy="30" r="3" />
        <circle cx="10" cy="50" r="3" />
        <circle cx="30" cy="50" r="3" />
        <circle cx="50" cy="50" r="3" />
      </svg>
    </div>
  );
}

// ============================================================================
// Main Component
// ============================================================================

export function DashboardHeader({
  adminName: adminNameProp,
  className,
  onSearch,
}: DashboardHeaderProps) {
  const router = useRouter();
  const { user } = useAuthUser();
  const unreadCount = useNotificationStore(selectUnreadCount);
  const fetchUnreadCount = useNotificationStore(state => state.fetchUnreadCount);

  const [currentTime, setCurrentTime] = useState<Date | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [mounted, setMounted] = useState(false);

  // Get admin name from props or auth context
  const adminName = adminNameProp ?? user?.displayName ?? user?.email?.split('@')[0] ?? 'Admin';

  // Hydration-safe mounting
  useEffect(() => {
    setMounted(true);
    setCurrentTime(new Date());
  }, []);

  // Real-time clock update (every second)
  useEffect(() => {
    if (!mounted) return;

    const timer = setInterval(() => {
      setCurrentTime(new Date());
    }, 1000);

    return () => clearInterval(timer);
  }, [mounted]);

  // Fetch unread count on mount and periodically
  useEffect(() => {
    if (!mounted) return;

    // Initial fetch
    void fetchUnreadCount();

    // Poll every 30 seconds
    const pollTimer = setInterval(() => {
      void fetchUnreadCount();
    }, 30_000);

    return () => clearInterval(pollTimer);
  }, [mounted, fetchUnreadCount]);

  // Format date in Italian
  const formatDate = useCallback((date: Date) => {
    return date.toLocaleDateString('it-IT', {
      weekday: 'long',
      day: 'numeric',
      month: 'long',
      year: 'numeric',
    });
  }, []);

  // Format time in 24h format
  const formatTime = useCallback((date: Date) => {
    return date.toLocaleTimeString('it-IT', {
      hour: '2-digit',
      minute: '2-digit',
    });
  }, []);

  // Handle search submission
  const handleSearchSubmit = useCallback(
    (e: React.FormEvent) => {
      e.preventDefault();
      if (searchQuery.trim()) {
        if (onSearch) {
          onSearch(searchQuery.trim());
        } else {
          // Default: navigate to search page
          router.push(`/admin/search?q=${encodeURIComponent(searchQuery.trim())}`);
        }
      }
    },
    [searchQuery, onSearch, router]
  );

  // Handle search input change
  const handleSearchChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setSearchQuery(e.target.value);
  }, []);

  return (
    <header
      className={`relative overflow-hidden rounded-2xl bg-gradient-to-br from-stone-900 via-stone-800 to-stone-900 p-6 text-white shadow-xl ${className ?? ''}`}
      data-testid="dashboard-header"
    >
      {/* Background decorations */}
      <div className="absolute inset-0 opacity-10" aria-hidden="true">
        <div className="absolute -right-10 -top-10 h-40 w-40 rounded-full bg-orange-500 blur-3xl" />
        <div className="absolute -bottom-10 -left-10 h-32 w-32 rounded-full bg-amber-500 blur-3xl" />
      </div>
      <DicePattern className="right-4 top-4" />

      <div className="relative flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
        {/* Welcome section */}
        <div className="flex items-center gap-4">
          <div className="flex h-14 w-14 items-center justify-center rounded-xl bg-gradient-to-br from-orange-500 to-amber-600 shadow-lg shadow-orange-500/25">
            <MeepleDecoration className="h-8 w-8 text-white" />
          </div>
          <div>
            <p className="text-sm text-stone-400">Bentornato,</p>
            <h1 className="font-quicksand text-2xl font-bold" data-testid="admin-name">
              {adminName}
            </h1>
            {mounted && currentTime && (
              <p className="text-sm text-stone-400">
                {formatDate(currentTime)} ·{' '}
                <span className="font-mono text-orange-400" data-testid="current-time">
                  {formatTime(currentTime)}
                </span>
              </p>
            )}
            {!mounted && (
              <p className="text-sm text-stone-400">
                <span className="animate-pulse">Caricamento...</span>
              </p>
            )}
          </div>
        </div>

        {/* Search and notifications */}
        <div className="flex items-center gap-3">
          {/* Search form */}
          <form onSubmit={handleSearchSubmit} className="relative">
            <Search
              className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-stone-500"
              aria-hidden="true"
            />
            <input
              type="search"
              placeholder="Cerca utenti, giochi, log..."
              value={searchQuery}
              onChange={handleSearchChange}
              className="h-10 w-64 rounded-lg border border-stone-700 bg-stone-800/50 pl-10 pr-4 text-sm text-white placeholder-stone-500 backdrop-blur-sm transition-all focus:border-orange-500 focus:outline-none focus:ring-2 focus:ring-orange-500/20"
              aria-label="Cerca nel pannello admin"
              data-testid="search-input"
            />
          </form>

          {/* Notifications button */}
          <Link
            href="/admin/notifications"
            className="relative flex h-10 w-10 items-center justify-center rounded-lg border border-stone-700 bg-stone-800/50 text-stone-400 transition-all hover:border-orange-500 hover:text-orange-400 focus:border-orange-500 focus:outline-none focus:ring-2 focus:ring-orange-500/20"
            aria-label={`Notifiche${unreadCount > 0 ? ` (${unreadCount} non lette)` : ''}`}
            data-testid="notifications-button"
          >
            <Bell className="h-5 w-5" aria-hidden="true" />
            {unreadCount > 0 && (
              <span
                className="absolute -right-1 -top-1 flex h-5 w-5 items-center justify-center rounded-full bg-orange-500 text-[10px] font-bold text-white"
                aria-hidden="true"
                data-testid="notification-badge"
              >
                {unreadCount > 99 ? '99+' : unreadCount}
              </span>
            )}
          </Link>
        </div>
      </div>
    </header>
  );
}

export default DashboardHeader;
