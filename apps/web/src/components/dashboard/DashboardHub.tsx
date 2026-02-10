/**
 * DashboardHub - Main Navigation Hub (Issue #3975, #3979)
 *
 * Multi-section dashboard with aggregated data from `/api/v1/dashboard`
 *
 * Features:
 * - Hero Stats Overview
 * - Active Sessions Widget
 * - Library Snapshot
 * - Activity Feed Timeline
 * - Wishlist Highlights (Issue #3920)
 * - Catalog Trending (Issue #3921)
 * - Chat History
 * - Quick Actions Grid
 * - Responsive Layout: Mobile (1-col), Tablet (2-col), Desktop (3-col asymmetric)
 * - Framer Motion stagger animations with reduced motion support
 * - Lazy loading below-fold sections (Intersection Observer)
 * - Touch-friendly targets (44px min) on mobile
 * - Skeleton Loading States
 * - Error handling for partial failures
 *
 * @see Epic #3901 - Dashboard Hub Core (MVP)
 * @see Issue #3921 - Catalog Trending Widget
 * @see docs/07-frontend/dashboard-overview-hub.md
 */

'use client';

import { Component, type ReactNode } from 'react';

import { motion } from 'framer-motion';
import { useInView } from 'react-intersection-observer';

import { useDashboardAnalytics } from '@/hooks/useDashboardAnalytics';
import { useDashboardData } from '@/hooks/useDashboardData';
import {
  adaptStatsForHeroStats,
  adaptActiveSessions,
  adaptChatThreads,
} from '@/lib/adapters/dashboardAdapter';
import { useReducedMotion } from '@/lib/animations';

import { ActiveSessionsWidget } from './ActiveSessionsWidget';
import { ActivityFeed } from './ActivityFeed';
import { CatalogTrending } from './CatalogTrending';
import { ChatHistorySection } from './ChatHistorySection';
import { HeroStats } from './HeroStats';
import { LibrarySnapshot } from './LibrarySnapshot';
import { QuickActionsGrid } from './QuickActionsGrid';
import { WishlistHighlights } from './WishlistHighlights';

// ============================================================================
// Animation Variants
// ============================================================================

const containerVariants = {
  hidden: {},
  visible: {
    transition: {
      staggerChildren: 0.08,
    },
  },
};

const sectionVariants = {
  hidden: { opacity: 0, y: 12 },
  visible: {
    opacity: 1,
    y: 0,
    transition: { duration: 0.3, ease: 'easeOut' },
  },
};

// No-animation fallback for reduced motion
const noMotionVariants = {
  hidden: { opacity: 1 },
  visible: { opacity: 1 },
};

// ============================================================================
// Lazy Section Wrapper
// ============================================================================

interface LazySectionProps {
  children: ReactNode;
  className?: string;
  fallbackHeight?: string;
}

function LazySection({ children, className, fallbackHeight = 'h-64' }: LazySectionProps) {
  const { ref, inView } = useInView({
    triggerOnce: true,
    threshold: 0.1,
    rootMargin: '100px 0px',
  });

  return (
    <div ref={ref} className={className}>
      {inView ? (
        children
      ) : (
        <div className={`${fallbackHeight} animate-pulse rounded-2xl bg-gray-200`} />
      )}
    </div>
  );
}

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

      {/* Wishlist Highlights Skeleton */}
      <div className="h-80 animate-pulse rounded-2xl bg-gray-200" />

      {/* Catalog Trending Skeleton */}
      <div className="h-80 animate-pulse rounded-2xl bg-gray-200 md:col-span-1 lg:col-span-2" />

      {/* Chat History Skeleton */}
      <div className="h-64 animate-pulse rounded-2xl bg-gray-200 md:col-span-1 lg:col-span-2" />

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
        className="min-h-[44px] min-w-[44px] rounded-lg bg-red-600 px-4 py-2 text-white hover:bg-red-700"
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
  fallback: ReactNode | ((props: { resetErrorBoundary: () => void; error: Error }) => ReactNode);
  children: ReactNode;
  onReset?: () => void;
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

  resetErrorBoundary = () => {
    this.setState({ hasError: false, error: null });
    this.props.onReset?.();
  };

  render() {
    if (this.state.hasError && this.state.error) {
      return typeof this.props.fallback === 'function'
        ? this.props.fallback({ resetErrorBoundary: this.resetErrorBoundary, error: this.state.error })
        : this.props.fallback;
    }

    return this.props.children;
  }
}

// ============================================================================
// Section Error Fallback (with retry)
// ============================================================================

function SectionErrorFallback({ label, onRetry }: { label: string; onRetry: () => void }) {
  return (
    <div className="flex items-center justify-between rounded-2xl border-2 border-dashed border-yellow-200 bg-yellow-50 p-6">
      <p className="text-sm text-yellow-800">
        ⚠️ {label} failed to load. Other sections are still working.
      </p>
      <button
        onClick={onRetry}
        className="ml-4 min-h-[44px] min-w-[44px] shrink-0 rounded-lg bg-yellow-600 px-3 py-1.5 text-xs text-white hover:bg-yellow-700"
      >
        Retry
      </button>
    </div>
  );
}

