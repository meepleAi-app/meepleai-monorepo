/**
 * ToolkitsTabPanel — content for the "Toolkits" tab on /players/[id] (Issue #1113).
 *
 * Placeholder. Backend does not yet expose per-player toolkit data on
 * PlayerStatistics. Tracked by follow-up issue for the toolkit-detail cluster
 * (parent spec §3, cluster #2).
 */

'use client';

import type { JSX } from 'react';

import { DetailPageContainer } from '@/components/layout/PageContainer';

export interface ToolkitsTabPanelLabels {
  readonly title: string;
  readonly comingSoon: string;
}

export interface ToolkitsTabPanelProps {
  readonly labels: ToolkitsTabPanelLabels;
}

export function ToolkitsTabPanel({ labels }: ToolkitsTabPanelProps): JSX.Element {
  return (
    <DetailPageContainer data-slot="toolkits-tab-panel" className="items-center gap-2 py-12">
      <h2 className="text-lg font-semibold">{labels.title}</h2>
      <p className="text-muted-foreground text-center">{labels.comingSoon}</p>
    </DetailPageContainer>
  );
}
