/**
 * DashboardHub - Main Navigation Hub (Issue #3975)
 *
 * Multi-section dashboard with aggregated data from `/api/v1/dashboard`
 *
 * Features:
 * - Hero Stats Overview
 * - Active Sessions Widget
 * - Library Snapshot
 * - Activity Feed Timeline
 * - Chat History
 * - Quick Actions Grid
 * - Responsive Layout: Mobile (1-col), Tablet (2-col), Desktop (3-col asymmetric)
 * - Skeleton Loading States
 * - Error handling for partial failures
 *
 * @see Epic #3901 - Dashboard Hub Core (MVP)
 * @see docs/07-frontend/dashboard-overview-hub.md
 */

'use client';

import { Component, type ReactNode } from 'react';

import { useDashboardData } from '@/hooks/useDashboardData';
import {
  adaptStatsForHeroStats,
  adaptActiveSessions,
  adaptChatThreads,
} from '@/lib/adapters/dashboardAdapter';

import { ActiveSessionsWidget } from './ActiveSessionsWidget';
import { ActivityFeed } from './ActivityFeed';
import { ChatHistorySection } from './ChatHistorySection';
import { HeroStats } from './HeroStats';
import { LibrarySnapshot } from './LibrarySnapshot';
import { QuickActionsGrid } from './QuickActionsGrid';

// ============================================================================
// Loading Skeleton
// ============================================================================

function DashboardSkeleton() {
  return (
    <div className="grid grid-cols-1 gap-6 p-4 md:grid-cols-2 md:gap-6 md:p-6 lg:grid-cols-3 lg:gap-8 lg:p-8">
      {/* Hero Stats Skeleton */}
      <div className="col-span-full h-32 animate-pulse rounded-2xl bg-gray-200" />

      {/* Active Sessions Skeleton */}
      <div className="col-span-full h-64 animate-pulse rounded-2xl bg-gray-200" />

      {/* Library Snapshot Skeleton */}
      <div className="h-96 animate-pulse rounded-2xl bg-gray-200" />

      {/* Activity Feed Skeleton */}
      <div className="h-96 animate-pulse rounded-2xl bg-gray-200 md:col-span-1 lg:col-span-2" />

      {/* Chat History Skeleton */}
      <div className="h-64 animate-pulse rounded-2xl bg-gray-200 md:col-span-2" />

      {/* Quick Actions Skeleton */}
      <div className="h-48 animate-pulse rounded-2xl bg-gray-200 md:col-span-2 lg:col-span-1" />
    </div>
  );
}

// ============================================================================
// Error Fallback
// ============================================================================

interface ErrorFallbackProps {
  error: Error;
  resetErrorBoundary: () => void;
}

function DashboardErrorFallback({ error, resetErrorBoundary }: ErrorFallbackProps) {
  return (
    <div className="flex min-h-[400px] flex-col items-center justify-center gap-4 rounded-2xl border-2 border-dashed border-red-200 bg-red-50 p-8">
      <div className="text-6xl">⚠️</div>
      <h2 className="text-2xl font-semibold text-red-900">Dashboard Error</h2>
      <p className="max-w-md text-center text-red-700">{error.message}</p>
      <button
        onClick={resetErrorBoundary}
        className="rounded-lg bg-red-600 px-4 py-2 text-white hover:bg-red-700"
      >
        Try Again
      </button>
    </div>
  );
}

// ============================================================================
// Simple Error Boundary (React 18+)
// ============================================================================

interface ErrorBoundaryProps {
  fallback: ReactNode;
  children: ReactNode;
}

interface ErrorBoundaryState {
  hasError: boolean;
  error: Error | null;
}

class SimpleErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  constructor(props: ErrorBoundaryProps) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error: Error): ErrorBoundaryState {
    return { hasError: true, error };
  }

  render() {
    if (this.state.hasError) {
      return this.props.fallback;
    }

    return this.props.children;
  }
}

// ============================================================================
// Main DashboardHub Component
// ============================================================================

