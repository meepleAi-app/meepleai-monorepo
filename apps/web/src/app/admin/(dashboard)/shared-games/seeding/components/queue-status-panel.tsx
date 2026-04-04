'use client';

import { useCallback, useEffect, useState } from 'react';

import { Loader2Icon, ClockIcon } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { api } from '@/lib/api';
import type { BggQueueStatus } from '@/lib/api/schemas/seeding.schemas';

const QUEUE_POLL_MS = 3000;

export function QueueStatusPanel() {
  const [status, setStatus] = useState<BggQueueStatus | null>(null);

  const fetchStatus = useCallback(async () => {
    try {
      const data = await api.sharedGames.getBggQueueStatus();
      setStatus(data);
    } catch {
      // Silently fail — panel is supplementary
    }
  }, []);

  useEffect(() => {
    void fetchStatus();
    const interval = setInterval(() => void fetchStatus(), QUEUE_POLL_MS);
    return () => clearInterval(interval);
  }, [fetchStatus]);

  if (!status || (status.totalQueued === 0 && status.totalProcessing === 0)) {
    return null;
  }

  const eta = status.totalQueued + status.totalProcessing;

  return (
    <Card className="border-blue-200 bg-blue-50/50 dark:border-blue-800/40 dark:bg-blue-900/10">
      <CardHeader className="pb-2">
        <CardTitle className="font-quicksand text-sm font-semibold flex items-center gap-2">
          <Loader2Icon className="h-4 w-4 animate-spin text-blue-500" />
          Enrichment Queue Active
        </CardTitle>
      </CardHeader>
      <CardContent className="flex items-center gap-4 text-sm">
        <div className="flex items-center gap-1.5">
          <Badge variant="outline" className="bg-slate-100">
            {status.totalQueued}
          </Badge>
          <span className="text-muted-foreground">queued</span>
        </div>
        <div className="flex items-center gap-1.5">
          <Badge variant="outline" className="bg-blue-100">
            {status.totalProcessing}
          </Badge>
          <span className="text-muted-foreground">processing</span>
        </div>
        <div className="flex items-center gap-1.5 text-muted-foreground">
          <ClockIcon className="h-3.5 w-3.5" />
          <span>~{eta}s remaining</span>
        </div>
      </CardContent>
    </Card>
  );
}
