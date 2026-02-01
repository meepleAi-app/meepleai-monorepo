/**
 * Dashboard Loading Skeleton - Issue #3307
 *
 * Suspense boundary fallback showing:
 * - Header skeleton with search bar
 * - Section skeletons with cards
 * - Responsive layout (1-col mobile → 3-col desktop)
 */

export default function DashboardLoading() {
  return (
    <div className="min-h-screen bg-gradient-to-b from-amber-50/50 to-orange-50/30 dark:from-slate-950 dark:to-slate-900">
      {/* Header Skeleton */}
      <div className="sticky top-0 z-10 border-b border-amber-200/50 bg-white/80 px-4 py-4 backdrop-blur-sm dark:border-slate-800 dark:bg-slate-950/80">
        <div className="mx-auto max-w-7xl">
          {/* Greeting skeleton */}
          <div className="mb-4 h-8 w-48 animate-pulse rounded-lg bg-amber-200/50 dark:bg-slate-700" />
          {/* Search bar skeleton */}
          <div className="h-12 w-full animate-pulse rounded-xl bg-amber-100/50 dark:bg-slate-800" />
        </div>
      </div>

      {/* Content Skeleton */}
      <div className="mx-auto max-w-7xl px-4 py-6">
        {/* Section skeletons - 3 sections */}
        <div className="space-y-8">
          {[1, 2, 3].map((section) => (
            <div key={section} className="space-y-4">
              {/* Section header skeleton */}
              <div className="flex items-center justify-between">
                <div className="h-6 w-32 animate-pulse rounded-lg bg-amber-200/50 dark:bg-slate-700" />
                <div className="h-8 w-20 animate-pulse rounded-lg bg-amber-100/50 dark:bg-slate-800" />
              </div>

              {/* Cards grid - responsive */}
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
                {[1, 2, 3].map((card) => (
                  <div
                    key={card}
                    className="h-48 animate-pulse rounded-xl bg-white/50 shadow-sm dark:bg-slate-800/50"
                  >
                    {/* Card image placeholder */}
                    <div className="h-24 rounded-t-xl bg-amber-100/50 dark:bg-slate-700" />
                    {/* Card content placeholder */}
                    <div className="space-y-2 p-4">
                      <div className="h-4 w-3/4 rounded bg-amber-200/30 dark:bg-slate-600" />
                      <div className="h-3 w-1/2 rounded bg-amber-100/30 dark:bg-slate-700" />
                    </div>
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Loading indicator */}
      <div className="fixed bottom-24 left-1/2 -translate-x-1/2">
        <div className="flex items-center gap-2 rounded-full bg-amber-600 px-4 py-2 text-sm text-white shadow-lg dark:bg-amber-500">
          <div className="h-4 w-4 animate-spin rounded-full border-2 border-white border-t-transparent" />
          <span>Loading dashboard...</span>
        </div>
      </div>
    </div>
  );
}
