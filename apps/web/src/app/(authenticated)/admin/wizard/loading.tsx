import { Skeleton } from '@/components/ui/feedback/skeleton';

export default function WizardLoading() {
  return (
    <div className="max-w-4xl mx-auto p-6 space-y-6">
      <div>
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-4 w-96 mt-2" />
      </div>
      <div className="flex gap-2">
        {Array.from({ length: 5 }).map((_, i) => (
          <Skeleton key={i} className="h-10 w-24 rounded-full" />
        ))}
      </div>
      <div className="rounded-xl border border-[rgba(45,42,38,0.08)] p-6 space-y-4">
        <Skeleton className="h-6 w-48" />
        <Skeleton className="h-40 w-full rounded-lg" />
        <Skeleton className="h-10 w-32" />
      </div>
    </div>
  );
}
