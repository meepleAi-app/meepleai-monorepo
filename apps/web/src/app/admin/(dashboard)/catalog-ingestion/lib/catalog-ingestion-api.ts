'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';

// ─── Types ───────────────────────────────────────────────────────────────────

export interface ExcelImportResult {
  total: number;
  created: number;
  duplicates: number;
  errors: number;
  rowErrors: ExcelRowError[];
}

export interface ExcelRowError {
  rowNumber: number;
  columnName?: string;
  errorMessage: string;
}

export interface EnqueueResult {
  enqueued: number;
  skipped: number;
}

// ─── API base path ───────────────────────────────────────────────────────────

const BASE = '/api/v1/admin/catalog-ingestion';

// ─── API functions ───────────────────────────────────────────────────────────

async function importExcel(file: File): Promise<ExcelImportResult> {
  const formData = new FormData();
  formData.append('file', file);
  const res = await fetch(BASE + '/excel-import', {
    method: 'POST',
    body: formData,
    credentials: 'include',
  });
  if (!res.ok) throw new Error(`Import failed: ${res.status}`);
  return res.json();
}

async function enqueueEnrichment(gameIds: string[]): Promise<EnqueueResult> {
  const res = await fetch(BASE + '/enqueue-enrichment', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ sharedGameIds: gameIds }),
    credentials: 'include',
  });
  if (!res.ok) throw new Error(`Enqueue failed: ${res.status}`);
  return res.json();
}

async function enqueueAllSkeletons(): Promise<EnqueueResult> {
  const res = await fetch(BASE + '/enqueue-all-skeletons', {
    method: 'POST',
    credentials: 'include',
  });
  if (!res.ok) throw new Error(`Enqueue all failed: ${res.status}`);
  return res.json();
}

async function markComplete(gameIds: string[]): Promise<{ completed: number }> {
  const res = await fetch(BASE + '/mark-complete', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ sharedGameIds: gameIds }),
    credentials: 'include',
  });
  if (!res.ok) throw new Error(`Mark complete failed: ${res.status}`);
  return res.json();
}

async function exportExcel(status?: string, hasPdf?: boolean): Promise<void> {
  const params = new URLSearchParams();
  if (status) params.set('status', status);
  if (hasPdf !== undefined) params.set('hasPdf', String(hasPdf));
  const res = await fetch(`${BASE}/excel-export?${params}`, {
    credentials: 'include',
  });
  if (!res.ok) throw new Error(`Export failed: ${res.status}`);
  const blob = await res.blob();
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = 'catalog-export.xlsx';
  a.click();
  URL.revokeObjectURL(url);
}

// ─── React Query Hooks ──────────────────────────────────────────────────────

export function useExcelImport() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: importExcel,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['catalog-games'] }),
  });
}

export function useEnqueueEnrichment() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: enqueueEnrichment,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['catalog-games'] }),
  });
}

export function useEnqueueAllSkeletons() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: enqueueAllSkeletons,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['catalog-games'] }),
  });
}

export function useMarkComplete() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: markComplete,
    onSuccess: () => qc.invalidateQueries({ queryKey: ['catalog-games'] }),
  });
}

export function useExcelExport() {
  return useMutation({
    mutationFn: (params: { status?: string; hasPdf?: boolean }) =>
      exportExcel(params.status, params.hasPdf),
  });
}

// ====== #1861/#1835 — Catalog sync run history ======

export type CatalogSyncStatusValue = 'running' | 'idle' | 'never_run';
export type CatalogRunStatus = 'Queued' | 'Success' | 'Failed' | 'TimedOut' | 'Running';
export type CatalogSyncProvider = 'BggApi' | 'CsvImport' | 'Manual';

export interface CatalogSyncRunSummary {
  id: string;
  provider: CatalogSyncProvider;
  status: CatalogRunStatus;
  title: string;
  createdAt: string;
  startedAt: string | null;
  completedAt: string | null;
  duration: string | null; // TimeSpan serialised as "hh:mm:ss" or null
  itemsAdded: number;
  itemsUpdated: number;
  itemsFailed: number;
  errorCode: string | null;
  errorDetail: string | null;
  triggeredByUserId: string | null;
}

export interface CatalogSyncCumulative {
  gamesTotal: number;
}

export interface CatalogSyncStatusResponse {
  status: CatalogSyncStatusValue;
  lastRun: CatalogSyncRunSummary | null;
  currentRun: CatalogSyncRunSummary | null;
  cumulative: CatalogSyncCumulative;
  nextScheduled: string | null;
}

export interface PagedCatalogSyncRunsResponse {
  items: CatalogSyncRunSummary[];
  total: number;
  page: number;
  pageSize: number;
  hasMore: boolean;
}

export interface CatalogSyncRunLogsResponse {
  runId: string;
  status: CatalogRunStatus;
  errorCode: string | null;
  errorDetail: string | null;
  logsAvailable: boolean;
  logs: string[];
  logsUnavailableReason: string | null;
}

export interface TriggerCatalogSyncResponse {
  runId: string;
}

export class CatalogSyncApiError extends Error {
  constructor(
    public readonly status: number,
    message: string
  ) {
    super(message);
    this.name = 'CatalogSyncApiError';
  }
}

const SYNC_BASE = '/api/v1/admin/catalog-ingestion';

export async function fetchCatalogSyncStatus(): Promise<CatalogSyncStatusResponse> {
  const res = await fetch(`${SYNC_BASE}/status`, { method: 'GET', credentials: 'include' });
  if (!res.ok)
    throw new CatalogSyncApiError(res.status, `Failed to fetch status: ${res.statusText}`);
  return res.json();
}

export async function fetchCatalogSyncRuns({
  page = 1,
  pageSize = 12,
}: { page?: number; pageSize?: number } = {}): Promise<PagedCatalogSyncRunsResponse> {
  const res = await fetch(`${SYNC_BASE}/runs?page=${page}&pageSize=${pageSize}`, {
    method: 'GET',
    credentials: 'include',
  });
  if (!res.ok) throw new CatalogSyncApiError(res.status, `Failed to fetch runs: ${res.statusText}`);
  return res.json();
}

export async function fetchCatalogSyncRunLogs(
  runId: string,
  tail = 100
): Promise<CatalogSyncRunLogsResponse | null> {
  const res = await fetch(`${SYNC_BASE}/runs/${runId}/logs?tail=${tail}`, {
    method: 'GET',
    credentials: 'include',
  });
  if (res.status === 404) return null;
  if (!res.ok) throw new CatalogSyncApiError(res.status, `Failed to fetch logs: ${res.statusText}`);
  return res.json();
}

export async function triggerCatalogSync(
  provider: CatalogSyncProvider
): Promise<TriggerCatalogSyncResponse> {
  const res = await fetch(`${SYNC_BASE}/trigger`, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ provider }),
  });
  if (!res.ok) {
    const body = await res.json().catch(() => ({}));
    throw new CatalogSyncApiError(
      res.status,
      (body as { error?: string }).error ?? `Trigger failed: ${res.statusText}`
    );
  }
  return res.json();
}
