import { useQuery, useQueries } from '@tanstack/react-query';

import { apiClient } from '@/lib/api/client';

// ── Types matching backend DTOs ────────────────────────────────────────

export type JobStatus = 'Queued' | 'Processing' | 'Completed' | 'Failed' | 'Cancelled';

export interface ProcessingJobDto {
  id: string;
  pdfDocumentId: string;
  pdfFileName: string;
  userId: string;
  status: JobStatus;
  priority: number;
  currentStep: string | null;
  createdAt: string;
  startedAt: string | null;
  completedAt: string | null;
  errorMessage: string | null;
  retryCount: number;
  maxRetries: number;
  canRetry: boolean;
}

export interface ProcessingStepDto {
  id: string;
  stepName: string;
  status: string;
  startedAt: string | null;
  completedAt: string | null;
  durationMs: number | null;
  metadataJson: string | null;
  logEntries: StepLogEntryDto[];
}

export interface StepLogEntryDto {
  id: string;
  timestamp: string;
  level: string;
  message: string;
}

export interface ProcessingJobDetailDto extends ProcessingJobDto {
  steps: ProcessingStepDto[];
}

export interface PaginatedQueueResponse {
  jobs: ProcessingJobDto[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// ── Filters ────────────────────────────────────────────────────────────

export interface QueueFilters {
  status?: string;
  search?: string;
  fromDate?: string;
  toDate?: string;
  page?: number;
  pageSize?: number;
}

// ── API Client ─────────────────────────────────────────────────────────

async function fetchQueue(filters: QueueFilters): Promise<PaginatedQueueResponse> {
  const query = new URLSearchParams();
  if (filters.status && filters.status !== 'all') query.set('status', filters.status);
  if (filters.search) query.set('search', filters.search);
  if (filters.fromDate) query.set('fromDate', filters.fromDate);
  if (filters.toDate) query.set('toDate', filters.toDate);
  if (filters.page !== undefined) query.set('page', filters.page.toString());
  if (filters.pageSize !== undefined) query.set('pageSize', filters.pageSize.toString());

  const qs = query.toString();
  const url = `/api/v1/admin/queue${qs ? `?${qs}` : ''}`;
  const result = await apiClient.get<PaginatedQueueResponse>(url);
  return result ?? { jobs: [], total: 0, page: 1, pageSize: 20, totalPages: 0 };
}

async function fetchJobDetail(jobId: string): Promise<ProcessingJobDetailDto | null> {
  return await apiClient.get<ProcessingJobDetailDto>(`/api/v1/admin/queue/${jobId}`);
}

export async function cancelJob(jobId: string): Promise<void> {
  await apiClient.post(`/api/v1/admin/queue/${jobId}/cancel`);
}

export async function retryJob(jobId: string): Promise<void> {
  await apiClient.post(`/api/v1/admin/queue/${jobId}/retry`);
}

export async function removeJob(jobId: string): Promise<void> {
  await apiClient.delete(`/api/v1/admin/queue/${jobId}`);
}

export async function reorderQueue(orderedJobIds: string[]): Promise<void> {
  await apiClient.put('/api/v1/admin/queue/reorder', { orderedJobIds });
}

export async function enqueuePdf(
  pdfDocumentId: string,
  priority: number = 0
): Promise<{ jobId: string }> {
  return await apiClient.post<{ jobId: string }>('/api/v1/admin/queue/enqueue', {
    pdfDocumentId,
    priority,
  });
}

// ── New API functions (Issue #5458) ───────────────────────────────────

export type ProcessingPriority = 'Low' | 'Normal' | 'High' | 'Urgent';

export interface QueueConfigDto {
  isPaused: boolean;
  maxConcurrentWorkers: number;
  updatedAt: string;
  updatedBy: string | null;
}

export interface QueueStatusDto {
  queueDepth: number;
  backpressureThreshold: number;
  isUnderPressure: boolean;
  isPaused: boolean;
  maxConcurrentWorkers: number;
  estimatedWaitMinutes: number;
}

export interface BulkReindexResult {
  enqueuedCount: number;
  skippedCount: number;
  errors: { jobId: string; reason: string }[];
}

export interface PdfTextResult {
  id: string;
  fileName: string;
  extractedText: string | null;
  processingStatus: string;
  processedAt: string | null;
  pageCount: number | null;
  characterCount: number | null;
  processingError: string | null;
}

export async function bumpPriority(jobId: string, newPriority: ProcessingPriority): Promise<void> {
  await apiClient.patch(`/api/v1/admin/queue/${jobId}/priority`, { newPriority });
}

export async function getQueueConfig(): Promise<QueueConfigDto> {
  const result = await apiClient.get<QueueConfigDto>('/api/v1/admin/queue/config');
  return result!;
}

export async function updateQueueConfig(
  isPaused?: boolean,
  maxConcurrentWorkers?: number
): Promise<void> {
  await apiClient.patch('/api/v1/admin/queue/config', { isPaused, maxConcurrentWorkers });
}

export async function bulkReindexFailed(): Promise<BulkReindexResult> {
  const result = await apiClient.post<BulkReindexResult>('/api/v1/admin/queue/reindex-failed');
  return result!;
}

export async function getExtractedText(pdfDocumentId: string): Promise<PdfTextResult | null> {
  return await apiClient.get<PdfTextResult>(
    `/api/v1/admin/queue/documents/${pdfDocumentId}/extracted-text`
  );
}

export async function getQueueStatus(): Promise<QueueStatusDto> {
  const result = await apiClient.get<QueueStatusDto>('/api/v1/admin/queue/status');
  return result!;
}

// ── Dashboard Metrics (Issue #5459) ──────────────────────────────────

export interface PhaseTimingDto {
  phase: string;
  avgDurationSeconds: number;
  minDurationSeconds: number;
  maxDurationSeconds: number;
  sampleCount: number;
}

export interface DashboardMetricsDto {
  phaseTimings: PhaseTimingDto[];
  totalProcessed: number;
  totalFailed: number;
  failureRatePercent: number;
  avgTotalDurationSeconds: number;
  period: string;
}

// ── Queue Alerts (Issue #5460) ────────────────────────────────────────

export type QueueAlertType = 'DocumentStuck' | 'QueueDepthHigh' | 'HighFailureRate';
export type QueueAlertSeverity = 'Warning' | 'Critical';

export interface QueueAlertDto {
  type: QueueAlertType;
  severity: QueueAlertSeverity;
  message: string;
  detectedAt: string;
  data: unknown;
}

export async function getActiveAlerts(): Promise<QueueAlertDto[]> {
  const result = await apiClient.get<QueueAlertDto[]>('/api/v1/admin/queue/alerts');
  return result ?? [];
}

export type MetricsPeriod = '24h' | '7d' | '30d';

export async function getDashboardMetrics(
  period: MetricsPeriod = '24h'
): Promise<DashboardMetricsDto> {
  const result = await apiClient.get<DashboardMetricsDto>(
    `/api/v1/admin/queue/metrics?period=${period}`
  );
  return result!;
}

// ── React Query Hooks ──────────────────────────────────────────────────

export function useQueueList(filters: QueueFilters, sseConnected: boolean = false) {
  return useQuery({
    queryKey: ['admin', 'queue', filters],
    queryFn: () => fetchQueue(filters),
    staleTime: sseConnected ? 30_000 : 10_000,
    // When SSE is connected, rely on SSE-triggered invalidation; poll less frequently as fallback
    refetchInterval: sseConnected ? 60_000 : 15_000,
  });
}

const TERMINAL_STATUSES: JobStatus[] = ['Completed', 'Failed', 'Cancelled'];

export function useJobDetail(jobId: string | null, sseConnected: boolean = false) {
  return useQuery({
    queryKey: ['admin', 'queue', 'detail', jobId],
    queryFn: () => fetchJobDetail(jobId!),
    enabled: !!jobId,
    staleTime: sseConnected ? 30_000 : 5_000,
    refetchInterval: query => {
      const data = query.state.data as ProcessingJobDetailDto | null | undefined;
      if (data && TERMINAL_STATUSES.includes(data.status)) return false;
      // When SSE is connected, use longer fallback interval
      return sseConnected ? 60_000 : 10_000;
    },
  });
}

// ── Queue Stats (accurate total counts per status) ───────────────────

const STATS_STATUSES: JobStatus[] = ['Queued', 'Processing', 'Completed', 'Failed'];

export function useQueueStats() {
  return useQueries({
    queries: STATS_STATUSES.map(status => ({
      queryKey: ['admin', 'queue', 'stats', status],
      queryFn: () => fetchQueue({ status, pageSize: 1 }),
      staleTime: 15_000,
      refetchInterval: 30_000,
    })),
  });
}

export function useQueueConfig() {
  return useQuery({
    queryKey: ['admin', 'queue', 'config'],
    queryFn: getQueueConfig,
    staleTime: 10_000,
    refetchInterval: 30_000,
  });
}

export function useQueueStatus() {
  return useQuery({
    queryKey: ['admin', 'queue', 'status'],
    queryFn: getQueueStatus,
    staleTime: 10_000,
    refetchInterval: 15_000,
  });
}

export function useActiveAlerts() {
  return useQuery({
    queryKey: ['admin', 'queue', 'alerts'],
    queryFn: getActiveAlerts,
    staleTime: 30_000,
    refetchInterval: 60_000,
  });
}

export function useDashboardMetrics(period: MetricsPeriod = '24h') {
  return useQuery({
    queryKey: ['admin', 'queue', 'metrics', period],
    queryFn: () => getDashboardMetrics(period),
    staleTime: 30_000,
    refetchInterval: 60_000,
  });
}