export function DashboardHub() {
  const { data, isLoading, error } = useDashboardData();

  // Loading State
  if (isLoading) {
    return <DashboardSkeleton />;
  }

  // Error State (Full Dashboard Failure)
  if (error) {
    return (
      <div className="p-4 md:p-6 lg:p-8">
        <DashboardErrorFallback error={error} resetErrorBoundary={() => window.location.reload()} />
      </div>
    );
  }

  // No data (should not happen, but defensive)
  if (!data) {
    return (
      <div className="p-4 md:p-6 lg:p-8">
        <DashboardErrorFallback
          error={new Error('No dashboard data available')}
          resetErrorBoundary={() => window.location.reload()}
        />
      </div>
    );
  }

  // Adapt data to component formats
  const heroStats = adaptStatsForHeroStats(data);
  const activeSessions = adaptActiveSessions(data.activeSessions);
  const chatThreads = adaptChatThreads(data.chatHistory);

  // Convert activity events to ActivityFeed format
  const activityEvents = data.recentActivity.map((event) => ({
    id: event.id,
    type: event.type,
    title:
      event.type === 'game_added'
        ? `Added "${event.gameName}"`
        : event.type === 'session_completed'
          ? `Played "${event.gameName}"`
          : event.type === 'chat_saved'
            ? `Chat: "${event.topic}"`
            : `Wishlisted "${event.gameName}"`,
    entityId: event.gameId || event.sessionId || event.chatId,
    entityType:
      event.type === 'game_added'
        ? ('game' as const)
        : event.type === 'session_completed'
          ? ('session' as const)
          : event.type === 'chat_saved'
            ? ('chat' as const)
            : ('wishlist' as const),
    timestamp: event.timestamp.toISOString(),
  }));

  // Success State - Render Dashboard
  return (
    <div className="grid grid-cols-1 gap-4 p-4 md:grid-cols-2 md:gap-6 md:p-6 lg:grid-cols-3 lg:gap-8 lg:p-8">
      {/* Hero Stats Overview - Full Width */}
      <section className="col-span-full">
        <SimpleErrorBoundary
          fallback={
            <div className="rounded-2xl border-2 border-dashed border-yellow-200 bg-yellow-50 p-6">
              <p className="text-sm text-yellow-800">
                ⚠️ Hero Stats failed to load. Other sections are still working.
              </p>
            </div>
          }
        >
          <HeroStats stats={heroStats} />
        </SimpleErrorBoundary>
      </section>

      {/* Active Sessions Widget - Full Width */}
      <section className="col-span-full">
        <SimpleErrorBoundary
          fallback={
            <div className="rounded-2xl border-2 border-dashed border-yellow-200 bg-yellow-50 p-6">
              <p className="text-sm text-yellow-800">
                ⚠️ Active Sessions failed to load. Other sections are still working.
              </p>
            </div>
          }
        >
          <ActiveSessionsWidget sessions={activeSessions} />
        </SimpleErrorBoundary>
      </section>

      {/* Library Snapshot - Sidebar (1 col) */}
      <section className="md:col-span-1 lg:col-span-1">
        <SimpleErrorBoundary
          fallback={
            <div className="rounded-2xl border-2 border-dashed border-yellow-200 bg-yellow-50 p-6">
              <p className="text-sm text-yellow-800">
                ⚠️ Library Snapshot failed to load. Other sections are still working.
              </p>
            </div>
          }
        >
          <LibrarySnapshot quota={data.librarySnapshot.quota} topGames={data.librarySnapshot.topGames} />
        </SimpleErrorBoundary>
      </section>

      {/* Activity Feed - Main Content (2 cols on desktop) */}
      <section className="md:col-span-1 lg:col-span-2">
        <SimpleErrorBoundary
          fallback={
            <div className="rounded-2xl border-2 border-dashed border-yellow-200 bg-yellow-50 p-6">
              <p className="text-sm text-yellow-800">
                ⚠️ Activity Feed failed to load. Other sections are still working.
              </p>
            </div>
          }
        >
          <ActivityFeed events={activityEvents} />
        </SimpleErrorBoundary>
      </section>

      {/* Chat History - 2 cols */}
      <section className="md:col-span-2">
        <SimpleErrorBoundary
          fallback={
            <div className="rounded-2xl border-2 border-dashed border-yellow-200 bg-yellow-50 p-6">
              <p className="text-sm text-yellow-800">
                ⚠️ Chat History failed to load. Other sections are still working.
              </p>
            </div>
          }
        >
          <ChatHistorySection threads={chatThreads} />
        </SimpleErrorBoundary>
      </section>

      {/* Quick Actions Grid - 1 col */}
      <section className="md:col-span-2 lg:col-span-1">
        <SimpleErrorBoundary
          fallback={
            <div className="rounded-2xl border-2 border-dashed border-yellow-200 bg-yellow-50 p-6">
              <p className="text-sm text-yellow-800">
                ⚠️ Quick Actions failed to load. Other sections are still working.
              </p>
            </div>
          }
        >
          <QuickActionsGrid />
        </SimpleErrorBoundary>
      </section>
    </div>
  );
}
