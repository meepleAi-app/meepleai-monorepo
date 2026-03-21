import { Skeleton } from '@/components/ui/feedback/skeleton';

export default function ChatLoading() {
  return (
    <div className="flex h-[calc(100vh-64px)]">
      <div className="hidden md:flex w-72 flex-col border-r border-[rgba(45,42,38,0.08)] p-4 space-y-3">
        <Skeleton className="h-10 w-full rounded-lg" />
        {Array.from({ length: 8 }).map((_, i) => (
          <div key={i} className="flex items-center gap-3">
            <Skeleton className="h-8 w-8 rounded-full" />
            <div className="flex-1 space-y-1">
              <Skeleton className="h-4 w-28" />
              <Skeleton className="h-3 w-20" />
            </div>
          </div>
        ))}
      </div>
      <div className="flex-1 flex flex-col p-4">
        <div className="flex-1 space-y-4 py-4">
          {Array.from({ length: 3 }).map((_, i) => (
            <div key={i} className={`flex ${i % 2 === 0 ? 'justify-start' : 'justify-end'}`}>
              <div
                className={`flex items-start gap-3 max-w-[70%] ${i % 2 === 0 ? '' : 'flex-row-reverse'}`}
              >
                <Skeleton className="h-8 w-8 rounded-full flex-shrink-0" />
                <div className="space-y-1">
                  <Skeleton className={`h-16 ${i % 2 === 0 ? 'w-64' : 'w-48'} rounded-2xl`} />
                  <Skeleton className="h-3 w-16" />
                </div>
              </div>
            </div>
          ))}
        </div>
        <div className="border-t border-[rgba(45,42,38,0.08)] pt-4">
          <Skeleton className="h-12 w-full rounded-xl" />
        </div>
      </div>
    </div>
  );
}
