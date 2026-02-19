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
  if (filters.page) query.set('page', filters.page.toString());
  if (filters.pageSize) query.set('pageSize', filters.pageSize.toString());

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

export async function enqueuePdf(pdfDocumentId: string, priority: number = 0): Promise<{ jobId: string }> {
  return await apiClient.post<{ jobId: string }>('/api/v1/admin/queue/enqueue', {
    pdfDocumentId,
    priority,
  });
}

// ── React Query Hooks ──────────────────────────────────────────────────

export function useQueueList(filters: QueueFilters) {
  return useQuery({
    queryKey: ['admin', 'queue', filters],
    queryFn: () => fetchQueue(filters),
    staleTime: 10_000,
    refetchInterval: 15_000,
  });
}

const TERMINAL_STATUSES: JobStatus[] = ['Completed', 'Failed', 'Cancelled'];

export function useJobDetail(jobId: string | null) {
  return useQuery({
    queryKey: ['admin', 'queue', 'detail', jobId],
    queryFn: () => fetchJobDetail(jobId!),
    enabled: !!jobId,
    staleTime: 5_000,
    refetchInterval: (query) => {
      const data = query.state.data as ProcessingJobDetailDto | null | undefined;
      if (data && TERMINAL_STATUSES.includes(data.status)) return false;
      return 10_000;
    },
  });
}

// ── Queue Stats (accurate total counts per status) ───────────────────

const STATS_STATUSES: JobStatus[] = ['Queued', 'Processing', 'Completed', 'Failed'];

export function useQueueStats() {
  return useQueries({
    queries: STATS_STATUSES.map((status) => ({
      queryKey: ['admin', 'queue', 'stats', status],
      queryFn: () => fetchQueue({ status, pageSize: 1 }),
      staleTime: 15_000,
      refetchInterval: 30_000,
    })),
  });
}
