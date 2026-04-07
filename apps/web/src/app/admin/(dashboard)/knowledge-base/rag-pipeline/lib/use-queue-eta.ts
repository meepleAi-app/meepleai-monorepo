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

export function formatETA(totalMinutes: number): string {
  if (totalMinutes <= 0) return '\u2014';
  const wholeMinutes = Math.floor(totalMinutes);
  const seconds = Math.round((totalMinutes - wholeMinutes) * 60);
  if (wholeMinutes === 0) return `~${seconds}s`;
  return `~${wholeMinutes}m ${seconds > 0 ? `${seconds}s` : ''}`.trim();
}
