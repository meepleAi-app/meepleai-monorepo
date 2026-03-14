/**
 * usePlayRecords — React Query hooks for Play Records
 * Issue #3892: Frontend UI + Statistics Dashboard
 */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type {
  CreatePlayRecordRequest,
  AddPlayerRequest,
  RecordScoreRequest,
  UpdatePlayRecordRequest,
} from '@/lib/api/schemas/play-records.schemas';

export const playRecordKeys = {
  all: ['play-records'] as const,
  detail: (id: string) => [...playRecordKeys.all, id] as const,
  history: (params?: { page?: number; pageSize?: number; gameId?: string }) =>
    [...playRecordKeys.all, 'history', params] as const,
  statistics: (params?: { startDate?: string; endDate?: string }) =>
    [...playRecordKeys.all, 'statistics', params] as const,
};

export function usePlayRecord(id: string) {
  return useQuery({
    queryKey: playRecordKeys.detail(id),
    queryFn: () => api.playRecords.getById(id),
    enabled: !!id,
    staleTime: 30 * 1000,
  });
}

export function usePlayHistory(params?: { page?: number; pageSize?: number; gameId?: string }) {
  return useQuery({
    queryKey: playRecordKeys.history(params),
    queryFn: () => api.playRecords.getHistory(params),
    staleTime: 2 * 60 * 1000,
  });
}

export function usePlayStatistics(params?: { startDate?: string; endDate?: string }) {
  return useQuery({
    queryKey: playRecordKeys.statistics(params),
    queryFn: () => api.playRecords.getStatistics(params),
    staleTime: 5 * 60 * 1000,
  });
}

export function useCreatePlayRecord() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreatePlayRecordRequest) => api.playRecords.create(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: playRecordKeys.all });
    },
  });
}

export function useAddPlayer() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ recordId, data }: { recordId: string; data: AddPlayerRequest }) =>
      api.playRecords.addPlayer(recordId, data),
    onSuccess: (_data, { recordId }) => {
      queryClient.invalidateQueries({ queryKey: playRecordKeys.detail(recordId) });
    },
  });
}

export function useRecordScore() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ recordId, data }: { recordId: string; data: RecordScoreRequest }) =>
      api.playRecords.recordScore(recordId, data),
    onSuccess: (_data, { recordId }) => {
      queryClient.invalidateQueries({ queryKey: playRecordKeys.detail(recordId) });
    },
  });
}

export function useStartPlayRecord() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (recordId: string) => api.playRecords.start(recordId),
    onSuccess: (_data, recordId) => {
      queryClient.invalidateQueries({ queryKey: playRecordKeys.detail(recordId) });
      queryClient.invalidateQueries({ queryKey: playRecordKeys.all });
    },
  });
}

export function useCompletePlayRecord() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ recordId, data }: { recordId: string; data?: { manualDuration?: string } }) =>
      api.playRecords.complete(recordId, data),
    onSuccess: (_data, { recordId }) => {
      queryClient.invalidateQueries({ queryKey: playRecordKeys.detail(recordId) });
      queryClient.invalidateQueries({ queryKey: playRecordKeys.all });
    },
  });
}

export function useUpdatePlayRecord() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ recordId, data }: { recordId: string; data: UpdatePlayRecordRequest }) =>
      api.playRecords.update(recordId, data),
    onSuccess: (_data, { recordId }) => {
      queryClient.invalidateQueries({ queryKey: playRecordKeys.detail(recordId) });
      queryClient.invalidateQueries({ queryKey: playRecordKeys.all });
    },
  });
}
