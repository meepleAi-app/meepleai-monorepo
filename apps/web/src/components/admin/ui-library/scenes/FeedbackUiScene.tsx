'use client';

import { AlertCircle, CheckCircle, Info } from 'lucide-react';

import { Alert, AlertTitle, AlertDescription } from '@/components/ui/feedback/alert';
import { Progress } from '@/components/ui/feedback/progress';
import { Skeleton } from '@/components/ui/feedback/skeleton';

export default function FeedbackUiScene() {
  return (
    <div className="space-y-8">
      {/* Alerts */}
      <div className="space-y-3">
        <h3 className="font-quicksand text-base font-semibold text-foreground">Alert</h3>
        <div className="space-y-3">
          <Alert>
            <Info className="h-4 w-4" />
            <AlertTitle>Info</AlertTitle>
            <AlertDescription>Your changes have been saved automatically.</AlertDescription>
          </Alert>

          <Alert variant="destructive">
            <AlertCircle className="h-4 w-4" />
            <AlertTitle>Error</AlertTitle>
            <AlertDescription>
              Failed to connect to the embedding service. Check your configuration.
            </AlertDescription>
          </Alert>

          <Alert className="border-green-500/50 bg-green-50/50 text-green-900 dark:bg-green-950/20 dark:text-green-300">
            <CheckCircle className="h-4 w-4 text-green-600" />
            <AlertTitle>Success</AlertTitle>
            <AlertDescription>
              PDF processing completed. 248 chunks indexed successfully.
            </AlertDescription>
          </Alert>
        </div>
      </div>

      {/* Progress */}
      <div className="space-y-3">
        <h3 className="font-quicksand text-base font-semibold text-foreground">Progress</h3>
        <div className="space-y-4">
          <div className="space-y-1.5">
            <div className="flex justify-between font-nunito text-xs text-muted-foreground">
              <span>PDF indexing</span>
              <span>78%</span>
            </div>
            <Progress value={78} />
          </div>

          <div className="space-y-1.5">
            <div className="flex justify-between font-nunito text-xs text-muted-foreground">
              <span>Embedding generation</span>
              <span>42%</span>
            </div>
            <Progress value={42} />
          </div>

          <div className="space-y-1.5">
            <div className="flex justify-between font-nunito text-xs text-muted-foreground">
              <span>Complete</span>
              <span>100%</span>
            </div>
            <Progress value={100} />
          </div>
        </div>
      </div>

      {/* Skeleton */}
      <div className="space-y-3">
        <h3 className="font-quicksand text-base font-semibold text-foreground">Skeleton</h3>
        <div className="space-y-3 rounded-xl border border-border/60 p-4">
          <div className="flex items-center gap-3">
            <Skeleton className="h-12 w-12 rounded-full" />
            <div className="flex-1 space-y-2">
              <Skeleton className="h-4 w-32" />
              <Skeleton className="h-3 w-48" />
            </div>
          </div>
          <Skeleton className="h-24 w-full rounded-lg" />
          <div className="flex gap-2">
            <Skeleton className="h-7 w-20 rounded-full" />
            <Skeleton className="h-7 w-20 rounded-full" />
            <Skeleton className="h-7 w-16 rounded-full" />
          </div>
        </div>
      </div>
    </div>
  );
}
