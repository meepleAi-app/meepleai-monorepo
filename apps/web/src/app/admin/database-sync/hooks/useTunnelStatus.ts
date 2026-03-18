'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';

import type { TunnelStatusResult } from '../types/db-sync';

const BASE = '/api/v1/admin/database-sync';

async function fetchJson<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(url, { ...init, credentials: 'include' });
  if (!res.ok) throw new Error(`${res.status}: ${await res.text()}`);
  return res.json() as Promise<T>;
}

export function useTunnelStatus() {
  return useQuery<TunnelStatusResult>({
    queryKey: ['db-sync', 'tunnel-status'],
    queryFn: () => fetchJson<TunnelStatusResult>(`${BASE}/tunnel/status`),
    refetchInterval: 5000,
    retry: 1,
  });
}

export function useOpenTunnel() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => fetchJson<TunnelStatusResult>(`${BASE}/tunnel/open`, { method: 'POST' }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['db-sync', 'tunnel-status'] }),
  });
}

export function useCloseTunnel() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () =>
      fetchJson<TunnelStatusResult>(`${BASE}/tunnel/close`, {
        method: 'DELETE',
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['db-sync', 'tunnel-status'] }),
  });
}
