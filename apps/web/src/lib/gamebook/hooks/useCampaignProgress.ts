/**
 * Issue #1388: TanStack Query hook for per-book progress on a gamebook campaign.
 *
 * Returns `SessionBookProgressRow[]` (already sorted server-side by most recent
 * visit) so the FE `ResumeBooksList` can render rows without further sorting.
 * Disabled while `campaignId` is undefined (avoids a noisy request during the
 * initial route hydration).
 */

import { useQuery } from '@tanstack/react-query';

import { getCampaignProgress, type SessionBookProgressRow } from '@/lib/api/gamebook-campaigns';

export const campaignProgressKeys = {
  byCampaign: (campaignId: string) => ['gamebook', 'campaigns', campaignId, 'progress'] as const,
};

export function useCampaignProgress(campaignId: string | undefined) {
  return useQuery<SessionBookProgressRow[]>({
    queryKey: campaignId
      ? campaignProgressKeys.byCampaign(campaignId)
      : ['gamebook', 'campaigns', 'noop', 'progress'],
    // `enabled: !!campaignId` guarantees queryFn only runs when campaignId is set.
    // eslint-disable-next-line @typescript-eslint/no-non-null-assertion -- gated by enabled
    queryFn: () => getCampaignProgress(campaignId!),
    enabled: !!campaignId,
  });
}