// ============================================================================
// Main DashboardHub Component
// ============================================================================

export function DashboardHub() {
  const { data, isLoading, error } = useDashboardData();
  const shouldReduceMotion = useReducedMotion();
  const { trackClickThrough } = useDashboardAnalytics();

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

  const variants = shouldReduceMotion ? noMotionVariants : sectionVariants;
  const container = shouldReduceMotion ? undefined : containerVariants;

  // Success State - Render Dashboard
  return (
    <motion.div
      className="grid grid-cols-1 gap-4 p-4 md:grid-cols-2 md:gap-6 md:p-6 lg:grid-cols-3 lg:gap-8 lg:p-8"
      variants={container}
      initial="hidden"
      animate="visible"
    >
      {/* Hero Stats Overview - Full Width (above fold) */}
      <motion.section className="col-span-full" variants={variants}>
        <SimpleErrorBoundary
          fallback={({ resetErrorBoundary }) => (
            <SectionErrorFallback label="Hero Stats" onRetry={resetErrorBoundary} />
          )}
        >
          <HeroStats stats={heroStats} />
        </SimpleErrorBoundary>
      </motion.section>

      {/* Active Sessions Widget - Full Width (above fold) */}
      <motion.section className="col-span-full" variants={variants}>
        <SimpleErrorBoundary
          fallback={({ resetErrorBoundary }) => (
            <SectionErrorFallback label="Active Sessions" onRetry={resetErrorBoundary} />
          )}
        >
          <ActiveSessionsWidget sessions={activeSessions} />
        </SimpleErrorBoundary>
      </motion.section>

      {/* Library Snapshot - Sidebar (1 col, lazy loaded) */}
      <motion.section className="md:col-span-1 lg:col-span-1" variants={variants}>
        <LazySection fallbackHeight="h-96">
          <SimpleErrorBoundary
            fallback={({ resetErrorBoundary }) => (
              <SectionErrorFallback label="Library Snapshot" onRetry={resetErrorBoundary} />
            )}
          >
            <LibrarySnapshot quota={data.librarySnapshot.quota} topGames={data.librarySnapshot.topGames} onNavigate={trackClickThrough} />
          </SimpleErrorBoundary>
        </LazySection>
      </motion.section>

      {/* Activity Feed - Main Content (2 cols on desktop, lazy loaded) */}
      <motion.section className="md:col-span-1 lg:col-span-2" variants={variants}>
        <LazySection fallbackHeight="h-96">
          <SimpleErrorBoundary
            fallback={({ resetErrorBoundary }) => (
              <SectionErrorFallback label="Activity Feed" onRetry={resetErrorBoundary} />
            )}
          >
            <ActivityFeed events={activityEvents} />
          </SimpleErrorBoundary>
        </LazySection>
      </motion.section>

      {/* Wishlist Highlights - Sidebar (1 col, lazy loaded) - Issue #3920 */}
      <motion.section className="md:col-span-1 lg:col-span-1" variants={variants}>
        <LazySection fallbackHeight="h-80">
          <SimpleErrorBoundary
            fallback={({ resetErrorBoundary }) => (
              <SectionErrorFallback label="Wishlist Highlights" onRetry={resetErrorBoundary} />
            )}
          >
            <WishlistHighlights />
          </SimpleErrorBoundary>
        </LazySection>
      </motion.section>

      {/* Catalog Trending - 2 cols (lazy loaded) - Issue #3921 */}
      <motion.section className="md:col-span-1 lg:col-span-2" variants={variants}>
        <LazySection fallbackHeight="h-80">
          <SimpleErrorBoundary
            fallback={({ resetErrorBoundary }) => (
              <SectionErrorFallback label="Catalog Trending" onRetry={resetErrorBoundary} />
            )}
          >
            <CatalogTrending />
          </SimpleErrorBoundary>
        </LazySection>
      </motion.section>

      {/* Chat History - 2 cols (lazy loaded) */}
      <motion.section className="md:col-span-1 lg:col-span-2" variants={variants}>
        <LazySection fallbackHeight="h-64">
          <SimpleErrorBoundary
            fallback={({ resetErrorBoundary }) => (
              <SectionErrorFallback label="Chat History" onRetry={resetErrorBoundary} />
            )}
          >
            <ChatHistorySection threads={chatThreads} />
          </SimpleErrorBoundary>
        </LazySection>
      </motion.section>

      {/* Quick Actions Grid - 1 col (lazy loaded) */}
      <motion.section className="md:col-span-2 lg:col-span-1" variants={variants}>
        <LazySection fallbackHeight="h-48">
          <SimpleErrorBoundary
            fallback={({ resetErrorBoundary }) => (
              <SectionErrorFallback label="Quick Actions" onRetry={resetErrorBoundary} />
            )}
          >
            <QuickActionsGrid />
          </SimpleErrorBoundary>
        </LazySection>
      </motion.section>
    </motion.div>
  );
}
