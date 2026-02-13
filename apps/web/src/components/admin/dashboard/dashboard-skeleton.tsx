interface DashboardSkeletonProps {
  variant: 'stats' | 'table';
}

export function DashboardSkeleton({ variant }: DashboardSkeletonProps) {
  if (variant === 'stats') {
    return (
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
        {[...Array(4)].map((_, i) => (
          <div
            key={i}
            className="h-[180px] bg-white/40 backdrop-blur-sm rounded-2xl border border-slate-200/60 animate-pulse"
            style={{ animationDelay: `${i * 100}ms` }}
          />
        ))}
      </div>
    );
  }

  if (variant === 'table') {
    return (
      <div className="space-y-4">
        {/* Header skeleton */}
        <div className="h-12 bg-white/40 backdrop-blur-sm rounded-xl border border-slate-200/60 animate-pulse" />
        {/* Filters skeleton */}
        <div className="h-16 bg-white/40 backdrop-blur-sm rounded-xl border border-slate-200/60 animate-pulse" />
        {/* Table skeleton */}
        <div className="bg-white/40 backdrop-blur-sm rounded-xl border border-slate-200/60 p-6 space-y-4">
          {[...Array(5)].map((_, i) => (
            <div key={i} className="h-14 bg-slate-200/40 rounded-lg animate-pulse" />
          ))}
        </div>
      </div>
    );
  }

  return null;
}
