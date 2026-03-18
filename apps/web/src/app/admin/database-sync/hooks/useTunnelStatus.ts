'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';

import { fetchJson, DB_SYNC_API } from '../lib/api';

import type { TunnelStatusResult } from '../types/db-sync';

export function useTunnelStatus() {
  return useQuery<TunnelStatusResult>({
    queryKey: ['db-sync', 'tunnel-status'],
    queryFn: () => fetchJson<TunnelStatusResult>(`${DB_SYNC_API}/tunnel/status`),
    refetchInterval: 5000,
    retry: 1,
  });
}

export function useOpenTunnel() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () =>
      fetchJson<TunnelStatusResult>(`${DB_SYNC_API}/tunnel/open`, { method: 'POST' }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['db-sync', 'tunnel-status'] }),
  });
}

export function useCloseTunnel() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () =>
      fetchJson<TunnelStatusResult>(`${DB_SYNC_API}/tunnel/close`, {
        method: 'DELETE',
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['db-sync', 'tunnel-status'] }),
  });
}
