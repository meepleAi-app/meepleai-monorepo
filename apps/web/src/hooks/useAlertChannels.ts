/**
 * useAlertChannels — Issue #1840 SP5 F4-C7
 *
 * Query + mutations for the Canali drawer (email + slack). Re-fetches the
 * channel list after a successful upsert / test-connection so the UI status
 * pills (last-tested-at, last-test-status) reflect the latest server state.
 */

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { alertChannelsApi } from '@/lib/api/alert-channels.api';
import type {
  AlertChannel,
  AlertChannelType,
  TestAlertChannelConnectionResult,
  UpsertAlertChannelRequest,
} from '@/lib/api/schemas/alert-channels.schemas';

export const ALERT_CHANNELS_QUERY_KEY = ['admin', 'alert-channels'] as const;

export function useAlertChannels() {
  const queryClient = useQueryClient();

  const channelsQuery = useQuery<AlertChannel[]>({
    queryKey: ALERT_CHANNELS_QUERY_KEY,
    queryFn: () => alertChannelsApi.getAll(),
    // Channel configs are admin-edited rarely; refetch every 60s is enough to
    // keep status pills (last-tested-at) reasonably fresh without polling spam.
    refetchInterval: 60_000,
    retry: 1,
  });

  const upsertMutation = useMutation<
    AlertChannel,
    Error,
    { type: AlertChannelType; body: UpsertAlertChannelRequest }
  >({
    mutationFn: ({ type, body }) => alertChannelsApi.upsert(type, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ALERT_CHANNELS_QUERY_KEY });
    },
  });

  const testConnectionMutation = useMutation<
    TestAlertChannelConnectionResult,
    Error,
    AlertChannelType
  >({
    mutationFn: type => alertChannelsApi.testConnection(type),
    onSuccess: () => {
      // The BE persists lastTestedAt / lastTestStatus on the channel row;
      // refresh so the drawer status pill updates without a manual reload.
      void queryClient.invalidateQueries({ queryKey: ALERT_CHANNELS_QUERY_KEY });
    },
  });

  return {
    channels: channelsQuery.data ?? [],
    isLoading: channelsQuery.isLoading,
    isError: channelsQuery.isError,
    error: channelsQuery.error,
    refetch: channelsQuery.refetch,
    upsert: upsertMutation.mutateAsync,
    isUpserting: upsertMutation.isPending,
    testConnection: testConnectionMutation.mutateAsync,
    isTesting: testConnectionMutation.isPending,
  };
}
