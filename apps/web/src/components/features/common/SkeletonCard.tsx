export function SkeletonCard() {
  return (
    <div className="rounded-xl border border-border/50 bg-card overflow-hidden animate-pulse">
      <div className="aspect-[7/10] bg-muted" />
      <div className="p-4 space-y-2">
        <div className="h-4 bg-muted rounded w-3/5" />
        <div className="h-3 bg-muted rounded w-2/5" />
      </div>
    </div>
  );
}

export function SkeletonCardGrid({ count = 6 }: { count?: number }) {
  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6">
      {Array.from({ length: count }, (_, i) => (
        <SkeletonCard key={`skeleton-${i}`} />
      ))}
    </div>
  );
}
