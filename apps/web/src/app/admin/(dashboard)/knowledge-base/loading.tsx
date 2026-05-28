import { Skeleton } from '@/components/ui/feedback/skeleton';

export default function KnowledgeBaseLoading() {
  return (
    <div className="space-y-4">
      <Skeleton className="h-8 w-44" />
      <div className="grid grid-cols-1 lg:grid-cols-[300px_1fr] gap-4">
        <Skeleton className="h-[400px] rounded-lg" />
        <Skeleton className="h-[400px] rounded-lg" />
      </div>
    </div>
  );
}
