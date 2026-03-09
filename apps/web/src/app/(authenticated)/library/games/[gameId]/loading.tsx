/**
 * Loading State for Library Game Detail Page
 *
 * Skeleton matching the new LayoutShell-integrated layout:
 * compact hero card + tab content area.
 */

import { Skeleton } from '@/components/ui/feedback/skeleton';

export default function LibraryGameDetailLoading() {
  return (
    <div>
      {/* Hero card skeleton */}
      <div className="mx-auto max-w-6xl px-4 py-4">
        <div className="flex items-center gap-4 rounded-2xl border border-border/40 bg-card p-4 shadow-sm">
          <Skeleton className="h-16 w-16 flex-shrink-0 rounded-xl" />
          <div className="min-w-0 flex-1 space-y-2">
            <Skeleton className="h-5 w-2/3" />
            <Skeleton className="h-4 w-1/3" />
          </div>
          <div className="hidden items-center gap-3 sm:flex">
            <Skeleton className="h-4 w-14" />
            <Skeleton className="h-4 w-14" />
            <Skeleton className="h-4 w-14" />
          </div>
          <Skeleton className="h-9 w-20 rounded-lg" />
        </div>
      </div>

      {/* Tab content skeleton */}
      <div className="mx-auto max-w-6xl space-y-6 px-4 py-4">
        {/* Description card */}
        <div className="rounded-3xl border border-border/40 bg-card p-6 shadow-sm">
          <Skeleton className="mb-4 h-5 w-1/4" />
          <div className="space-y-2">
            <Skeleton className="h-4 w-full" />
            <Skeleton className="h-4 w-5/6" />
            <Skeleton className="h-4 w-4/6" />
          </div>
        </div>

        {/* Tags card */}
        <div className="rounded-3xl border border-border/40 bg-card p-6 shadow-sm">
          <div className="space-y-4">
            <div className="space-y-2">
              <Skeleton className="h-4 w-20" />
              <div className="flex flex-wrap gap-1.5">
                <Skeleton className="h-6 w-20 rounded-full" />
                <Skeleton className="h-6 w-24 rounded-full" />
                <Skeleton className="h-6 w-16 rounded-full" />
              </div>
            </div>
            <div className="space-y-2">
              <Skeleton className="h-4 w-20" />
              <div className="flex flex-wrap gap-1.5">
                <Skeleton className="h-6 w-24 rounded-full" />
                <Skeleton className="h-6 w-20 rounded-full" />
                <Skeleton className="h-6 w-28 rounded-full" />
              </div>
            </div>
          </div>
        </div>

        {/* User actions card */}
        <div className="rounded-3xl border border-border/40 bg-card p-6 shadow-sm">
          <div className="mb-6 flex flex-wrap items-center gap-3">
            <Skeleton className="h-10 w-32 rounded-xl" />
            <Skeleton className="h-10 w-32 rounded-xl" />
            <Skeleton className="h-10 w-10 rounded-xl" />
            <div className="flex-grow" />
            <Skeleton className="h-10 w-24 rounded-xl" />
          </div>
          <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
            {[1, 2, 3, 4].map(i => (
              <div key={i} className="rounded-xl bg-muted/40 p-4 text-center">
                <Skeleton className="mx-auto mb-2 h-4 w-20" />
                <Skeleton className="mx-auto h-5 w-14" />
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
