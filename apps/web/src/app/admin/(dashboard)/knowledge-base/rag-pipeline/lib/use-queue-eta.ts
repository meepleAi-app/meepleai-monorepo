'use client';

import { useQuery } from '@tanstack/react-query';

import { fetchBatchETA, type BatchETAResponse } from '../../queue/lib/queue-api';

export function useQueueETA(enabled: boolean = true) {
  return useQuery<BatchETAResponse>({
    queryKey: ['admin', 'queue', 'eta'],
    queryFn: fetchBatchETA,
    enabled,
    staleTime: 25_000,
    refetchInterval: 30_000,
  });
}

export function formatETA(totalSeconds: number): string {
  if (totalSeconds <= 0) return '\u2014';
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = Math.floor(totalSeconds % 60);
  if (minutes === 0) return `~${seconds}s`;
  return `~${minutes}m ${seconds > 0 ? `${seconds}s` : ''}`.trim();
}
