import { Skeleton } from '@/components/ui/feedback/skeleton';

export default function InvitationsLoading() {
  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <Skeleton className="h-8 w-44" />
          <Skeleton className="h-4 w-56 mt-2" />
        </div>
        <Skeleton className="h-10 w-36 rounded-lg" />
      </div>
      <div className="rounded-xl border border-[rgba(45,42,38,0.08)] overflow-hidden">
        <div className="bg-[rgba(45,42,38,0.02)] px-4 py-3 flex gap-4">
          <Skeleton className="h-4 w-48" />
          <Skeleton className="h-4 w-20" />
          <Skeleton className="h-4 w-28" />
          <Skeleton className="h-4 w-20" />
        </div>
        {Array.from({ length: 6 }).map((_, i) => (
          <div
            key={i}
            className="px-4 py-3 flex items-center gap-4 border-t border-[rgba(45,42,38,0.06)]"
          >
            <Skeleton className="h-4 w-48" />
            <Skeleton className="h-6 w-20 rounded-full" />
            <Skeleton className="h-4 w-28" />
            <Skeleton className="h-8 w-20 rounded-lg" />
          </div>
        ))}
      </div>
    </div>
  );
}
