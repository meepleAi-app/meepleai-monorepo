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
